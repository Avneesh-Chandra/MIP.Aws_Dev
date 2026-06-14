using MIP.Aws.Domain.Security;
using MediatR;

namespace MIP.Aws.Application.Features.Roles;

public sealed record GetPermissionCatalogQuery : IRequest<PermissionCatalogDto>;

public sealed class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, PermissionCatalogDto>
{
    public Task<PermissionCatalogDto> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
    {
        var mappings = ApplicationRoles.All
            .Select(role => new RolePermissionCatalogDto(role, RolePermissionMatrix.GetPermissionsForRole(role).OrderBy(p => p).ToArray()))
            .ToArray();

        var dto = new PermissionCatalogDto(ApplicationPermissions.All, mappings);
        return Task.FromResult(dto);
    }
}
