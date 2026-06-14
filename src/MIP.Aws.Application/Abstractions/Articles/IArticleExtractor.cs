namespace MIP.Aws.Application.Abstractions.Articles;

/// <summary>
/// Normalizes downloaded payloads into headline, cleaned text, and metadata (UTF-8, including Arabic).
/// </summary>
public interface IArticleExtractor
{
    bool Supports(string contentType, Uri sourceUri);

    Task<ArticleExtractionResult> ExtractAsync(Uri sourceUri, byte[] payload, string contentType, string? languageHint, CancellationToken cancellationToken);
}

public sealed record ArticleExtractionResult(
    string Headline,
    string CleanText,
    string RawHtml,
    string? Author,
    DateTimeOffset? PublishedAt,
    string? Section,
    IReadOnlyList<string> Tags,
    string? CanonicalUrl);
