using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Application.Features.Auth;

public sealed class LoginCommandHandler(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IApplicationDbContext db,
    IOptions<JwtOptions> jwtOptions,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null || !user.IsActive || user.IsDeleted)
        {
            AddAudit(null, "login.failed", "Invalid credentials or inactive user", request.IpAddress);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var valid = await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false);
        if (!valid)
        {
            AddAudit(user.Id, "login.failed", "Invalid password", request.IpAddress);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var roles = (IReadOnlyList<string>)(await userManager.GetRolesAsync(user).ConfigureAwait(false)).ToArray();
        var access = jwtTokenService.CreateAccessToken(user, roles);
        var (rawRefresh, hash, refreshExpires) = jwtTokenService.CreateRefreshToken();
        var accessMinutes = jwtOptions.Value.AccessTokenMinutes;
        var accessExpires = DateTimeOffset.UtcNow.AddMinutes(accessMinutes);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = request.IpAddress
        });

        AddAudit(user.Id, "login.success", null, request.IpAddress);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} authenticated successfully", user.Id);

        return new AuthResponseDto(
            access,
            rawRefresh,
            accessExpires,
            refreshExpires,
            user.Id,
            user.Email ?? request.Email,
            user.DisplayName,
            roles);
    }

    private void AddAudit(Guid? userId, string action, string? details, string? ip)
    {
        db.UserAuditLogs.Add(new UserAuditLog
        {
            UserId = userId,
            Action = action,
            Details = details,
            IpAddress = ip,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }
}
