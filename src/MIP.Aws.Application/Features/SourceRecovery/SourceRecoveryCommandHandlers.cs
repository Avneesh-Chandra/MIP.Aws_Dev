using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.SourceRecovery;

public sealed class AnalyzeSourceRecoveryCommandHandler(
    ISourceRecoveryAnalysisService recovery,
    ICurrentUserContext currentUser) : IRequestHandler<AnalyzeSourceRecoveryCommand, SourceRecoveryAnalysisDto>
{
    public Task<SourceRecoveryAnalysisDto> Handle(AnalyzeSourceRecoveryCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");
        return recovery.AnalyzeAndPersistAsync(request.DownloadJobId, userId, cancellationToken);
    }
}

public sealed class GetSourceRecoveryAnalysisQueryHandler(
    ISourceRecoveryAnalysisService recovery,
    IAuditService audit) : IRequestHandler<GetSourceRecoveryAnalysisQuery, SourceRecoveryAnalysisDto?>
{
    public async Task<SourceRecoveryAnalysisDto?> Handle(GetSourceRecoveryAnalysisQuery request, CancellationToken cancellationToken)
    {
        var result = await recovery.GetAnalysisAsync(request.AttemptId, cancellationToken).ConfigureAwait(false);
        if (result is not null)
        {
            await audit.RecordAdminActionAsync(
                SourceRecoveryAuditEvents.SuggestionViewed,
                "SourceRecoveryAttempt",
                request.AttemptId.ToString(),
                null,
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}

public sealed class PreviewSourceRecoveryCommandHandler(ISourceRecoveryOrchestrator orchestrator)
    : IRequestHandler<PreviewSourceRecoveryCommand, SourceRecoveryPreviewDto>
{
    public Task<SourceRecoveryPreviewDto> Handle(PreviewSourceRecoveryCommand request, CancellationToken cancellationToken) =>
        orchestrator.PreviewChangesAsync(request.AttemptId, request.OptionIndex, cancellationToken);
}

public sealed class ApplySourceRecoveryCommandHandler(
    ISourceRecoveryOrchestrator orchestrator,
    ICurrentUserContext currentUser) : IRequestHandler<ApplySourceRecoveryCommand, SourceRecoveryApplyResultDto>
{
    public Task<SourceRecoveryApplyResultDto> Handle(ApplySourceRecoveryCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var isAdmin = currentUser.IsInRole(ApplicationRoles.SuperAdmin)
                      || currentUser.IsInRole(ApplicationRoles.ContentAdmin);
        return orchestrator.ApplyAndRetryAsync(request.AttemptId, request.OptionIndex, userId, isAdmin, cancellationToken);
    }
}

public sealed class FinalizeSourceRecoveryAttemptCommandHandler(ISourceRecoveryOrchestrator orchestrator)
    : IRequestHandler<FinalizeSourceRecoveryAttemptCommand, SourceRecoveryApplyResultDto>
{
    public Task<SourceRecoveryApplyResultDto> Handle(
        FinalizeSourceRecoveryAttemptCommand request,
        CancellationToken cancellationToken) =>
        orchestrator.FinalizeAttemptAsync(request.AttemptId, cancellationToken);
}

public sealed class RollbackSourceRecoveryCommandHandler(
    ISourceRecoveryOrchestrator orchestrator,
    ICurrentUserContext currentUser) : IRequestHandler<RollbackSourceRecoveryCommand, Unit>
{
    public async Task<Unit> Handle(RollbackSourceRecoveryCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");
        await orchestrator.RollbackAsync(request.AttemptId, userId, request.Reason, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class GetSourceRecoveryHistoryQueryHandler(
    IApplicationDbContext db,
    ISourceRecoveryOrchestrator recoveryOrchestrator)
    : IRequestHandler<GetSourceRecoveryHistoryQuery, IReadOnlyList<SourceRecoveryHistoryItemDto>>
{
    public async Task<IReadOnlyList<SourceRecoveryHistoryItemDto>> Handle(
        GetSourceRecoveryHistoryQuery request,
        CancellationToken cancellationToken)
    {
        await recoveryOrchestrator.ReconcileAllAsync(cancellationToken).ConfigureAwait(false);

        var attemptsQuery = db.SourceRecoveryAttempts.AsNoTracking()
            .Include(a => a.NewsSource)
            .Where(a => !a.IsDeleted);

        if (request.MonitorDate is DateOnly monitorDate)
        {
            var dayStart = monitorDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            attemptsQuery = attemptsQuery.Where(a => a.CreatedAt >= dayStart && a.CreatedAt < dayEnd);
        }

        var attempts = await attemptsQuery
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(request.Take, 1, 200))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var autoAiRunIds = attempts
            .Where(a => a.AutoAiRecoveryRunId.HasValue)
            .Select(a => a.AutoAiRecoveryRunId!.Value)
            .Distinct()
            .ToList();
        var autoAiRuns = autoAiRunIds.Count == 0
            ? new Dictionary<Guid, AutoAiRecoveryRun>()
            : await db.AutoAiRecoveryRuns.AsNoTracking()
                .Where(r => autoAiRunIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken)
                .ConfigureAwait(false);

        var userIds = attempts
            .Where(a => a.AppliedByUserId.HasValue)
            .Select(a => a.AppliedByUserId!.Value)
            .Distinct()
            .ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.UserName })
            .ToDictionaryAsync(u => u.Id, cancellationToken)
            .ConfigureAwait(false);

        return attempts.Select(a =>
        {
            IReadOnlyList<SourceRecoveryOptionDto> options;
            try
            {
                options = string.IsNullOrWhiteSpace(a.AnalysisJson)
                    ? Array.Empty<SourceRecoveryOptionDto>()
                    : SourceRecoveryJsonParser.ParseOptions(a.AnalysisJson);
            }
            catch
            {
                options = Array.Empty<SourceRecoveryOptionDto>();
            }

            var title = a.SelectedOptionIndex >= 0
                ? options.FirstOrDefault(o => o.OptionIndex == a.SelectedOptionIndex)?.Title
                : null;
            title ??= options.FirstOrDefault()?.Title;

            var appliedBy = a.IsAutomatic
                ? "Automatic AI Recovery"
                : a.AppliedByUserId is Guid uid && users.TryGetValue(uid, out var user)
                    ? user.Email ?? user.UserName ?? uid.ToString()
                    : "—";

            autoAiRuns.TryGetValue(a.AutoAiRecoveryRunId ?? Guid.Empty, out var autoAiRun);
            var resultSummary = a.ResultSummary
                                ?? autoAiRun?.ResultSummary
                                ?? (a.Status == SourceRecoveryAttemptStatus.RetryEnqueued
                                    ? "Download retry in progress."
                                    : null);
            var predicted = a.PredictedSuccessPercent;
            int? actual = a.ActualSuccessPercent;
            if (actual is null && autoAiRun?.CompletedAt is not null)
            {
                actual = autoAiRun.Status switch
                {
                    AutoAiRecoveryRunStatus.CompletedSuccess => 100,
                    AutoAiRecoveryRunStatus.CompletedFailure
                        or AutoAiRecoveryRunStatus.CandidateFailed
                        or AutoAiRecoveryRunStatus.SkippedNoSuggestions => 0,
                    _ => null
                };
            }

            return new SourceRecoveryHistoryItemDto(
                a.Id,
                a.NewsSourceId,
                a.NewsSource.Name,
                a.DownloadJobId,
                a.RetryDownloadJobId,
                a.FailureType,
                title,
                appliedBy,
                a.Status,
                resultSummary,
                predicted,
                actual,
                a.IsAutomatic,
                a.CreatedAt,
                a.CompletedAt ?? autoAiRun?.CompletedAt);
        }).ToList();
    }
}

public sealed class GetRecoveryCenterFailuresQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRecoveryCenterFailuresQuery, IReadOnlyList<SourceRecoveryCenterItemDto>>
{
    public async Task<IReadOnlyList<SourceRecoveryCenterItemDto>> Handle(
        GetRecoveryCenterFailuresQuery request,
        CancellationToken cancellationToken)
    {
        var monitorDate = request.MonitorDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = monitorDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var failedJobs = await db.DownloadJobs.AsNoTracking()
            .Include(j => j.NewsSource)
            .Where(j => !j.IsDeleted
                        && j.Status == DownloadJobStatus.Failed
                        && j.CompletedAt >= dayStart
                        && j.CompletedAt < dayEnd)
            .OrderByDescending(j => j.CompletedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (failedJobs.Count == 0)
        {
            return Array.Empty<SourceRecoveryCenterItemDto>();
        }

        var jobIds = failedJobs.Select(j => j.Id).ToList();
        var attempts = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DownloadJobId != null && jobIds.Contains(a.DownloadJobId.Value))
            .Select(a => a.DownloadJobId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var attemptSet = attempts.ToHashSet();

        return failedJobs.Select(j => new SourceRecoveryCenterItemDto(
            j.NewsSourceId,
            j.NewsSource.Name,
            j.Id,
            SourceRecoveryFailureTypeMapper.Map(null, j.ErrorMessage),
            j.ErrorMessage ?? "Download failed.",
            j.CompletedAt ?? j.StartedAt,
            attemptSet.Contains(j.Id))).ToList();
    }
}
