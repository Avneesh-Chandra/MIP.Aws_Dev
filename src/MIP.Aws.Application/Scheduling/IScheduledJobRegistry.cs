namespace MIP.Aws.Application.Scheduling;

/// <summary>
/// Registers recurring background work (Hangfire in on-prem/Azure VM, or Azure Functions timer elsewhere).
/// </summary>
public interface IScheduledJobRegistry
{
    void RegisterRecurringJobs();
}
