using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Enums;

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
    string? BatchRecurrenceDays,
    bool PdfDownloadMailEnabled,
    string PdfDownloadMailRecipient,
    string PdfDownloadMailSubject,
    string PdfDownloadMailTimeUtc,
    IReadOnlyList<string> SelectedStatusEmailRecipients,
    IReadOnlyList<string> SelectedPdfDownloadMailRecipients,
    MailCompletionMode StatusEmailCompletionMode,
    MailCompletionMode PdfDownloadMailCompletionMode,
    PdfDownloadMailDesign PdfDownloadMailDesign);

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
        bool? pdfDownloadMailEnabled,
        string? pdfDownloadMailRecipient,
        string? pdfDownloadMailSubject,
        string? pdfDownloadMailTimeUtc,
        IReadOnlyList<string>? selectedStatusEmailRecipients,
        IReadOnlyList<string>? selectedPdfDownloadMailRecipients,
        MailCompletionMode? statusEmailCompletionMode,
        MailCompletionMode? pdfDownloadMailCompletionMode,
        PdfDownloadMailDesign? pdfDownloadMailDesign,
        CancellationToken cancellationToken);
}
