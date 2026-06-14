using System.Security.Claims;

namespace MIP.Aws.Domain.Security;

/// <summary>Evaluates permission claims and role matrix membership for authorization.</summary>
public static class PermissionEvaluator
{
    public static bool HasPermission(ClaimsPrincipal? user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (user.IsInRole(ApplicationRoles.SuperAdmin))
        {
            return true;
        }

        if (user.HasClaim(PermissionClaimTypes.Permission, permission))
        {
            return true;
        }

        foreach (var role in user.FindAll(ClaimTypes.Role).Select(c => c.Value))
        {
            if (RolePermissionMatrix.RoleHasPermission(role, permission))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasAnyPermission(ClaimsPrincipal? user, params string[] permissions) =>
        permissions.Any(p => HasPermission(user, p));

    public static IReadOnlyList<string> GetEffectivePermissions(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<string>();
        }

        if (user.IsInRole(ApplicationRoles.SuperAdmin))
        {
            return ApplicationPermissions.All;
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var fromMatrix = RolePermissionMatrix.GetPermissionsForRoles(roles);
        var fromClaims = user.FindAll(PermissionClaimTypes.Permission).Select(c => c.Value);
        return fromMatrix.Union(fromClaims, StringComparer.Ordinal).OrderBy(p => p).ToArray();
    }
}
