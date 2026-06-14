namespace MIP.Aws.Application.Configuration;

public sealed class AiSourceRecoveryOptions
{
    public const string SectionName = "AiSourceRecovery";

    public bool Enabled { get; set; } = true;

    /// <summary>Max HTML characters sent to the model per analysis.</summary>
    public int MaxHtmlChars { get; set; } = 120_000;

    /// <summary>MIPOperator may auto-apply options with confidence at or above this threshold.</summary>
    public int OperatorAutoApplyConfidenceThreshold { get; set; } = 90;

    /// <summary>Risk levels operators may apply without admin approval.</summary>
    public string OperatorAllowedRiskLevels { get; set; } = "Low";
}
