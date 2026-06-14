using MIP.Aws.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.Roles;

public sealed class GetRolesQueryHandler(RoleManager<ApplicationRole> roleManager) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleListItemDto>>
{
    public async Task<IReadOnlyList<RoleListItemDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        return await roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleListItemDto(r.Id, r.Name ?? string.Empty, r.Description))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
