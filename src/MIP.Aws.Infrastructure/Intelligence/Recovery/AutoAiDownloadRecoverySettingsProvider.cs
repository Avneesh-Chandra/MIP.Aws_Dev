using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

public sealed class AutoAiDownloadRecoverySettingsProvider(
    IApplicationDbContext db,
    IOptions<AutoAiDownloadRecoveryOptions> options) : IAutoAiDownloadRecoverySettingsReader
{
    public async Task<AutoAiDownloadRecoveryOptions> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        var effective = Clone(options.Value);
        var row = await db.AutoAiDownloadRecoverySettings.AsNoTracking()
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return effective;
        }

        effective.Enabled = row.Enabled;
        effective.RunAfterScheduledFailure = row.RunAfterScheduledFailure;
        effective.RunAfterManualFailure = row.RunAfterManualFailure;
        effective.MaxSuggestionsToTry = row.MaxSuggestionsToTry;
        effective.MinimumConfidence = row.MinimumConfidence;
        effective.MaximumRiskAllowed = row.MaximumRiskAllowed;
        effective.RequireHumanApprovalForMediumRisk = row.RequireHumanApprovalForMediumRisk;
        effective.CooldownMinutesPerSource = row.CooldownMinutesPerSource;
        effective.MaxAutoRecoveryAttemptsPerDayPerSource = row.MaxAutoRecoveryAttemptsPerDayPerSource;
        effective.ActivateSuccessfulCandidateAutomatically = row.ActivateSuccessfulCandidateAutomatically;
        effective.RollbackOnFailure = row.RollbackOnFailure;
        return effective;
    }

    private static AutoAiDownloadRecoveryOptions Clone(AutoAiDownloadRecoveryOptions source) => new()
    {
        Enabled = source.Enabled,
        RunAfterScheduledFailure = source.RunAfterScheduledFailure,
        RunAfterManualFailure = source.RunAfterManualFailure,
        MaxSuggestionsToTry = source.MaxSuggestionsToTry,
        MinimumConfidence = source.MinimumConfidence,
        MaximumRiskAllowed = source.MaximumRiskAllowed,
        RequireHumanApprovalForMediumRisk = source.RequireHumanApprovalForMediumRisk,
        OnlyForSourceTypes = source.OnlyForSourceTypes.ToArray(),
        CooldownMinutesPerSource = source.CooldownMinutesPerSource,
        MaxAutoRecoveryAttemptsPerDayPerSource = source.MaxAutoRecoveryAttemptsPerDayPerSource,
        ActivateSuccessfulCandidateAutomatically = source.ActivateSuccessfulCandidateAutomatically,
        RollbackOnFailure = source.RollbackOnFailure,
        SystemActorUserId = source.SystemActorUserId
    };
}
