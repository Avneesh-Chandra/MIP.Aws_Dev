using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MIP.Aws.Infrastructure.Reporting;

public sealed class AwsSesEmailSender : IReportEmailTransport
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly AwsSesOptions _sesOptions;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AwsSesEmailSender> _logger;

    public AwsSesEmailSender(
        IAmazonSimpleEmailServiceV2 ses,
        IOptions<AwsOptions> awsOptions,
        IOptions<EmailOptions> emailOptions,
        ILogger<AwsSesEmailSender> logger)
    {
        _ses = ses;
        _sesOptions = awsOptions.Value.Ses;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken)
    {
        var from = ResolveFromEmail();
        if (string.IsNullOrWhiteSpace(from))
        {
            return Failed(from, message.To, EmailSendOutcome.SkippedConfigurationMissing, "SES sender email is not configured.");
        }

        if (message.To.Count == 0)
        {
            return Failed(from, message.To, EmailSendOutcome.SkippedNoRecipients, "No recipients.");
        }

        try
        {
            var raw = BuildRawMessage(from, message);
            var response = await _ses.SendEmailAsync(new SendEmailRequest
            {
                FromEmailAddress = from,
                Destination = new Destination
                {
                    ToAddresses = message.To.ToList(),
                    CcAddresses = message.Cc?.ToList() ?? [],
                    BccAddresses = message.Bcc?.ToList() ?? []
                },
                Content = new EmailContent
                {
                    Raw = new RawMessage { Data = new MemoryStream(raw) }
                },
                ConfigurationSetName = string.IsNullOrWhiteSpace(_sesOptions.ConfigurationSet)
                    ? null
                    : _sesOptions.ConfigurationSet
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SES email sent to {To} | {Subject} | MessageId={MessageId}",
                string.Join(',', message.To), message.Subject, response.MessageId);

            return new ReportEmailSendResult(
                true,
                "aws-ses",
                from,
                message.To,
                [],
                response.MessageId,
                null,
                null,
                EmailSendOutcome.Sent,
                DateTimeOffset.UtcNow);
        }
        catch (MessageRejectedException ex)
        {
            var hint = ex.Message.Contains("not verified", StringComparison.OrdinalIgnoreCase)
                ? " SES sandbox: verify sender and recipient identities in the AWS console."
                : string.Empty;
            _logger.LogWarning(ex, "SES rejected email to {To}.{Hint}", string.Join(',', message.To), hint);
            return Failed(from, message.To, EmailSendOutcome.Failed, ex.Message + hint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SES send failed for {To}", string.Join(',', message.To));
            return Failed(from, message.To, EmailSendOutcome.Failed, ex.Message);
        }
    }

    private string ResolveFromEmail() =>
        !string.IsNullOrWhiteSpace(_emailOptions.FromEmail)
            ? _emailOptions.FromEmail.Trim()
            : _sesOptions.SenderEmail.Trim();

    private byte[] BuildRawMessage(string from, ReportEmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_emailOptions.FromDisplayName ?? "GFH MIP AWS", from));
        foreach (var to in message.To)
        {
            mime.To.Add(MailboxAddress.Parse(to));
        }

        if (message.Cc is not null)
        {
            foreach (var cc in message.Cc)
            {
                mime.Cc.Add(MailboxAddress.Parse(cc));
            }
        }

        if (message.Bcc is not null)
        {
            foreach (var bcc in message.Bcc)
            {
                mime.Bcc.Add(MailboxAddress.Parse(bcc));
            }
        }

        mime.Subject = message.Subject;
        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = StripHtml(message.HtmlBody)
        };

        foreach (var attachment in message.Attachments)
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        mime.Body = builder.ToMessageBody();
        using var ms = new MemoryStream();
        mime.WriteTo(ms);
        return ms.ToArray();
    }

    private static string StripHtml(string html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();

    private static ReportEmailSendResult Failed(
        string from,
        IReadOnlyList<string> to,
        EmailSendOutcome outcome,
        string? error) =>
        new(false, "aws-ses", from, to, [], null, null, error, outcome);
}
