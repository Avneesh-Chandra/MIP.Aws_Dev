using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>Operator-raised alert for admin manual intervention on a failed download.</summary>
public class AdminInterventionNotification : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public Guid? DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    public string FailureReason { get; set; } = string.Empty;

    public string? FailureCode { get; set; }

    public string SuggestedAction { get; set; } = string.Empty;

    public string? OperatorNote { get; set; }

    public AdminInterventionNotificationStatus Status { get; set; } = AdminInterventionNotificationStatus.Pending;

    public Guid CreatedByUserId { get; set; }

    public Guid? AcknowledgedByAdminId { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public Guid? ResolvedByAdminId { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
