using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MIP.Aws.Application.Features.Auth;

public sealed class RegisterUserCommandHandler(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IApplicationDbContext db)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(request.RoleName).ConfigureAwait(false);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{request.RoleName}' does not exist.");
        }

        if (!ApplicationRoles.All.Contains(role.Name!, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Only system roles may be assigned during registration.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            NormalizedUserName = request.Email.ToUpperInvariant(),
            EmailConfirmed = true,
            DisplayName = request.DisplayName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role.Name!).ConfigureAwait(false);

        db.UserAuditLogs.Add(new UserAuditLog
        {
            UserId = user.Id,
            Action = "user.registered",
            Details = role.Name,
            IpAddress = null,
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user.Id;
    }
}
