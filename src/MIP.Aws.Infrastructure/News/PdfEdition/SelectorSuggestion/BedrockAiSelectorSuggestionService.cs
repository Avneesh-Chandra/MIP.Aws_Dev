using System.Text.Json;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.SourceRecovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;

public sealed class BedrockAiSelectorSuggestionService(
    IAiTextGenerationService textGeneration,
    IOptions<AiOptions> aiOptions,
    ILogger<BedrockAiSelectorSuggestionService> logger) : IAiSelectorSuggestionService
{
    public bool IsEnabled => aiOptions.Value.Enabled && textGeneration.IsEnabled;

    public async Task<AiSelectorSuggestionResult?> SuggestAsync(
        string pageUrl,
        string pageTitle,
        string sanitizedHtmlFragment,
        IReadOnlyList<PdfSelectorCandidateElement> candidateElements,
        string? screenshotReference,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var userPrompt = $"""
            PageUrl: {pageUrl}
            PageTitle: {pageTitle}
            ScreenshotReference: {screenshotReference ?? "none"}
            CandidateElements: {JsonSerializer.Serialize(candidateElements)}
            HtmlFragment:
            {sanitizedHtmlFragment}
            """;

        var result = await textGeneration.GenerateAsync(
            new AiTextGenerationRequest(SourceRecoveryAiPrompts.SelectorSuggestionSystemPrompt, userPrompt, RequireJson: true),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            logger.LogWarning("Selector suggestion AI failed: {Error}", result.Error);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.Text);
            if (!doc.RootElement.TryGetProperty("suggestions", out var suggestions)
                || suggestions.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var items = new List<AiSelectorSuggestionItem>();
            foreach (var item in suggestions.EnumerateArray())
            {
                var selector = item.TryGetProperty("selector", out var sel) && sel.ValueKind == JsonValueKind.String
                    ? sel.GetString() ?? string.Empty
                    : string.Empty;
                if (selector.Length == 0)
                {
                    continue;
                }

                items.Add(new AiSelectorSuggestionItem(
                    selector,
                    item.TryGetProperty("selectorType", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() ?? "css" : "css",
                    item.TryGetProperty("purpose", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "pdf" : "pdf",
                    item.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var conf) ? conf : 0.7,
                    item.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("expectedAction", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() ?? "click" : "click"));
            }

            return items.Count > 0 ? new AiSelectorSuggestionResult(items) : null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Selector suggestion AI returned invalid JSON.");
            return null;
        }
    }
}
