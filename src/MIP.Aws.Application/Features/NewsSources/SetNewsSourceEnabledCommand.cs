using MIP.Aws.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record SetNewsSourceEnabledCommand(Guid NewsSourceId, bool IsEnabled) : IRequest<bool>;

public sealed class SetNewsSourceEnabledCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SetNewsSourceEnabledCommand, bool>
{
    public async Task<bool> Handle(SetNewsSourceEnabledCommand request, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .Include(s => s.DownloadSchedule)
            .FirstOrDefaultAsync(s => s.Id == request.NewsSourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("News source not found.");

        source.IsEnabled = request.IsEnabled;
        source.ModifiedAt = DateTimeOffset.UtcNow;

        if (source.DownloadSchedule is not null)
        {
            source.DownloadSchedule.IsEnabled = request.IsEnabled;
            source.DownloadSchedule.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return source.IsEnabled;
    }
}
