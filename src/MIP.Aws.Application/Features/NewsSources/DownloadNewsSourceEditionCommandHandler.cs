using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Jobs;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class DownloadNewsSourceEditionCommandHandler(IApplicationDbContext db, INewsDownloadJobScheduler scheduler)
    : IRequestHandler<DownloadNewsSourceEditionCommand, Unit>
{
    public async Task<Unit> Handle(DownloadNewsSourceEditionCommand request, CancellationToken cancellationToken)
    {
        await NewsSourceGuard.EnsureEnabledAsync(db, request.NewsSourceId, cancellationToken).ConfigureAwait(false);

        var exists = await db.NewsSources.AsNoTracking()
            .AnyAsync(s => s.Id == request.NewsSourceId && !s.IsDeleted && s.SourceType == NewsSourceType.WebPortalLogin, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException("News source was not found or is not a WebPortalLogin source.");
        }

        scheduler.EnqueueDownloadSingle(request.NewsSourceId);
        return Unit.Value;
    }
}
