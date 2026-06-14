namespace MIP.Aws.Domain.Enums;

/// <summary>How a download job was initiated (used for auto-recovery gating).</summary>
public enum DownloadJobTrigger
{
    Manual = 0,
    Scheduled = 1,
    Retry = 2,
    Recovery = 3,
    AutoAiRecovery = 4
}
