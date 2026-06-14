using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MIP.Aws.Application.Features.Roles;

public sealed record RemoveRoleCommand(Guid UserId, string RoleName) : IRequest<Unit>;

public sealed class RemoveRoleCommandHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IApplicationDbContext db) : IRequestHandler<RemoveRoleCommand, Unit>
{
    public async Task<Unit> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(request.RoleName).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Role '{request.RoleName}' does not exist.");

        if (!ApplicationRoles.All.Contains(role.Name!, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Only predefined system roles may be removed.");
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false)
            ?? throw new InvalidOperationException("User was not found.");

        if (user.IsDeleted)
        {
            throw new InvalidOperationException("User was not found.");
        }

        var result = await userManager.RemoveFromRoleAsync(user, role.Name!).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        db.UserAuditLogs.Add(new UserAuditLog
        {
            UserId = user.Id,
            Action = "role.removed",
            Details = role.Name,
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
