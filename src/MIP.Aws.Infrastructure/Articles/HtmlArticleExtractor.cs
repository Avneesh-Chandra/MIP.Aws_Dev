using System.Text;
using AngleSharp.Html.Parser;
using MIP.Aws.Application.Abstractions.Articles;

namespace MIP.Aws.Infrastructure.Articles;

/// <summary>
/// HTML article extraction with UTF-8 / Arabic-safe text extraction.
/// </summary>
public sealed class HtmlArticleExtractor : IArticleExtractor
{
    private static readonly HashSet<string> HtmlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "application/xhtml+xml"
    };

    public bool Supports(string contentType, Uri sourceUri) =>
        HtmlTypes.Any(t => contentType.Contains(t, StringComparison.OrdinalIgnoreCase));

    public Task<ArticleExtractionResult> ExtractAsync(Uri sourceUri, byte[] payload, string contentType, string? languageHint, CancellationToken cancellationToken)
    {
        var html = Encoding.UTF8.GetString(payload);
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);
        var title = doc.Title?.Trim();
        var headline = string.IsNullOrWhiteSpace(title) ? sourceUri.Host : title;

        var main = doc.QuerySelector("article") ?? doc.QuerySelector("main") ?? doc.Body;
        var bodyText = main?.TextContent.Trim() ?? string.Empty;
        if (bodyText.Length > 120_000)
        {
            bodyText = bodyText[..120_000];
        }

        var canonical = doc.QuerySelector("link[rel=canonical]")?.GetAttribute("href");
        Uri? canonicalUri = null;
        if (!string.IsNullOrWhiteSpace(canonical) && Uri.TryCreate(canonical, UriKind.Absolute, out var cu))
        {
            canonicalUri = cu;
        }

        var author = doc.QuerySelector("meta[name='author']")?.GetAttribute("content")?.Trim();
        var section = doc.QuerySelector("meta[property='article:section']")?.GetAttribute("content")?.Trim();

        IReadOnlyList<string> tags = Array.Empty<string>();
        var tagElements = doc.QuerySelectorAll("meta[property='article:tag'],a[rel='tag']");
        if (tagElements.Length > 0)
        {
            tags = tagElements.Select(e => e.GetAttribute("content") ?? e.TextContent).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(32).ToList()!;
        }

        DateTimeOffset? published = null;
        var pub = doc.QuerySelector("meta[property='article:published_time'],meta[name='pubdate']")?.GetAttribute("content");
        if (pub is not null && DateTimeOffset.TryParse(pub, out var p))
        {
            published = p;
        }

        var rawTrunc = html.Length > 400_000 ? html[..400_000] : html;
        return Task.FromResult(new ArticleExtractionResult(
            headline,
            bodyText,
            rawTrunc,
            author,
            published,
            section,
            tags,
            canonicalUri?.ToString()));
    }
}
