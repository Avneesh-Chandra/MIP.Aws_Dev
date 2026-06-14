using System.Diagnostics;

namespace MIP.Aws.Application.Abstractions.Telemetry;

/// <summary>
/// Lightweight propagation point for the per-operation correlation id. Middlewares populate this
/// from the inbound <c>X-Correlation-Id</c> header; jobs use <see cref="Create"/> to start a fresh
/// scope when invoked outside of a request.
/// </summary>
public sealed class CorrelationContext
{
    /// <summary>Request/job correlation id. Defaults to the current activity id when unset.</summary>
    public string CorrelationId { get; set; } = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    /// <summary>Optional user identity propagated for audit emission.</summary>
    public string? UserId { get; set; }

    /// <summary>Optional tenant identity propagated for audit emission.</summary>
    public string? TenantId { get; set; }

    /// <summary>Creates a brand new context (used when a Hangfire job kicks off without an inbound request).</summary>
    public static CorrelationContext Create() => new()
    {
        CorrelationId = Guid.NewGuid().ToString("N")
    };
}
