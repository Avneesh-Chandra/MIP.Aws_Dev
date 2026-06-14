namespace MIP.Aws.Application.Features.Roles;

public sealed record RolePermissionCatalogDto(
    string RoleName,
    IReadOnlyList<string> Permissions);

public sealed record PermissionCatalogDto(
    IReadOnlyList<string> AllPermissions,
    IReadOnlyList<RolePermissionCatalogDto> RoleMappings);
