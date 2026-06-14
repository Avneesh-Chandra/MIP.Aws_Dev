using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Portal;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Portal;
// PortalFieldMapper in Features.NewsSources
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>
/// Playwright-based automation for GFH-licensed subscriber portals only (no CAPTCHA/MFA bypass).
/// </summary>
public sealed class PlaywrightWebPortalAutomationService(
    IApplicationDbContext db,
    INewsCredentialProtector credentialProtector,
    IFileStorageService fileStorage,
    IOptions<StorageOptions> storageOptions,
    IOptions<NewsIngestionComplianceOptions> complianceOptions,
    IPortalDownloadStrategyResolver strategyResolver,
    ILogger<PlaywrightWebPortalAutomationService> logger) : IWebPortalAutomationService
{
    private readonly StorageOptions _storage = storageOptions.Value;
    private readonly NewsIngestionComplianceOptions _compliance = complianceOptions.Value;

    public async Task<NewsPortalLoginTestResultDto> TestLoginAsync(Guid newsSourceId, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .Include(s => s.Credential)
            .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, cancellationToken).ConfigureAwait(false);

        var probe = await ValidateAndResolveCredentialsAsync(
            source,
            requireDownloadAllowed: false,
            isLoginProbe: true,
            cancellationToken).ConfigureAwait(false);
        if (!probe.Success)
        {
            return new NewsPortalLoginTestResultDto(false, probe.Message, probe.FailureCode, null, null);
        }

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await LaunchBrowserAsync(playwright).ConfigureAwait(false);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "MIP.Aws/1.0 (licensed-portal; authorized-subscriber-automation)",
            Locale = source.DefaultLanguage ?? "en-US"
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(120_000);

        var strategy = strategyResolver.Resolve(source);
        var isPressReaderLogin = strategy.StrategyKey == PortalStrategyKeys.PressReader;
        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(source, null, PortalAuditEventKind.LoginAttempt, "Starting login test (no edition download).", null, cancellationToken).ConfigureAwait(false);

            var loginOutcome = await RunStrategyLoginAsync(strategy, page, source, null, probe.Username!, probe.Password!, cancellationToken).ConfigureAwait(false);
            if (!loginOutcome.Success)
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "login-test", cancellationToken).ConfigureAwait(false);
                await HandleLoginFailureAsync(source, null, loginOutcome, shot, html, cancellationToken).ConfigureAwait(false);
                return new NewsPortalLoginTestResultDto(false, loginOutcome.Message, loginOutcome.FailureCode, shot, html);
            }

            if (await PortalChallengeDetector.DetectCaptchaAsync(page).ConfigureAwait(false) || await PortalChallengeDetector.DetectMfaAsync(page).ConfigureAwait(false))
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "login-test", cancellationToken).ConfigureAwait(false);
                await BlockAutomationAsync(source, null, "CAPTCHA or MFA is required after login.", cancellationToken).ConfigureAwait(false);
                return new NewsPortalLoginTestResultDto(false, "CAPTCHA or MFA is required; automated login cannot continue.", "CaptchaOrMfa", shot, html);
            }

            await AddAuditAsync(
                source,
                null,
                isPressReaderLogin ? PressReaderPortalAuditEvents.LoginCompleted : PortalAuditEventKind.LoginSuccess,
                "Login test succeeded.",
                null,
                cancellationToken).ConfigureAwait(false);

            // Clear sticky manual-action gate after a successful probe (prior selector/CAPTCHA failures set this).
            if (source.RequiresManualAction)
            {
                source.RequiresManualAction = false;
                source.ModifiedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return new NewsPortalLoginTestResultDto(true, "Login test succeeded.", null, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login test failed for source {SourceId}", newsSourceId);
            var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "login-test", cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(source, null, PortalAuditEventKind.Failure, ex.Message, "UnexpectedError", cancellationToken, shot, html).ConfigureAwait(false);
            return new NewsPortalLoginTestResultDto(false, ex.Message, "UnexpectedError", shot, html);
        }
        finally
        {
            if (isPressReaderLogin)
            {
                await TryLogoutAsync(page, source, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task RunLicensedPortalDownloadForJobAsync(Guid downloadJobId, CancellationToken cancellationToken) =>
        RunJobInternalAsync(downloadJobId, cancellationToken);

    public async Task<NewsPortalDownloadTestResultDto> TestDownloadAsync(Guid newsSourceId, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .Include(s => s.Credential)
            .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, cancellationToken).ConfigureAwait(false);

        var strategy = strategyResolver.Resolve(source);
        var isPressReader = strategy.StrategyKey == PortalStrategyKeys.PressReader;
        var probe = await ValidateAndResolveCredentialsAsync(source, requireDownloadAllowed: true, isLoginProbe: false, cancellationToken).ConfigureAwait(false);
        if (!probe.Success)
        {
            if (isPressReader && probe.FailureCode == "DownloadNotAllowed")
            {
                await AddAuditAsync(source, null, PressReaderPortalAuditEvents.DownloadBlockedByCompliance, probe.Message, probe.FailureCode, cancellationToken).ConfigureAwait(false);
            }

            return new NewsPortalDownloadTestResultDto(false, probe.Message, probe.FailureCode, null, null, null, null, null);
        }

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await LaunchBrowserAsync(playwright).ConfigureAwait(false);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "MIP.Aws/1.0 (licensed-portal; authorized-subscriber-automation)",
            Locale = source.DefaultLanguage ?? "en-US",
            AcceptDownloads = true
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(120_000);

        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);

            var loginOutcome = await RunStrategyLoginAsync(strategy, page, source, null, probe.Username!, probe.Password!, cancellationToken).ConfigureAwait(false);
            if (!loginOutcome.Success)
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "download-test", cancellationToken).ConfigureAwait(false);
                return new NewsPortalDownloadTestResultDto(false, loginOutcome.Message, loginOutcome.FailureCode, null, null, null, shot, html);
            }

            if (await PortalChallengeDetector.DetectCaptchaAsync(page).ConfigureAwait(false) || await PortalChallengeDetector.DetectMfaAsync(page).ConfigureAwait(false))
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "download-test", cancellationToken).ConfigureAwait(false);
                source.RequiresManualAction = true;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return new NewsPortalDownloadTestResultDto(false, "CAPTCHA or MFA detected.", "CaptchaOrMfa", null, null, null, shot, html);
            }

            var session = new PortalAutomationSession
            {
                Page = page,
                Source = source,
                DownloadJobId = null,
                Username = probe.Username!,
                Password = probe.Password!
            };

            var downloadOutcome = await strategy.DownloadEditionAsync(session, cancellationToken).ConfigureAwait(false);
            if (!downloadOutcome.Success)
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "download-test", cancellationToken).ConfigureAwait(false);
                return new NewsPortalDownloadTestResultDto(false, downloadOutcome.Message, downloadOutcome.FailureCode, null, null, null, shot, html);
            }

            var viewUrl = downloadOutcome.DownloadedFileId is Guid fid
                ? $"/api/v1/news-sources/{newsSourceId}/pdf/{fid}"
                : downloadOutcome.StoredRelativePath is not null
                    ? $"/api/v1/news-sources/{newsSourceId}/pdf/latest"
                    : null;

            return new NewsPortalDownloadTestResultDto(
                true,
                downloadOutcome.Message,
                null,
                downloadOutcome.DownloadedFileId,
                downloadOutcome.StoredRelativePath,
                viewUrl,
                null,
                null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Portal download test failed for source {SourceId}", newsSourceId);
            var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "download-test", cancellationToken).ConfigureAwait(false);
            return new NewsPortalDownloadTestResultDto(false, ex.Message, "UnexpectedError", null, null, null, shot, html);
        }
        finally
        {
            if (isPressReader)
            {
                await TryLogoutAsync(page, source, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<NewsPortalLogoutTestResultDto> TestLogoutAsync(Guid newsSourceId, CancellationToken cancellationToken)
    {
        var source = await db.NewsSources
            .Include(s => s.Credential)
            .FirstAsync(s => s.Id == newsSourceId && !s.IsDeleted, cancellationToken).ConfigureAwait(false);

        if (source.SourceType != NewsSourceType.WebPortalLogin)
        {
            return new NewsPortalLogoutTestResultDto(false, "Logout probe applies to WebPortalLogin sources only.", "InvalidSourceType", null, null, false);
        }

        var strategy = strategyResolver.Resolve(source);
        var probe = await ValidateAndResolveCredentialsAsync(source, requireDownloadAllowed: false, isLoginProbe: true, cancellationToken).ConfigureAwait(false);

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await LaunchBrowserAsync(playwright).ConfigureAwait(false);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "MIP.Aws/1.0 (licensed-portal; authorized-subscriber-automation)",
            Locale = source.DefaultLanguage ?? "en-US"
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(120_000);

        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(source, null, PortalAuditEventKind.Logout, "Starting logout probe.", null, cancellationToken).ConfigureAwait(false);

            if (PressReaderPortalLogin.IsBrandedDarAlKhaleejSource(source))
            {
                var editionUrl = !string.IsNullOrWhiteSpace(source.EditionUrl) ? source.EditionUrl.Trim() : source.BaseUrl?.Trim();
                if (string.IsNullOrWhiteSpace(editionUrl))
                {
                    return new NewsPortalLogoutTestResultDto(false, "EditionUrl is required.", "EditionUrlMissing", null, null, false);
                }

                try
                {
                    await page.GotoAsync(editionUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60_000 }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return new NewsPortalLogoutTestResultDto(false, $"Could not open edition: {ex.Message}", "NavigationFailed", null, null, false);
                }

                var hadSession = await PressReaderPortalLogin.IsLoggedInForLogoutAsync(page).ConfigureAwait(false);
                if (!hadSession && probe.Success)
                {
                    var loginOutcome = await RunStrategyLoginAsync(strategy, page, source, null, probe.Username!, probe.Password!, cancellationToken).ConfigureAwait(false);
                    if (!loginOutcome.Success)
                    {
                        var (loginShot, loginHtml) = await CaptureFailureArtifactsAsync(source, null, page, "logout-test", cancellationToken).ConfigureAwait(false);
                        return new NewsPortalLogoutTestResultDto(false, $"Could not sign in before logout test: {loginOutcome.Message}", loginOutcome.FailureCode, loginShot, loginHtml, false);
                    }

                    hadSession = await PressReaderPortalLogin.IsLoggedInForLogoutAsync(page).ConfigureAwait(false);
                }

                if (!hadSession)
                {
                    var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "logout-test", cancellationToken).ConfigureAwait(false);
                    return new NewsPortalLogoutTestResultDto(
                        false,
                        "No subscriber session in the probe browser. Run Test login first, or save portal credentials so the probe can sign in before signing out.",
                        "NotLoggedIn",
                        shot,
                        html,
                        false);
                }

                var released = await PressReaderPortalLogin.TryReleaseDarAlKhaleejSessionAsync(page, source, cancellationToken).ConfigureAwait(false);
                if (!released)
                {
                    var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "logout-test", cancellationToken).ConfigureAwait(false);
                    return new NewsPortalLogoutTestResultDto(
                        false,
                        "Sign-out did not complete. Click the subscriber id (e.g. JM37955283) in the top-right, then Sign out / تسجيل الخروج.",
                        "LogoutIncomplete",
                        shot,
                        html,
                        true);
                }

                await AddAuditAsync(
                    source,
                    null,
                    PortalAuditEventKind.Logout,
                    "Logout probe succeeded (subscriber menu → Sign out).",
                    null,
                    cancellationToken).ConfigureAwait(false);

                return new NewsPortalLogoutTestResultDto(
                    true,
                    "PressReader sign-out completed; concurrent session slot should be released.",
                    null,
                    null,
                    null,
                    true);
            }

            if (!string.IsNullOrWhiteSpace(source.LogoutUrl))
            {
                try
                {
                    await page.GotoAsync(source.LogoutUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
                    await AddAuditAsync(source, null, PortalAuditEventKind.Logout, "Visited LogoutUrl for logout probe.", null, cancellationToken).ConfigureAwait(false);
                    return new NewsPortalLogoutTestResultDto(true, "Visited configured LogoutUrl.", null, null, null, true);
                }
                catch (Exception ex)
                {
                    return new NewsPortalLogoutTestResultDto(false, ex.Message, "NavigationFailed", null, null, false);
                }
            }

            return new NewsPortalLogoutTestResultDto(
                false,
                "No branded daralkhaleej session and no LogoutUrl configured for this source.",
                "LogoutNotConfigured",
                null,
                null,
                false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logout test failed for source {SourceId}", newsSourceId);
            var (shot, html) = await CaptureFailureArtifactsAsync(source, null, page, "logout-test", cancellationToken).ConfigureAwait(false);
            return new NewsPortalLogoutTestResultDto(false, ex.Message, "UnexpectedError", shot, html, false);
        }
    }

    private async Task RunJobInternalAsync(Guid downloadJobId, CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs
            .Include(j => j.NewsSource)!.ThenInclude(s => s!.Credential)
            .FirstAsync(j => j.Id == downloadJobId, cancellationToken).ConfigureAwait(false);

        var source = job.NewsSource ?? throw new InvalidOperationException("Download job has no news source.");
        var strategy = strategyResolver.Resolve(source);
        var probe = await ValidateAndResolveCredentialsAsync(source, requireDownloadAllowed: true, isLoginProbe: false, cancellationToken).ConfigureAwait(false);
        if (!probe.Success)
        {
            if (strategy.StrategyKey == PortalStrategyKeys.PressReader && probe.FailureCode == "DownloadNotAllowed")
            {
                await AddAuditAsync(source, job.Id, PressReaderPortalAuditEvents.DownloadBlockedByCompliance, probe.Message, probe.FailureCode, cancellationToken).ConfigureAwait(false);
            }

            await FailJobAsync(job, source, probe.Message, probe.FailureCode, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await LaunchBrowserAsync(playwright).ConfigureAwait(false);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "MIP.Aws/1.0 (licensed-portal; authorized-subscriber-automation)",
            Locale = source.DefaultLanguage ?? "en-US",
            AcceptDownloads = true
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(120_000);

        var sw = Stopwatch.StartNew();
        var isPressReaderJob = strategy.StrategyKey == PortalStrategyKeys.PressReader;
        try
        {
            var isPressReader = isPressReaderJob;
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(
                source,
                job.Id,
                isPressReader ? PressReaderPortalAuditEvents.LoginStarted : PortalAuditEventKind.LoginAttempt,
                "Starting licensed portal download session.",
                null,
                cancellationToken).ConfigureAwait(false);

            var loginOutcome = await RunStrategyLoginAsync(strategy, page, source, job.Id, probe.Username!, probe.Password!, cancellationToken).ConfigureAwait(false);
            if (!loginOutcome.Success)
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, job.Id, page, "download", cancellationToken).ConfigureAwait(false);
                if (isPressReader)
                {
                    await AddAuditAsync(source, job.Id, PressReaderPortalAuditEvents.LoginFailed, loginOutcome.Message, loginOutcome.FailureCode, cancellationToken, shot, html).ConfigureAwait(false);
                }

                await HandleLoginFailureAsync(source, job, loginOutcome, shot, html, cancellationToken).ConfigureAwait(false);
                return;
            }

            await AddAuditAsync(
                source,
                job.Id,
                isPressReader ? PressReaderPortalAuditEvents.LoginCompleted : PortalAuditEventKind.LoginSuccess,
                "Login succeeded.",
                null,
                cancellationToken).ConfigureAwait(false);

            if (await PortalChallengeDetector.DetectCaptchaAsync(page).ConfigureAwait(false) || await PortalChallengeDetector.DetectMfaAsync(page).ConfigureAwait(false))
            {
                await CaptureFailureArtifactsAsync(source, job.Id, page, "download", cancellationToken).ConfigureAwait(false);
                await BlockAutomationAsync(source, job, "Interactive challenge detected after login.", cancellationToken).ConfigureAwait(false);
                return;
            }

            await AddAuditAsync(
                source,
                job.Id,
                isPressReader ? PressReaderPortalAuditEvents.DownloadStarted : PortalAuditEventKind.LoginAttempt,
                "Starting edition download.",
                null,
                cancellationToken).ConfigureAwait(false);

            var session = new PortalAutomationSession
            {
                Page = page,
                Source = source,
                DownloadJobId = job.Id,
                Username = probe.Username!,
                Password = probe.Password!
            };

            var downloadOutcome = await strategy.DownloadEditionAsync(session, cancellationToken).ConfigureAwait(false);
            if (!downloadOutcome.Success)
            {
                var (shot, html) = await CaptureFailureArtifactsAsync(source, job.Id, page, "download", cancellationToken).ConfigureAwait(false);
                await AddAuditAsync(
                    source,
                    job.Id,
                    isPressReader ? PressReaderPortalAuditEvents.DownloadFailed : PortalAuditEventKind.Failure,
                    downloadOutcome.Message,
                    downloadOutcome.FailureCode,
                    cancellationToken,
                    shot,
                    html).ConfigureAwait(false);

                if (downloadOutcome.FailureCode is "DownloadSelectorMissing" or "ContextMenuNotFound" or "DownloadMenuNotFound")
                {
                    await RaiseAlertAsync(source, SourceIngestionAlertType.DownloadControlMissing, downloadOutcome.Message, cancellationToken).ConfigureAwait(false);
                }

                await FailJobAsync(job, source, downloadOutcome.Message, downloadOutcome.FailureCode, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (isPressReader && downloadOutcome.FailureCode is null)
            {
                await AddAuditAsync(
                    source,
                    job.Id,
                    PressReaderPortalAuditEvents.DownloadCompleted,
                    $"Stored PressReader PDF at {downloadOutcome.StoredRelativePath}.",
                    null,
                    cancellationToken,
                    null,
                    downloadOutcome.StoredRelativePath).ConfigureAwait(false);
            }
            else
            {
                await AddAuditAsync(source, job.Id, PortalAuditEventKind.DownloadCompleted, $"Stored edition at {downloadOutcome.StoredRelativePath}.", null, cancellationToken).ConfigureAwait(false);
            }

            sw.Stop();
            job.Status = DownloadJobStatus.Succeeded;
            job.ErrorMessage = null;
            job.HttpStatusCode = 200;
            job.DurationMs = sw.ElapsedMilliseconds;
            source.LastDownloadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Portal download failed for job {JobId}", downloadJobId);
            await CaptureFailureArtifactsAsync(source, job.Id, page, "download", cancellationToken).ConfigureAwait(false);
            await FailJobAsync(job, source, ex.Message, "UnexpectedError", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (isPressReaderJob)
            {
                await TryLogoutAsync(page, source, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string GuessContentType(string ext) =>
        ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream";

    private static async Task<(bool Success, string Message, string? FailureCode)> RunStrategyLoginAsync(
        IPortalDownloadStrategy strategy,
        IPage page,
        NewsSource source,
        Guid? jobId,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var session = new PortalAutomationSession
        {
            Page = page,
            Source = source,
            DownloadJobId = jobId,
            Username = username,
            Password = password
        };

        var result = await strategy.LoginAsync(session, cancellationToken).ConfigureAwait(false);
        return (result.Success, result.Message, result.FailureCode);
    }

    private async Task<(bool Success, string Message, string? FailureCode, string? Username, string? Password)> ValidateAndResolveCredentialsAsync(
        NewsSource source,
        bool requireDownloadAllowed,
        bool isLoginProbe,
        CancellationToken cancellationToken)
    {
        if (source.SourceType != NewsSourceType.WebPortalLogin)
        {
            return (false, "Source is not configured as WebPortalLogin.", "WrongSourceType", null, null);
        }

        // ───── Manual-assisted MFA / OTP guard (compliance) ─────
        // If the source is declared as manual-assisted (RequiresMfa / RequiresOtp /
        // ManualLoginRequired / LoginMethod = ManualOtpAssisted | ManualBrowserSession),
        // unattended Test-Login and unattended Download are refused unconditionally. The
        // operator must run an assisted-login session via IAssistedPortalLoginService.
        if (source.RequiresMfa
            || source.RequiresOtp
            || source.ManualLoginRequired
            || source.LoginMethod is PortalLoginMethod.ManualOtpAssisted or PortalLoginMethod.ManualBrowserSession)
        {
            source.RequiresManualAction = true;
            await RaiseAlertAsync(source, SourceIngestionAlertType.OtpRequired,
                "Source requires manual-assisted login (MFA/OTP). Use Start-Assisted-Login workflow.",
                cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(source, null, "OtpRequired",
                "Unattended automation refused: source requires manual-assisted login.",
                "ManualAssistedRequired", cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (false,
                "This source requires manual-assisted login (MFA/OTP). Unattended automation is disabled by policy.",
                "ManualAssistedRequired", null, null);
        }

        if (source.AcquisitionMode is not (ContentAcquisitionMode.LicensedWebPortalSubscriber
            or ContentAcquisitionMode.LicensedFeedOrApi
            or ContentAcquisitionMode.PartnerManagedConnector))
        {
            return (false, "Acquisition mode must be a licensed channel (e.g. LicensedWebPortalSubscriber).", "InvalidAcquisitionMode", null, null);
        }

        if (requireDownloadAllowed && !source.IsDownloadAllowed)
        {
            return (false, "IsDownloadAllowed is false; enable only after confirming publisher terms allow automated download.", "DownloadNotAllowed", null, null);
        }

        if (source.RequiresCaptcha)
        {
            source.RequiresManualAction = true;
            await RaiseAlertAsync(source, SourceIngestionAlertType.CaptchaDetected, "Source is flagged as CAPTCHA-gated; automation disabled.", cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (false, "This source is marked as requiring CAPTCHA; automated login/download is disabled.", "RequiresCaptcha", null, null);
        }

        // Login probes may run even when a prior attempt set RequiresManualAction (e.g. old English selectors).
        // Scheduled downloads still require the flag to be cleared by a successful probe or admin edit.
        if (!isLoginProbe && source.RequiresManualAction)
        {
            return (false, "Source requires manual action (CAPTCHA/MFA or prior failure). Resolve alerts before retrying.", "RequiresManualAction", null, null);
        }

        var isPressReader = PressReaderPortalLogin.IsPressReaderSource(source)
            || string.Equals(PortalFieldMapper.NormalizeStrategyKey(source.PortalStrategyKey), PortalStrategyKeys.PressReader, StringComparison.Ordinal);
        if (!isPressReader && string.IsNullOrWhiteSpace(source.LoginUrl))
        {
            return (false, "LoginUrl is required.", "LoginUrlMissing", null, null);
        }

        if (isPressReader && string.IsNullOrWhiteSpace(source.EditionUrl) && string.IsNullOrWhiteSpace(source.LoginUrl))
        {
            return (false, "EditionUrl is required for PressReader sources.", "EditionUrlMissing", null, null);
        }

        if (!string.IsNullOrWhiteSpace(source.SuccessUrlPattern))
        {
            try
            {
                _ = new Regex(source.SuccessUrlPattern!, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            }
            catch
            {
                return (false, "SuccessUrlPattern is not a valid regular expression.", "InvalidSuccessUrlPattern", null, null);
            }
        }

        var pwdPayload = source.Credential?.ProtectedCredentialPayload;
        var cred = pwdPayload is null ? null : credentialProtector.Unprotect(pwdPayload);
        var password = cred?.Password;
        var username = !string.IsNullOrWhiteSpace(source.PortalUsername) ? source.PortalUsername.Trim() : cred?.Username;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            if (!string.IsNullOrWhiteSpace(pwdPayload))
            {
                return (false,
                    "Stored credentials cannot be decrypted. Re-enter the portal password in PressReader settings and save again.",
                    "CredentialsNeedReEntry",
                    null,
                    null);
            }

            return (false, "Portal username and encrypted password are required.", "MissingCredentials", null, null);
        }

        if (source.LoginMethod is not (PortalLoginMethod.FormCssSelectors or PortalLoginMethod.SuccessUrlPattern))
        {
            return (false, "Unsupported LoginMethod for automated portal login.", "UnsupportedLoginMethod", null, null);
        }

        if (!isPressReader &&
            (string.IsNullOrWhiteSpace(source.UsernameSelector) ||
             string.IsNullOrWhiteSpace(source.PasswordSelector) ||
             string.IsNullOrWhiteSpace(source.SubmitSelector)))
        {
            return (false, "UsernameSelector, PasswordSelector, and SubmitSelector are required.", "SelectorsMissing", null, null);
        }

        if (source.LoginMethod == PortalLoginMethod.SuccessUrlPattern && string.IsNullOrWhiteSpace(source.SuccessUrlPattern))
        {
            return (false, "SuccessUrlPattern is required when LoginMethod is SuccessUrlPattern.", "SuccessUrlPatternMissing", null, null);
        }

        return (true, string.Empty, null, username, password);
    }

    private async Task<(bool Success, string Message, string? FailureCode)> TryLoginAsync(
        IPage page,
        NewsSource source,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (PressReaderPortalLogin.IsPressReaderSource(source))
        {
            return await PressReaderPortalLogin.TryLoginAsync(page, source, username, password, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await page.GotoAsync(source.LoginUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, $"Could not open login page: {ex.Message}", "NavigationFailed");
        }

        if (await DetectCaptchaAsync(page).ConfigureAwait(false))
        {
            return (false, "CAPTCHA detected on the login page.", "CaptchaOnLoginPage");
        }

        if (await DetectMfaAsync(page).ConfigureAwait(false))
        {
            return (false, "MFA or OTP challenge detected on the login page.", "MfaOnLoginPage");
        }

        try
        {
            await page.FillAsync(source.UsernameSelector!, username).ConfigureAwait(false);
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            await page.FillAsync(source.PasswordSelector!, password).ConfigureAwait(false);
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            await page.ClickAsync(source.SubmitSelector!).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return (false, "Timeout while interacting with login form (selector mismatch or slow page).", "SelectorTimeout");
        }
        catch (Exception ex)
        {
            return (false, $"Login form interaction failed: {ex.Message}", "SelectorNotFound");
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 90_000 }).ConfigureAwait(false);
        }
        catch
        {
            // some SPAs never reach networkidle
        }

        if (await DetectCaptchaAsync(page).ConfigureAwait(false))
        {
            return (false, "CAPTCHA appeared after submitting credentials.", "CaptchaAfterLogin");
        }

        if (await DetectMfaAsync(page).ConfigureAwait(false))
        {
            return (false, "MFA challenge appeared after submitting credentials.", "MfaAfterLogin");
        }

        if (!string.IsNullOrWhiteSpace(source.LoginSuccessSelector))
        {
            try
            {
                await page.WaitForSelectorAsync(source.LoginSuccessSelector!, new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 60_000 }).ConfigureAwait(false);
            }
            catch
            {
                return (false, $"LoginSuccessSelector not found within timeout: {source.LoginSuccessSelector}", "LoginSuccessSelectorTimeout");
            }
        }

        if (!string.IsNullOrWhiteSpace(source.SuccessUrlPattern))
        {
            var re = new Regex(source.SuccessUrlPattern!, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            var matched = false;
            for (var i = 0; i < 40; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (re.IsMatch(page.Url))
                {
                    matched = true;
                    break;
                }

                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
            }

            if (!matched)
            {
                return (false, "SuccessUrlPattern did not match the current URL after login.", "SuccessUrlMismatch");
            }
        }

        return (true, string.Empty, null);
    }

    private async Task HandleLoginFailureAsync(
        NewsSource source,
        DownloadJob? job,
        (bool Success, string Message, string? FailureCode) loginOutcome,
        string? screenshotRel,
        string? htmlRel,
        CancellationToken cancellationToken)
    {
        await AddAuditAsync(
            source,
            job?.Id,
            PortalAuditEventKind.LoginFailure,
            loginOutcome.Message,
            loginOutcome.FailureCode,
            cancellationToken,
            screenshotRel,
            htmlRel).ConfigureAwait(false);

        if (loginOutcome.FailureCode is "CaptchaOnLoginPage" or "CaptchaAfterLogin" or "MfaOnLoginPage" or "MfaAfterLogin")
        {
            await BlockAutomationAsync(source, job, loginOutcome.Message, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Do not stick RequiresManualAction on login-test probes — operators must be able to retry after fixing selectors.
        if (job is not null)
        {
            if (loginOutcome.FailureCode is "SelectorNotFound" or "SelectorTimeout" or "LoginSuccessSelectorTimeout")
            {
                source.RequiresManualAction = true;
                await RaiseAlertAsync(source, SourceIngestionAlertType.SelectorFailure, loginOutcome.Message, cancellationToken).ConfigureAwait(false);
            }
            else if (loginOutcome.FailureCode is "SuccessUrlMismatch" or "InvalidSuccessUrlPattern")
            {
                source.RequiresManualAction = true;
                await RaiseAlertAsync(source, SourceIngestionAlertType.LayoutChanged, loginOutcome.Message, cancellationToken).ConfigureAwait(false);
            }
        }

        if (job is not null)
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = loginOutcome.Message;
            job.HttpStatusCode = 401;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteHtmlOnlyEditionAsync(
        NewsSource source,
        DownloadJob job,
        string htmlRel,
        byte[] htmlBytes,
        Stopwatch sw,
        CancellationToken cancellationToken)
    {
        var logicalKey = htmlRel.Replace(Path.DirectorySeparatorChar, '/');
        db.DownloadedFiles.Add(new DownloadedFile
        {
            Id = Guid.NewGuid(),
            DownloadJobId = job.Id,
            ContentType = "text/html",
            OriginalUrl = source.EditionUrl!,
            BlobUri = logicalKey,
            SizeBytes = htmlBytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(htmlBytes)),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await AddAuditAsync(
            source,
            job.Id,
            PortalAuditEventKind.DownloadCompleted,
            "No DownloadSelector configured — stored HTML edition snapshot only.",
            null,
            cancellationToken,
            null,
            logicalKey).ConfigureAwait(false);

        logger.LogInformation(
            "Portal job {JobId} for source {SourceId} succeeded with HTML snapshot only (PressReader-style viewer).",
            job.Id,
            source.Id);

        sw.Stop();
        job.Status = DownloadJobStatus.Succeeded;
        job.ErrorMessage = null;
        job.HttpStatusCode = 200;
        job.DurationMs = sw.ElapsedMilliseconds;
        source.LastDownloadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FailJobAsync(DownloadJob job, NewsSource source, string message, string? code, CancellationToken cancellationToken)
    {
        job.Status = DownloadJobStatus.Failed;
        job.ErrorMessage = message;
        await AddAuditAsync(source, job.Id, PortalAuditEventKind.Failure, message, code, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task BlockAutomationAsync(NewsSource source, DownloadJob? job, string message, CancellationToken cancellationToken)
    {
        source.RequiresManualAction = true;
        var type = message.Contains("MFA", StringComparison.OrdinalIgnoreCase) || message.Contains("OTP", StringComparison.OrdinalIgnoreCase)
            ? SourceIngestionAlertType.MfaDetected
            : SourceIngestionAlertType.CaptchaDetected;
        await RaiseAlertAsync(source, type, message, cancellationToken).ConfigureAwait(false);
        if (job is not null)
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = message;
            job.HttpStatusCode = 403;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task RaiseAlertAsync(NewsSource source, SourceIngestionAlertType type, string message, CancellationToken cancellationToken)
    {
        db.SourceIngestionAlerts.Add(new SourceIngestionAlert
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            AlertType = type,
            Message = message[..Math.Min(message.Length, 3990)],
            CreatedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    private async Task AddAuditAsync(
        NewsSource source,
        Guid? downloadJobId,
        string eventKind,
        string message,
        string? failureCode,
        CancellationToken cancellationToken,
        string? screenshotRelativePath = null,
        string? htmlSnapshotRelativePath = null)
    {
        db.PortalDownloadAuditLogs.Add(new PortalDownloadAuditLog
        {
            Id = Guid.NewGuid(),
            NewsSourceId = source.Id,
            DownloadJobId = downloadJobId,
            EventKind = eventKind,
            Message = message[..Math.Min(message.Length, 3990)],
            FailureCode = failureCode,
            ScreenshotRelativePath = screenshotRelativePath,
            HtmlSnapshotRelativePath = htmlSnapshotRelativePath,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string? ScreenshotRel, string? HtmlRel)> CaptureFailureArtifactsAsync(
        NewsSource source,
        Guid? jobId,
        IPage page,
        string prefix,
        CancellationToken cancellationToken)
    {
        var dir = BuildEditionRelativeDirectory(source.Name);
        string? shot = null;
        string? html = null;
        try
        {
            shot = $"{dir}/{prefix}-screenshot.png";
            var png = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true }).ConfigureAwait(false);
            await fileStorage.WriteAsync(shot, png, cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(source, jobId, PortalAuditEventKind.Screenshot, $"Saved {shot}", null, cancellationToken, shot, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Screenshot failed");
        }

        try
        {
            html = $"{dir}/{prefix}-page.html";
            var bytes = Encoding.UTF8.GetBytes(await page.ContentAsync().ConfigureAwait(false));
            await fileStorage.WriteAsync(html, bytes, cancellationToken).ConfigureAwait(false);
            await AddAuditAsync(source, jobId, PortalAuditEventKind.HtmlSnapshot, $"Saved {html}", null, cancellationToken, null, html).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HTML snapshot failed");
        }

        return (shot, html);
    }

    private async Task TryLogoutAsync(IPage page, NewsSource source, CancellationToken cancellationToken)
    {
        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            if (PressReaderPortalLogin.IsBrandedDarAlKhaleejSource(source))
            {
                var released = await PressReaderPortalLogin.TryReleaseDarAlKhaleejSessionAsync(page, source, cancellationToken).ConfigureAwait(false);
                await AddAuditAsync(
                    source,
                    null,
                    PortalAuditEventKind.Logout,
                    released
                        ? "daralkhaleej PressReader sign-out completed (subscriber menu → Sign out)."
                        : "daralkhaleej PressReader sign-out attempted but subscriber chip still visible or menu not found.",
                    released ? null : "LogoutIncomplete",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "PressReader branded logout skipped or failed.");
        }

        if (string.IsNullOrWhiteSpace(source.LogoutUrl))
        {
            return;
        }

        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            await page.GotoAsync(source.LogoutUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            await AddAuditAsync(source, null, PortalAuditEventKind.Logout, "Visited LogoutUrl.", null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Logout navigation skipped or failed.");
        }
    }

    private string BuildEditionRelativeDirectory(string sourceName)
    {
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var safe = SanitizeFolderName(sourceName);
        var root = _storage.NewspapersRelativePath.TrimEnd('/', '\\');
        return $"{root}/{safe}/{day}".Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "source" : s[..Math.Min(80, s.Length)];
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        var min = Math.Max(0, _compliance.PortalActionDelayMinMs);
        var max = Math.Max(min, _compliance.PortalActionDelayMaxMs);
        var ms = Random.Shared.Next(min, max + 1);
        await Task.Delay(ms, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> DetectCaptchaAsync(IPage page)
    {
        try
        {
            var locator = page.Locator("iframe[src*='recaptcha'],iframe[title*='reCAPTCHA'],.g-recaptcha,[class*='h-captcha'],[data-sitekey],#cf-turnstile");
            return await locator.CountAsync().ConfigureAwait(false) > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> DetectMfaAsync(IPage page)
    {
        try
        {
            var otp = page.Locator("input[autocomplete='one-time-code'],input[name*='otp' i],input[id*='otp' i]");
            if (await otp.CountAsync().ConfigureAwait(false) > 0)
            {
                return true;
            }

            var body = await page.InnerTextAsync("body").ConfigureAwait(false);
            if (body.Contains("two-factor", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("two step", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("verification code", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("authenticator", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright) =>
        playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

    private static class PortalAuditEventKind
    {
        public const string LoginAttempt = "LoginAttempt";
        public const string LoginSuccess = "LoginSuccess";
        public const string LoginFailure = "LoginFailure";
        public const string HtmlSnapshot = "HtmlSnapshot";
        public const string Screenshot = "Screenshot";
        public const string DownloadCompleted = "DownloadCompleted";
        public const string Failure = "Failure";
        public const string Logout = "Logout";
    }
}
