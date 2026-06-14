using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class GetDownloadMetricsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDownloadMetricsQuery, DownloadMetricsDto>
{
    public async Task<DownloadMetricsDto> Handle(GetDownloadMetricsQuery request, CancellationToken cancellationToken)
    {
        var total = await db.DownloadJobs.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);
        var failed = await db.DownloadJobs.AsNoTracking().CountAsync(j => j.Status == DownloadJobStatus.Failed, cancellationToken).ConfigureAwait(false);
        var active = await db.NewsSources.AsNoTracking().CountAsync(s => s.IsEnabled && !s.IsDeleted, cancellationToken).ConfigureAwait(false);
        var pending = await db.DownloadJobs.AsNoTracking().CountAsync(j => j.Status == DownloadJobStatus.Running, cancellationToken).ConfigureAwait(false);

        var latest = await db.DownloadJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .Select(j => new RecentDownloadJobDto(
                j.Id,
                j.NewsSourceId,
                j.NewsSource!.Name,
                j.Status.ToString(),
                j.CreatedAt,
                j.CompletedAt,
                j.DurationMs,
                j.HttpStatusCode))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DownloadMetricsDto(total, failed, active, pending, latest);
    }
}
