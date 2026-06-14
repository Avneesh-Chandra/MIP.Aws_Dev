using System.Text.Json;
using System.Text.Json.Serialization;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;

public static class PdfSelectorSuggestionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<AiSelectorSuggestionItem> Parse(string rawJson)
    {
        var dto = JsonSerializer.Deserialize<AiResponseDto>(PdfSelectorSuggestionJsonFence.Strip(rawJson), JsonOptions)
                  ?? throw new InvalidOperationException("AI returned empty JSON.");

        if (dto.Suggestions is null || dto.Suggestions.Count == 0)
        {
            return Array.Empty<AiSelectorSuggestionItem>();
        }

        var results = new List<AiSelectorSuggestionItem>();
        foreach (var item in dto.Suggestions)
        {
            if (string.IsNullOrWhiteSpace(item.Selector))
            {
                continue;
            }

            if (!PdfSelectorSuggestionSanitizer.IsValidCssSelector(item.Selector))
            {
                continue;
            }

            if (!TryMapPurpose(item.Purpose, out var purpose))
            {
                continue;
            }

            if (!TryMapExpectedAction(item.ExpectedAction, out var expectedAction))
            {
                continue;
            }

            var confidence = Math.Clamp(item.Confidence ?? 0, 0, 1);
            results.Add(new AiSelectorSuggestionItem(
                item.Selector.Trim(),
                string.IsNullOrWhiteSpace(item.SelectorType) ? "Css" : item.SelectorType.Trim(),
                purpose.ToString(),
                confidence,
                TruncateReason(item.Reason),
                expectedAction.ToString()));
        }

        return results;
    }

    private static bool TryMapPurpose(string? value, out PdfSelectorPurpose purpose)
    {
        purpose = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse<PdfSelectorPurpose>(value.Trim(), true, out purpose);
    }

    private static bool TryMapExpectedAction(string? value, out PdfSelectorExpectedAction action)
    {
        action = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        foreach (PdfSelectorExpectedAction candidate in Enum.GetValues<PdfSelectorExpectedAction>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                action = candidate;
                return true;
            }
        }

        return false;
    }

    private static string TruncateReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim()[..Math.Min(reason.Trim().Length, 2000)];

    private sealed class AiResponseDto
    {
        public List<AiSuggestionDto>? Suggestions { get; set; }
    }

    private sealed class AiSuggestionDto
    {
        public string? Selector { get; set; }
        public string? SelectorType { get; set; }
        public string? Purpose { get; set; }
        public double? Confidence { get; set; }
        public string? Reason { get; set; }
        public string? ExpectedAction { get; set; }
    }
}
