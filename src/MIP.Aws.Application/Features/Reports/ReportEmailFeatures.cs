using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Scheduling;
using MIP.Aws.Application.Time;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Application.Features.Reports;

public sealed record MailConfigStatusDto(
    string ActiveProvider,
    bool SesEnabled,
    bool SesSenderConfigured,
    string? SesSenderAddress,
    bool DevelopmentSafetyEnabled,
    string? RedirectAllTo,
    string? FromEmail,
    bool IsReady,
    string? ConfigurationMessage,
    bool MailAutomationEnabled,
    bool StatusEmailEnabled,
    string StatusEmailRecipient,
    string StatusEmailTimeUtc,
    string? AdminPortalUrl);

public sealed record TestEmailResponseDto(
    bool Success,
    string Provider,
    string? MessageId,
    string? OperationId,
    DateTimeOffset? SentAt,
    Guid? EmailLogId,
    string? Error);

public sealed record GetMailConfigStatusQuery : IRequest<MailConfigStatusDto>;

public sealed record SendTestReportEmailCommand(string To, string Subject, string HtmlBody) : IRequest<TestEmailResponseDto>;

public sealed record UpdateMailSettingsCommand(
    MailActiveProvider ActiveProvider,
    bool DevelopmentSafetyEnabled,
    string? RedirectAllTo,
    string? SubjectPrefix) : IRequest;

public sealed record UpdateMailSchedulerSettingsCommand(
    bool StatusEmailEnabled,
    string? StatusEmailRecipient,
    bool MailAutomationEnabled,
    string? AdminRecipientEmail,
    string? BatchScheduleTimeUtc = null,
    string? BatchRecurrence = null,
    string? BatchRecurrenceDays = null,
    bool? PdfDownloadMailEnabled = null,
    string? PdfDownloadMailRecipient = null,
    string? PdfDownloadMailSubject = null,
    string? PdfDownloadMailTimeUtc = null,
    IReadOnlyList<string>? SelectedStatusEmailRecipients = null,
    IReadOnlyList<string>? SelectedPdfDownloadMailRecipients = null,
    MailCompletionMode? StatusEmailCompletionMode = null,
    MailCompletionMode? PdfDownloadMailCompletionMode = null) : IRequest;

public sealed record SendDownloadMonitorStatusEmailCommand(
    DateOnly? MonitorDate,
    string? RecipientOverride = null,
    bool BypassThrottle = false) : IRequest<bool>;

public sealed record SendPdfDownloadMailCommand(bool BypassSchedule = true) : IRequest<bool>;

public sealed record GetMailSettingsQuery : IRequest<MailSettingsDto>;

public sealed record MailSettingsDto(
    MailActiveProvider ActiveProvider,
    bool DevelopmentSafetyEnabled,
    string? RedirectAllTo,
    string? SubjectPrefix,
    bool StatusEmailEnabled,
    string StatusEmailRecipient,
    bool MailAutomationEnabled,
    string StatusEmailTimeUtc,
    string AdminRecipientEmail,
    string BatchScheduleTimeUtc,
    string BatchRecurrence,
    string? BatchRecurrenceDays,
    string BatchScheduleSummary,
    bool PdfDownloadMailEnabled,
    string PdfDownloadMailRecipient,
    string PdfDownloadMailSubject,
    string PdfDownloadMailTimeUtc,
    IReadOnlyList<string> SelectedStatusEmailRecipients,
    IReadOnlyList<string> SelectedPdfDownloadMailRecipients,
    MailCompletionMode StatusEmailCompletionMode,
    MailCompletionMode PdfDownloadMailCompletionMode);

public sealed record EmailLogListItemDto(
    Guid Id,
    Guid? ReportId,
    Guid? ReportScheduleId,
    Guid? BriefId,
    string Provider,
    string FromEmail,
    string To,
    string? Cc,
    string? Bcc,
    string Subject,
    EmailDeliveryStatus Status,
    DateTimeOffset? SentAt,
    string? ErrorMessage,
    int RetryCount,
    string? MessageId,
    string? ProviderOperationId,
    DateTimeOffset CreatedAt);

