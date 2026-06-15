using MailKit.Net.Smtp;
using MailKit.Security;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MIP.Aws.Infrastructure.Reporting;

public sealed class SmtpEmailSender : IReportEmailSender
{
    private readonly SmtpOptions _smtp;
    private readonly EmailOptions _email;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<EmailOptions> emailOptions,
        ILogger<SmtpEmailSender> logger)
    {
        _smtp = smtpOptions.Value;
        _email = emailOptions.Value;
        _logger = logger;
    }

    public async Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken)
    {
        if (!_smtp.IsConfigured)
        {
            return new ReportEmailSendResult(
                false, "smtp", _email.FromEmail, message.To, [], null, null,
                "SMTP is not configured.", EmailSendOutcome.SkippedConfigurationMissing);
        }

        if (message.To.Count == 0)
        {
            return new ReportEmailSendResult(
                false, "smtp", _email.FromEmail, message.To, [], null, null,
                "No recipients.", EmailSendOutcome.SkippedNoRecipients);
        }

        var from = string.IsNullOrWhiteSpace(_email.FromEmail) ? _smtp.ResolvedFromEmail : _email.FromEmail;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_email.FromDisplayName ?? "MIP.Aws", from));
        foreach (var to in message.To)
        {
            mime.To.Add(MailboxAddress.Parse(to));
        }

        mime.Subject = message.Subject;
        var builder = new BodyBuilder { HtmlBody = message.HtmlBody };
        foreach (var attachment in message.Attachments)
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        mime.Body = builder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                _smtp.Host,
                _smtp.Port,
                _smtp.UseSsl ? SecureSocketOptions.SslOnConnect
                : _smtp.UseStartTls ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto,
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(_smtp.Username))
            {
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password, cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("SMTP email sent to {To} | {Subject}", string.Join(',', message.To), message.Subject);
            return new ReportEmailSendResult(
                true, "smtp", from, message.To, [], null, null, null, EmailSendOutcome.Sent, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed");
            return new ReportEmailSendResult(
                false, "smtp", from, message.To, [], null, null, ex.Message, EmailSendOutcome.Failed);
        }
    }

    public Task<int> RetryFailedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}
