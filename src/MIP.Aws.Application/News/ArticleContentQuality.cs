using System.Text.RegularExpressions;

namespace MIP.Aws.Application.News;

/// <summary>
/// Shared rules for readable headlines/snippets across executive digest and review studio.
/// </summary>
public static class ArticleContentQuality
{
    private static readonly Regex IssueIdHeadline = new(@"^issue\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MostlyTechnical = new(
        @"(<<PAGE|Copy text|Copy link|out of \d+/\d+|PDF edition stored|binary artifact|Alsharqalawsat|Pages:\s*\d+\s*out of)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsReadableHeadline(string? headline, string? sourceName = null)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            return false;
        }

        var h = headline.Trim();
        var minLen = h.Any(c => c is >= '\u0600' and <= '\u06FF') ? 14 : 18;
        if (h.Length < minLen)
        {
            return false;
        }

        if (IssueIdHeadline.IsMatch(h) || MostlyTechnical.IsMatch(h))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sourceName) &&
            string.Equals(h, sourceName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var letters = h.Count(char.IsLetter);
        if (letters < 12)
        {
            return false;
        }

        if (h.Count(c => c == '/') > 4 || h.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static bool IsReadableSnippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim();
        if (t.Length < 40)
        {
            return false;
        }

        if (MostlyTechnical.IsMatch(t) || t.Contains("<<PAGE", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public static bool IsReviewEligible(string? headline, string? body, string? sourceName)
    {
        if (!IsReadableHeadline(headline, sourceName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return true;
        }

        var b = body.Trim();
        if (MostlyTechnical.IsMatch(b) || b.Contains("<<PAGE", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(b, headline?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsReadableSnippet(b) || b.Length >= 80;
    }

    public static string NormalizeSnippet(string text, int maxLen = 320)
    {
        text = text.Trim().Replace("\r\n", " ").Replace('\n', ' ');
        while (text.Contains("  ", StringComparison.Ordinal))
        {
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (text.Length <= maxLen)
        {
            return text;
        }

        var cut = text[..maxLen];
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > 160 ? cut[..lastSpace] + "…" : cut + "…";
    }

    public static string? BuildDisplaySnippet(string headline, string? body)
    {
        var text = OmitDuplicateHeadline(body, headline);
        return string.IsNullOrWhiteSpace(text) ? null : NormalizeSnippet(text);
    }

    public static string? OmitDuplicateHeadline(string? body, string headline)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var text = body.Trim();
        if (IsDistinctTeaser(text, headline))
        {
            return text;
        }

        if (text.StartsWith(headline, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = text[headline.Length..].TrimStart(':', '-', '–', ' ', '\t');
            if (IsDistinctTeaser(remainder, headline))
            {
                return remainder;
            }
        }

        return null;
    }

    private static bool IsDistinctTeaser(string teaser, string headline)
    {
        if (string.IsNullOrWhiteSpace(teaser) || teaser.Length < 35)
        {
            return false;
        }

        if (string.Equals(teaser.Trim(), headline.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
