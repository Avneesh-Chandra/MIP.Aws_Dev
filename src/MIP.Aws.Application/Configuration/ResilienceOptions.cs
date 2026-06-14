namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Single source of truth for Polly retry / circuit-breaker / timeout knobs. Specific Azure services
/// can override the defaults via per-service sub-sections.
/// </summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    public ResiliencePolicy Default { get; set; } = new();

    public ResiliencePolicy AzureOpenAi { get; set; } = new()
    {
        RetryCount = 5,
        BaseDelayMilliseconds = 800,
        CircuitBreakerFailures = 10,
        CircuitBreakerDurationSeconds = 45,
        TimeoutSeconds = 120
    };

    public ResiliencePolicy DocumentIntelligence { get; set; } = new()
    {
        RetryCount = 4,
        BaseDelayMilliseconds = 600,
        TimeoutSeconds = 180
    };

    public ResiliencePolicy MicrosoftGraph { get; set; } = new()
    {
        RetryCount = 4,
        CircuitBreakerFailures = 8,
        TimeoutSeconds = 60
    };

    public ResiliencePolicy NewsDownload { get; set; } = new()
    {
        RetryCount = 3,
        BaseDelayMilliseconds = 500,
        TimeoutSeconds = 90
    };

    public ResiliencePolicy AzureBlob { get; set; } = new()
    {
        RetryCount = 5,
        TimeoutSeconds = 60
    };

    public ResiliencePolicy Redis { get; set; } = new()
    {
        RetryCount = 2,
        BaseDelayMilliseconds = 100,
        TimeoutSeconds = 5
    };
}

/// <summary>
/// A single resilience profile composed of retry + circuit-breaker + timeout policies.
/// </summary>
public sealed class ResiliencePolicy
{
    /// <summary>How many transient retries to attempt.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Base of the exponential delay (milliseconds), jittered at runtime.</summary>
    public int BaseDelayMilliseconds { get; set; } = 250;

    /// <summary>Failure count that trips the circuit breaker. 0 disables the breaker.</summary>
    public int CircuitBreakerFailures { get; set; } = 6;

    /// <summary>Duration the breaker remains open before allowing a probe (seconds).</summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>Per-attempt timeout. 0 disables the timeout policy.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
