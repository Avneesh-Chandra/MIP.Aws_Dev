using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class GetLatestPortalEditionQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetLatestPortalEditionQuery, PortalLatestEditionDto?>
{
    public async Task<PortalLatestEditionDto?> Handle(GetLatestPortalEditionQuery request, CancellationToken cancellationToken)
    {
        var file = await db.DownloadedFiles.AsNoTracking()
            .Include(f => f.DownloadJob)
            .Where(f => !f.IsDeleted
                        && f.DownloadJob!.NewsSourceId == request.NewsSourceId
                        && f.ContentType == "application/pdf")
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return file is null
            ? null
            : new PortalLatestEditionDto(
                file.Id,
                file.BlobUri,
                file.SizeBytes,
                file.Sha256,
                file.CreatedAt,
                $"/api/v1/news-sources/{request.NewsSourceId}/pdf/{file.Id}");
    }
}

public sealed class GetPortalEditionHistoryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPortalEditionHistoryQuery, IReadOnlyList<PortalEditionHistoryItemDto>>
{
    public async Task<IReadOnlyList<PortalEditionHistoryItemDto>> Handle(
        GetPortalEditionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 100);
        return await db.DownloadedFiles.AsNoTracking()
            .Include(f => f.DownloadJob)
            .Where(f => !f.IsDeleted
                        && f.DownloadJob!.NewsSourceId == request.NewsSourceId
                        && f.ContentType == "application/pdf")
            .OrderByDescending(f => f.CreatedAt)
            .Take(take)
            .Select(f => new PortalEditionHistoryItemDto(
                f.Id,
                f.DownloadJobId,
                f.BlobUri,
                f.ContentType,
                f.SizeBytes,
                f.Sha256,
                f.CreatedAt,
                $"/api/v1/news-sources/{request.NewsSourceId}/pdf/{f.Id}"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class GetPortalEditionDownloadProgressQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPortalEditionDownloadProgressQuery, PortalEditionDownloadProgressDto?>
{
    public async Task<PortalEditionDownloadProgressDto?> Handle(
        GetPortalEditionDownloadProgressQuery request,
        CancellationToken cancellationToken)
    {
        var since = request.Since ?? DateTimeOffset.UtcNow.AddMinutes(-15);
        var job = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.NewsSourceId == request.NewsSourceId && j.CreatedAt >= since)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            if (request.Since is not null && DateTimeOffset.UtcNow - request.Since.Value < TimeSpan.FromMinutes(2))
            {
                return new PortalEditionDownloadProgressDto(12, "Queued…", false, null, null);
            }

            return null;
        }

        return job.Status switch
        {
            DownloadJobStatus.Pending => new(18, "Queued…", false, job.Status.ToString(), null),
            DownloadJobStatus.Running => MapRunning(job),
            DownloadJobStatus.Succeeded => new(100, "Complete", true, job.Status.ToString(), null),
            DownloadJobStatus.Failed => new(100, "Failed", true, job.Status.ToString(), job.ErrorMessage),
            DownloadJobStatus.Cancelled => new(100, "Cancelled", true, job.Status.ToString(), job.ErrorMessage),
            _ => new(0, "Unknown", false, job.Status.ToString(), job.ErrorMessage)
        };
    }

    private static PortalEditionDownloadProgressDto MapRunning(Domain.Entities.DownloadJob job)
    {
        var elapsed = job.StartedAt is null
            ? TimeSpan.Zero
            : DateTimeOffset.UtcNow - job.StartedAt.Value;
        var percent = Math.Clamp(35 + (int)elapsed.TotalSeconds, 35, 92);
        return new(percent, "Downloading from portal…", false, job.Status.ToString(), null);
    }
}
