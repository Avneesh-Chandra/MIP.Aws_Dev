using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.Reports;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Reporting;

public sealed class MailConfigStatusService(
    IMailSettingsService mailSettings,
    IOptions<AwsOptions> awsOptions,
    IOptions<EmailOptions> emailOptions,
    IOptions<MicrosoftGraphOptions> graphOptions,
    IOptions<MailAutomationOptions> mailAutomation) : IMailConfigStatusService
{
    public async Task<MailConfigStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var effective = await mailSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        var ses = awsOptions.Value.Ses;
        var email = emailOptions.Value;
        var graph = graphOptions.Value;
        var automation = mailAutomation.Value;

        var provider = email.Provider?.Trim() ?? string.Empty;
        string? from;
        bool transportEnabled;
        bool senderConfigured;
        bool ready;
        string? message;

        if (string.Equals(provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
        {
            from = !string.IsNullOrWhiteSpace(email.FromEmail) ? email.FromEmail.Trim() : graph.SenderMailbox.Trim();
            transportEnabled = graph.Enabled;
            senderConfigured = !string.IsNullOrWhiteSpace(from);
            ready = graph.IsConfigured && senderConfigured;
            message = ready
                ? null
                : "Configure Email:Provider=MicrosoftGraph and MicrosoftGraph (Enabled, TenantId, ClientId, ClientSecret, SenderMailbox=gfhnews@gfh.com). Ensure Mail.Send application permission with admin consent.";
        }
        else if (string.Equals(provider, "AwsSes", StringComparison.OrdinalIgnoreCase))
        {
            from = !string.IsNullOrWhiteSpace(email.FromEmail) ? email.FromEmail.Trim() : ses.SenderEmail.Trim();
            transportEnabled = ses.Enabled;
            senderConfigured = !string.IsNullOrWhiteSpace(from);
            ready = ses.Enabled && senderConfigured;
            message = ready
                ? null
                : "Configure Email:Provider=AwsSes, Aws:Ses:Enabled=true, and a verified sender address in SES.";
        }
        else if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            from = email.FromEmail?.Trim();
            transportEnabled = true;
            senderConfigured = !string.IsNullOrWhiteSpace(from);
            ready = senderConfigured;
            message = ready ? null : "Configure Email:Provider=Smtp and Email:FromEmail / Smtp settings.";
        }
        else
        {
            from = email.FromEmail?.Trim();
            transportEnabled = false;
            senderConfigured = !string.IsNullOrWhiteSpace(from);
            ready = false;
            message = $"Email provider '{provider}' is Mock or unknown. Set Email:Provider to MicrosoftGraph, AwsSes, or Smtp.";
        }

        return new MailConfigStatusDto(
            string.IsNullOrWhiteSpace(provider) ? effective.ActiveProvider.ToString() : provider,
            transportEnabled,
            senderConfigured,
            from,
            effective.DevelopmentSafetyEnabled,
            string.IsNullOrWhiteSpace(effective.RedirectAllTo) ? null : effective.RedirectAllTo,
            from,
            ready,
            message,
            scheduler.MailAutomationEnabled,
            scheduler.StatusEmailEnabled,
            scheduler.StatusEmailRecipient,
            scheduler.StatusEmailTimeUtc,
            scheduler.AdminPortalUrl);
    }
}
