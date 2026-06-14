using MIP.Aws.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class DeleteNewsSourceCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteNewsSourceCommand, Unit>
{
    public async Task<Unit> Handle(DeleteNewsSourceCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.NewsSources.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            throw new InvalidOperationException("News source was not found.");
        }

        entity.IsDeleted = true;
        entity.IsEnabled = false;
        entity.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
