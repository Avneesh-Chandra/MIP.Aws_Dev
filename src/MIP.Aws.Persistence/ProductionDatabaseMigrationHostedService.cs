using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Persistence;

/// <summary>
/// Applies production EF migrations after Kestrel starts so ECS /health/live can pass while migrations run.
/// Uses a SQL application lock so only one task migrates when several ECS tasks start together.
/// </summary>
public sealed class ProductionDatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<ProductionDatabaseMigrationHostedService> logger) : IHostedService
{
    private const string MigrationAppLockResource = "MIP_Aws_EfMigrate";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(3);

    public async Task StartAsync(CancellationToken cancellationToken)
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
        if (string.IsNullOrWhiteSpace(main))
        {
            return;
        }

        await DatabaseBootstrap.EnsureAuxiliarySqlCatalogAsync(configuration, cancellationToken)
            .ConfigureAwait(false);
        await SqlServerDatabaseEnsurer.EnsureDatabaseExistsAsync(main, cancellationToken).ConfigureAwait(false);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaIntelligenceDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("No pending EF Core migrations.");
            return;
        }

        logger.LogInformation(
            "Applying {Count} pending EF Core migration(s): {Migrations}",
            pending.Count,
            string.Join(", ", pending));

        await using var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var lockAcquired = await TryAcquireMigrationLockAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!lockAcquired)
        {
            logger.LogWarning(
                "Could not acquire EF migration lock within {Timeout}; another task is likely migrating.",
                LockTimeout);
            await WaitForMigrationsAsync(db, pending, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var stillPending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            if (stillPending.Count > 0)
            {
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("EF Core migrations applied successfully.");
            }
        }
        finally
        {
            await ReleaseMigrationLockAsync(connection, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> TryAcquireMigrationLockAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = @timeoutMs;
            SELECT @result;
            """;

        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.Value = MigrationAppLockResource;
        command.Parameters.Add(resource);

        var timeout = command.CreateParameter();
        timeout.ParameterName = "@timeoutMs";
        timeout.Value = (int)LockTimeout.TotalMilliseconds;
        command.Parameters.Add(timeout);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is int code && code >= 0;
    }

    private static async Task ReleaseMigrationLockAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_releaseapplock
                @Resource = @resource,
                @LockOwner = 'Session';
            SELECT @result;
            """;

        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.Value = MigrationAppLockResource;
        command.Parameters.Add(resource);

        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForMigrationsAsync(
        MediaIntelligenceDbContext db,
        IReadOnlyList<string> expectedPending,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(LockTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("EF Core migrations were applied by another task.");
                return;
            }

            if (!pending.SequenceEqual(expectedPending))
            {
                logger.LogInformation(
                    "Pending migrations changed while waiting (now {Count}); continuing startup.",
                    pending.Count);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning(
            "Timed out waiting for another task to finish EF Core migrations. Pending: {Migrations}",
            string.Join(", ", expectedPending));
    }
}
