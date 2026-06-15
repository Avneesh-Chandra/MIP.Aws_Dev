using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions.Intelligence;

namespace MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;

/// <summary>
/// Removes secrets and sensitive form data before sending page content to the AI provider.
/// </summary>
public static partial class PdfSelectorSuggestionSanitizer
{
    private static readonly Regex PasswordInputRegex = PasswordInputPattern();
    private static readonly Regex HiddenInputRegex = HiddenInputPattern();
    private static readonly Regex TokenAttributeRegex = TokenAttributePattern();
    private static readonly Regex ScriptBlockRegex = ScriptBlockPattern();
    private static readonly Regex StyleBlockRegex = StyleBlockPattern();

    public static string SanitizeHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitized = html;
        sanitized = ScriptBlockRegex.Replace(sanitized, "<!-- script removed -->");
        sanitized = StyleBlockRegex.Replace(sanitized, "<!-- style removed -->");
        sanitized = PasswordInputRegex.Replace(sanitized, "<input type=\"password\" value=\"[REDACTED]\" />");
        sanitized = HiddenInputRegex.Replace(sanitized, m =>
        {
            var name = m.Groups[1].Value;
            return name.Contains("token", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("csrf", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("session", StringComparison.OrdinalIgnoreCase)
                ? "<input type=\"hidden\" name=\"[REDACTED]\" value=\"[REDACTED]\" />"
                : m.Value;
        });
        sanitized = TokenAttributeRegex.Replace(sanitized, "$1=\"[REDACTED]\"");
        return sanitized;
    }

    public static IReadOnlyList<PdfSelectorCandidateElement> ExtractCandidateElements(string html, int maxElements)
    {
        if (string.IsNullOrWhiteSpace(html) || maxElements <= 0)
        {
            return Array.Empty<PdfSelectorCandidateElement>();
        }

        var results = new List<PdfSelectorCandidateElement>();
        var tagRegex = TagPattern();
        foreach (Match match in tagRegex.Matches(html))
        {
            if (results.Count >= maxElements)
            {
                break;
            }

            var tag = match.Groups[1].Value.ToLowerInvariant();
            if (tag is not ("a" or "button" or "img" or "input"))
            {
                continue;
            }

            var attrs = match.Groups[2].Value;
            var text = tag == "img" ? null : StripTags(match.Groups[3].Value);
            var element = new PdfSelectorCandidateElement(
                tag,
                Truncate(CleanText(text), 120),
                ReadAttr(attrs, "href"),
                ReadAttr(attrs, "aria-label"),
                ReadAttr(attrs, "title"),
                ReadAttr(attrs, "alt"),
                ReadAttr(attrs, "class"),
                ReadAttr(attrs, "id"),
                ReadAttr(attrs, "role"));

            if (IsSensitive(element))
            {
                continue;
            }

            if (HasPdfSignal(element))
            {
                results.Add(element);
            }
        }

        if (results.Count < maxElements)
        {
            foreach (Match match in tagRegex.Matches(html))
            {
                if (results.Count >= maxElements)
                {
                    break;
                }

                var tag = match.Groups[1].Value.ToLowerInvariant();
                if (tag is not ("a" or "button" or "img"))
                {
                    continue;
                }

                var attrs = match.Groups[2].Value;
                var text = tag == "img" ? null : StripTags(match.Groups[3].Value);
                var element = new PdfSelectorCandidateElement(
                    tag,
                    Truncate(CleanText(text), 120),
                    ReadAttr(attrs, "href"),
                    ReadAttr(attrs, "aria-label"),
                    ReadAttr(attrs, "title"),
                    ReadAttr(attrs, "alt"),
                    ReadAttr(attrs, "class"),
                    ReadAttr(attrs, "id"),
                    ReadAttr(attrs, "role"));

                if (IsSensitive(element) || results.Any(x => x.Tag == element.Tag && x.Href == element.Href && x.Text == element.Text))
                {
                    continue;
                }

                results.Add(element);
            }
        }

        return results;
    }

    public static bool IsValidCssSelector(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return false;
        }

        var trimmed = selector.Trim();
        if (trimmed.Length > 512)
        {
            return false;
        }

        if (trimmed.Contains('\n', StringComparison.Ordinal) || trimmed.Contains('\r', StringComparison.Ordinal))
        {
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower.Contains("javascript:", StringComparison.Ordinal)
            || lower.Contains("<script", StringComparison.Ordinal)
            || lower.Contains("onerror=", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool HasPdfSignal(PdfSelectorCandidateElement element)
    {
        static bool Has(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && (value.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                || value.Contains("e-paper", StringComparison.OrdinalIgnoreCase)
                || value.Contains("epaper", StringComparison.OrdinalIgnoreCase)
                || value.Contains("edition", StringComparison.OrdinalIgnoreCase)
                || value.Contains("today", StringComparison.OrdinalIgnoreCase)
                || value.Contains("download", StringComparison.OrdinalIgnoreCase));

        return Has(element.Href) || Has(element.Text) || Has(element.AriaLabel) || Has(element.Title) || Has(element.Alt);
    }

    private static bool IsSensitive(PdfSelectorCandidateElement element)
    {
        static bool Sensitive(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && (value.Contains("password", StringComparison.OrdinalIgnoreCase)
                || value.Contains("token", StringComparison.OrdinalIgnoreCase)
                || value.Contains("csrf", StringComparison.OrdinalIgnoreCase)
                || value.Contains("session", StringComparison.OrdinalIgnoreCase)
                || value.Contains("cookie", StringComparison.OrdinalIgnoreCase));

        return Sensitive(element.Text) || Sensitive(element.Href) || Sensitive(element.Id) || Sensitive(element.Class);
    }

    private static string? ReadAttr(string attrs, string name)
    {
        var pattern = new Regex($@"{name}\s*=\s*(""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
        var m = pattern.Match(attrs);
        if (!m.Success)
        {
            return null;
        }

        var value = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        return WebUtility.HtmlDecode(value.Trim());
    }

    private static string StripTags(string value) => Regex.Replace(value, "<[^>]+>", " ").Trim();

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Regex.Replace(WebUtility.HtmlDecode(value), "\\s+", " ").Trim();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value[..max];
    }

    [GeneratedRegex("<input[^>]*type\\s*=\\s*[\"']password[\"'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordInputPattern();

    [GeneratedRegex("<input[^>]*type\\s*=\\s*[\"']hidden[\"'][^>]*name\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenInputPattern();

    [GeneratedRegex("(data-(?:token|csrf|session)|authorization|cookie)\\s*=\\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex TokenAttributePattern();

    [GeneratedRegex("<script\\b[^>]*>[\\s\\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlockPattern();

    [GeneratedRegex("<style\\b[^>]*>[\\s\\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlockPattern();

    [GeneratedRegex("<(a|button|img|input)\\b([^>]*)>(.*?)</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagPattern();
}
