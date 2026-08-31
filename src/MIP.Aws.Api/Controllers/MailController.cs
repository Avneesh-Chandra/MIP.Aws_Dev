using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.Reports;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

[ApiController]
[Route("api/v1/mail")]
[Authorize]
public sealed class MailController(IMediator mediator) : ControllerBase
{
    [HttpGet("config/status")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<MailConfigStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MailConfigStatusDto>>> GetConfigStatusAsync(CancellationToken cancellationToken)
    {
        var status = await mediator.Send(new GetMailConfigStatusQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<MailConfigStatusDto>.Ok(status, "Mail configuration status"));
    }

    [HttpGet("settings")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse<MailSettingsDto>>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await mediator.Send(new GetMailSettingsQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<MailSettingsDto>.Ok(settings, "Mail settings"));
    }

    [HttpPut("settings")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse>> UpdateSettingsAsync(
        [FromBody] UpdateMailSettingsApiRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateMailSettingsCommand(
            body.ActiveProvider,
            body.DevelopmentSafetyEnabled,
            body.RedirectAllTo,
            body.SubjectPrefix), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Mail settings updated"));
    }

    [HttpPut("settings/scheduler")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse>> UpdateSchedulerSettingsAsync(
        [FromBody] UpdateMailSchedulerSettingsApiRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateMailSchedulerSettingsCommand(
            body.StatusEmailEnabled,
            body.StatusEmailRecipient,
            body.MailAutomationEnabled,
            body.AdminRecipientEmail,
            body.BatchScheduleTimeUtc,
            body.BatchRecurrence,
            body.BatchRecurrenceDays,
            body.PdfDownloadMailEnabled,
            body.PdfDownloadMailRecipient,
            body.PdfDownloadMailSubject,
            body.PdfDownloadMailTimeUtc,
            body.SelectedStatusEmailRecipients,
            body.SelectedPdfDownloadMailRecipients,
            body.StatusEmailCompletionMode,
            body.PdfDownloadMailCompletionMode), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Scheduler mail settings updated"));
    }

    [HttpPost("test")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse<TestEmailResponseDto>>> SendTestAsync(
        [FromBody] SendTestEmailApiRequest body,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SendTestReportEmailCommand(
            body.To,
            body.Subject,
            body.HtmlBody), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<TestEmailResponseDto>.Ok(result, result.Success ? "Test email sent" : "Test email failed"));
    }

    [HttpPost("pdf-download-mail")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse<bool>>> SendPdfDownloadMailAsync(
        CancellationToken cancellationToken)
    {
        var sent = await mediator.Send(
                new SendPdfDownloadMailCommand(BypassSchedule: true),
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<bool>.Ok(
            sent,
            sent ? "PDF Download mail sent." : "PDF Download mail was not sent."));
    }

    [HttpPost("status-email")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse<bool>>> SendStatusEmailAsync(
        [FromQuery] DateOnly? date,
        [FromQuery] bool bypassThrottle = true,
        CancellationToken cancellationToken = default)
    {
        var sent = await mediator.Send(
            new SendDownloadMonitorStatusEmailCommand(date, RecipientOverride: null, BypassThrottle: bypassThrottle),
            cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<bool>.Ok(
            sent,
            sent ? "Download monitor status email sent." : "Download monitor status email was not sent."));
    }

    [HttpGet("logs/recent")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmailLogListItemDto>>>> RecentLogsAsync(
        [FromQuery] int take = 100,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var logs = await mediator.Send(new GetRecentEmailLogsQuery(take, date), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<EmailLogListItemDto>>.Ok(logs, "Email logs"));
    }

    public sealed record SendTestEmailApiRequest(string To, string Subject, string HtmlBody);

    public sealed record UpdateMailSettingsApiRequest(
        MailActiveProvider ActiveProvider,
        bool DevelopmentSafetyEnabled,
        string? RedirectAllTo,
        string? SubjectPrefix);

    public sealed record UpdateMailSchedulerSettingsApiRequest(
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
        MailCompletionMode? PdfDownloadMailCompletionMode = null);
}
