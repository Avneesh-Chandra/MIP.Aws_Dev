using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Reporting;

public sealed class MailSettingsService(
    IApplicationDbContext db,
    IOptions<MailOptions> mailOptions,
    IOptions<EmailSafetyOptions> legacySafetyOptions,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    IOptions<MailAutomationOptions> mailAutomationOptions) : IMailSettingsService
{
    private static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public async Task<EffectiveMailSettings> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        var file = mailOptions.Value;
        var legacy = legacySafetyOptions.Value;
        var row = await db.MailSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == SingletonId && !x.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        var provider = row is not null && Enum.TryParse<MailActiveProvider>(row.ActiveProvider, true, out var parsed)
            ? parsed
            : file.ActiveProvider;

        var safety = row?.DevelopmentSafetyEnabled ?? file.DevelopmentSafetyEnabled;
        var redirect = !string.IsNullOrWhiteSpace(row?.RedirectAllTo) ? row!.RedirectAllTo! :
            !string.IsNullOrWhiteSpace(file.RedirectAllTo) ? file.RedirectAllTo :
            legacy.RedirectAllTo;
        var prefix = !string.IsNullOrWhiteSpace(row?.SubjectPrefix) ? row!.SubjectPrefix :
            !string.IsNullOrWhiteSpace(file.SubjectPrefix) ? file.SubjectPrefix : legacy.PrefixSubject;
        var domains = file.AllowedDomains is { Length: > 0 } ? file.AllowedDomains : legacy.AllowedDomains;

        return new EffectiveMailSettings(provider, safety, redirect ?? string.Empty, prefix ?? string.Empty, domains);
    }

    public async Task<EffectiveSchedulerMailSettings> GetEffectiveSchedulerAsync(CancellationToken cancellationToken)
    {
        var scheduler = schedulerOptions.Value;
        var automation = mailAutomationOptions.Value;
        var row = await db.MailSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == SingletonId && !x.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return new EffectiveSchedulerMailSettings(
            row?.StatusEmailEnabled ?? scheduler.StatusEmailEnabled,
            !string.IsNullOrWhiteSpace(row?.StatusEmailRecipient) ? row!.StatusEmailRecipient!.Trim() : scheduler.StatusEmailRecipient,
            row?.MailAutomationEnabled ?? automation.Enabled,
            scheduler.StatusEmailTimeUtc,
            scheduler.AdminPortalUrl);
    }

    public async Task UpdateAsync(
        MailActiveProvider activeProvider,
        bool developmentSafetyEnabled,
        string? redirectAllTo,
        string? subjectPrefix,
        CancellationToken cancellationToken)
    {
        var row = await GetOrCreateRowAsync(cancellationToken).ConfigureAwait(false);
        row.ActiveProvider = activeProvider.ToString();
        row.DevelopmentSafetyEnabled = developmentSafetyEnabled;
        row.RedirectAllTo = redirectAllTo?.Trim();
        row.SubjectPrefix = string.IsNullOrWhiteSpace(subjectPrefix) ? "[GFH-MIP-TEST]" : subjectPrefix.Trim();
        row.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSchedulerAsync(
        bool statusEmailEnabled,
        string? statusEmailRecipient,
        bool mailAutomationEnabled,
        CancellationToken cancellationToken)
    {
        var row = await GetOrCreateRowAsync(cancellationToken).ConfigureAwait(false);
        row.StatusEmailEnabled = statusEmailEnabled;
        row.StatusEmailRecipient = statusEmailRecipient?.Trim();
        row.MailAutomationEnabled = mailAutomationEnabled;
        row.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailSettings> GetOrCreateRowAsync(CancellationToken cancellationToken)
    {
        var row = await db.MailSettings.FirstOrDefaultAsync(x => x.Id == SingletonId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new MailSettings
            {
                Id = SingletonId,
                ActiveProvider = mailOptions.Value.ActiveProvider.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.MailSettings.Add(row);
        }

        return row;
    }
}
