using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Scheduling;
using MIP.Aws.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Reporting;

public sealed class MailSettingsService(
    IApplicationDbContext db,
    IOptions<MailOptions> mailOptions,
    IOptions<EmailSafetyOptions> legacySafetyOptions,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    IOptions<MailAutomationOptions> mailAutomationOptions,
    ILogger<MailSettingsService> logger) : IMailSettingsService
{
    private static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public async Task<EffectiveMailSettings> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        var file = mailOptions.Value;
        var legacy = legacySafetyOptions.Value;
        var row = await TryLoadMailSettingsRowAsync(cancellationToken).ConfigureAwait(false);

        var provider = row is not null && Enum.TryParse<MailActiveProvider>(row.ActiveProvider, true, out var parsed)
            ? parsed
            : file.ActiveProvider;

        var safety = row?.DevelopmentSafetyEnabled ?? file.DevelopmentSafetyEnabled;
        var redirect = !string.IsNullOrWhiteSpace(row?.RedirectAllTo) ? row!.RedirectAllTo! :
            !string.IsNullOrWhiteSpace(file.RedirectAllTo) ? file.RedirectAllTo :
            legacy.RedirectAllTo;
        var prefix = !string.IsNullOrWhiteSpace(row?.SubjectPrefix) ? row!.SubjectPrefix :
            !string.IsNullOrWhiteSpace(file.SubjectPrefix) ? file.SubjectPrefix : legacy.PrefixSubject;
        var domains = file.AllowedDomains is { Length: > 0 } ? file.AllowedDomains : legacy.AllowedDomains;

        return new EffectiveMailSettings(provider, safety, redirect ?? string.Empty, prefix ?? string.Empty, domains);
    }

    public async Task<EffectiveSchedulerMailSettings> GetEffectiveSchedulerAsync(CancellationToken cancellationToken)
    {
        var scheduler = schedulerOptions.Value;
        var automation = mailAutomationOptions.Value;
        var row = await TryLoadMailSettingsRowAsync(cancellationToken).ConfigureAwait(false);

        var batchTime = !string.IsNullOrWhiteSpace(row?.BatchScheduleTimeUtc)
            ? row!.BatchScheduleTimeUtc!.Trim()
            : scheduler.FirstSourceScheduleTimeUtc;
        if (!DownloadBatchSchedule.TryNormalizeTimeUtc(batchTime, out var normalizedTime, out _))
        {
            normalizedTime = scheduler.FirstSourceScheduleTimeUtc;
        }

        var recurrence = DownloadBatchSchedule.ParseRecurrence(row?.BatchRecurrence);
        var days = recurrence == DownloadBatchRecurrence.CustomDays
            ? DownloadBatchSchedule.FormatDays(DownloadBatchSchedule.ParseDays(row?.BatchRecurrenceDays))
            : recurrence == DownloadBatchRecurrence.Weekdays ? "1,2,3,4,5" : null;

        var pdfMailTime = !string.IsNullOrWhiteSpace(row?.PdfDownloadMailTimeUtc)
            ? row!.PdfDownloadMailTimeUtc!.Trim()
            : scheduler.PdfDownloadMailTimeUtc;
        if (!DownloadBatchSchedule.TryNormalizeTimeUtc(pdfMailTime, out var normalizedPdfMailTime, out _))
        {
            normalizedPdfMailTime = scheduler.PdfDownloadMailTimeUtc;
        }

        var pdfMailSubject = !string.IsNullOrWhiteSpace(row?.PdfDownloadMailSubject)
            ? row!.PdfDownloadMailSubject!.Trim()
            : scheduler.PdfDownloadMailSubject;
        if (string.Equals(pdfMailSubject, "GFH PDF Download", StringComparison.OrdinalIgnoreCase))
        {
            pdfMailSubject = scheduler.PdfDownloadMailSubject;
        }

        return new EffectiveSchedulerMailSettings(
            row?.StatusEmailEnabled ?? scheduler.StatusEmailEnabled,
            !string.IsNullOrWhiteSpace(row?.StatusEmailRecipient) ? row!.StatusEmailRecipient!.Trim() : scheduler.StatusEmailRecipient,
            row?.MailAutomationEnabled ?? automation.Enabled,
            scheduler.StatusEmailTimeUtc,
            scheduler.AdminPortalUrl,
            !string.IsNullOrWhiteSpace(row?.AdminRecipientEmail)
                ? row!.AdminRecipientEmail!.Trim()
                : scheduler.AdminRecipientEmail,
            normalizedTime,
            recurrence.ToString(),
            days,
            row?.PdfDownloadMailEnabled ?? scheduler.PdfDownloadMailEnabled,
            !string.IsNullOrWhiteSpace(row?.PdfDownloadMailRecipient)
                ? row!.PdfDownloadMailRecipient!.Trim()
                : scheduler.PdfDownloadMailRecipient,
            pdfMailSubject,
            normalizedPdfMailTime);
    }

    public async Task UpdateAsync(
        MailActiveProvider activeProvider,
        bool developmentSafetyEnabled,
        string? redirectAllTo,
        string? subjectPrefix,
        CancellationToken cancellationToken)
    {
        var row = await GetOrCreateRowAsync(cancellationToken).ConfigureAwait(false);
        row.ActiveProvider = activeProvider.ToString();
        row.DevelopmentSafetyEnabled = developmentSafetyEnabled;
        row.RedirectAllTo = redirectAllTo?.Trim();
        row.SubjectPrefix = string.IsNullOrWhiteSpace(subjectPrefix) ? "[GFH-MIP-TEST]" : subjectPrefix.Trim();
        row.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSchedulerAsync(
        bool statusEmailEnabled,
        string? statusEmailRecipient,
        bool mailAutomationEnabled,
        string? adminRecipientEmail,
        string? batchScheduleTimeUtc,
        string? batchRecurrence,
        string? batchRecurrenceDays,
        bool? pdfDownloadMailEnabled,
        string? pdfDownloadMailRecipient,
        string? pdfDownloadMailSubject,
        string? pdfDownloadMailTimeUtc,
        CancellationToken cancellationToken)
    {
        if (!DownloadBatchSchedule.TryNormalizeTimeUtc(batchScheduleTimeUtc, out var timeUtc, out var timeError))
        {
            throw new InvalidOperationException(timeError);
        }

        var row = await GetOrCreateRowAsync(cancellationToken).ConfigureAwait(false);
        var requestedPdfMailTime = string.IsNullOrWhiteSpace(pdfDownloadMailTimeUtc)
            ? row.PdfDownloadMailTimeUtc ?? schedulerOptions.Value.PdfDownloadMailTimeUtc
            : pdfDownloadMailTimeUtc;
        if (!DownloadBatchSchedule.TryNormalizeTimeUtc(requestedPdfMailTime, out var pdfMailTimeUtc, out var pdfMailTimeError))
        {
            throw new InvalidOperationException(pdfMailTimeError);
        }

        var recurrence = DownloadBatchSchedule.ParseRecurrence(batchRecurrence);
        string? days = null;
        if (recurrence == DownloadBatchRecurrence.CustomDays)
        {
            var parsedDays = DownloadBatchSchedule.ParseDays(batchRecurrenceDays);
            if (parsedDays.Count == 0)
            {
                throw new InvalidOperationException("Select at least one weekday for a custom batch schedule.");
            }

            days = DownloadBatchSchedule.FormatDays(parsedDays);
        }
        else if (recurrence == DownloadBatchRecurrence.Weekdays)
        {
            days = "1,2,3,4,5";
        }

        row.StatusEmailEnabled = statusEmailEnabled;
        row.StatusEmailRecipient = statusEmailRecipient?.Trim();
        row.MailAutomationEnabled = mailAutomationEnabled;
        row.AdminRecipientEmail = adminRecipientEmail?.Trim();
        row.BatchScheduleTimeUtc = timeUtc;
        row.BatchRecurrence = recurrence.ToString();
        row.BatchRecurrenceDays = days;
        if (pdfDownloadMailEnabled is bool enabled)
        {
            row.PdfDownloadMailEnabled = enabled;
        }

        if (pdfDownloadMailRecipient is not null)
        {
            row.PdfDownloadMailRecipient = pdfDownloadMailRecipient.Trim();
        }

        if (pdfDownloadMailSubject is not null)
        {
            row.PdfDownloadMailSubject = string.IsNullOrWhiteSpace(pdfDownloadMailSubject)
                ? schedulerOptions.Value.PdfDownloadMailSubject
                : pdfDownloadMailSubject.Trim();
        }

        if (pdfDownloadMailTimeUtc is not null)
        {
            row.PdfDownloadMailTimeUtc = pdfMailTimeUtc;
        }
        row.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailSettings> GetOrCreateRowAsync(CancellationToken cancellationToken)
    {
        var row = await db.MailSettings.FirstOrDefaultAsync(x => x.Id == SingletonId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new MailSettings
            {
                Id = SingletonId,
                ActiveProvider = mailOptions.Value.ActiveProvider.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.MailSettings.Add(row);
        }

        return row;
    }

    private Task<MailSettings?> TryLoadMailSettingsRowAsync(CancellationToken cancellationToken) =>
        TryLoadMailSettingsRowTrackedAsync(tracked: false, cancellationToken);

    private async Task<MailSettings?> TryLoadMailSettingsRowTrackedAsync(
        bool tracked,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = tracked
                ? db.MailSettings.Where(x => x.Id == SingletonId && !x.IsDeleted)
                : db.MailSettings.AsNoTracking().Where(x => x.Id == SingletonId && !x.IsDeleted);
            return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsMissingMailSettingsSchema(ex))
        {
            logger.LogWarning(
                ex,
                "MailSettings query failed (schema may be behind migrations); using appsettings defaults.");
            return null;
        }
    }

    private static bool IsMissingMailSettingsSchema(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && (sql.Number == 207 || sql.Number == 208))
            {
                return true;
            }

            if (current.Message.Contains("AdminRecipientEmail", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("BatchScheduleTimeUtc", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("BatchRecurrence", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("PdfDownloadMail", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
