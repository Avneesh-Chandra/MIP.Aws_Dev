using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Reporting;

public sealed record EmailSafetyAppliedResult(
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody,
    string? OriginalRecipients);

public sealed class ReportEmailSafetyService(IOptions<EmailSafetyOptions> legacySafetyOptions)
{
    public EmailSafetyAppliedResult Apply(
        IReadOnlyList<string> to,
        string subject,
        string htmlBody,
        EffectiveMailSettings settings)
    {
        var legacy = legacySafetyOptions.Value;
        var enabled = settings.DevelopmentSafetyEnabled || legacy.Enabled;
        if (!enabled)
        {
            return new EmailSafetyAppliedResult(to, subject, htmlBody, null);
        }

        var original = string.Join("; ", to.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        var redirect = !string.IsNullOrWhiteSpace(settings.RedirectAllTo)
            ? settings.RedirectAllTo.Trim()
            : legacy.RedirectAllTo?.Trim();

        if (string.IsNullOrWhiteSpace(redirect))
        {
            return new EmailSafetyAppliedResult(to, subject, htmlBody, original);
        }

        var prefix = !string.IsNullOrWhiteSpace(settings.SubjectPrefix)
            ? settings.SubjectPrefix.Trim()
            : legacy.PrefixSubject?.Trim() ?? string.Empty;
        var prefixWithSpace = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + " ";
        var safeSubject = subject.StartsWith(prefixWithSpace, StringComparison.OrdinalIgnoreCase) ? subject : prefixWithSpace + subject;
        var banner = "<p style=\"background:#fff3cd;color:#664d03;padding:8px;border-radius:4px;font-size:12px;\"><strong>Development safety active.</strong> This email was redirected for testing.</p>";
        var note = $"<p style=\"color:#666;font-size:12px;\"><em>Original recipients: {System.Net.WebUtility.HtmlEncode(original)}</em></p>";
        var safeBody = banner + note + htmlBody;

        return new EmailSafetyAppliedResult([redirect], safeSubject, safeBody, original);
    }

    public bool IsRecipientAllowed(string email, EffectiveMailSettings settings)
    {
        var legacy = legacySafetyOptions.Value;
        var enabled = settings.DevelopmentSafetyEnabled || legacy.Enabled;
        if (!enabled)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(settings.RedirectAllTo) || !string.IsNullOrWhiteSpace(legacy.RedirectAllTo))
        {
            return true;
        }

        var domains = settings.AllowedDomains is { Length: > 0 } ? settings.AllowedDomains : legacy.AllowedDomains;
        if (domains is not { Length: > 0 })
        {
            return true;
        }

        var at = email.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var domain = email[(at + 1)..];
        return domains.Any(d => string.Equals(d.Trim(), domain, StringComparison.OrdinalIgnoreCase));
    }
}
