using System.Text.RegularExpressions;

namespace MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;

internal static partial class PdfSelectorSuggestionJsonFence
{
    public static string Strip(string raw)
    {
        var m = FencePattern().Match(raw);
        return m.Success ? m.Groups[1].Value : raw.Trim();
    }

    [GeneratedRegex("```(?:json)?\\s*(\\{.*\\})\\s*```", RegexOptions.Singleline)]
    private static partial Regex FencePattern();
}
