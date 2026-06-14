using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Jobs;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class EnqueueNewsSourceDownloadCommandHandler(
    IApplicationDbContext db,
    INewsDownloadJobScheduler scheduler)
    : IRequestHandler<EnqueueNewsSourceDownloadCommand, Unit>
{
    public async Task<Unit> Handle(EnqueueNewsSourceDownloadCommand request, CancellationToken cancellationToken)
    {
        await NewsSourceGuard.EnsureEnabledAsync(db, request.NewsSourceId, cancellationToken).ConfigureAwait(false);
        scheduler.EnqueueDownloadSingle(request.NewsSourceId);
        return Unit.Value;
    }
}
