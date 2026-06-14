namespace MIP.Aws.Application.Abstractions.Intelligence;

/// <summary>
/// End-to-end pipeline: downloaded file → OCR → segmentation → persisted articles → AI enrichment.
/// </summary>
public interface INewspaperIntelligencePipeline
{
    Task ProcessDownloadJobAsync(Guid downloadJobId, CancellationToken cancellationToken);

    Task ProcessDownloadedFileAsync(Guid downloadedFileId, CancellationToken cancellationToken);

    Task RetryFailedAiAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Picks recent downloaded files without a successful OCR job and runs the pipeline (bounded batch).
    /// </summary>
    Task ProcessUnprocessedDownloadedFilesAsync(int maxFiles, CancellationToken cancellationToken);
}
