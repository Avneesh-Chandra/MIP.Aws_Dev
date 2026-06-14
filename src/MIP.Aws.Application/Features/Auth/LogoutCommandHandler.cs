using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Common;
using MIP.Aws.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.Auth;

public sealed class LogoutCommandHandler(IApplicationDbContext db) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = TokenHasher.Hash(request.RefreshToken);
            var token = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash && x.UserId == request.UserId, cancellationToken).ConfigureAwait(false);
            if (token is not null && token.RevokedAt is null)
            {
                token.RevokedAt = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            var tokens = await db.RefreshTokens.Where(x => x.UserId == request.UserId && x.RevokedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var t in tokens)
            {
                t.RevokedAt = DateTimeOffset.UtcNow;
            }
        }

        db.UserAuditLogs.Add(new UserAuditLog
        {
            UserId = request.UserId,
            Action = "logout",
            Details = null,
            IpAddress = request.IpAddress,
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
