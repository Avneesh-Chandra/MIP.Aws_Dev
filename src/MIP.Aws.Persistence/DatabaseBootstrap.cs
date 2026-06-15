using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MIP.Aws.Persistence;

public static class DatabaseBootstrap
{
    /// <summary>
    /// Applies pending EF Core migrations (creates the application database and schema if needed).
    /// </summary>
    public static async Task MigrateMediaIntelligenceAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaIntelligenceDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the Hangfire catalog exists when it differs from the main EF catalog.
    /// </summary>
    public static async Task EnsureAuxiliarySqlCatalogAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var main = configuration.GetConnectionString("DefaultConnection");
        var auxiliary = configuration.GetConnectionString("Hangfire");
        if (string.IsNullOrWhiteSpace(main) || string.IsNullOrWhiteSpace(auxiliary))
        {
            return;
        }

        var mainDb = new SqlConnectionStringBuilder(main).InitialCatalog;
        var auxDb = new SqlConnectionStringBuilder(auxiliary).InitialCatalog;
        if (string.Equals(mainDb, auxDb, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await SqlServerDatabaseEnsurer.EnsureDatabaseExistsAsync(auxiliary, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Development/local convenience: ensure auxiliary SQL catalog (if configured) then apply EF migrations.
    /// </summary>
    public static async Task ApplyDevelopmentDatabaseAsync(
        IHostEnvironment environment,
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var main = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(main))
        {
            await SqlServerDatabaseEnsurer.EnsureDatabaseExistsAsync(main, cancellationToken).ConfigureAwait(false);
        }

        await EnsureAuxiliarySqlCatalogAsync(configuration, cancellationToken).ConfigureAwait(false);
        await services.MigrateMediaIntelligenceAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Production first-deploy: set Database:AutoMigrateOnStartup=true in ECS env (dev only).
    /// </summary>
    public static async Task ApplyProductionMigrationIfRequestedAsync(
        IHostEnvironment environment,
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        if (!configuration.GetValue("Database:AutoMigrateOnStartup", false))
        {
            return;
        }

        var main = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(main))
        {
            await SqlServerDatabaseEnsurer.EnsureDatabaseExistsAsync(main, cancellationToken).ConfigureAwait(false);
        }

        await EnsureAuxiliarySqlCatalogAsync(configuration, cancellationToken).ConfigureAwait(false);
        await services.MigrateMediaIntelligenceAsync(cancellationToken).ConfigureAwait(false);
    }
}
