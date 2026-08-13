using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Application.Abstractions.Reporting;

public sealed record EffectiveMailSettings(
    MailActiveProvider ActiveProvider,
    bool DevelopmentSafetyEnabled,
    string RedirectAllTo,
    string SubjectPrefix,
    string[] AllowedDomains);

public sealed record EffectiveSchedulerMailSettings(
    bool StatusEmailEnabled,
    string StatusEmailRecipient,
    bool MailAutomationEnabled,
    string StatusEmailTimeUtc,
    string? AdminPortalUrl,
    string AdminRecipientEmail,
    string BatchScheduleTimeUtc,
    string BatchRecurrence,
    string? BatchRecurrenceDays);

public interface IMailSettingsService
{
    Task<EffectiveMailSettings> GetEffectiveAsync(CancellationToken cancellationToken);

    Task<EffectiveSchedulerMailSettings> GetEffectiveSchedulerAsync(CancellationToken cancellationToken);

    Task UpdateAsync(
        MailActiveProvider activeProvider,
        bool developmentSafetyEnabled,
        string? redirectAllTo,
        string? subjectPrefix,
        CancellationToken cancellationToken);

    Task UpdateSchedulerAsync(
        bool statusEmailEnabled,
        string? statusEmailRecipient,
        bool mailAutomationEnabled,
        string? adminRecipientEmail,
        string? batchScheduleTimeUtc,
        string? batchRecurrence,
        string? batchRecurrenceDays,
        CancellationToken cancellationToken);
}
