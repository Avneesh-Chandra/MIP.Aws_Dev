namespace MIP.Aws.Application.Abstractions.Intelligence;

/// <summary>
/// Advisory AI selector suggestion. Never auto-applied.
/// </summary>
public sealed record AiSelectorSuggestionItem(
    string Selector,
    string SelectorType,
    string Purpose,
    double Confidence,
    string Reason,
    string ExpectedAction);

public sealed record AiSelectorSuggestionResult(IReadOnlyList<AiSelectorSuggestionItem> Suggestions);

public interface IAiSelectorSuggestionService
{
    bool IsEnabled { get; }

    Task<AiSelectorSuggestionResult?> SuggestAsync(
        string pageUrl,
        string pageTitle,
        string sanitizedHtmlFragment,
        IReadOnlyList<PdfSelectorCandidateElement> candidateElements,
        string? screenshotReference,
        CancellationToken cancellationToken);
}
