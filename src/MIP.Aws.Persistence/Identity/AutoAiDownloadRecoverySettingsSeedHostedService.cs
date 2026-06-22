using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Persistence.Identity;

/// <summary>
/// Ensures auto AI download recovery is enabled in the database so worker/API agree after deploy.
/// </summary>
public sealed class AutoAiDownloadRecoverySettingsSeedHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AutoAiDownloadRecoverySettingsSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var row = await db.AutoAiDownloadRecoverySettings
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AutoAiDownloadRecoverySettings
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                Enabled = true,
                RunAfterScheduledFailure = true,
                RunAfterManualFailure = true,
                MaxSuggestionsToTry = 5,
                MinimumConfidence = 0.6,
                MaximumRiskAllowed = "High",
                RequireHumanApprovalForMediumRisk = false,
                CooldownMinutesPerSource = 15,
                MaxAutoRecoveryAttemptsPerDayPerSource = 5,
                ActivateSuccessfulCandidateAutomatically = true,
                RollbackOnFailure = true
            };
            db.AutoAiDownloadRecoverySettings.Add(row);
            logger.LogInformation("Seeded AutoAiDownloadRecoverySettings with Enabled=true.");
        }
        else if (!row.Enabled || !row.RunAfterScheduledFailure || !row.RunAfterManualFailure)
        {
            row.Enabled = true;
            row.RunAfterScheduledFailure = true;
            row.RunAfterManualFailure = true;
            row.ModifiedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Updated AutoAiDownloadRecoverySettings to compulsory enabled state.");
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
