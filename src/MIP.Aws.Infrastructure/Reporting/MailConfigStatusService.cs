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
    IOptions<MailAutomationOptions> mailAutomation) : IMailConfigStatusService
{
    public async Task<MailConfigStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var effective = await mailSettings.GetEffectiveAsync(cancellationToken).ConfigureAwait(false);
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        var ses = awsOptions.Value.Ses;
        var email = emailOptions.Value;
        var automation = mailAutomation.Value;
        var from = !string.IsNullOrWhiteSpace(email.FromEmail) ? email.FromEmail.Trim() : ses.SenderEmail.Trim();
        var sesReady = ses.Enabled
            && string.Equals(email.Provider, "AwsSes", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(from);
        var message = sesReady
            ? null
            : "Configure Email:Provider=AwsSes, Aws:Ses:Enabled=true, and a verified sender address in SES.";

        return new MailConfigStatusDto(
            effective.ActiveProvider.ToString(),
            ses.Enabled,
            !string.IsNullOrWhiteSpace(from),
            from,
            effective.DevelopmentSafetyEnabled,
            string.IsNullOrWhiteSpace(effective.RedirectAllTo) ? null : effective.RedirectAllTo,
            from,
            sesReady,
            message,
            scheduler.MailAutomationEnabled,
            scheduler.StatusEmailEnabled,
            scheduler.StatusEmailRecipient,
            scheduler.StatusEmailTimeUtc,
            scheduler.AdminPortalUrl);
    }
}
