using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Common;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Application.Features.Auth;

public sealed class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = TokenHasher.Hash(request.RefreshToken);
        var existing = await db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || !existing.IsActive || existing.User is null || !existing.User.IsActive || existing.User.IsDeleted)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        var (rawRefresh, newHash, refreshExpires) = jwtTokenService.CreateRefreshToken();
        existing.ReplacedByTokenHash = newHash;

        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = request.IpAddress
        };
        db.RefreshTokens.Add(newToken);

        var user = existing.User;
        var roles = (IReadOnlyList<string>)(await userManager.GetRolesAsync(user).ConfigureAwait(false)).ToArray();
        var access = jwtTokenService.CreateAccessToken(user, roles);
        var accessExpires = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes);

        db.UserAuditLogs.Add(new UserAuditLog
        {
            UserId = user.Id,
            Action = "token.refresh",
            Details = null,
            IpAddress = request.IpAddress,
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AuthResponseDto(
            access,
            rawRefresh,
            accessExpires,
            refreshExpires,
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles);
    }
}
