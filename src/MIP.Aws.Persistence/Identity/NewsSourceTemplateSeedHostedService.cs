using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Persistence.Identity;

/// <summary>
/// Development-only seed for newspaper-source templates that require manual-assisted MFA/OTP
/// login (e.g. Dainik Bhaskar — mobile + SMS OTP). Idempotent: only inserts when no source with
/// the same base URL already exists. NEVER seeds credentials.
/// </summary>
public sealed class NewsSourceTemplateSeedHostedService(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    ILogger<NewsSourceTemplateSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            await SeedBhaskarTemplateAsync(db, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "News-source template seed skipped due to error (non-fatal).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedBhaskarTemplateAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        const string baseUrl = "https://www.bhaskar.com/";
        var exists = await db.NewsSources.AnyAsync(s => s.BaseUrl == baseUrl && !s.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var source = new NewsSource
        {
            Id = Guid.NewGuid(),
            Name = "Dainik Bhaskar (template — manual-assisted MFA/OTP)",
            BaseUrl = baseUrl,
            SourceType = NewsSourceType.WebPortalLogin,
            AcquisitionMode = ContentAcquisitionMode.LicensedWebPortalSubscriber,
            SourceAccessMode = NewsSourceAccessMode.LicensedSubscriberPortal,
            LoginUrl = "https://www.bhaskar.com/account/login",
            EditionUrl = "https://www.bhaskar.com/epaper",
            DefaultLanguage = "hi",
            Country = "IN",
            LoginMethod = PortalLoginMethod.ManualOtpAssisted,
            RequiresLogin = true,
            RequiresAuthentication = true,
            UseHeadlessBrowser = false,
            RequiresMfa = true,
            RequiresOtp = true,
            ManualLoginRequired = true,
            RequiresManualAction = true,
            IsDownloadAllowed = false, // operator must verify publisher terms before flipping this
            AssistedSessionTimeoutMinutes = 30,
            OtpInstructions = "Enter the mobile number on file and the OTP sent by SMS. Do NOT share the OTP with any other system. After login completes, return to GFH MIP and click 'I have completed login'.",
            IsEnabled = true,
            Notes = "Development template only. Confirm GFH subscription, publisher terms, and download permission before enabling IsDownloadAllowed.",
            CreatedAt = now
        };

        db.NewsSources.Add(source);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Seeded Dainik Bhaskar manual-assisted template source {SourceId}.", source.Id);
    }
}
