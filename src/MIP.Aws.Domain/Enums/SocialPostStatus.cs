namespace MIP.Aws.Domain.Enums;

public enum SocialPostStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Scheduled = 3,
    Published = 4,
    Failed = 5,
    Rejected = 6,
    AiGenerated = 7,
    AnalystEdited = 8,
    PendingCompliance = 9
}
