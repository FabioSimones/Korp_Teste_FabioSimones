using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

namespace Billing.Api.Features.Invoices;

/// <summary>
/// Externalized configuration for the resilience pipeline applied to every
/// HTTP call from Billing.Api to Inventory.Api. Bound from the configuration
/// section <c>InventoryApi:Resilience</c> (see appsettings.json and
/// docs/technical-details.md, section "Resiliência Billing -&gt; Inventory
/// (Task 11)", for the rationale behind each value).
/// </summary>
public class InventoryResilienceOptions
{
    /// <summary>Timeout applied to a single HTTP attempt (innermost stage of the pipeline).</summary>
    public double AttemptTimeoutSeconds { get; set; } = 3;

    /// <summary>Timeout applied to the whole call, including every retry (outermost stage).</summary>
    public double TotalTimeoutSeconds { get; set; } = 12;

    /// <summary>Extra margin added on top of <see cref="TotalTimeoutSeconds"/> for <c>HttpClient.Timeout</c>, a coarse safety net that must never fire before the pipeline's own total timeout.</summary>
    public double SafetyTimeoutMarginSeconds { get; set; } = 5;

    /// <summary>Maximum number of retry attempts after the first try.</summary>
    public int RetryMaxAttempts { get; set; } = 3;

    /// <summary>Base delay for the exponential backoff with jitter applied between retries.</summary>
    public double RetryBaseDelaySeconds { get; set; } = 0.5;

    /// <summary>Fraction of failed calls, within the sampling window, required to open the circuit.</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>Rolling window used to evaluate the failure ratio.</summary>
    public double CircuitBreakerSamplingDurationSeconds { get; set; } = 10;

    /// <summary>Minimum number of calls, within the sampling window, before the circuit is allowed to open.</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 4;

    /// <summary>How long the circuit stays open before a single probe call is allowed through (half-open).</summary>
    public double CircuitBreakerBreakDurationSeconds { get; set; } = 15;
}

/// <summary>
/// Builds the resilience pipeline (total timeout, retry with exponential
/// backoff and jitter, circuit breaker, per-attempt timeout) shared by both
/// Billing-&gt;Inventory typed HTTP clients (<see cref="IInventoryProductClient"/>
/// and <see cref="IInventoryStockClient"/>), so the two integration points
/// behave identically under failure. See docs/technical-details.md for the
/// stage order, the transient/non-transient classification, and known
/// limitations.
/// </summary>
public static class InventoryResiliencePipeline
{
    public static void AddInventoryResilience(
        this IHttpClientBuilder httpClientBuilder,
        InventoryResilienceOptions options)
    {
        httpClientBuilder.AddResilienceHandler("inventory-api", (pipelineBuilder, context) =>
        {
            var logger = context.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Billing.Api.Resilience.InventoryApi");

            // Stage order, outer to inner (first added = outermost, executes
            // first and wraps everything below it):
            //   1. Total timeout   - caps the whole call, including retries.
            //   2. Retry           - exponential backoff with jitter, limited attempts.
            //   3. Circuit breaker - fails fast without waiting once the
            //                        threshold trips, sitting *inside* retry so
            //                        an open circuit still counts as an
            //                        exhausted attempt for the caller, but
            //                        *outside* the per-attempt timeout so it
            //                        observes each attempt's outcome.
            //   4. Attempt timeout - bounds a single HTTP try, innermost/closest
            //                        to the actual call.
            // This mirrors the layout used by
            // Microsoft.Extensions.Http.Resilience's own
            // AddStandardResilienceHandler(), adjusted to this service's
            // externalized configuration instead of its defaults.
            pipelineBuilder
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Name = "inventory-api-total-timeout",
                    Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds),
                })
                .AddRetry(new HttpRetryStrategyOptions
                {
                    Name = "inventory-api-retry",
                    MaxRetryAttempts = options.RetryMaxAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds),
                    // Respect Retry-After when the Inventory service sends it
                    // with a 429/503 response (native support from
                    // HttpRetryStrategyOptions).
                    ShouldRetryAfterHeader = true,
                    ShouldHandle = args => ValueTask.FromResult(IsTransientFailure(args.Outcome)),
                    OnRetry = args =>
                    {
                        // No sensitive data logged: only the attempt count and
                        // a short classification of the failure (exception
                        // type name or HTTP status code).
                        logger.LogWarning(
                            "Retrying call to Inventory.Api ({RequestUri}): attempt {AttemptNumber} after {Reason}.",
                            DescribeRequestUri(args.Outcome),
                            args.AttemptNumber + 1,
                            DescribeOutcome(args.Outcome));
                        return ValueTask.CompletedTask;
                    },
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    Name = "inventory-api-circuit-breaker",
                    FailureRatio = options.CircuitBreakerFailureRatio,
                    SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds),
                    MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                    BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds),
                    ShouldHandle = args => ValueTask.FromResult(IsTransientFailure(args.Outcome)),
                    OnOpened = args =>
                    {
                        logger.LogError(
                            "Circuit breaker to Inventory.Api opened for {BreakDuration} after {Reason}.",
                            args.BreakDuration,
                            DescribeOutcome(args.Outcome));
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("Circuit breaker to Inventory.Api closed; calls resumed normally.");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = _ =>
                    {
                        logger.LogInformation("Circuit breaker to Inventory.Api half-open; probing with the next call.");
                        return ValueTask.CompletedTask;
                    },
                })
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Name = "inventory-api-attempt-timeout",
                    Timeout = TimeSpan.FromSeconds(options.AttemptTimeoutSeconds),
                });
        });
    }

    /// <summary>
    /// Transient-failure classification shared by the retry and circuit
    /// breaker stages: <see cref="HttpRequestException"/> (connection-level
    /// failure), <see cref="TimeoutRejectedException"/> (either pipeline
    /// timeout stage tripping), and HTTP 408/429/5xx. Explicitly excludes
    /// 400/404/409 (legitimate Inventory business responses) and any
    /// cancellation requested by the caller, which surfaces as
    /// <see cref="OperationCanceledException"/> and is never matched here.
    /// </summary>
    private static bool IsTransientFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException)
        {
            return true;
        }

        if (outcome.Exception is TimeoutRejectedException)
        {
            return true;
        }

        if (outcome.Result is { } response)
        {
            var status = (int)response.StatusCode;
            return status == (int)HttpStatusCode.RequestTimeout
                || status == (int)HttpStatusCode.TooManyRequests
                || status >= 500;
        }

        return false;
    }

    private static string DescribeOutcome(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } exception
            ? exception.GetType().Name
            : $"HTTP {(int)outcome.Result!.StatusCode}";

    private static string DescribeRequestUri(Outcome<HttpResponseMessage> outcome) =>
        outcome.Result?.RequestMessage?.RequestUri?.PathAndQuery ?? "unknown";
}
