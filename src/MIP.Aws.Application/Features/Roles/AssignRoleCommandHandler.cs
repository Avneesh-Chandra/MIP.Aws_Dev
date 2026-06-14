using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MIP.Aws.Application.Features.Roles;

public sealed class AssignRoleCommandHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IApplicationDbContext db)
    : IRequestHandler<AssignRoleCommand, Unit>
{
    public async Task<Unit> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(request.RoleName).ConfigureAwait(false);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{request.RoleName}' does not exist.");
        }

        if (!ApplicationRoles.All.Contains(role.Name!, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Only predefined system roles may be assigned.");
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            throw new InvalidOperationException("User was not found.");
        }

        var result = await userManager.AddToRoleAsync(user, role.Name!).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        db.UserAuditLogs.Add(new UserAuditLog
        {
            UserId = user.Id,
            Action = "role.assigned",
            Details = role.Name,
            IpAddress = null,
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
