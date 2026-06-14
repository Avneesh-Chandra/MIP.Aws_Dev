using System.Diagnostics;

namespace MIP.Aws.Application.Abstractions.Telemetry;

/// <summary>
/// Cross-cutting telemetry helper used by Hangfire jobs, AI pipelines, and report generation to
/// emit structured spans, durations, and counters that flow through OpenTelemetry and Application Insights.
/// </summary>
public interface ITelemetryService
{
    /// <summary>Starts an internal activity (span) with the supplied display name.</summary>
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);

    /// <summary>Records the duration of a logical operation (milliseconds) on the platform histogram.</summary>
    void RecordDuration(string operationName, double elapsedMilliseconds, IReadOnlyDictionary<string, object?>? tags = null);

    /// <summary>Increments a named counter (e.g. download.failed, ai.completed).</summary>
    void IncrementCounter(string counterName, long delta = 1, IReadOnlyDictionary<string, object?>? tags = null);

    /// <summary>Records a gauge-style value (e.g. queue depth).</summary>
    void RecordGauge(string gaugeName, double value, IReadOnlyDictionary<string, object?>? tags = null);
}
