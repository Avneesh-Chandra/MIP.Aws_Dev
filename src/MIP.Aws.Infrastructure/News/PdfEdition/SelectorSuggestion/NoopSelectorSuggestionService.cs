using MIP.Aws.Application.Abstractions.Intelligence;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;

public sealed class NoopSelectorSuggestionService(ILogger<NoopSelectorSuggestionService> logger) : IAiSelectorSuggestionService
{
    public bool IsEnabled => false;

    public Task<AiSelectorSuggestionResult?> SuggestAsync(
        string pageUrl,
        string pageTitle,
        string sanitizedHtmlFragment,
        IReadOnlyList<PdfSelectorCandidateElement> candidateElements,
        string? screenshotReference,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("AI selector suggestion is disabled; skipping request for {Url}.", pageUrl);
        return Task.FromResult<AiSelectorSuggestionResult?>(null);
    }
}
