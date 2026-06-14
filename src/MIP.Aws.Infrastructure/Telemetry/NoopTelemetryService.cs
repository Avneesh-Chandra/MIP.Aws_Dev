using System.Diagnostics;
using MIP.Aws.Application.Abstractions.Telemetry;

namespace MIP.Aws.Infrastructure.Telemetry;

public sealed class NoopTelemetryService : ITelemetryService
{
    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) =>
        new Activity(name).Start();

    public void RecordDuration(string operationName, double elapsedMilliseconds, IReadOnlyDictionary<string, object?>? tags = null)
    {
    }

    public void IncrementCounter(string counterName, long delta = 1, IReadOnlyDictionary<string, object?>? tags = null)
    {
    }

    public void RecordGauge(string gaugeName, double value, IReadOnlyDictionary<string, object?>? tags = null)
    {
    }
}