public sealed record GetRecentEmailLogsQuery(int Take = 25, DateOnly? LogDate = null) : IRequest<IReadOnlyList<EmailLogListItemDto>>;

public sealed class GetMailConfigStatusQueryHandler(IMailConfigStatusService statusService)
    : IRequestHandler<GetMailConfigStatusQuery, MailConfigStatusDto>
{
    public Task<MailConfigStatusDto> Handle(GetMailConfigStatusQuery request, CancellationToken cancellationToken) =>
        statusService.GetStatusAsync(cancellationToken);
}

public sealed class GetMailSettingsQueryHandler(IMailSettingsService mailSettings)
    : IRequestHandler<GetMailSettingsQuery, MailSettingsDto>
{
    public async Task<MailSettingsDto> Handle(GetMailSettingsQuery request, CancellationToken cancellationToken)
    {
        var effective = await mailSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        var recurrence = DownloadBatchSchedule.ParseRecurrence(scheduler.BatchRecurrence);
        return new MailSettingsDto(
            effective.ActiveProvider,
            effective.DevelopmentSafetyEnabled,
            effective.RedirectAllTo,
            effective.SubjectPrefix,
            scheduler.StatusEmailEnabled,
            scheduler.StatusEmailRecipient,
            scheduler.MailAutomationEnabled,
            scheduler.StatusEmailTimeUtc,
            scheduler.AdminRecipientEmail,
            scheduler.BatchScheduleTimeUtc,
            recurrence.ToString(),
            scheduler.BatchRecurrenceDays,
            DownloadBatchSchedule.Describe(
                scheduler.BatchScheduleTimeUtc,
                recurrence,
                scheduler.BatchRecurrenceDays),
            scheduler.PdfDownloadMailEnabled,
            scheduler.PdfDownloadMailRecipient,
            scheduler.PdfDownloadMailSubject,
            scheduler.PdfDownloadMailTimeUtc,
            scheduler.SelectedStatusEmailRecipients,
            scheduler.SelectedPdfDownloadMailRecipients,
            scheduler.StatusEmailCompletionMode,
            scheduler.PdfDownloadMailCompletionMode);
    }
}

public sealed class UpdateMailSettingsCommandHandler(IMailSettingsService mailSettings)
    : IRequestHandler<UpdateMailSettingsCommand>
{
    public Task Handle(UpdateMailSettingsCommand request, CancellationToken cancellationToken) =>
        mailSettings.UpdateAsync(
            request.ActiveProvider,
            request.DevelopmentSafetyEnabled,
            request.RedirectAllTo,
            request.SubjectPrefix,
            cancellationToken);
}

public sealed class UpdateMailSchedulerSettingsCommandHandler(
    IMailSettingsService mailSettings,
    IScheduledJobRegistry scheduledJobs)
    : IRequestHandler<UpdateMailSchedulerSettingsCommand>
{
    public async Task Handle(UpdateMailSchedulerSettingsCommand request, CancellationToken cancellationToken)
    {
        await mailSettings.UpdateSchedulerAsync(
            request.StatusEmailEnabled,
            request.StatusEmailRecipient,
            request.MailAutomationEnabled,
            request.AdminRecipientEmail,
            request.BatchScheduleTimeUtc,
            request.BatchRecurrence,
            request.BatchRecurrenceDays,
            request.PdfDownloadMailEnabled,
            request.PdfDownloadMailRecipient,
            request.PdfDownloadMailSubject,
            request.PdfDownloadMailTimeUtc,
            request.SelectedStatusEmailRecipients,
            request.SelectedPdfDownloadMailRecipients,
            request.StatusEmailCompletionMode,
            request.PdfDownloadMailCompletionMode,
            cancellationToken).ConfigureAwait(false);

        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        scheduledJobs.ApplyDownloadBatchSchedule(
            scheduler.BatchScheduleTimeUtc,
            DownloadBatchSchedule.ParseRecurrence(scheduler.BatchRecurrence),
            scheduler.BatchRecurrenceDays);
        scheduledJobs.ApplyPdfDownloadMailSchedule(
            scheduler.PdfDownloadMailEnabled,
            scheduler.PdfDownloadMailTimeUtc);
    }
}

