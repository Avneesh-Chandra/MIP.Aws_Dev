using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Prevents duplicate download-monitor status emails (batch initial, recovery follow-up, manual debounce).</summary>
public static class DownloadMonitorStatusEmailGuard
{
    internal static readonly TimeSpan AdHocStatusEmailCooldown = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan UnconfirmedClaimStaleAfter = TimeSpan.FromMinutes(20);

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
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(monitorDate);
        var dayStart = new DateTimeOffset(monitorDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        return db.EmailLogs.AsNoTracking()
            .AnyAsync(
                e => !e.IsDeleted
                     && e.Subject == subject
                     && (e.Status == EmailDeliveryStatus.Sent
                         || e.Status == EmailDeliveryStatus.RedirectedBySafety)
                     && e.SentAt >= dayStart
                     && e.SentAt < dayEnd,
                cancellationToken);
    }

    public static Task<bool> ShouldThrottleAdHocDailyStatusEmailAsync(
        IApplicationDbContext db,
        DateOnly monitorDate,
        CancellationToken cancellationToken)
    {
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildInitialSubject(monitorDate);
        var cutoff = DateTimeOffset.UtcNow - AdHocStatusEmailCooldown;
        return db.EmailLogs.AsNoTracking()
            .AnyAsync(
                e => !e.IsDeleted
                     && e.Subject == subject
                     && e.SentAt >= cutoff
                     && (e.Status == EmailDeliveryStatus.Sent
                         || e.Status == EmailDeliveryStatus.RedirectedBySafety),
                cancellationToken);
    }

    public static async Task<bool> TryClaimInitialStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        if (await HasConfirmedInitialStatusForMonitorDateAsync(db, monitorDate, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        var dayStart = new DateTimeOffset(monitorDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        await ReleaseStaleInitialClaimForMonitorDateAsync(
                db,
                monitorDate,
                dayStart,
                dayEnd,
                cancellationToken)
            .ConfigureAwait(false);

        if (await HasActiveInitialStatusClaimForMonitorDateAsync(
                db,
                dayStart,
                dayEnd,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var claimedAt = DateTimeOffset.UtcNow;
        var updated = await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted
                        && b.StartedAt >= dayStart
                        && b.StartedAt < dayEnd
                        && b.StatusEmailSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.StatusEmailSentAt, claimedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    public static Task<bool> HasActiveInitialStatusClaimForMonitorDateAsync(
        IApplicationDbContext db,
        DateOnly monitorDate,
        CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(monitorDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        return HasActiveInitialStatusClaimForMonitorDateAsync(db, dayStart, dayEnd, cancellationToken);
    }

    private static async Task<bool> HasActiveInitialStatusClaimForMonitorDateAsync(
        IApplicationDbContext db,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        CancellationToken cancellationToken)
    {
        var latestClaim = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted
                        && b.StartedAt >= dayStart
                        && b.StartedAt < dayEnd
                        && b.StatusEmailSentAt != null)
            .OrderByDescending(b => b.StatusEmailSentAt)
            .Select(b => b.StatusEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return latestClaim is DateTimeOffset claim
               && DateTimeOffset.UtcNow - claim < UnconfirmedClaimStaleAfter;
    }

    public static async Task ReleaseInitialStatusEmailClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        if (await HasConfirmedInitialStatusForMonitorDateAsync(db, monitorDate, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var dayStart = new DateTimeOffset(monitorDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted && b.StartedAt >= dayStart && b.StartedAt < dayEnd)
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
        CancellationToken cancellationToken)
    {
        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var dayStart = new DateTimeOffset(monitorDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        return ReleaseStaleInitialClaimForMonitorDateAsync(db, monitorDate, dayStart, dayEnd, cancellationToken);
    }

    public static Task ReleaseStaleRecoveryFollowUpClaimIfNeededAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var subject = DownloadMonitorBatchStatusEmailCoordinator.BuildRecoveryFollowUpSubject(
            DateOnly.FromDateTime(batchStartedAt.UtcDateTime));
        return ReleaseStaleRecoveryFollowUpClaimAsync(db, batchStartedAt, subject, cancellationToken);
    }

    private static async Task ReleaseStaleInitialClaimForMonitorDateAsync(
        IApplicationDbContext db,
        DateOnly monitorDate,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        CancellationToken cancellationToken)
    {
        if (await HasConfirmedInitialStatusForMonitorDateAsync(db, monitorDate, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var latestClaim = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted
                        && b.StartedAt >= dayStart
                        && b.StartedAt < dayEnd
                        && b.StatusEmailSentAt != null)
            .OrderByDescending(b => b.StatusEmailSentAt)
            .Select(b => b.StatusEmailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (latestClaim is null || DateTimeOffset.UtcNow - latestClaim.Value < UnconfirmedClaimStaleAfter)
        {
            return;
        }

        await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted && b.StartedAt >= dayStart && b.StartedAt < dayEnd)
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
