namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Controls whether this process runs a Hangfire job server (workers) or only enqueues jobs / hosts the dashboard.
/// </summary>
public sealed class HangfireHostOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>When false, Hangfire storage and dashboard remain available but no background workers run in this process.</summary>
    public bool EnableJobServer { get; set; } = true;
}
