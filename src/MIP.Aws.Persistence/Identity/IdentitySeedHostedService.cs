using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Persistence.Identity;

/// <summary>
/// Seeds system roles, default SuperAdmin, and optional Development/UAT role test accounts.
/// Passwords are supplied through configuration — never hard-coded in source.
/// </summary>
public sealed class IdentitySeedHostedService(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    IOptions<IdentitySeedOptions> seedOptions,
    ILogger<IdentitySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = seedOptions.Value;

        foreach (var roleName in ApplicationRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                continue;
            }

            var role = new ApplicationRole
            {
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
                Description = $"System role: {roleName}",
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await roleManager.CreateAsync(role).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                logger.LogError("Failed to create role {Role}: {Errors}", roleName, string.Join(",", result.Errors.Select(e => e.Description)));
            }
            else
            {
                logger.LogInformation("Seeded role {Role}", roleName);
            }
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultAdminPassword))
        {
            await EnsureUserAsync(
                    userManager,
                    options.DefaultAdminEmail,
                    options.DefaultAdminPassword,
                    "GFH System Administrator",
                    ApplicationRoles.SuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            logger.LogWarning(
                "Identity seed skipped default SuperAdmin user: {Section}:DefaultAdminPassword is not configured.",
                IdentitySeedOptions.SectionName);
        }

        if (hostEnvironment.IsDevelopment() || options.SeedDevelopmentRoleUsers)
        {
            foreach (var user in options.DevelopmentUsers.Where(u =>
                         !string.IsNullOrWhiteSpace(u.Email)
                         && !string.IsNullOrWhiteSpace(u.Password)
                         && !string.IsNullOrWhiteSpace(u.Role)))
            {
                await EnsureUserAsync(
                        userManager,
                        user.Email.Trim(),
                        user.Password,
                        string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email.Trim() : user.DisplayName.Trim(),
                        user.Role.Trim(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (hostEnvironment.IsDevelopment() && options.DevelopmentUsers.Count > 0)
            {
                LogDevelopmentCredentials(options.DevelopmentUsers);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string displayName,
        string role,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                EmailConfirmed = true,
                DisplayName = displayName,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var create = await userManager.CreateAsync(user, password).ConfigureAwait(false);
            if (!create.Succeeded)
            {
                logger.LogError("Failed to create user {Email}: {Errors}", email, string.Join(",", create.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Seeded user {Email} ({Role})", email, role);
        }

        if (!await userManager.IsInRoleAsync(user, role).ConfigureAwait(false))
        {
            await userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
        }
    }

    private void LogDevelopmentCredentials(IReadOnlyList<SeedDevelopmentUserOptions> users)
    {
        logger.LogWarning("=== MIP.Aws — Development test accounts (local only) ===");
        foreach (var user in users)
        {
            logger.LogWarning(
                "{Role}: {Email} → {Route}",
                user.Role,
                user.Email,
                RoleDashboardRoutes.GetHomeRoute([user.Role]));
        }

        logger.LogWarning("=== End development credentials (never enable in Production) ===");
    }
}
