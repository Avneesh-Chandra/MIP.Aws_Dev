using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.News;

/// <summary>
/// Pulls individual story headlines from publisher HTML (homepages and e-paper pages).
/// </summary>
public sealed class HtmlEditionStoryExtractor
{
    private static readonly HtmlParser Parser = new();

    public IReadOnlyList<EditionStoryDraft> Extract(NewsSource source, string html, int maxStories = 8)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<EditionStoryDraft>();
        }

        var key = ResolveConnectorKey(source);
        return key switch
        {
            "news.aawsat" => ExtractAawsat(source.Name, html, maxStories),
            "news.akhbar-alkhaleej" => ExtractAkhbar(source.Name, html, maxStories),
            "news.alqabas" => ExtractAlQabas(source.Name, html, maxStories),
            "news.alayam" => ExtractAlAyam(source.Name, html, maxStories),
            _ => ExtractGeneric(source.Name, html, maxStories, new Uri(NormalizeBaseUrl(source.BaseUrl)))
        };
    }

    private static string ResolveConnectorKey(NewsSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.ConnectorKey))
        {
            return source.ConnectorKey;
        }

        var host = new Uri(NormalizeBaseUrl(source.BaseUrl)).Host;
        return host switch
        {
            var h when h.Contains("akhbar-alkhaleej", StringComparison.OrdinalIgnoreCase) => "news.akhbar-alkhaleej",
            var h when h.Contains("alqabas", StringComparison.OrdinalIgnoreCase) => "news.alqabas",
            var h when h.Contains("alayam", StringComparison.OrdinalIgnoreCase) => "news.alayam",
            var h when h.Contains("aawsat", StringComparison.OrdinalIgnoreCase) => "news.aawsat",
            _ => string.Empty
        };
    }

    private static string NormalizeBaseUrl(string baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost/" : baseUrl.TrimEnd('/') + "/";

    private static IReadOnlyList<EditionStoryDraft> ExtractAawsat(string sourceName, string html, int max)
    {
        var doc = Parser.ParseDocument(html);
        var baseUri = new Uri("https://aawsat.com/");
        var stories = new List<EditionStoryDraft>();

        foreach (var link in doc.QuerySelectorAll("a[href*='aawsat.com/']"))
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || href.Contains("/files/pdf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(
                link.QuerySelector("h2,h3,h4")?.TextContent ?? link.TextContent);
            if (!HeadlineQuality.IsReadableHeadline(headline, sourceName) ||
                !HtmlStoryExtractionHelpers.TryResolveUri(href, baseUri, out var uri))
            {
                continue;
            }

            stories.Add(CreateDraft(headline, uri.ToString(), link));
            if (stories.Count >= max)
            {
                break;
            }
        }

        return Dedupe(stories, max);
    }

    private static IReadOnlyList<EditionStoryDraft> ExtractAkhbar(string sourceName, string html, int max)
    {
        var doc = Parser.ParseDocument(html);
        var baseUri = new Uri("https://akhbar-alkhaleej.com/");
        var stories = new List<EditionStoryDraft>();

        foreach (var link in doc.QuerySelectorAll("a[href*='/news/article/']"))
        {
            var href = link.GetAttribute("href");
            if (!HtmlStoryExtractionHelpers.TryResolveUri(href, baseUri, out var uri))
            {
                continue;
            }

            var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(
                link.QuerySelector("h2,h3,h4,.title")?.TextContent
                ?? link.ParentElement?.QuerySelector("h2,h3,h4")?.TextContent
                ?? link.GetAttribute("title")
                ?? link.TextContent);

            if (!HeadlineQuality.IsReadableHeadline(headline, sourceName))
            {
                continue;
            }

            stories.Add(CreateDraft(headline, uri.ToString(), link));
            if (stories.Count >= max)
            {
                break;
            }
        }

        if (stories.Count < max)
        {
            foreach (var h3 in doc.QuerySelectorAll("h3"))
            {
                var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(h3.TextContent);
                if (!HeadlineQuality.IsReadableHeadline(headline, sourceName))
                {
                    continue;
                }

                var anchor = h3.QuerySelector("a") ?? h3.Closest("a");
                if (anchor is null ||
                    !HtmlStoryExtractionHelpers.TryResolveUri(anchor.GetAttribute("href"), baseUri, out var uri) ||
                    !uri.AbsoluteUri.Contains("/news/article/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stories.Add(CreateDraft(headline, uri.ToString(), h3));
                if (stories.Count >= max)
                {
                    break;
                }
            }
        }

        return Dedupe(stories, max);
    }

    private static IReadOnlyList<EditionStoryDraft> ExtractAlQabas(string sourceName, string html, int max)
    {
        var stories = new List<EditionStoryDraft>();

        foreach (var (title, url, teaser) in HtmlStoryExtractionHelpers.ExtractAlQabasFromNextData(html, max))
        {
            if (!HeadlineQuality.IsReadableHeadline(title, sourceName))
            {
                continue;
            }

            var snippet = HtmlStoryExtractionHelpers.BuildStorySnippet(title, teaser, null);
            stories.Add(new EditionStoryDraft(title, url, string.IsNullOrWhiteSpace(snippet) ? null : snippet));
        }

        if (stories.Count >= max)
        {
            return Dedupe(stories, max);
        }

        var doc = Parser.ParseDocument(html);
        var baseUri = new Uri("https://alqabas.com/");
        foreach (var link in doc.QuerySelectorAll("a[href*='alqabas.com/']"))
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || href.Contains("/archive/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(
                link.QuerySelector("h2,h3,h4")?.TextContent ?? link.TextContent);
            if (!HeadlineQuality.IsReadableHeadline(headline, sourceName) ||
                !HtmlStoryExtractionHelpers.TryResolveUri(href, baseUri, out var uri))
            {
                continue;
            }

            stories.Add(CreateDraft(headline, uri.ToString(), link));
            if (stories.Count >= max)
            {
                break;
            }
        }

        return Dedupe(stories, max);
    }

    private static IReadOnlyList<EditionStoryDraft> ExtractAlAyam(string sourceName, string html, int max)
    {
        var doc = Parser.ParseDocument(html);
        var baseUri = new Uri("https://www.alayam.com/");
        var stories = new List<EditionStoryDraft>();

        foreach (var link in doc.QuerySelectorAll("a[href*='/News.html']"))
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) ||
                !Regex.IsMatch(href, @"/online/[^/]+/\d+/News\.html", RegexOptions.IgnoreCase))
            {
                continue;
            }

            if (!HtmlStoryExtractionHelpers.TryResolveUri(href, baseUri, out var uri))
            {
                continue;
            }

            var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(
                link.QuerySelector("h2,h3,h4,.title")?.TextContent
                ?? link.ParentElement?.QuerySelector("h2,h3,h4")?.TextContent
                ?? link.GetAttribute("title")
                ?? link.TextContent);

            if (!HeadlineQuality.IsReadableHeadline(headline, sourceName))
            {
                continue;
            }

            stories.Add(CreateDraft(headline, uri.ToString(), link));
            if (stories.Count >= max)
            {
                break;
            }
        }

        if (stories.Count < max)
        {
            foreach (var h in doc.QuerySelectorAll("h2,h3"))
            {
                var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(h.TextContent);
                if (!HeadlineQuality.IsReadableHeadline(headline, sourceName))
                {
                    continue;
                }

                var anchor = h.QuerySelector("a") ?? h.Closest("a");
                if (anchor is null || !HtmlStoryExtractionHelpers.TryResolveUri(anchor.GetAttribute("href"), baseUri, out var uri))
                {
                    continue;
                }

                stories.Add(CreateDraft(headline, uri.ToString(), h));
                if (stories.Count >= max)
                {
                    break;
                }
            }
        }

        return Dedupe(stories, max);
    }

    private static EditionStoryDraft CreateDraft(string headline, string url, IElement? context)
    {
        var teaser = HtmlStoryExtractionHelpers.FindListingTeaser(context, headline);
        var snippet = HtmlStoryExtractionHelpers.BuildStorySnippet(headline, teaser, null);
        return new EditionStoryDraft(headline, url, string.IsNullOrWhiteSpace(snippet) ? null : snippet);
    }

    private static IReadOnlyList<EditionStoryDraft> ExtractGeneric(string sourceName, string html, int max, Uri baseUri)
    {
        var doc = Parser.ParseDocument(html);
        var stories = new List<EditionStoryDraft>();

        foreach (var h in doc.QuerySelectorAll("h1,h2,h3"))
        {
            var headline = HtmlStoryExtractionHelpers.NormalizeHeadlineText(h.TextContent);
            if (!HeadlineQuality.IsReadableHeadline(headline, sourceName))
            {
                continue;
            }

            var link = h.QuerySelector("a") ?? h.Closest("a");
            var url = link?.GetAttribute("href") ?? doc.BaseUri?.ToString() ?? baseUri.ToString();
            if (!HtmlStoryExtractionHelpers.TryResolveUri(url, baseUri, out var uri))
            {
                uri = baseUri;
            }

            stories.Add(CreateDraft(headline, uri.ToString(), h));
            if (stories.Count >= max)
            {
                break;
            }
        }

        return Dedupe(stories, max);
    }

    private static IReadOnlyList<EditionStoryDraft> Dedupe(List<EditionStoryDraft> stories, int max) =>
        stories
            .GroupBy(s => s.Headline.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .Take(max)
            .ToList();

    public sealed record EditionStoryDraft(string Headline, string Url, string? Snippet);
}
