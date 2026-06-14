using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

public sealed class GetPdfEditionHistoryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPdfEditionHistoryQuery, IReadOnlyList<PdfEditionHistoryItemDto>>
{
    public async Task<IReadOnlyList<PdfEditionHistoryItemDto>> Handle(
        GetPdfEditionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 100);
        return await db.PdfEditionDownloads.AsNoTracking()
            .Where(x => !x.IsDeleted && x.NewsSourceId == request.NewsSourceId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new PdfEditionHistoryItemDto(
                x.Id,
                x.SourceUrl,
                x.SavedPath,
                x.FileName,
                x.FileSizeBytes,
                x.EditionDate,
                x.DiscoveryConfidence,
                x.DiscoveryMethod.ToString(),
                x.Status.ToString(),
                x.FailureReason,
                x.DiscoveredAt,
                x.DownloadedAt,
                x.DownloadedFileId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class GetPdfEditionDownloadProgressQueryHandler(IPdfEditionDownloadProgressTracker progressTracker)
    : IRequestHandler<GetPdfEditionDownloadProgressQuery, PdfEditionDownloadProgressDto?>
{
    public Task<PdfEditionDownloadProgressDto?> Handle(
        GetPdfEditionDownloadProgressQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = progressTracker.Get(request.NewsSourceId);
        return Task.FromResult(snapshot is null
            ? null
            : new PdfEditionDownloadProgressDto(snapshot.Percent, snapshot.Phase, snapshot.IsComplete));
    }
}

public sealed class StreamPdfEditionQueryHandler(
    IApplicationDbContext db,
    IFileStorageService fileStorage,
    IAuditService audit) : IRequestHandler<StreamPdfEditionQuery, PdfEditionStreamResult?>
{
    public async Task<PdfEditionStreamResult?> Handle(StreamPdfEditionQuery request, CancellationToken cancellationToken)
    {
        var file = await db.DownloadedFiles.AsNoTracking()
            .Include(f => f.DownloadJob)
            .FirstOrDefaultAsync(
                f => f.Id == request.FileId && !f.IsDeleted && f.DownloadJob!.NewsSourceId == request.NewsSourceId,
                cancellationToken)
            .ConfigureAwait(false);

        if (file is null)
        {
            return null;
        }

        // Path traversal guard — blob keys must stay relative without ..
        if (file.BlobUri.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid stored path.");
        }

        var bytes = await fileStorage.ReadAsync(file.BlobUri, cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        await audit.RecordAdminActionAsync(
            PdfEditionAuditEvents.ViewedByUser,
            "DownloadedFile",
            file.Id.ToString(),
            new { request.NewsSourceId, inline = request.Inline },
            cancellationToken).ConfigureAwait(false);

        return new PdfEditionStreamResult(bytes, file.ContentType, "today-edition.pdf");
    }
}
