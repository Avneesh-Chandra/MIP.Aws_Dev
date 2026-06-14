using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>Learned recovery pattern reused for similar failures across sources.</summary>
public class SourceRecoveryKnowledgeEntry : AuditableEntity
{
    public string FailureType { get; set; } = string.Empty;

    public string? PortalStrategyKey { get; set; }

    public string? ConnectorKey { get; set; }

    public string FieldName { get; set; } = string.Empty;

    public string? OldSelector { get; set; }

    public string NewSelector { get; set; } = string.Empty;

    public SelectorRecoveryStrategy Strategy { get; set; } = SelectorRecoveryStrategy.Css;

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    public string? Notes { get; set; }

    public Guid? SourceRecoveryAttemptId { get; set; }
}
