using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>Singleton-style runtime overrides for automatic AI download recovery.</summary>
public sealed class AutoAiDownloadRecoverySettings : AuditableEntity
{
    public bool Enabled { get; set; }

    public bool RunAfterScheduledFailure { get; set; } = true;

    public bool RunAfterManualFailure { get; set; }

    public int MaxSuggestionsToTry { get; set; } = 3;

    public double MinimumConfidence { get; set; } = 0.70;

    public string MaximumRiskAllowed { get; set; } = "Medium";

    public bool RequireHumanApprovalForMediumRisk { get; set; }

    public int CooldownMinutesPerSource { get; set; } = 60;

    public int MaxAutoRecoveryAttemptsPerDayPerSource { get; set; } = 3;

    public bool ActivateSuccessfulCandidateAutomatically { get; set; } = true;

    public bool RollbackOnFailure { get; set; } = true;
}
