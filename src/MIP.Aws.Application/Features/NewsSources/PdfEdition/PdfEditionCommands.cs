using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Enums;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

public sealed record DiscoverPdfEditionCommand(Guid NewsSourceId) : IRequest<PdfEditionDownloadOutcome>;

public sealed record DownloadTodayPdfCommand(Guid NewsSourceId, bool EnqueueOcr = true) : IRequest<PdfEditionDownloadOutcome>;

/// <summary>
/// Operator-provided PDF or issue-viewer URL when automatic discovery fails.
/// </summary>
public sealed record DownloadManualPdfCommand(
    Guid NewsSourceId,
    string ManualUrl,
    bool SaveAsDiscoveryPageUrl = false,
    bool EnqueueOcr = true) : IRequest<PdfEditionDownloadOutcome>;

public sealed record GetLatestPdfEditionQuery(Guid NewsSourceId) : IRequest<PdfEditionDownloadOutcome?>;

public sealed record GetPdfEditionHistoryQuery(Guid NewsSourceId, int Take = 20) : IRequest<IReadOnlyList<PdfEditionHistoryItemDto>>;

public sealed record GetPdfEditionDownloadProgressQuery(Guid NewsSourceId)
    : IRequest<PdfEditionDownloadProgressDto?>;

public sealed record PdfEditionDownloadProgressDto(int Percent, string Phase, bool IsComplete);

public sealed record PdfEditionHistoryItemDto(
    Guid Id,
    string SourceUrl,
    string? SavedPath,
    string FileName,
    long? FileSizeBytes,
    DateOnly EditionDate,
    double DiscoveryConfidence,
    string DiscoveryMethod,
    string Status,
    string? FailureReason,
    DateTimeOffset? DiscoveredAt,
    DateTimeOffset? DownloadedAt,
    Guid? DownloadedFileId);

public sealed record StreamPdfEditionQuery(Guid NewsSourceId, Guid FileId, bool Inline) : IRequest<PdfEditionStreamResult?>;

public sealed record PdfEditionStreamResult(
    byte[] Content,
    string ContentType,
    string FileName);
