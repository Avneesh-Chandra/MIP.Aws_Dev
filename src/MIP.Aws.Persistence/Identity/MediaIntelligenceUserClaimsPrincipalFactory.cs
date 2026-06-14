using System.Security.Claims;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Persistence.Identity;

/// <summary>Adds role and fine-grained permission claims to the Identity cookie principal.</summary>
public sealed class MediaIntelligenceUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);
        var roles = await UserManager.GetRolesAsync(user).ConfigureAwait(false);

        foreach (var permission in RolePermissionMatrix.GetPermissionsForRoles(roles))
        {
            identity.AddClaim(new Claim(PermissionClaimTypes.Permission, permission));
        }

        return identity;
    }
}
