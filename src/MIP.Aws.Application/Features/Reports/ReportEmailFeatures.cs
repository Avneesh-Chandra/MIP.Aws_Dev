using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    bool MailAutomationEnabled) : IRequest;

public sealed record SendDownloadMonitorStatusEmailCommand(DateOnly? MonitorDate) : IRequest<bool>;

public sealed record GetMailSettingsQuery : IRequest<MailSettingsDto>;

public sealed record MailSettingsDto(
    MailActiveProvider ActiveProvider,
    bool DevelopmentSafetyEnabled,
    string? RedirectAllTo,
    string? SubjectPrefix,
    bool StatusEmailEnabled,
    string StatusEmailRecipient,
    bool MailAutomationEnabled,
    string StatusEmailTimeUtc);

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

public sealed record GetRecentEmailLogsQuery(int Take = 25) : IRequest<IReadOnlyList<EmailLogListItemDto>>;

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
        return new MailSettingsDto(
            effective.ActiveProvider,
            effective.DevelopmentSafetyEnabled,
            effective.RedirectAllTo,
            effective.SubjectPrefix,
            scheduler.StatusEmailEnabled,
            scheduler.StatusEmailRecipient,
            scheduler.MailAutomationEnabled,
            scheduler.StatusEmailTimeUtc);
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

public sealed class UpdateMailSchedulerSettingsCommandHandler(IMailSettingsService mailSettings)
    : IRequestHandler<UpdateMailSchedulerSettingsCommand>
{
    public Task Handle(UpdateMailSchedulerSettingsCommand request, CancellationToken cancellationToken) =>
        mailSettings.UpdateSchedulerAsync(
            request.StatusEmailEnabled,
            request.StatusEmailRecipient,
            request.MailAutomationEnabled,
            cancellationToken);
}

public sealed class SendTestReportEmailCommandHandler(IReportEmailSender sender)
    : IRequestHandler<SendTestReportEmailCommand, TestEmailResponseDto>
{
    public async Task<TestEmailResponseDto> Handle(SendTestReportEmailCommand request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ReportEmailMessage([request.To.Trim()], request.Subject, request.HtmlBody, []),
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
}

public sealed class SendDownloadMonitorStatusEmailCommandHandler(IDownloadMonitorDailyStatusEmailService statusEmail)
    : IRequestHandler<SendDownloadMonitorStatusEmailCommand, bool>
{
    public async Task<bool> Handle(SendDownloadMonitorStatusEmailCommand request, CancellationToken cancellationToken)
    {
        await statusEmail.SendDailyStatusEmailAsync(request.MonitorDate, cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed class GetRecentEmailLogsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRecentEmailLogsQuery, IReadOnlyList<EmailLogListItemDto>>
{
    public async Task<IReadOnlyList<EmailLogListItemDto>> Handle(GetRecentEmailLogsQuery request, CancellationToken cancellationToken) =>
        await db.EmailLogs.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(request.Take)
            .Select(e => new EmailLogListItemDto(
                e.Id, e.ReportId, e.ReportScheduleId, e.BriefId, e.Provider, e.FromEmail, e.Recipient,
                e.Cc, e.Bcc, e.Subject, e.Status, e.SentAt, e.LastError, e.AttemptCount, e.MessageId,
                e.ProviderOperationId, e.CreatedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
