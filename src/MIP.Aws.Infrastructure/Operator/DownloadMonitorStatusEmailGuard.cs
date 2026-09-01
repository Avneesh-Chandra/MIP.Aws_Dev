using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Prevents duplicate download-monitor status emails (batch initial, recovery follow-up, manual debounce).</summary>
public static class DownloadMonitorStatusEmailGuard
{
    internal static readonly TimeSpan AdHocStatusEmailCooldown = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan UnconfirmedClaimStaleAfter = TimeSpan.FromMinutes(20);
    private const double EnqueueClaimToleranceMilliseconds = 100;

    public static async Task<bool> HasConfirmedDeliveryAsync(
        IApplicationDbContext db,
        string subject,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken)
    {
        var query = db.EmailLogs.AsNoTracking()
            .Where(e => !e.IsDeleted
                        && e.Subject == subject
                        && (e.Status == EmailDeliveryStatus.Sent
                            || e.Status == EmailDeliveryStatus.RedirectedBySafety));

        if (notBefore is DateTimeOffset since)
        {
            query = query.Where(e => e.SentAt >= since);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public static Task<bool> HasConfirmedInitialStatusForMonitorDateAsync(
        IApplicationDbContext db,
        DateOnly monitorDate,
        CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(monitorDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var canonicalSubject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(monitorDate);
        var legacySubject = canonicalSubject.Replace('—', '-');

        return db.EmailLogs.AsNoTracking()
            .AnyAsync(
                e => !e.IsDeleted
                     && (e.Subject == canonicalSubject || e.Subject == legacySubject)
                     && (e.Status == EmailDeliveryStatus.Sent
                         || e.Status == EmailDeliveryStatus.RedirectedBySafety)
                     && e.SentAt >= dayStart
                     && e.SentAt < dayEnd,
                cancellationToken);
    }

    public static async Task<bool> HasConfirmedInitialStatusForBatchAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var canonicalSubject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(monitorDate);
        var legacySubject = canonicalSubject.Replace('—', '-');

        if (await db.EmailLogs.AsNoTracking()
                .AnyAsync(
                    e => !e.IsDeleted
                         && (e.Subject == canonicalSubject || e.Subject == legacySubject)
                         && (e.Status == EmailDeliveryStatus.Sent
                             || e.Status == EmailDeliveryStatus.RedirectedBySafety)
                         && e.SentAt >= batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        var statusEmailSentAt = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => b.StatusEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return statusEmailSentAt is not null
               && !IsInitialStatusEnqueueClaim(statusEmailSentAt, batchStartedAt);
    }

    public static Task<bool> ShouldThrottleAdHocDailyStatusEmailAsync(
        IApplicationDbContext db,
        DateOnly monitorDate,
        CancellationToken cancellationToken)
    {
        var canonicalSubject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(monitorDate);
        var legacySubject = canonicalSubject.Replace('—', '-');
        var cutoff = DateTimeOffset.UtcNow - AdHocStatusEmailCooldown;
        return db.EmailLogs.AsNoTracking()
            .AnyAsync(
                e => !e.IsDeleted
                     && (e.Subject == canonicalSubject || e.Subject == legacySubject)
                     && e.SentAt >= cutoff
                     && (e.Status == EmailDeliveryStatus.Sent
                         || e.Status == EmailDeliveryStatus.RedirectedBySafety),
                cancellationToken);
    }

    public static bool IsInitialStatusEnqueueClaim(DateTimeOffset? statusEmailSentAt, DateTimeOffset batchStartedAt) =>
        statusEmailSentAt is not null
        && Math.Abs((statusEmailSentAt.Value - batchStartedAt).TotalMilliseconds) < EnqueueClaimToleranceMilliseconds;

    public static bool IsInitialStatusSendInProgressClaim(
        DateTimeOffset? statusEmailSentAt,
        DateTimeOffset batchStartedAt) =>
        statusEmailSentAt is not null
        && !IsInitialStatusEnqueueClaim(statusEmailSentAt, batchStartedAt)
        && DateTimeOffset.UtcNow - statusEmailSentAt.Value < UnconfirmedClaimStaleAfter;

    public static async Task<bool> TryMarkInitialStatusEmailEnqueuedAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasConfirmedInitialStatusForBatchAsync(db, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        if (await HasActiveInitialStatusSendInFlightForBatchAsync(db, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        var updated = await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted
                        && b.StartedAt == batchStartedAt
                        && b.StatusEmailSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.StatusEmailSentAt, batchStartedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    public static async Task<bool> TryClaimInitialStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasConfirmedInitialStatusForBatchAsync(db, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        await ReleaseStaleInitialClaimIfNeededAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false);

        if (await HasActiveInitialStatusSendInFlightForBatchAsync(db, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        var claimedAt = DateTimeOffset.UtcNow;
        var updated = await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted
                        && b.StartedAt == batchStartedAt
                        && (b.StatusEmailSentAt == null || b.StatusEmailSentAt == batchStartedAt))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.StatusEmailSentAt, claimedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    public static Task<bool> HasActiveInitialStatusClaimForBatchAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken) =>
        HasActiveInitialStatusSendInFlightForBatchAsync(db, batchStartedAt, cancellationToken);

    private static async Task<bool> HasActiveInitialStatusSendInFlightForBatchAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var statusEmailSentAt = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => b.StatusEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return statusEmailSentAt is not null
               && IsInitialStatusSendInProgressClaim(statusEmailSentAt, batchStartedAt);
    }

    public static async Task<bool> IsInitialStatusEnqueueClaimOnBatchAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var statusEmailSentAt = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => b.StatusEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return IsInitialStatusEnqueueClaim(statusEmailSentAt, batchStartedAt);
    }

    public static async Task ReleaseInitialStatusEmailClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasConfirmedInitialStatusForBatchAsync(db, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.StatusEmailSentAt, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> TryClaimRecoveryFollowUpEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildRecoveryFollowUpSubject(
            DateOnly.FromDateTime(batchStartedAt.UtcDateTime));

        if (await HasConfirmedDeliveryAsync(db, subject, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ReleaseStaleRecoveryFollowUpClaimAsync(db, batchStartedAt, subject, cancellationToken)
            .ConfigureAwait(false);

        var updated = await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted
                        && b.StartedAt == batchStartedAt
                        && b.StatusEmailSentAt != null
                        && b.RecoveryFollowUpEmailSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.RecoveryFollowUpEmailSentAt, batchStartedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    public static bool IsRecoveryFollowUpEnqueueClaim(DateTimeOffset? recoveryFollowUpEmailSentAt, DateTimeOffset batchStartedAt) =>
        recoveryFollowUpEmailSentAt is not null
        && Math.Abs((recoveryFollowUpEmailSentAt.Value - batchStartedAt).TotalSeconds) < 2;

    public static async Task ReleaseRecoveryFollowUpEmailClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildRecoveryFollowUpSubject(
            DateOnly.FromDateTime(batchStartedAt.UtcDateTime));
        if (await HasConfirmedDeliveryAsync(db, subject, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted
                        && b.StartedAt == batchStartedAt
                        && b.RecoveryFollowUpEmailSentAt == batchStartedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.RecoveryFollowUpEmailSentAt, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static Task ReleaseStaleInitialClaimIfNeededAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken) =>
        ReleaseStaleInitialClaimAsync(db, batchStartedAt, cancellationToken);

    public static Task ReleaseStaleRecoveryFollowUpClaimIfNeededAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildRecoveryFollowUpSubject(
            DateOnly.FromDateTime(batchStartedAt.UtcDateTime));
        return ReleaseStaleRecoveryFollowUpClaimAsync(db, batchStartedAt, subject, cancellationToken);
    }

    private static async Task ReleaseStaleInitialClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasConfirmedInitialStatusForBatchAsync(db, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var claim = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => b.StatusEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (claim is null || IsInitialStatusEnqueueClaim(claim, batchStartedAt))
        {
            return;
        }

        if (DateTimeOffset.UtcNow - claim.Value < UnconfirmedClaimStaleAfter)
        {
            return;
        }

        await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.StatusEmailSentAt, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ReleaseStaleRecoveryFollowUpClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        string subject,
        CancellationToken cancellationToken)
    {
        var claim = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => b.RecoveryFollowUpEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (claim is null || !IsRecoveryFollowUpEnqueueClaim(claim, batchStartedAt))
        {
            return;
        }

        if (await HasConfirmedDeliveryAsync(db, subject, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (DateTimeOffset.UtcNow - claim.Value < UnconfirmedClaimStaleAfter)
        {
            return;
        }

        await ReleaseRecoveryFollowUpEmailClaimAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false);
    }
}
