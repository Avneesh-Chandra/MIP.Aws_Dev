namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Centralized observability configuration: OpenTelemetry exporters, Application Insights connection,
/// and the service identity used in spans, metrics, and structured logs.
/// </summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>Logical service name emitted as <c>service.name</c> on every signal.</summary>
    public string ServiceName { get; set; } = "MIP.Aws";

    /// <summary>Logical role (api/worker/blazor) used to filter and route signals.</summary>
    public string ServiceRole { get; set; } = "api";

    /// <summary>Deployment environment label (development/staging/production).</summary>
    public string Environment { get; set; } = "development";

    /// <summary>Free-form service version stamped onto resources for release correlation.</summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>Master switch for OpenTelemetry traces and metrics.</summary>
    public bool EnableOpenTelemetry { get; set; } = true;

    /// <summary>Master switch for Application Insights ingestion.</summary>
    public bool EnableApplicationInsights { get; set; }

    /// <summary>Azure Monitor / Application Insights connection string (preferred over instrumentation key).</summary>
    public string? ApplicationInsightsConnectionString { get; set; }

    /// <summary>OTLP endpoint (gRPC/HTTP) for traces and metrics — used when present.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Sampling probability between 0.0 (off) and 1.0 (always).</summary>
    public double TraceSamplingProbability { get; set; } = 1.0;

    /// <summary>Adds a Seq sink for structured logs in development environments.</summary>
    public bool EnableSeqSink { get; set; }

    /// <summary>Seq server URL used when <see cref="EnableSeqSink"/> is true.</summary>
    public string? SeqServerUrl { get; set; } = "http://localhost:5341";
}
