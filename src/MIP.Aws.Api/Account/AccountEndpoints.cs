using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Account;

/// <summary>
/// Browser cookie sign-in for Blazor UI.
/// </summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/account/cookie-login", CookieLoginAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
    }

    private static async Task<IResult> CookieLoginAsync(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        [FromForm] string email,
        [FromForm] string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Results.Redirect("/Account/Login?error=missing");
        }

        var result = await signInManager.PasswordSignInAsync(
                email.Trim(),
                password,
                isPersistent: true,
                lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            var user = await userManager.FindByEmailAsync(email.Trim()).ConfigureAwait(false);
            var roles = user is null
                ? Array.Empty<string>()
                : (IReadOnlyList<string>)(await userManager.GetRolesAsync(user).ConfigureAwait(false)).ToArray();
            return Results.Redirect(RoleDashboardRoutes.GetHomeRoute(roles));
        }

        if (result.IsLockedOut)
        {
            return Results.Redirect("/Account/Login?error=locked");
        }

        if (result.RequiresTwoFactor)
        {
            return Results.Redirect("/Account/Login?error=mfa");
        }

        return Results.Redirect("/Account/Login?error=invalid");
    }
}
