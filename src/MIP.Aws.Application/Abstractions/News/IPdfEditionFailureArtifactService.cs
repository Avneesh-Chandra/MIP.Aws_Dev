namespace MIP.Aws.Application.Abstractions.News;

/// <summary>
/// Captures Playwright screenshot/HTML artifacts for failed public PDF edition downloads.
/// </summary>
public interface IPdfEditionFailureArtifactService
{
    /// <summary>
    /// Ensures portal audit artifacts exist for a failed download job (idempotent).
    /// </summary>
    Task EnsureFailureArtifactsAsync(
        Guid newsSourceId,
        Guid downloadJobId,
        string? pageUrl,
        string failureMessage,
        string? failureCode,
        CancellationToken cancellationToken);
}
