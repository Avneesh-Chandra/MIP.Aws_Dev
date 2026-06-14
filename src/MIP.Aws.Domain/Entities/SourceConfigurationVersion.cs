using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>Versioned news-source automation configuration produced by AI recovery or manual admin edits.</summary>
public class SourceConfigurationVersion : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public int VersionNumber { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>JSON snapshot of allowed configuration fields (selectors, waits, interaction order).</summary>
    public string JsonConfiguration { get; set; } = "{}";

    public SourceConfigurationVersionStatus Status { get; set; } = SourceConfigurationVersionStatus.Candidate;

    public Guid? CreatedByUserId { get; set; }

    public Guid? SourceRecoveryAttemptId { get; set; }

    public SourceRecoveryAttempt? SourceRecoveryAttempt { get; set; }
}
