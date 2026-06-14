using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Intelligence or operational alert for dashboards and optional email escalation.
/// </summary>
public sealed class IntelligenceAlert : AuditableEntity
{
    public IntelligenceAlertType AlertType { get; set; }

    public IntelligenceAlertSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? MetadataJson { get; set; }

    public bool IsAcknowledged { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public Guid? AcknowledgedByUserId { get; set; }

    public Guid? RelatedArticleId { get; set; }

    public Guid? RelatedDownloadJobId { get; set; }

    public Guid? RelatedOcrJobId { get; set; }
}
