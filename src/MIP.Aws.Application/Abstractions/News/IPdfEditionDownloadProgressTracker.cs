namespace MIP.Aws.Application.Abstractions.News;

/// <summary>
/// In-memory progress for an active PDF edition download (polled by the admin UI).
/// </summary>
public interface IPdfEditionDownloadProgressTracker
{
    void Report(Guid newsSourceId, int percent, string phase);

    PdfEditionDownloadProgressSnapshot? Get(Guid newsSourceId);

    void Complete(Guid newsSourceId);

    void Clear(Guid newsSourceId);
}

public sealed record PdfEditionDownloadProgressSnapshot(
    int Percent,
    string Phase,
    bool IsComplete,
    DateTimeOffset UpdatedAt);
