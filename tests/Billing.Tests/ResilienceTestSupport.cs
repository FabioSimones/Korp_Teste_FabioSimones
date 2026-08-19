using Billing.Api.Features.Invoices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Billing.Tests;

/// <summary>
/// A primary <see cref="HttpMessageHandler"/> whose response for each call is
/// produced by a caller-supplied delegate, receiving the 1-based attempt
/// number. Used to script transient-failure/success sequences, delays (for
/// timeout tests), and to count exactly how many HTTP attempts reached the
/// "network" boundary underneath the resilience pipeline (Task 11 tests).
/// </summary>
internal sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;
    private int _callCount;

    public ScriptedHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public int CallCount => _callCount;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _callCount);
        return await _responder(request, attempt, cancellationToken);
    }
}

/// <summary>
/// Captures every formatted log message emitted through the standard
/// <see cref="ILoggerFactory"/>/<see cref="ILogger"/> pipeline, used to assert
/// that the resilience pipeline's structured logs (retry/circuit breaker
/// open/close, Task 11) are actually produced, without depending on a
/// specific logging provider (console, etc).
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToArray();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private void Capture(string message)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _owner;

        public CapturingLogger(CapturingLoggerProvider owner)
        {
            _owner = owner;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _owner.Capture(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

/// <summary>
/// Builds a production <see cref="InventoryStockClient"/>/<see cref="InventoryProductClient"/>
/// wired to the real <see cref="InventoryResiliencePipeline.AddInventoryResilience"/>
/// pipeline (exactly as <c>Program.cs</c> does), but backed by a
/// <see cref="ScriptedHttpMessageHandler"/> instead of a real socket, so
/// Task 11 tests can exercise the actual retry/timeout/circuit-breaker
/// behavior deterministically and quickly. Independent of
/// <c>WebApplicationFactory</c>/ASP.NET Core hosting entirely.
/// </summary>
internal static class ResilientInventoryClientFactory
{
    public static (IInventoryStockClient Client, ServiceProvider Provider) CreateStockClient(
        InventoryResilienceOptions options,
        HttpMessageHandler primaryHandler,
        CapturingLoggerProvider? loggerProvider = null)
    {
        var services = BuildBaseServices(loggerProvider);

        var httpClientBuilder = services.AddHttpClient<IInventoryStockClient, InventoryStockClient>(client =>
        {
            client.BaseAddress = new Uri("http://inventory.local");
            client.Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds + options.SafetyTimeoutMarginSeconds);
        });
        httpClientBuilder.AddInventoryResilience(options);
        httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IInventoryStockClient>(), provider);
    }

    public static (IInventoryProductClient Client, ServiceProvider Provider) CreateProductClient(
        InventoryResilienceOptions options,
        HttpMessageHandler primaryHandler,
        CapturingLoggerProvider? loggerProvider = null)
    {
        var services = BuildBaseServices(loggerProvider);

        var httpClientBuilder = services.AddHttpClient<IInventoryProductClient, InventoryProductClient>(client =>
        {
            client.BaseAddress = new Uri("http://inventory.local");
            client.Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds + options.SafetyTimeoutMarginSeconds);
        });
        httpClientBuilder.AddInventoryResilience(options);
        httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IInventoryProductClient>(), provider);
    }

    private static ServiceCollection BuildBaseServices(CapturingLoggerProvider? loggerProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });
        return services;
    }

    /// <summary>
    /// Fast-but-representative resilience settings for tests: short attempt
    /// timeout, a couple of retries with minimal backoff, and a
    /// quick-to-trip/quick-to-recover circuit breaker, so tests do not need
    /// long real-time waits (Task 11, avoid <c>Thread.Sleep</c>/long delays).
    /// </summary>
    public static InventoryResilienceOptions FastTestOptions(
        int retryMaxAttempts = 2,
        double attemptTimeoutSeconds = 0.4,
        double totalTimeoutSeconds = 8,
        // High by default so tests focused purely on retry/timeout behavior
        // do not accidentally trip the circuit breaker mid-test; the
        // dedicated circuit breaker test lowers this explicitly.
        int circuitBreakerMinimumThroughput = 100,
        double circuitBreakerBreakDurationSeconds = 1) =>
        new()
        {
            AttemptTimeoutSeconds = attemptTimeoutSeconds,
            TotalTimeoutSeconds = totalTimeoutSeconds,
            SafetyTimeoutMarginSeconds = 10,
            RetryMaxAttempts = retryMaxAttempts,
            RetryBaseDelaySeconds = 0.05,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerSamplingDurationSeconds = 2,
            CircuitBreakerMinimumThroughput = circuitBreakerMinimumThroughput,
            CircuitBreakerBreakDurationSeconds = circuitBreakerBreakDurationSeconds,
        };
}
