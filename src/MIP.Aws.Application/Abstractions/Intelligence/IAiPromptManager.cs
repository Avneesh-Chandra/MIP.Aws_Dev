namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAiPromptManager
{
    (string System, string User) BuildArticleIntelligencePrompts(string headline, string body, string language);

    (string System, string User) BuildPdfSelectorSuggestionPrompts(
        string pageUrl,
        string pageTitle,
        string sanitizedHtmlFragment,
        IReadOnlyList<PdfSelectorCandidateElement> candidateElements,
        string? screenshotReference);
}
