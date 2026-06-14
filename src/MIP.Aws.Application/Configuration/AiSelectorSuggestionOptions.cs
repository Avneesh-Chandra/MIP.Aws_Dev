namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Feature flag and limits for AI-assisted PDF selector suggestions (advisory only).
/// </summary>
public sealed class AiSelectorSuggestionOptions
{
    public const string SectionName = "AiSelectorSuggestion";

    public bool Enabled { get; set; }

    /// <summary>Maximum candidate DOM snippets sent to the model per request.</summary>
    public int MaxCandidateElements { get; set; } = 40;

    /// <summary>Maximum sanitized HTML characters sent to the model.</summary>
    public int MaxHtmlChars { get; set; } = 8000;
}
