using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Persistence.Identity;

/// <summary>
/// Idempotent seed for GFH Gulf newspaper catalog (public PDF/HTML + PressReader subscriber portals).
/// PressReader credentials are read from configuration / User Secrets — never committed to source control.
/// </summary>
public sealed class NewspaperCatalogSeedHostedService(
    IServiceProvider serviceProvider,
    IOptions<NewspaperCatalogOptions> catalogOptions,
    IHostEnvironment hostEnvironment,
    ILogger<NewspaperCatalogSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!catalogOptions.Value.SeedOnStartup)
        {
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<INewsCredentialProtector>();
            var options = catalogOptions.Value;

            await SeedPublicSourceAsync(
                db,
                name: "Akhbar Al Khaleej",
                baseUrl: "https://akhbar-alkhaleej.com",
                connectorKey: "news.akhbar-alkhaleej",
                editionUrl: "https://media.akhbar-alkhaleej.com/source",
                sourceType: NewsSourceType.PublicHtml,
                acquisitionMode: ContentAcquisitionMode.PublicWebWithRobotsRespect,
                country: "BH",
                language: "ar",
                pdfDiscovery: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Idempotent patch so Azure/production matches local Public PDF discovery catalog.
            await EnsureAkhbarPdfDiscoverySettingsAsync(db, cancellationToken).ConfigureAwait(false);
            await EnsureAawsatPublicPdfDiscoveryAsync(db, cancellationToken).ConfigureAwait(false);
            await EnsureAlAyamPublicPdfSettingsAsync(db, options.AlAyamRecoveryTestBreak, cancellationToken).ConfigureAwait(false);

            await SeedPublicSourceAsync(
                db,
                name: "Kuwait - Al Qabas",
                baseUrl: "https://alqabas.com/",
                connectorKey: "news.alqabas",
                editionUrl: "https://d.alqabas.com/archive",
                sourceType: NewsSourceType.PublicPdf,
                acquisitionMode: ContentAcquisitionMode.PublicWebWithRobotsRespect,
                country: "KW",
                language: "ar",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await SeedPublicSourceAsync(
                db,
                name: "Bahrain - Al Ayam",
                baseUrl: "https://www.alayam.com/epaper",
                connectorKey: "news.alayam",
                editionUrl: "https://www.alayam.com/epaper",
                sourceType: NewsSourceType.PublicPdf,
                acquisitionMode: ContentAcquisitionMode.PartnerManagedConnector,
                country: "BH",
                language: "ar",
                useHeadlessBrowser: true,
                pdfDiscovery: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await SeedPublicSourceAsync(
                db,
                name: "KSA - Al Sharq Al Awsat",
                baseUrl: "https://aawsat.com/",
                connectorKey: "news.aawsat",
                editionUrl: "https://aawsat.com/files/pdf/",
                sourceType: NewsSourceType.PublicHtml,
                acquisitionMode: ContentAcquisitionMode.PublicWebWithRobotsRespect,
                country: "SA",
                language: "ar",
                useHeadlessBrowser: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await SeedPressReaderSourceAsync(
                db,
                protector,
                options,
                name: "UAE - Al Khaleej",
                editionUrl: "https://daralkhaleej.pressreader.com/al-khaleej-9aj7",
                country: "AE",
                cancellationToken).ConfigureAwait(false);

            await SeedPressReaderSourceAsync(
                db,
                protector,
                options,
                name: "UAE - Al Khaleej Economy",
                editionUrl: "https://daralkhaleej.pressreader.com/alkhaleej-economy",
                country: "AE",
                cancellationToken).ConfigureAwait(false);

            await EnsureDarAlKhaleejPressReaderSourcesAsync(db, cancellationToken).ConfigureAwait(false);
            await EnsureDarAlKhaleejPressReaderLoginSelectorsAsync(db, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Newspaper catalog seed skipped due to error (non-fatal).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedPublicSourceAsync(
        IApplicationDbContext db,
        string name,
        string baseUrl,
        string connectorKey,
        string editionUrl,
        NewsSourceType sourceType,
        ContentAcquisitionMode acquisitionMode,
        string country,
        string language,
        bool useHeadlessBrowser = false,
        bool pdfDiscovery = false,
        CancellationToken cancellationToken = default)
    {
        if (await db.NewsSources.AnyAsync(
                s => !s.IsDeleted && (s.ConnectorKey == connectorKey || s.BaseUrl == baseUrl),
                cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var source = new NewsSource
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseUrl = baseUrl,
            EditionUrl = editionUrl,
            SourceType = sourceType,
            ConnectorKey = connectorKey,
            AcquisitionMode = acquisitionMode,
            SourceAccessMode = NewsSourceAccessMode.PublicWeb,
            RequiresLogin = false,
            RequiresAuthentication = false,
            UseHeadlessBrowser = useHeadlessBrowser,
            IsDownloadAllowed = true,
            DefaultLanguage = language,
            Country = country,
            IsEnabled = true,
            DownloadFrequencyMinutes = 360,
            Notes = "GFH newspaper catalog (public edition discovery).",
            CreatedAt = now
        };

        if (pdfDiscovery)
        {
            source.PdfDiscoveryEnabled = true;
            source.PdfDiscoveryMode = PdfDiscoveryMode.Hybrid;
            source.PdfDiscoveryPageUrl = baseUrl.TrimEnd('/');
            source.PdfLinkKeywords = "pdf,download,open as pdf,edition,e-paper,illustrated pages,النسخة الورقية,تحميل,العدد";
            source.PreferTodayEdition = true;
            source.PreferLatestEdition = true;
            source.RequirePdfContentType = true;
            source.MinimumPdfSizeKb = 100;
            source.UseHeadlessBrowser = true;
        }

        db.NewsSources.Add(source);
        db.DownloadSchedules.Add(new DownloadSchedule
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            CronExpression = "0 6 * * *",
            TimeZoneId = "Asia/Bahrain",
            IsEnabled = true,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded newspaper source {Name} ({SourceId}).", name, source.Id);
    }

    private async Task SeedPressReaderSourceAsync(
        IApplicationDbContext db,
        INewsCredentialProtector protector,
        NewspaperCatalogOptions options,
        string name,
        string editionUrl,
        string country,
        CancellationToken cancellationToken)
    {
        var editionKey = NewsSourceUrlRules.GetUniquenessKey(
            NewsSourceType.WebPortalLogin,
            editionUrl,
            editionUrl,
            "PressReader");
        var candidates = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.SourceType == NewsSourceType.WebPortalLogin)
            .Select(s => new { s.EditionUrl, s.BaseUrl })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasEdition = candidates.Any(s =>
            s.EditionUrl == editionUrl
            || s.BaseUrl == editionUrl
            || (s.EditionUrl != null && NewsSourceUrlRules.Normalize(s.EditionUrl) == editionKey)
            || NewsSourceUrlRules.Normalize(s.BaseUrl) == editionKey);
        if (hasEdition)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var source = new NewsSource
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseUrl = editionUrl,
            EditionUrl = editionUrl,
            LoginUrl = editionUrl,
            SourceType = NewsSourceType.WebPortalLogin,
            ConnectorKey = "news.pressreader",
            AcquisitionMode = ContentAcquisitionMode.LicensedWebPortalSubscriber,
            SourceAccessMode = NewsSourceAccessMode.LicensedSubscriberPortal,
            PortalStrategyKey = "PressReader",
            LoginMethod = PortalLoginMethod.FormCssSelectors,
            UsernameSelector = "input[placeholder*='البريد']",
            PasswordSelector = "input[placeholder*='كلمة المرور']",
            SubmitSelector = "[role='dialog'] button:has-text('تسجيل الدخول')",
            LoginIconSelector = "button:has-text('تسجيل الدخول')",
            RequiresLogin = true,
            RequiresManualAction = false,
            RequiresAuthentication = true,
            IsDownloadAllowed = false,
            PdfDiscoveryEnabled = true,
            UseHeadlessBrowser = true,
            DownloadWaitTimeoutSeconds = 180,
            MinimumPdfSizeKb = 100,
            DefaultLanguage = "ar",
            Country = country,
            IsEnabled = true,
            DownloadFrequencyMinutes = 360,
            Notes = "GFH licensed PressReader portal. Enter subscriber credentials in admin UI; enable download only after compliance approval.",
            CreatedAt = now
        };

        db.NewsSources.Add(source);
        db.DownloadSchedules.Add(new DownloadSchedule
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            CronExpression = "0 7 * * *",
            TimeZoneId = "Asia/Dubai",
            IsEnabled = true,
            CreatedAt = now
        });

        // Credentials are never seeded in Development — admins enter them via the encrypted UI only.
        logger.LogInformation(
            "PressReader source {Name} seeded without credentials. Add username/password via News Sources admin UI.",
            name);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded PressReader source {Name} ({SourceId}).", name, source.Id);
    }

    private static async Task EnsureAkhbarPdfDiscoverySettingsAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var matches = await db.NewsSources
            .Where(s => !s.IsDeleted && (s.BaseUrl.Contains("akhbar-alkhaleej") || s.Name.Contains("Akhbar Al Khaleej")))
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (matches.Count == 0)
        {
            return;
        }

        var source = matches[0];
        foreach (var duplicate in matches.Skip(1))
        {
            duplicate.IsDeleted = true;
            duplicate.ModifiedAt = DateTimeOffset.UtcNow;
        }

        source.Name = "Akhbar Al Khaleej";
        source.BaseUrl = "https://akhbar-alkhaleej.com";
        source.SourceType = NewsSourceType.PublicHtml;
        source.RequiresLogin = false;
        source.RequiresManualAction = false;
        source.IsDownloadAllowed = true;
        source.PdfDiscoveryEnabled = true;
        source.PdfDiscoveryMode = PdfDiscoveryMode.Hybrid;
        source.PdfDiscoveryPageUrl = "https://akhbar-alkhaleej.com";
        source.PdfLinkKeywords = "pdf,download,open as pdf,edition,e-paper,illustrated pages,النسخة الورقية,تحميل,العدد";
        source.PreferTodayEdition = true;
        source.PreferLatestEdition = true;
        source.RequirePdfContentType = true;
        source.MinimumPdfSizeKb = 100;
        source.UseHeadlessBrowser = true;
        source.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDarAlKhaleejPressReaderSourcesAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var sources = await db.NewsSources
            .Where(s => !s.IsDeleted
                        && s.SourceType == NewsSourceType.WebPortalLogin
                        && (s.EditionUrl != null && s.EditionUrl.Contains("daralkhaleej.pressreader.com")
                            || s.BaseUrl.Contains("daralkhaleej.pressreader.com")))
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sources.Count == 0)
        {
            return;
        }

        var canonical = new (string PathSuffix, string Name, string EditionUrl)[]
        {
            ("al-khaleej-9aj7", "UAE - Al Khaleej", "https://daralkhaleej.pressreader.com/al-khaleej-9aj7"),
            ("alkhaleej-economy", "UAE - Al Khaleej Economy", "https://daralkhaleej.pressreader.com/alkhaleej-economy")
        };

        foreach (var (pathSuffix, name, editionUrl) in canonical)
        {
            var identityKeys = NewsSourceUrlRules.GetIdentityKeys(
                NewsSourceType.WebPortalLogin,
                editionUrl,
                editionUrl,
                "PressReader");

            var matches = sources
                .Where(s =>
                {
                    var keys = NewsSourceUrlRules.GetIdentityKeys(
                        s.SourceType,
                        s.BaseUrl,
                        s.EditionUrl,
                        s.PortalStrategyKey);
                    return (s.EditionUrl?.Contains(pathSuffix, StringComparison.OrdinalIgnoreCase) ?? false)
                           || s.BaseUrl.Contains(pathSuffix, StringComparison.OrdinalIgnoreCase)
                           || keys.Any(k => identityKeys.Contains(k, StringComparer.OrdinalIgnoreCase));
                })
                .ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            var keeper = matches[0];
            foreach (var duplicate in matches.Skip(1))
            {
                duplicate.IsDeleted = true;
                duplicate.ModifiedAt = DateTimeOffset.UtcNow;
            }

            keeper.Name = name;
            keeper.BaseUrl = editionUrl;
            keeper.EditionUrl = editionUrl;
            keeper.LoginUrl ??= editionUrl;
            keeper.PortalStrategyKey ??= "PressReader";
            keeper.ConnectorKey ??= "news.pressreader";
            keeper.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDarAlKhaleejPressReaderLoginSelectorsAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var sources = await db.NewsSources
            .Where(s => !s.IsDeleted
                        && s.SourceType == NewsSourceType.WebPortalLogin
                        && (s.EditionUrl != null && s.EditionUrl.Contains("daralkhaleej.pressreader.com")
                            || s.BaseUrl.Contains("daralkhaleej.pressreader.com")))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var source in sources)
        {
            source.PortalStrategyKey ??= "PressReader";
            source.UsernameSelector ??= "input[placeholder*='البريد']";
            source.PasswordSelector ??= "input[placeholder*='كلمة المرور']";
            source.SubmitSelector ??= "[role='dialog'] button:has-text('تسجيل الدخول')";
            source.LoginIconSelector ??= "button:has-text('تسجيل الدخول')";
            source.DownloadMenuItemSelector ??= "li:has-text('تنزيل'), [role='menuitem']:has-text('تنزيل'), button:has-text('تنزيل')";
            source.ContextMenuSelector ??= "[class*='page-actions'] button, [class*='toolbar'] button[class*='more']";
            source.NewspaperCanvasSelector ??= "[class*='issue-page'], [class*='page-image'], #reader";
            source.DownloadWaitTimeoutSeconds = Math.Max(
                source.DownloadWaitTimeoutSeconds,
                DarAlKhaleejPressReaderBaseline.DownloadWaitTimeoutSeconds);
            source.ModifiedAt = DateTimeOffset.UtcNow;
        }

        if (sources.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureAawsatPublicPdfDiscoveryAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .Where(s => !s.IsDeleted && s.BaseUrl.Contains("aawsat.com"))
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (source is null)
        {
            return;
        }

        source.Name = "Asharq Al-Awsat";
        source.BaseUrl = "https://aawsat.com";
        source.SourceType = NewsSourceType.PublicHtml;
        source.ConnectorKey = "news.aawsat";
        source.RequiresLogin = false;
        source.RequiresManualAction = false;
        source.IsDownloadAllowed = true;
        source.PdfDiscoveryEnabled = true;
        source.PdfDiscoveryPageUrl = "https://aawsat.com";
        // ManualSelector: use Playwright click path (Download → Full Publication), not homepage link auto-scan.
        source.PdfDiscoveryMode = PdfDiscoveryMode.ManualSelector;
        source.EditionUrl = AawsatPublicPdfBaseline.EditionUrl;
        source.PdfDownloadSelector = "button[aria-label='Download']";
        source.PdfLinkSelector = "a:has-text('Full Publication')";
        source.PdfLinkKeywords ??= "pdf,download,edition,e-paper,النسخة الورقية,تحميل,العدد,full publication,النسخة الكاملة";
        source.PreferTodayEdition = true;
        source.PreferLatestEdition = true;
        source.RequirePdfContentType = true;
        source.MinimumPdfSizeKb = Math.Max(source.MinimumPdfSizeKb, 100);
        source.UseHeadlessBrowser = true;
        source.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureAlAyamPublicPdfSettingsAsync(
        IApplicationDbContext db,
        bool applyRecoveryTestBreak,
        CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .Where(s => !s.IsDeleted
                        && (s.ConnectorKey == "news.alayam"
                            || s.BaseUrl.Contains("alayam.com")
                            || (s.EditionUrl != null && s.EditionUrl.Contains("alayam.com"))))
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (source is null)
        {
            return;
        }

        source.Name = "Bahrain - Al Ayam";
        source.BaseUrl = "https://www.alayam.com/epaper";
        source.EditionUrl = "https://www.alayam.com/epaper";
        source.SourceType = NewsSourceType.PublicPdf;
        source.ConnectorKey = "news.alayam";
        source.AcquisitionMode = ContentAcquisitionMode.PartnerManagedConnector;
        source.RequiresLogin = false;
        source.RequiresManualAction = false;
        source.IsDownloadAllowed = true;
        source.PdfDiscoveryEnabled = true;
        source.PdfDiscoveryMode = PdfDiscoveryMode.ManualSelector;
        source.PdfDiscoveryPageUrl = "https://www.alayam.com/epaper";
        source.PdfLinkSelector = "a#aPDFdownloadAllPages, a:has-text('كل الصفحات')";
        source.PdfLinkKeywords ??= "pdf,download,all pages,كل الصفحات,النسخة الورقية,epaper,INAF";
        source.PreferTodayEdition = true;
        source.PreferLatestEdition = true;
        source.RequirePdfContentType = true;
        source.MinimumPdfSizeKb = Math.Max(source.MinimumPdfSizeKb, 100);
        source.UseHeadlessBrowser = true;
        source.ModifiedAt = DateTimeOffset.UtcNow;

        if (applyRecoveryTestBreak)
        {
            source.PdfLinkSelector = AlAyamPublicPdfBaseline.Broken.PdfLinkSelector;
            source.BaseUrl = AlAyamPublicPdfBaseline.Broken.EpaperUrl;
            source.EditionUrl = AlAyamPublicPdfBaseline.Broken.EpaperUrl;
            source.PdfDiscoveryPageUrl = AlAyamPublicPdfBaseline.Broken.EpaperUrl;
        }

        var duplicates = await db.NewsSources
            .Where(s => !s.IsDeleted
                        && s.Id != source.Id
                        && (s.ConnectorKey == "news.alayam"
                            || s.BaseUrl.Contains("alayam.com")
                            || (s.EditionUrl != null && s.EditionUrl.Contains("alayam.com"))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var duplicate in duplicates)
        {
            duplicate.IsDeleted = true;
            duplicate.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
