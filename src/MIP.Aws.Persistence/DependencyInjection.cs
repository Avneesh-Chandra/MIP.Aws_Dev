using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MIP.Aws.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<MediaIntelligenceDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(MediaIntelligenceDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            }));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<MediaIntelligenceDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, MediaIntelligenceUserClaimsPrincipalFactory>();

        services.Configure<IdentitySeedOptions>(configuration.GetSection(IdentitySeedOptions.SectionName));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<MediaIntelligenceDbContext>());
        services.AddHostedService<IdentitySeedHostedService>();
        services.AddHostedService<NewsSourceTemplateSeedHostedService>();
        services.AddHostedService<NewspaperCatalogSeedHostedService>();

        return services;
    }
}
