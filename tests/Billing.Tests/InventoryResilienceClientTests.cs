using System.Net;
using System.Net.Http.Json;
using Billing.Api.Features.Invoices;

namespace Billing.Tests;

/// <summary>
/// Task 11: exercises the real resilience pipeline
/// (<see cref="InventoryResiliencePipeline.AddInventoryResilience"/>) wired
/// exactly as <c>Program.cs</c> wires it, against a
/// <see cref="ScriptedHttpMessageHandler"/> instead of a real socket, so each
/// scenario (transient failure recovery, retry exhaustion, timeout, non-
/// retryable business responses, caller cancellation, circuit breaker) can be
/// asserted deterministically, including the exact number of HTTP attempts
/// observed by the fake "network" layer underneath the pipeline.
/// </summary>
public class InventoryResilienceClientTests
{
    private static readonly StockDebitRequestDto DebitRequest =
        new(Guid.NewGuid(), [new StockDebitItemRequestDto(1, 2)]);

    private static HttpResponseMessage SuccessResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new StockDebitResultDto(
                DebitRequest.OperationId,
                [new StockDebitItemResultDto(1, "SKU-1", 2, 8)])),
        };

    private static HttpResponseMessage TransientFailureResponse(HttpStatusCode status = HttpStatusCode.ServiceUnavailable) =>
        new(status);

    [Fact]
    public async Task DebitAsync_Recovers_After_A_Transient_Failure_Then_Succeeds_With_Exact_Retry_Count()
    {
        // Arrange: first attempt fails transiently (503), second succeeds.
        var handler = new ScriptedHttpMessageHandler((_, attempt, _) =>
            Task.FromResult(attempt == 1 ? TransientFailureResponse() : SuccessResponse()));

        var options = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 2);
        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler);
        await using var _ = provider;

        // Act
        var result = await client.DebitAsync(DebitRequest, CancellationToken.None);

        // Assert: succeeded after exactly one retry (two HTTP attempts total).
        Assert.Equal(DebitRequest.OperationId, result.OperationId);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_Exhausts_Retries_And_Surfaces_ServiceUnavailable_With_Exact_Attempt_Count()
    {
        // Arrange: every attempt fails transiently.
        var handler = new ScriptedHttpMessageHandler((_, _, _) => Task.FromResult(TransientFailureResponse()));
        var options = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 2);
        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler);
        await using var _ = provider;

        // Act
        var ex = await Assert.ThrowsAsync<InventoryServiceUnavailableException>(
            () => client.DebitAsync(DebitRequest, CancellationToken.None));

        // Assert: 1 initial attempt + 2 retries = 3 HTTP attempts, then the
        // caller-facing contract is InventoryServiceUnavailableException
        // (never a raw Polly type), which InvoicesController maps to 503.
        Assert.Equal(3, handler.CallCount);
        Assert.NotNull(ex);
        Assert.Equal(InventoryUnavailableReason.Unavailable, ex.Reason);
    }

    [Fact]
    public async Task DebitAsync_When_Every_Attempt_Exceeds_The_Per_Attempt_Timeout_Surfaces_ServiceUnavailable()
    {
        // Arrange: every attempt hangs well past the configured per-attempt
        // timeout; the handler itself must observe/respect the
        // cancellation token the timeout strategy signals.
        var options = ResilientInventoryClientFactory.FastTestOptions(
            retryMaxAttempts: 2,
            attemptTimeoutSeconds: 0.3,
            totalTimeoutSeconds: 8);

        var handler = new ScriptedHttpMessageHandler(async (_, _, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return SuccessResponse();
        });

        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler);
        await using var _ = provider;

        // Act
        var ex = await Assert.ThrowsAsync<InventoryServiceUnavailableException>(
            () => client.DebitAsync(DebitRequest, CancellationToken.None));

        // Assert: 1 initial attempt + 2 retries, each individually timed out.
        Assert.Equal(3, handler.CallCount);
        Assert.Contains("demorou mais que o esperado", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InventoryUnavailableReason.Timeout, ex.Reason);
    }

    [Fact]
    public async Task DebitAsync_On_NotFound_Does_Not_Retry_And_Maps_To_ProductNotFound()
    {
        // Arrange: Inventory's 404 is a legitimate business response, never transient.
        var handler = new ScriptedHttpMessageHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var options = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 3);
        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler);
        await using var _ = provider;

        // Act
        await Assert.ThrowsAsync<InvoiceProductNotFoundException>(
            () => client.DebitAsync(DebitRequest, CancellationToken.None));

        // Assert: exactly one HTTP attempt, despite up to 3 retries being configured.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_On_Conflict_Does_Not_Retry_And_Maps_To_InsufficientBalance()
    {
        // Arrange: Inventory's 409 (insufficient balance) is also never transient.
        var handler = new ScriptedHttpMessageHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { Detail = "Insufficient balance for product SKU-1." }),
        }));
        var options = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 3);
        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler);
        await using var _ = provider;

        // Act
        await Assert.ThrowsAsync<InsufficientStockBalanceException>(
            () => client.DebitAsync(DebitRequest, CancellationToken.None));

        // Assert: exactly one HTTP attempt, despite up to 3 retries being configured.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_When_Caller_Cancels_Propagates_Cancellation_Without_Retrying()
    {
        // Arrange: the caller (not the pipeline) requests cancellation up front.
        var handler = new ScriptedHttpMessageHandler((_, _, _) => Task.FromResult(SuccessResponse()));
        var options = ResilientInventoryClientFactory.FastTestOptions(retryMaxAttempts: 3);
        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler);
        await using var _ = provider;

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert: propagates as a cancellation, never translated into
        // InventoryServiceUnavailableException, and never retried.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.DebitAsync(DebitRequest, cts.Token));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Circuit_Breaker_Opens_After_Threshold_Then_Half_Opens_And_Closes_On_Success()
    {
        // Arrange: every call fails until explicitly told otherwise, minimum
        // throughput of 2 so the circuit can open after exactly 2 failures,
        // and a short break duration so the test does not wait long.
        var handlerShouldSucceed = false;
        var handler = new ScriptedHttpMessageHandler((_, _, _) =>
            Task.FromResult(handlerShouldSucceed ? SuccessResponse() : TransientFailureResponse()));

        var loggerProvider = new CapturingLoggerProvider();
        var options = ResilientInventoryClientFactory.FastTestOptions(
            // One retry per logical call, so a single always-failing
            // DebitAsync call produces exactly 2 HTTP attempts (initial +
            // 1 retry), reaching the minimum throughput below within that
            // single call.
            retryMaxAttempts: 1,
            circuitBreakerMinimumThroughput: 2,
            circuitBreakerBreakDurationSeconds: 1);

        var (client, provider) = ResilientInventoryClientFactory.CreateStockClient(options, handler, loggerProvider);
        await using var _ = provider;

        // Act: a single failing call (initial attempt + its retry) produces
        // 2 failed HTTP attempts, meeting the minimum throughput (2) with a
        // 100% failure ratio (>= 50%), so the breaker opens right after the
        // second attempt's failure is recorded.
        await Assert.ThrowsAsync<InventoryServiceUnavailableException>(
            () => client.DebitAsync(DebitRequest, CancellationToken.None));
        Assert.Equal(2, handler.CallCount);

        // Assert: the circuit is now open; the very next call fails fast
        // without reaching the handler at all.
        await Assert.ThrowsAsync<InventoryServiceUnavailableException>(
            () => client.DebitAsync(DebitRequest, CancellationToken.None));
        Assert.Equal(2, handler.CallCount);

        // Act: wait past the break duration, then let the probe call succeed.
        handlerShouldSucceed = true;
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        var result = await client.DebitAsync(DebitRequest, CancellationToken.None);

        // Assert: the half-open probe reached the handler and succeeded,
        // closing the circuit again.
        Assert.Equal(DebitRequest.OperationId, result.OperationId);
        Assert.Equal(3, handler.CallCount);

        // A further call after closing should also reach the handler normally.
        await client.DebitAsync(DebitRequest, CancellationToken.None);
        Assert.Equal(4, handler.CallCount);

        // Assert: structured logs were emitted for the breaker opening and
        // closing (Task 11 requirement), without any sensitive payload.
        var messages = loggerProvider.Messages;
        Assert.Contains(messages, m => m.Contains("Circuit breaker to Inventory.Api opened", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("Circuit breaker to Inventory.Api closed", StringComparison.Ordinal));
    }
}
