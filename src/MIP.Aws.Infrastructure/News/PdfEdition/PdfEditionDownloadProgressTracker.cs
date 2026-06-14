using System.Collections.Concurrent;
using MIP.Aws.Application.Abstractions.News;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PdfEditionDownloadProgressTracker : IPdfEditionDownloadProgressTracker
{
    private readonly ConcurrentDictionary<Guid, PdfEditionDownloadProgressSnapshot> _state = new();

    public void Report(Guid newsSourceId, int percent, string phase)
    {
        var clamped = Math.Clamp(percent, 0, 99);
        _state[newsSourceId] = new PdfEditionDownloadProgressSnapshot(
            clamped,
            phase,
            IsComplete: false,
            DateTimeOffset.UtcNow);
    }

    public PdfEditionDownloadProgressSnapshot? Get(Guid newsSourceId) =>
        _state.TryGetValue(newsSourceId, out var snapshot) ? snapshot : null;

    public void Complete(Guid newsSourceId)
    {
        _state[newsSourceId] = new PdfEditionDownloadProgressSnapshot(
            100,
            "Complete",
            IsComplete: true,
            DateTimeOffset.UtcNow);
    }

    public void Clear(Guid newsSourceId) => _state.TryRemove(newsSourceId, out _);
}
