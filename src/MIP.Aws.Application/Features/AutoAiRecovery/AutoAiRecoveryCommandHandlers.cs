using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

public sealed class TriggerAutoAiRecoveryCommandHandler(
    IApplicationDbContext db,
    IAutoAiDownloadRecoveryOrchestrator orchestrator) : IRequestHandler<TriggerAutoAiRecoveryCommand, AutoAiRecoveryResultDto>
{
    public async Task<AutoAiRecoveryResultDto> Handle(TriggerAutoAiRecoveryCommand request, CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.DownloadJobId && !j.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Download job was not found.");

        return await orchestrator.RecoverAsync(
            job.NewsSourceId,
            job.Id,
            AutoAiRecoveryTrigger.OperatorRequested,
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class GetAutoAiRecoveryStatusQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAutoAiRecoveryStatusQuery, AutoAiRecoveryStatusDto?>
{
    public async Task<AutoAiRecoveryStatusDto?> Handle(GetAutoAiRecoveryStatusQuery request, CancellationToken cancellationToken)
    {
        var run = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.FailedDownloadJobId == request.DownloadJobId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return run is null
            ? null
            : new AutoAiRecoveryStatusDto(
                run.Id,
                run.FailedDownloadJobId,
                run.Status,
                run.ResultSummary,
                run.SuggestionsTried,
                run.SuccessfulOptionTitle,
                run.RetryDownloadJobId,
                run.CompletedAt);
    }
}

public sealed class GetAutoAiRecoveryTimelineQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAutoAiRecoveryTimelineQuery, IReadOnlyList<AutoAiRecoveryTimelineStepDto>>
{
    public async Task<IReadOnlyList<AutoAiRecoveryTimelineStepDto>> Handle(
        GetAutoAiRecoveryTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var json = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.FailedDownloadJobId == request.DownloadJobId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.TimelineJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return AutoAiRecoveryTimelineJson.Deserialize(json);
    }
}

public sealed class GetAutoAiDownloadRecoverySettingsQueryHandler(IAutoAiDownloadRecoverySettingsReader provider)
    : IRequestHandler<GetAutoAiDownloadRecoverySettingsQuery, AutoAiDownloadRecoverySettingsDto>
{
    public async Task<AutoAiDownloadRecoverySettingsDto> Handle(
        GetAutoAiDownloadRecoverySettingsQuery request,
        CancellationToken cancellationToken)
    {
        var s = await provider.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(s);
    }

    internal static AutoAiDownloadRecoverySettingsDto ToDto(Application.Configuration.AutoAiDownloadRecoveryOptions s) =>
        new(
            s.Enabled,
            s.RunAfterScheduledFailure,
            s.RunAfterManualFailure,
            s.MaxSuggestionsToTry,
            s.MinimumConfidence,
            s.MaximumRiskAllowed,
            s.RequireHumanApprovalForMediumRisk,
            s.CooldownMinutesPerSource,
            s.MaxAutoRecoveryAttemptsPerDayPerSource,
            s.ActivateSuccessfulCandidateAutomatically,
            s.RollbackOnFailure);
}

public sealed class UpdateAutoAiDownloadRecoverySettingsCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateAutoAiDownloadRecoverySettingsCommand, AutoAiDownloadRecoverySettingsDto>
{
    public async Task<AutoAiDownloadRecoverySettingsDto> Handle(
        UpdateAutoAiDownloadRecoverySettingsCommand request,
        CancellationToken cancellationToken)
    {
        var row = await db.AutoAiDownloadRecoverySettings
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.ModifiedAt ?? s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AutoAiDownloadRecoverySettings { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            db.AutoAiDownloadRecoverySettings.Add(row);
        }

        row.Enabled = request.Settings.Enabled;
        row.RunAfterScheduledFailure = request.Settings.RunAfterScheduledFailure;
        row.RunAfterManualFailure = request.Settings.RunAfterManualFailure;
        row.MaxSuggestionsToTry = request.Settings.MaxSuggestionsToTry;
        row.MinimumConfidence = request.Settings.MinimumConfidence;
        row.MaximumRiskAllowed = request.Settings.MaximumRiskAllowed;
        row.RequireHumanApprovalForMediumRisk = request.Settings.RequireHumanApprovalForMediumRisk;
        row.CooldownMinutesPerSource = request.Settings.CooldownMinutesPerSource;
        row.MaxAutoRecoveryAttemptsPerDayPerSource = request.Settings.MaxAutoRecoveryAttemptsPerDayPerSource;
        row.ActivateSuccessfulCandidateAutomatically = request.Settings.ActivateSuccessfulCandidateAutomatically;
        row.RollbackOnFailure = request.Settings.RollbackOnFailure;
        row.ModifiedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Settings;
    }
}

public sealed class SetSourceAutoAiRecoveryCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SetSourceAutoAiRecoveryCommand, bool>
{
    public async Task<bool> Handle(SetSourceAutoAiRecoveryCommand request, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .FirstOrDefaultAsync(s => s.Id == request.SourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Source was not found.");

        source.AutoAiRecoveryEnabled = request.Enabled;
        source.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