public sealed class SendTestReportEmailCommandHandler(IReportEmailSender sender)
    : IRequestHandler<SendTestReportEmailCommand, TestEmailResponseDto>
{
    public async Task<TestEmailResponseDto> Handle(SendTestReportEmailCommand request, CancellationToken cancellationToken)
    {
        var recipients = ParseRecipientList(request.To);
        if (recipients.Count == 0)
        {
            return new TestEmailResponseDto(
                false,
                string.Empty,
                null,
                null,
                null,
                null,
                "No valid recipients. Use semicolon- or comma-separated email addresses.");
        }

        var result = await sender.SendAsync(
            new ReportEmailMessage(recipients, request.Subject, request.HtmlBody, []),
            cancellationToken).ConfigureAwait(false);

        return new TestEmailResponseDto(
            result.Success,
            result.Provider,
            result.MessageId,
            result.OperationId,
            result.SentAt,
            result.EmailLogIds.FirstOrDefault(),
            result.ErrorMessage);
    }

    private static IReadOnlyList<string> ParseRecipientList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}

public sealed class SendDownloadMonitorStatusEmailCommandHandler(IDownloadMonitorDailyStatusEmailService statusEmail)
    : IRequestHandler<SendDownloadMonitorStatusEmailCommand, bool>
{
    public async Task<bool> Handle(SendDownloadMonitorStatusEmailCommand request, CancellationToken cancellationToken)
    {
        return await statusEmail.SendManualStatusEmailAsync(
                request.MonitorDate,
                ParseRecipientOverride(request.RecipientOverride),
                request.BypassThrottle,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<string>? ParseRecipientOverride(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var recipients = value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return recipients.Count == 0 ? null : recipients;
    }
}

public sealed class SendPdfDownloadMailCommandHandler(IPdfDownloadMailService pdfDownloadMail)
    : IRequestHandler<SendPdfDownloadMailCommand, bool>
{
    public Task<bool> Handle(
        SendPdfDownloadMailCommand request,
        CancellationToken cancellationToken) =>
        pdfDownloadMail.SendForLatestBatchAsync(cancellationToken, request.BypassSchedule);
}

public sealed class GetRecentEmailLogsQueryHandler(
    IApplicationDbContext db,
    IOptions<ApplicationDisplayOptions> displayOptions)
    : IRequestHandler<GetRecentEmailLogsQuery, IReadOnlyList<EmailLogListItemDto>>
{
    public async Task<IReadOnlyList<EmailLogListItemDto>> Handle(GetRecentEmailLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.EmailLogs.AsNoTracking().AsQueryable();

        if (request.LogDate is DateOnly logDate)
        {
            var tz = MipDisplayTimeZone.Resolve(displayOptions.Value.DisplayTimeZoneId);
            var localStart = logDate.ToDateTime(TimeOnly.MinValue);
            var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, tz));
            var endUtc = startUtc.AddDays(1);
            query = query.Where(e => (e.SentAt ?? e.CreatedAt) >= startUtc && (e.SentAt ?? e.CreatedAt) < endUtc);
        }

        return await query
            .OrderByDescending(e => e.SentAt ?? e.CreatedAt)
            .Take(request.Take)
            .Select(e => new EmailLogListItemDto(
                e.Id, e.ReportId, e.ReportScheduleId, e.BriefId, e.Provider, e.FromEmail, e.Recipient,
                e.Cc, e.Bcc, e.Subject, e.Status, e.SentAt, e.LastError, e.AttemptCount, e.MessageId,
                e.ProviderOperationId, e.CreatedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
