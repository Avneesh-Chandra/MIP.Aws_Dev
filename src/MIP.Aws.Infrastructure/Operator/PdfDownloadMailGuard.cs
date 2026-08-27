using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Operator;

internal static class PdfDownloadMailGuard
{
    private static readonly TimeSpan ClaimStaleAfter = TimeSpan.FromMinutes(20);

    public static async Task<bool> TryClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        string subject,
        CancellationToken cancellationToken,
        bool allowResend = false)
    {
        if (!allowResend
            && await HasConfirmedDeliveryAsync(db, subject, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!allowResend)
        {
            await ReleaseStaleClaimAsync(db, batchStartedAt, subject, cancellationToken).ConfigureAwait(false);
        }

        return await db.DownloadMonitorBatchRuns
            .Where(batch => !batch.IsDeleted
                            && batch.StartedAt == batchStartedAt
                            && (allowResend || batch.PdfDownloadMailSentAt == null))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    batch => batch.PdfDownloadMailSentAt,
                    DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    public static async Task ReleaseAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        string subject,
        CancellationToken cancellationToken)
    {
        if (await HasConfirmedDeliveryAsync(db, subject, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await db.DownloadMonitorBatchRuns
            .Where(batch => !batch.IsDeleted && batch.StartedAt == batchStartedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    batch => batch.PdfDownloadMailSentAt,
                    (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ReleaseStaleClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        string subject,
        CancellationToken cancellationToken)
    {
        var claimedAt = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(batch => !batch.IsDeleted && batch.StartedAt == batchStartedAt)
            .Select(batch => batch.PdfDownloadMailSentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (claimedAt is null
            || DateTimeOffset.UtcNow - claimedAt.Value < ClaimStaleAfter
            || await HasConfirmedDeliveryAsync(db, subject, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await db.DownloadMonitorBatchRuns
            .Where(batch => !batch.IsDeleted
                            && batch.StartedAt == batchStartedAt
                            && batch.PdfDownloadMailSentAt == claimedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    batch => batch.PdfDownloadMailSentAt,
                    (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<bool> HasConfirmedDeliveryAsync(
        IApplicationDbContext db,
        string subject,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken) =>
        db.EmailLogs.AsNoTracking()
            .AnyAsync(
                email => !email.IsDeleted
                         && email.SentAt >= batchStartedAt
                         && (email.Subject == subject || email.Subject.EndsWith(subject))
                         && (email.Status == EmailDeliveryStatus.Sent
                             || email.Status == EmailDeliveryStatus.RedirectedBySafety),
                cancellationToken);
}
