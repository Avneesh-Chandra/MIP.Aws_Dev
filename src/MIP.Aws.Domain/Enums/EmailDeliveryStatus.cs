namespace MIP.Aws.Domain.Enums;

public enum EmailDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    SkippedConfigurationMissing = 3,
    SkippedNoRecipients = 4,
    SkippedApprovalRequired = 5,
    RedirectedBySafety = 6
}
