namespace MIP.Aws.Application.Abstractions.News;

public sealed record PublicHtmlExtractionResult(
    int ArticlesDiscovered,
    int ArticlesStored,
    int ArticlesSkippedDuplicate,
    string? SnapshotPath);

public interface IPublicHtmlArticleExtractionService
{
    Task<PublicHtmlExtractionResult> ExtractAsync(Guid newsSourceId, CancellationToken cancellationToken);
}

public sealed record InternalArticleReportResult(
    Guid ReportId,
    string RelativePath,
    string DownloadUrl,
    string Disclaimer);

public interface IInternalArticleReportService
{
    Task<InternalArticleReportResult> GenerateAsync(Guid newsSourceId, CancellationToken cancellationToken);
}

public sealed record PdfDiscoveryStatusDto(
    string LastPdfDiscoveryOutcome,
    bool RealPdfFound,
    bool NoPublicPdfAvailable,
    bool AiSelectorSuggestionsAvailable,
    bool PublicArticlesExtracted,
    int PendingAiSelectorSuggestions,
    int PublicArticleCount,
    DateTimeOffset? LastPdfDiscoveredAt,
    DateTimeOffset? LastPublicHtmlExtractedAt,
    string? UserMessage);
