namespace MIP.Aws.Application.Configuration;

public sealed class AutoAiDownloadRecoveryOptions
{
    public const string SectionName = "AutoAiDownloadRecovery";

    public bool Enabled { get; set; } = true;

    public bool RunAfterScheduledFailure { get; set; } = true;

    public bool RunAfterManualFailure { get; set; } = true;

    public int MaxSuggestionsToTry { get; set; } = 3;

    public double MinimumConfidence { get; set; } = 0.70;

    public string MaximumRiskAllowed { get; set; } = "Medium";

    public bool RequireHumanApprovalForMediumRisk { get; set; }

    public string[] OnlyForSourceTypes { get; set; } =
    [
        "PublicPdf",
        "PublicHtml",
        "WebPortalLogin"
    ];

    public int CooldownMinutesPerSource { get; set; } = 60;

    public int MaxAutoRecoveryAttemptsPerDayPerSource { get; set; } = 3;

    public bool ActivateSuccessfulCandidateAutomatically { get; set; } = true;

    public bool RollbackOnFailure { get; set; } = true;

    /// <summary>Actor user id recorded on automatic recovery attempts (system account).</summary>
    public Guid SystemActorUserId { get; set; } = Guid.Empty;
}
