using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.News;

/// <summary>
/// Validates, downloads, and persists public PDF editions with compliance checks.
/// </summary>
public interface IPdfEditionDownloadService
{
    Task<PdfEditionDownloadOutcome> DiscoverOnlyAsync(Guid newsSourceId, CancellationToken cancellationToken);

    Task<PdfEditionDownloadOutcome> DownloadTodayAsync(Guid newsSourceId, bool enqueueOcr, CancellationToken cancellationToken);

    Task<PdfEditionDownloadOutcome> DownloadManualAsync(
        Guid newsSourceId,
        string manualUrl,
        bool saveAsDiscoveryPageUrl,
        bool enqueueOcr,
        CancellationToken cancellationToken);

    Task<PdfEditionDownloadOutcome?> GetLatestAsync(Guid newsSourceId, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the public PDF edition pipeline against an existing <see cref="DownloadJob"/> (e.g. AI recovery retry).
    /// </summary>
    Task<PdfEditionDownloadOutcome> ExecuteDownloadJobAsync(Guid downloadJobId, CancellationToken cancellationToken);
}

public sealed record PdfEditionDownloadOutcome(
    Guid? PdfEditionDownloadId,
    Guid? DownloadJobId,
    Guid? DownloadedFileId,
    PdfEditionStatus Status,
    string? SourceUrl,
    string? SavedPath,
    string? ViewUrl,
    long? FileSizeBytes,
    string? Sha256Hash,
    double? Confidence,
    string? DiscoveryMethod,
    string? FailureReason,
    IReadOnlyList<PdfEditionCandidateDto> Candidates);

public sealed record PdfEditionCandidateDto(
    string Url,
    double Confidence,
    string Method,
    string? Label,
    bool IsTodayEdition);
