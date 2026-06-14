using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Application.Abstractions.Reporting;

public sealed record EffectiveMailSettings(
    MailActiveProvider ActiveProvider,
    bool DevelopmentSafetyEnabled,
    string RedirectAllTo,
    string SubjectPrefix,
    string[] AllowedDomains);

public interface IMailSettingsService
{
    Task<EffectiveMailSettings> GetEffectiveAsync(CancellationToken cancellationToken);

    Task UpdateAsync(
        MailActiveProvider activeProvider,
        bool developmentSafetyEnabled,
        string? redirectAllTo,
        string? subjectPrefix,
        CancellationToken cancellationToken);
}
