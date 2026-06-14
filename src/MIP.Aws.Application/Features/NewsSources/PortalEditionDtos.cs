namespace MIP.Aws.Application.Features.NewsSources;

public sealed record NewsPortalDownloadTestResultDto(
    bool Success,
    string Message,
    string? FailureCode,
    Guid? DownloadedFileId,
    string? PdfRelativePath,
    string? PdfViewUrl,
    string? ScreenshotRelativePath,
    string? HtmlSnapshotRelativePath);

public sealed record PortalEditionHistoryItemDto(
    Guid DownloadedFileId,
    Guid? DownloadJobId,
    string BlobUri,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedAt,
    string? PdfViewUrl);

public sealed record PortalLatestEditionDto(
    Guid DownloadedFileId,
    string BlobUri,
    long SizeBytes,
    string Sha256,
    DateTimeOffset DownloadedAt,
    string PdfViewUrl);
