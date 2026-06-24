using MIP.Aws.Application.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Persistence;

/// <summary>Registers Hangfire recurring jobs after migrations finish so SQL schema is ready.</summary>
public sealed class HangfireRecurringJobsHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<HangfireRecurringJobsHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await DatabaseBootstrap.EnsureAuxiliarySqlCatalogAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        if (!environment.IsDevelopment()
            && configuration.GetValue("Database:AutoMigrateOnStartup", false))
        {
            await WaitForMigrationsAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<IScheduledJobRegistry>().RegisterRecurringJobs();
            logger.LogInformation("Hangfire recurring jobs registered.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Hangfire recurring jobs on startup.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WaitForMigrationsAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaIntelligenceDbContext>();
            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            if (!pending.Any())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning("Timed out waiting for EF migrations before registering Hangfire jobs.");
    }
}
