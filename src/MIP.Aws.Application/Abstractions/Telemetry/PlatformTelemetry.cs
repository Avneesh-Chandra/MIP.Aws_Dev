using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MIP.Aws.Application.Abstractions.Telemetry;

/// <summary>
/// Platform-wide ActivitySource and Meter names. Centralizing them ensures OpenTelemetry pipelines
/// in the API and Worker hosts subscribe to the same instruments.
/// </summary>
public static class PlatformTelemetry
{
    public const string SourceName = "MIP.Aws";

    public const string MeterName = "MIP.Aws.Metrics";

    /// <summary>Activity source used by application services, jobs, and middlewares.</summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    /// <summary>Meter used by the platform telemetry service.</summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");
}

/// <summary>
/// Conventional metric and counter names. Use these constants to keep dashboards consistent.
/// </summary>
public static class TelemetryNames
{
    public const string ApiLatency = "api.latency.ms";
    public const string OcrDuration = "ocr.duration.ms";
    public const string AiDuration = "ai.duration.ms";
    public const string DownloadDuration = "download.duration.ms";
    public const string ReportDuration = "report.duration.ms";
    public const string EmailDuration = "email.duration.ms";

    public const string DownloadFailures = "download.failures";
    public const string DownloadSuccess = "download.success";
    public const string AiCompleted = "ai.completed";
    public const string AiFailures = "ai.failures";
    public const string OcrCompleted = "ocr.completed";
    public const string OcrFailures = "ocr.failures";
    public const string ReportsGenerated = "reports.generated";
    public const string EmailsSent = "emails.sent";
    public const string EmailsFailed = "emails.failed";
    public const string LoginSuccess = "auth.login.success";
    public const string LoginFailure = "auth.login.failure";
    public const string LoginThrottled = "auth.login.throttled";
    public const string CacheHit = "cache.hit";
    public const string CacheMiss = "cache.miss";
    public const string HangfireQueueDepth = "hangfire.queue.depth";
}
