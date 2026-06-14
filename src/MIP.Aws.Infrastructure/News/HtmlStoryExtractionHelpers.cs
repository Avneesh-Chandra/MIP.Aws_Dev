using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace MIP.Aws.Infrastructure.News;

internal static class HtmlStoryExtractionHelpers
{
    private static readonly Regex NextDataRegex = new(
        @"<script\s+id=""__NEXT_DATA__""[^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SlugTitlePairRegex = new(
        @"""slug""\s*:\s*""(%[^""]{8,})""[^}]{0,400}?""title""\s*:\s*""((?:\\.|[^""\\]){12,400})""",
        RegexOptions.Compiled);

    private static readonly Regex TitleSlugPairRegex = new(
        @"""title""\s*:\s*""((?:\\.|[^""\\]){12,400})""[^}]{0,400}?""slug""\s*:\s*""(%[^""]{8,})""",
        RegexOptions.Compiled);

    public static bool TryResolveUri(string? href, Uri baseUri, out Uri absolute)
    {
        absolute = baseUri;
        if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
        {
            absolute = abs;
            return true;
        }

        if (Uri.TryCreate(baseUri, href, out abs))
        {
            absolute = abs;
            return true;
        }

        return false;
    }

    public static string NormalizeHeadlineText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = System.Net.WebUtility.HtmlDecode(text.Trim());
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    public static string? FindListingTeaser(IElement? context, string headline)
    {
        if (context is null)
        {
            return null;
        }

        var container = context.Closest("article, li, .card, .news-item, .post, .story, .item") ?? context.ParentElement;
        if (container is not null)
        {
            foreach (var node in container.QuerySelectorAll("p, .excerpt, .summary, .desc, .lead, .subtitle, span.summary"))
            {
                var text = NormalizeHeadlineText(node.TextContent);
                if (IsDistinctTeaser(text, headline))
                {
                    return text;
                }
            }
        }

        var sibling = context.NextElementSibling;
        while (sibling is not null)
        {
            if (string.Equals(sibling.TagName, "P", StringComparison.OrdinalIgnoreCase))
            {
                var text = NormalizeHeadlineText(sibling.TextContent);
                if (IsDistinctTeaser(text, headline))
                {
                    return text;
                }
            }

            sibling = sibling.NextElementSibling;
        }

        return null;
    }

    public static string? ExtractArticleLead(string html, string headline)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var doc = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        var selectors = new[]
        {
            "article p",
            ".article-body p",
            ".article-content p",
            ".post-content p",
            ".entry-content p",
            ".news-content p",
            ".content p",
            "main p"
        };

        foreach (var selector in selectors)
        {
            foreach (var p in doc.QuerySelectorAll(selector))
            {
                var text = NormalizeHeadlineText(p.TextContent);
                if (IsDistinctTeaser(text, headline))
                {
                    return text;
                }
            }
        }

        return null;
    }

    public static bool IsDistinctTeaser(string? teaser, string headline)
    {
        if (string.IsNullOrWhiteSpace(teaser))
        {
            return false;
        }

        teaser = teaser.Trim();
        headline = headline.Trim();
        if (teaser.Length < 35)
        {
            return false;
        }

        if (string.Equals(teaser, headline, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (teaser.Length < headline.Length + 20)
        {
            return false;
        }

        return true;
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

    public static bool LooksLikeArticleTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 12)
        {
            return false;
        }

        if (title.Contains("subscribe", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("????", StringComparison.Ordinal))
        {
            return false;
        }

        var arabic = title.Count(c => c is >= '\u0600' and <= '\u06FF');
        var latin = title.Count(c => c is >= 'A' and <= 'z');
        return arabic >= 8 || latin >= 12;
    }

    private static readonly Regex DescriptionNearTitleRegex = new(
        @"""title""\s*:\s*""((?:\\.|[^""\\]){12,400})""[^}]{0,800}?""(?:description|summary|subtitle|excerpt)""\s*:\s*""((?:\\.|[^""\\]){20,800})""",
        RegexOptions.Compiled);

    public static IReadOnlyList<(string Title, string Url, string? Teaser)> ExtractAlQabasFromNextData(string html, int max)
    {
        var match = NextDataRegex.Match(html);
        if (!match.Success)
        {
            return Array.Empty<(string, string, string?)>();
        }

        var json = match.Groups[1].Value;
        var stories = new List<(string Title, string Url, string? Teaser)>();

        foreach (Match m in DescriptionNearTitleRegex.Matches(json))
        {
            var title = NormalizeHeadlineText(UnescapeJson(m.Groups[1].Value));
            var teaser = NormalizeHeadlineText(UnescapeJson(m.Groups[2].Value));
            if (!LooksLikeArticleTitle(title))
            {
                continue;
            }

            if (!IsDistinctTeaser(teaser, title))
            {
                teaser = null;
            }

            stories.Add((title, "https://alqabas.com/", teaser));
        }

        void Collect(Match m, bool slugFirst)
        {
            var slug = Uri.UnescapeDataString(slugFirst ? m.Groups[1].Value : m.Groups[2].Value);
            var title = UnescapeJson(slugFirst ? m.Groups[2].Value : m.Groups[1].Value);
            title = NormalizeHeadlineText(title);
            if (!LooksLikeArticleTitle(title))
            {
                return;
            }

            var url = slug.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? slug
                : $"https://alqabas.com/{slug.TrimStart('/')}";
            if (!stories.Any(s => string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase)))
            {
                stories.Add((title, url, null));
            }
        }

        foreach (Match m in SlugTitlePairRegex.Matches(match.Groups[1].Value))
        {
            Collect(m, slugFirst: true);
        }

        foreach (Match m in TitleSlugPairRegex.Matches(match.Groups[1].Value))
        {
            Collect(m, slugFirst: false);
        }

        return stories
            .GroupBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(max)
            .ToList();
    }

    public static string BuildStorySnippet(string headline, string? teaser, string? articleLead) =>
        IsDistinctTeaser(teaser, headline) ? teaser!
        : IsDistinctTeaser(articleLead, headline) ? articleLead!
        : string.Empty;

    private static string UnescapeJson(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string>($"\"{value}\"") ?? value;
        }
        catch
        {
            return value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\n", " ", StringComparison.Ordinal).Trim();
        }
    }
}
