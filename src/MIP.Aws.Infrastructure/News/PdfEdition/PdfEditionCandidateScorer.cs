using System.Globalization;
using System.Text.RegularExpressions;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Scores visible PDF link candidates from publisher pages (English + Arabic keywords).
/// </summary>
public static class PdfEditionCandidateScorer
{
    private static readonly string[] SocialHosts =
    [
        "twitter.com",
        "x.com",
        "facebook.com",
        "instagram.com",
        "youtube.com",
        "t.me"
    ];
    private static readonly string[] DefaultKeywords =
    [
        "pdf", "download", "open as pdf", "edition", "e-paper", "epaper", "newspaper",
        "illustrated pages", "today",
        "بي دي إف", "تحميل", "العدد", "النسخة الورقية", "النسخة الإلكترونية", "الجريدة", "اليوم"
    ];

    public sealed record ScoredCandidate(Uri Url, double Confidence, string Label, bool IsTodayEdition);

    public static IReadOnlyList<ScoredCandidate> ScoreFromHtml(
        string html,
        Uri pageUri,
        NewsSource source,
        DateOnly today)
    {
        var keywords = ParseKeywords(source.PdfLinkKeywords);
        var results = new List<ScoredCandidate>();
        var anchorPattern = new Regex(
            @"<a\b[^>]*\bhref\s*=\s*[""'](?<href>[^""']+)[""'][^>]*>(?<text>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        foreach (Match match in anchorPattern.Matches(html))
        {
            var href = match.Groups["href"].Value.Trim();
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var absolute = ToAbsolute(pageUri, href);
            if (absolute is null)
            {
                continue;
            }

            var text = StripTags(match.Groups["text"].Value);
            var context = ExtractContext(html, match.Index, 200);
            var score = ScoreUrlAndText(absolute, text, context, keywords, source, today);
            if (score.Confidence > 0)
            {
                results.Add(new ScoredCandidate(absolute, score.Confidence, score.Label, score.IsToday));
            }
        }

        // Direct .pdf URLs anywhere in HTML (including query strings, e.g. edition.pdf?token=...)
        foreach (Match m in Regex.Matches(html, @"https?://[^\s""'<>]+\.pdf(?:\?[^\s""'<>]*)?", RegexOptions.IgnoreCase))
        {
            if (Uri.TryCreate(m.Value, UriKind.Absolute, out var pdfUri))
            {
                var score = ScoreUrlAndText(pdfUri, "pdf", string.Empty, keywords, source, today);
                var confidence = LooksLikeEditionPdfPath(pdfUri)
                    ? Math.Max(score.Confidence, 0.95)
                    : Math.Max(score.Confidence, 0.9);
                results.Add(new ScoredCandidate(pdfUri, confidence, score.Label, score.IsToday));
            }
        }

        // pdf.php?i= style — often a viewer/interstitial, not a direct file download
        foreach (Match m in Regex.Matches(html, @"https?://[^\s""'<>]*pdf\.php\?[^""'\s<>]+", RegexOptions.IgnoreCase))
        {
            if (Uri.TryCreate(m.Value, UriKind.Absolute, out var pdfUri))
            {
                results.Add(new ScoredCandidate(pdfUri, 0.55, "pdf.php link", ContainsToday(html, today)));
            }
        }

        return results
            .GroupBy(c => c.Url.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Confidence).First())
            .OrderByDescending(c => c.Confidence)
            .ThenByDescending(c => c.IsTodayEdition)
            .ToList();
    }

    public static double ScoreHref(string href, string? text, string? context, NewsSource source, DateOnly today)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            return 0;
        }

        return ScoreUrlAndText(uri, text ?? string.Empty, context ?? string.Empty, ParseKeywords(source.PdfLinkKeywords), source, today).Confidence;
    }

    private static (double Confidence, string Label, bool IsToday) ScoreUrlAndText(
        Uri url,
        string text,
        string context,
        IReadOnlyList<string> keywords,
        NewsSource source,
        DateOnly today)
    {
        var combined = $"{text} {context}".ToLowerInvariant();
        var href = url.ToString();
        double score = 0;
        var label = text.Length > 0 ? text.Trim()[..Math.Min(text.Trim().Length, 80)] : href;

        if (IsSocialHost(url))
        {
            return (0, label, false);
        }

        if (HrefLooksLikePdfFile(href))
        {
            score += 0.45;
        }

        if (LooksLikeEditionPdfPath(url))
        {
            score += 0.35;
        }
        else if (href.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.15;
        }

        if (combined.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.2;
        }

        foreach (var kw in keywords)
        {
            if (combined.Contains(kw, StringComparison.OrdinalIgnoreCase) || href.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.08;
            }
        }

        if (combined.Contains("illustrated pages", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.15;
        }

        var isToday = ContainsToday(combined, today) || (source.PreferTodayEdition && ContainsToday(context, today));
        if (isToday && source.PreferTodayEdition)
        {
            score += 0.2;
        }

        if (source.PreferLatestEdition && score > 0)
        {
            score += 0.05;
        }

        // Asharq Al-Awsat discovery often surfaces homepage nav links; keep only issue/pdf-like paths.
        if (string.Equals(source.ConnectorKey, "news.aawsat", StringComparison.OrdinalIgnoreCase))
        {
            var path = url.AbsolutePath;
            var looksLikeIssue = path.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase);
            var looksLikePdf = HrefLooksLikePdfFile(href);
            if (!(looksLikeIssue || looksLikePdf))
            {
                return (0, label, false);
            }

            if (looksLikeIssue)
            {
                score += 0.35;
            }
        }

        return (Math.Min(1.0, score), label, isToday);
    }

    private static bool IsSocialHost(Uri uri) =>
        SocialHosts.Any(h => uri.Host.Contains(h, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsToday(string text, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        if (lower.Contains("today", StringComparison.Ordinal) || lower.Contains("اليوم", StringComparison.Ordinal))
        {
            return true;
        }

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d MMM yyyy", "dd MMM yyyy" };
        foreach (var fmt in formats)
        {
            if (text.Contains(today.ToString(fmt, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HrefLooksLikePdfFile(string href)
    {
        var path = href;
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeEditionPdfPath(Uri url) =>
        Regex.IsMatch(url.AbsolutePath, @"/source/\d+/pdf/.+\.pdf$", RegexOptions.IgnoreCase);

    private static IReadOnlyList<string> ParseKeywords(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultKeywords;
        }

        return configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(DefaultKeywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Uri? ToAbsolute(Uri pageUri, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        return Uri.TryCreate(pageUri, href, out var relative) ? relative : null;
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, "<[^>]+>", " ").Trim();

    private static string ExtractContext(string html, int index, int radius)
    {
        var start = Math.Max(0, index - radius);
        var len = Math.Min(html.Length - start, radius * 2);
        return StripTags(html.Substring(start, len));
    }
}
