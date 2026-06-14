namespace MIP.Aws.Application.Abstractions.Reporting;

/// <summary>Unified application email sender (ACS, Microsoft Graph, or SMTP).</summary>
public interface IEmailSender
{
    Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Unified report and schedule email sender.</summary>
public interface IReportEmailSender : IEmailSender
{
    Task<int> RetryFailedAsync(CancellationToken cancellationToken);
}

public sealed record ReportEmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment> Attachments,
    Guid? ReportId = null,
    Guid? ReportScheduleId = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null,
    Guid? BriefId = null);

public sealed record ReportEmailSendResult(
    bool Success,
    string Provider,
    string FromEmail,
    IReadOnlyList<string> DeliveredTo,
    IReadOnlyList<Guid> EmailLogIds,
    string? MessageId,
    string? OperationId,
    string? ErrorMessage,
    EmailSendOutcome Outcome,
    DateTimeOffset? SentAt = null);

public enum EmailSendOutcome
{
    Sent,
    Failed,
    SkippedConfigurationMissing,
    SkippedNoRecipients
}
