using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Reporting;

/// <summary>Routes outbound mail through AWS SES with safety and audit logging.</summary>
public sealed class ReportEmailDispatcher(
    IApplicationDbContext db,
    IReportEmailTransport transport,
    ReportEmailSafetyService safety,
    IMailSettingsService mailSettings,
    IOptions<AwsOptions> awsOptions,
    IOptions<EmailOptions> emailOptions,
    ILogger<ReportEmailDispatcher> logger) : IReportEmailSender
{
    public async Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken)
    {
        var effective = await mailSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var to = message.To.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (to.Count == 0)
        {
            var skippedId = await WriteLogAsync(
                message, "None", string.Empty, string.Empty, EmailDeliveryStatus.SkippedNoRecipients,
                "No recipients were provided.", null, null, null, cancellationToken).ConfigureAwait(false);
            return new ReportEmailSendResult(false, "None", string.Empty, [], [skippedId], null, null,
                "No recipients were provided.", EmailSendOutcome.SkippedNoRecipients);
        }

        foreach (var recipient in to)
        {
            if (!safety.IsRecipientAllowed(recipient, effective))
            {
                var err = $"Recipient {recipient} is outside allowed domains and development safety is active.";
                var logId = await WriteLogAsync(message, "None", string.Empty, recipient,
                    EmailDeliveryStatus.Failed, err, null, null, string.Join("; ", to), cancellationToken).ConfigureAwait(false);
                return new ReportEmailSendResult(false, "None", string.Empty, [], [logId], null, null, err, EmailSendOutcome.Failed);
            }
        }

        var ses = awsOptions.Value.Ses;
        var email = emailOptions.Value;
        if (!ses.Enabled || !string.Equals(email.Provider, "AwsSes", StringComparison.OrdinalIgnoreCase))
        {
            var err = "AWS SES is not enabled. Set Email:Provider=AwsSes and Aws:Ses:Enabled=true.";
            var logId = await WriteLogAsync(message, "aws-ses", string.Empty, to[0],
                EmailDeliveryStatus.SkippedConfigurationMissing, err, null, null, string.Join("; ", to), cancellationToken)
                .ConfigureAwait(false);
            return new ReportEmailSendResult(false, "aws-ses", string.Empty, [], [logId], null, null, err,
                EmailSendOutcome.SkippedConfigurationMissing);
        }

        var from = !string.IsNullOrWhiteSpace(email.FromEmail) ? email.FromEmail.Trim() : ses.SenderEmail.Trim();
        if (string.IsNullOrWhiteSpace(from))
        {
            var err = "SES sender email is not configured.";
            var logId = await WriteLogAsync(message, "aws-ses", string.Empty, to[0],
                EmailDeliveryStatus.SkippedConfigurationMissing, err, null, null, string.Join("; ", to), cancellationToken)
                .ConfigureAwait(false);
            return new ReportEmailSendResult(false, "aws-ses", string.Empty, [], [logId], null, null, err,
                EmailSendOutcome.SkippedConfigurationMissing);
        }

        var safe = safety.Apply(to, message.Subject, message.HtmlBody, effective);
        var cc = message.Cc?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];
        var bcc = message.Bcc?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];

        if (safe.To.Count <= 1)
        {
            return await SendToRecipientsAsync(
                    message,
                    safe.To,
                    safe.Subject,
                    safe.HtmlBody,
                    safe.OriginalRecipients,
                    cc,
                    bcc,
                    from,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var aggregatedDelivered = new List<string>();
        var aggregatedLogIds = new List<Guid>();
        string? lastMessageId = null;
        string? lastOperationId = null;
        DateTimeOffset? lastSentAt = null;
        var failures = new List<string>();

        foreach (var recipient in safe.To)
        {
            var result = await SendToRecipientsAsync(
                    message,
                    [recipient],
                    safe.Subject,
                    safe.HtmlBody,
                    safe.OriginalRecipients,
                    cc,
                    bcc,
                    from,
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                aggregatedDelivered.AddRange(result.DeliveredTo);
                aggregatedLogIds.AddRange(result.EmailLogIds);
                lastMessageId = result.MessageId;
                lastOperationId = result.OperationId;
                lastSentAt = result.SentAt;
            }
            else if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                failures.Add($"{recipient}: {result.ErrorMessage}");
            }
        }

        if (aggregatedDelivered.Count == 0)
        {
            return new ReportEmailSendResult(
                false,
                "aws-ses",
                from,
                [],
                aggregatedLogIds,
                null,
                null,
                failures.Count > 0 ? string.Join(" | ", failures) : "All recipients failed.",
                EmailSendOutcome.Failed);
        }

        if (failures.Count > 0)
        {
            logger.LogWarning(
                "Mail sent to {DeliveredCount}/{TotalCount} recipients. Failures: {Failures}",
                aggregatedDelivered.Count,
                safe.To.Count,
                string.Join(" | ", failures));
        }

        return new ReportEmailSendResult(
            true,
            "aws-ses",
            from,
            aggregatedDelivered,
            aggregatedLogIds,
            lastMessageId,
            lastOperationId,
            failures.Count > 0 ? string.Join(" | ", failures) : null,
            EmailSendOutcome.Sent,
            lastSentAt);
    }

    private async Task<ReportEmailSendResult> SendToRecipientsAsync(
        ReportEmailMessage message,
        IReadOnlyList<string> recipients,
        string subject,
        string htmlBody,
        string? originalRecipients,
        IReadOnlyList<string> cc,
        IReadOnlyList<string> bcc,
        string from,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            return new ReportEmailSendResult(false, "aws-ses", from, [], [], null, null,
                "No recipients.", EmailSendOutcome.SkippedNoRecipients);
        }

        var redirected = originalRecipients is not null
            && !string.Equals(string.Join(";", recipients), originalRecipients, StringComparison.OrdinalIgnoreCase);

        var sendMessage = new ReportEmailMessage(
            recipients,
            subject,
            htmlBody,
            message.Attachments,
            message.ReportId,
            message.ReportScheduleId,
            cc,
            bcc,
            message.BriefId);

        var sendResult = await transport.SendAsync(sendMessage, cancellationToken).ConfigureAwait(false);
        if (!sendResult.Success)
        {
            var logIds = new List<Guid>();
            foreach (var recipient in recipients)
            {
                var id = await WriteLogAsync(message, sendResult.Provider, sendResult.FromEmail, recipient,
                    EmailDeliveryStatus.Failed, sendResult.ErrorMessage, null, null, originalRecipients,
                    cancellationToken, cc, bcc).ConfigureAwait(false);
                logIds.Add(id);
            }

            return sendResult with { EmailLogIds = logIds };
        }

        var sentAt = DateTimeOffset.UtcNow;
        var status = redirected ? EmailDeliveryStatus.RedirectedBySafety : EmailDeliveryStatus.Sent;
        var successLogIds = new List<Guid>();
        foreach (var recipient in recipients)
        {
            var id = await WriteLogAsync(message, sendResult.Provider, sendResult.FromEmail, recipient, status,
                null, sendResult.MessageId, sendResult.OperationId, originalRecipients, cancellationToken, cc, bcc, sentAt)
                .ConfigureAwait(false);
            successLogIds.Add(id);
        }

        logger.LogInformation("Mail sent via {Provider} to {Recipients}.", sendResult.Provider, string.Join(", ", recipients));
        return sendResult with { DeliveredTo = recipients, EmailLogIds = successLogIds, SentAt = sentAt };
    }

    public Task<int> RetryFailedAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    private async Task<Guid> WriteLogAsync(
        ReportEmailMessage message,
        string provider,
        string fromEmail,
        string to,
        EmailDeliveryStatus status,
        string? error,
        string? messageId,
        string? operationId,
        string? originalRecipients,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? cc = null,
        IReadOnlyList<string>? bcc = null,
        DateTimeOffset? sentAt = null)
    {
        var id = Guid.NewGuid();
        db.EmailLogs.Add(new EmailLog
        {
            Id = id,
            ReportId = message.ReportId,
            ReportScheduleId = message.ReportScheduleId,
            BriefId = message.BriefId,
            Provider = provider,
            FromEmail = fromEmail,
            Recipient = to,
            Cc = cc is { Count: > 0 } ? string.Join(";", cc) : null,
            Bcc = bcc is { Count: > 0 } ? string.Join(";", bcc) : null,
            Subject = message.Subject,
            Status = status,
            AttemptCount = 1,
            LastError = error,
            MessageId = messageId,
            ProviderOperationId = operationId,
            OriginalRecipients = originalRecipients,
            SentAt = sentAt ?? (status is EmailDeliveryStatus.Sent or EmailDeliveryStatus.RedirectedBySafety ? DateTimeOffset.UtcNow : null),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }
}
