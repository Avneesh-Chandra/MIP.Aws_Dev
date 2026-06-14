using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Portal;
using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>
/// Licensed PressReader download: open page actions (⋮ menu or right-click under the spread) → Download.
/// </summary>
public sealed class PressReaderDownloadStrategy(
    IApplicationDbContext db,
    IFileStorageService fileStorage,
    IOptions<StorageOptions> storageOptions,
    ILogger<PressReaderDownloadStrategy> logger) : IPortalDownloadStrategy
{
    private static readonly string[] DefaultCanvasSelectors =
    [
        "[class*='issue-page']",
        "[class*='IssuePage']",
        "[class*='page-image']",
        "[class*='PageImage']",
        "#reader",
        "[class*='reader'] img",
        "canvas",
        "img[alt*='page' i]"
    ];

    /// <summary>PressReader overlay after right-click (daralkhaleej uses Arabic labels).</summary>
    private static readonly string[] PageActionsPanelIndicators =
    [
        "text=تنزيل",
        "text=عرض النص",
        "text=إشارة مرجعية",
        "text=مشاركة",
        "text=نسخ",
        "text=Text View",
        "text=Save to Collection",
        "text=Print",
        "text=Download",
        "button:has-text('Cancel')",
        "button:has-text('إلغاء')"
    ];

    private static readonly string[] DarAlKhaleejDownloadMenuSelectors =
    [
        "button:has-text('تنزيل')",
        "[role='button']:has-text('تنزيل')",
        ":text-is('تنزيل')",
        "a:has-text('تنزيل')"
    ];

    private static readonly string[] DefaultDownloadMenuSelectors =
    [
        "button:has-text('Download')",
        "[role='button']:has-text('Download')",
        "a:has-text('Download')",
        ":text-is('Download')"
    ];

    /// <summary>Second step labels (daralkhaleej Arabic UI + English).</summary>
    private static readonly string[] DownloadIssueAsPdfTextLabels =
    [
        "Download Issue as PDF",
        "تنزيل العدد بصيغة PDF",
        "تنزيل العدد كملف PDF",
        "تنزيل العدد كـ PDF",
        "تنزيل الإصدار بصيغة PDF",
        "تنزيل النسخة بصيغة PDF",
        "تنزيل عدد بصيغة PDF"
    ];

    private static readonly string[] DownloadPageAsPdfTextLabels =
    [
        "Download Page as PDF",
        "تنزيل الصفحة بصيغة PDF",
        "تنزيل الصفحة كملف PDF",
        "تنزيل الصفحة كـ PDF"
    ];

    /// <summary>Second step: Download modal after تنزيل (image2 — pick full issue, not single page).</summary>
    private static readonly string[] DownloadIssueAsPdfOptionSelectors =
    [
        "text=Download Issue as PDF",
        "text=تنزيل العدد بصيغة PDF",
        "text=تنزيل العدد كملف PDF",
        "button:has-text('Download Issue as PDF')",
        "button:has-text('تنزيل العدد')",
        "[role='button']:has-text('Download Issue as PDF')",
        "[role='menuitem']:has-text('Download Issue as PDF')",
        "[role='menuitem']:has-text('تنزيل العدد')",
        "li:has-text('Download Issue as PDF')",
        "li:has-text('تنزيل العدد')",
        "a:has-text('Download Issue as PDF')",
        "a:has-text('تنزيل العدد')"
    ];

    /// <summary>Third step: PDF Download terms dialog confirm button (image3).</summary>
    private static readonly string[] PdfDownloadConfirmSelectors =
    [
        "[role='dialog']:has-text('PDF Download') button:has-text('Download Issue as PDF')",
        "[role='dialog']:has-text('تنزيل PDF') button:has-text('تنزيل العدد')",
        "[role='dialog']:has-text('personal use') button:has-text('Download Issue as PDF')",
        "[role='dialog']:has-text('الاستخدام الشخصي') button:has-text('تنزيل العدد')",
        ".modal:has-text('PDF Download') button:has-text('Download Issue as PDF')",
        "button:has-text('Download Issue as PDF')",
        "button:has-text('تنزيل العدد بصيغة PDF')",
        "button:has-text('تنزيل العدد كملف PDF')"
    ];

    /// <summary>Vertical ⋮ control under the centered newspaper page (daralkhaleej carousel reader).</summary>
    private static readonly string[] DefaultPageActionsMenuSelectors =
    [
        "[class*='page-actions'] button",
        "[class*='PageActions'] button",
        "[class*='page-toolbar'] button[class*='more']",
        "[class*='toolbar'] button[class*='more']",
        "button[class*='ellipsis']",
        "button[class*='overflow']",
        "button[aria-label*='More' i]",
        "button[aria-label*='Actions' i]",
        "button[aria-label*='options' i]",
        "button[aria-label*='خيارات']",
        "button[aria-label*='المزيد']"
    ];

    public string StrategyKey => PortalStrategyKeys.PressReader;

    public bool CanHandle(NewsSource source) =>
        PressReaderPortalLogin.IsPressReaderSource(source)
        || string.Equals(
            PortalFieldMapper.NormalizeStrategyKey(source.PortalStrategyKey),
            PortalStrategyKeys.PressReader,
            StringComparison.Ordinal);

    public async Task<PortalLoginStepResult> LoginAsync(PortalAutomationSession session, CancellationToken cancellationToken)
    {
        var outcome = await PressReaderPortalLogin.TryLoginAsync(
            session.Page,
            session.Source,
            session.Username,
            session.Password,
            cancellationToken).ConfigureAwait(false);

        return new PortalLoginStepResult(outcome.Success, outcome.Message, outcome.FailureCode);
    }

    public async Task<PortalEditionDownloadStepResult> DownloadEditionAsync(
        PortalAutomationSession session,
        CancellationToken cancellationToken)
    {
        var source = session.Source;
        var page = session.Page;
        var editionUrl = !string.IsNullOrWhiteSpace(source.EditionUrl) ? source.EditionUrl.Trim() : source.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(editionUrl))
        {
            return new PortalEditionDownloadStepResult(false, "EditionUrl is required.", "EditionUrlMissing");
        }

        try
        {
            if (!page.Url.Contains(new Uri(editionUrl).Host, StringComparison.OrdinalIgnoreCase))
            {
                await page.GotoAsync(editionUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60_000 }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new PortalEditionDownloadStepResult(false, $"Could not open edition: {ex.Message}", "NavigationFailed");
        }

        await DismissLoginDialogIfPresentAsync(page).ConfigureAwait(false);

        if (await PressReaderPortalLogin.TryGetLoginBlockerAsync(page).ConfigureAwait(false) is { } blocker)
        {
            return new PortalEditionDownloadStepResult(false, blocker.Message, blocker.FailureCode);
        }

        if (!await PressReaderPortalLogin.IsBrandedDarAlKhaleejLoggedInAsync(page, source).ConfigureAwait(false))
        {
            var login = await PressReaderPortalLogin.TryLoginAsync(
                page, source, session.Username, session.Password, cancellationToken).ConfigureAwait(false);
            if (!login.Success)
            {
                return new PortalEditionDownloadStepResult(false, login.Message, login.FailureCode);
            }
        }

        if (await PortalChallengeDetector.DetectCaptchaAsync(page).ConfigureAwait(false)
            || await PortalChallengeDetector.DetectMfaAsync(page).ConfigureAwait(false))
        {
            return new PortalEditionDownloadStepResult(false, "CAPTCHA or MFA detected on edition page.", "CaptchaOrMfa");
        }

        if (!await PressReaderPortalLogin.IsBrandedDarAlKhaleejLoggedInAsync(page, source).ConfigureAwait(false))
        {
            if (await PressReaderPortalLogin.TryGetLoginBlockerAsync(page).ConfigureAwait(false) is { } postLoginBlocker)
            {
                return new PortalEditionDownloadStepResult(false, postLoginBlocker.Message, postLoginBlocker.FailureCode);
            }

            return new PortalEditionDownloadStepResult(
                false,
                "PressReader Sign in modal is still open; subscriber is not authenticated on the edition page.",
                "LoginRequired");
        }

        if (!string.IsNullOrWhiteSpace(source.DownloadSelector))
        {
            return await ClickDirectDownloadAsync(session, cancellationToken).ConfigureAwait(false);
        }

        return await OpenPageActionsAndDownloadAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DismissLoginDialogIfPresentAsync(IPage page)
    {
        try
        {
            var cancel = page.Locator("[role='dialog'] button:has-text('Cancel'), button:has-text('Cancel')").First;
            if (await cancel.CountAsync() > 0 && await cancel.IsVisibleAsync().ConfigureAwait(false))
            {
                await cancel.ClickAsync(new LocatorClickOptions { Timeout = 3_000 }).ConfigureAwait(false);
            }
        }
        catch
        {
            // optional
        }
    }

    private async Task<PortalEditionDownloadStepResult> ClickDirectDownloadAsync(
        PortalAutomationSession session,
        CancellationToken cancellationToken)
    {
        var page = session.Page;
        var source = session.Source;
        var timeoutMs = Math.Clamp(source.DownloadWaitTimeoutSeconds, 30, 600) * 1000;
        try
        {
            await page.ClickAsync(source.DownloadSelector!, new PageClickOptions { Timeout = 60_000 }).ConfigureAwait(false);
            var wizard = await CompletePressReaderIssuePdfWizardAsync(page, timeoutMs, cancellationToken).ConfigureAwait(false);
            if (!wizard.Success)
            {
                return new PortalEditionDownloadStepResult(false, wizard.Message!, wizard.FailureCode);
            }

            return await SavePressReaderPdfAsync(session, wizard.Download!, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new PortalEditionDownloadStepResult(false, "Timed out waiting for PressReader download.", "DownloadTimeout");
        }
    }

    private async Task<PortalEditionDownloadStepResult> OpenPageActionsAndDownloadAsync(
        PortalAutomationSession session,
        CancellationToken cancellationToken)
    {
        var page = session.Page;
        var source = session.Source;
        var timeoutMs = Math.Clamp(source.DownloadWaitTimeoutSeconds, 30, 600) * 1000;

        if (await PressReaderPortalLogin.TryGetLoginBlockerAsync(page).ConfigureAwait(false) is { } blocker)
        {
            return new PortalEditionDownloadStepResult(false, blocker.Message, blocker.FailureCode);
        }

        if (!await PressReaderPortalLogin.IsBrandedDarAlKhaleejLoggedInAsync(page, source).ConfigureAwait(false))
        {
            return new PortalEditionDownloadStepResult(
                false,
                "PressReader Sign in modal is still open; cannot open the page actions panel until login succeeds.",
                "LoginRequired");
        }

        if (!await EnsurePageActionsPanelReadyAsync(page, source, cancellationToken).ConfigureAwait(false))
        {
            return new PortalEditionDownloadStepResult(
                false,
                "Page actions panel (عرض النص / تنزيل) is not open on the edition reader. Click the newspaper spread to open the actions menu (see تنزيل with the download icon).",
                "ContextMenuNotFound");
        }

        try
        {
            if (!await RevealDownloadSubmenuAsync(page, source, cancellationToken).ConfigureAwait(false))
            {
                var downloadPanelVisible = await IsPageActionsPanelOpenAsync(page).ConfigureAwait(false);
                return new PortalEditionDownloadStepResult(
                    false,
                    downloadPanelVisible
                        ? "Download (تنزيل) row is visible in page actions but the PDF submenu did not open. Left-click تنزيل in the menu, then choose Download Issue as PDF."
                        : "Download (تنزيل) row not found in page actions. Click the newspaper spread to open the actions menu first.",
                    downloadPanelVisible ? "DownloadSubmenuNotFound" : "DownloadMenuNotFound");
            }

            var wizard = await CompletePressReaderIssuePdfWizardAsync(page, timeoutMs, cancellationToken).ConfigureAwait(false);
            if (!wizard.Success)
            {
                return new PortalEditionDownloadStepResult(false, wizard.Message!, wizard.FailureCode);
            }

            return await SavePressReaderPdfAsync(session, wizard.Download!, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new PortalEditionDownloadStepResult(
                false,
                "Download was clicked but no PDF file arrived (timeout). Complete the Download → Download Issue as PDF → confirm flow on the portal.",
                "DownloadTimeout");
        }
    }

    /// <summary>
    /// PressReader daralkhaleej: تنزيل → Download menu → Download Issue as PDF → PDF Download confirm → file.
    /// </summary>
    private static async Task<(bool Success, IDownload? Download, string? Message, string? FailureCode)> CompletePressReaderIssuePdfWizardAsync(
        IPage page,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        await Task.Delay(600, cancellationToken).ConfigureAwait(false);

        if (!await WaitForPressReaderDownloadSubmenuAsync(page, cancellationToken, 8_000).ConfigureAwait(false))
        {
            return (false, null, "Download submenu did not appear after تنزيل / Download (step 2).", "DownloadSubmenuNotFound");
        }

        if (!await ClickDownloadIssueAsPdfOptionAsync(page, cancellationToken).ConfigureAwait(false))
        {
            return (false, null, "Download Issue as PDF / تنزيل العدد بصيغة PDF was not found in the Download menu (step 2).", "DownloadIssueOptionNotFound");
        }

        await Task.Delay(600, cancellationToken).ConfigureAwait(false);

        if (!await WaitForPdfDownloadConfirmDialogAsync(page, cancellationToken).ConfigureAwait(false))
        {
            return (false, null, "PDF Download confirmation dialog did not appear (step 3).", "PdfConfirmDialogNotFound");
        }

        try
        {
            var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = timeoutMs });
            if (!await ClickPdfDownloadConfirmButtonAsync(page, cancellationToken).ConfigureAwait(false))
            {
                return (false, null, "Could not click confirm on the PDF Download dialog (Download Issue as PDF / تنزيل العدد).", "PdfConfirmButtonNotFound");
            }

            var download = await downloadTask.ConfigureAwait(false);
            return (true, download, null, null);
        }
        catch (TimeoutException)
        {
            return (
                false,
                null,
                "PDF Download confirm was clicked but no file arrived (timeout). Check subscription allows issue PDF download.",
                "DownloadTimeout");
        }
    }

    private static async Task<bool> WaitForPressReaderDownloadSubmenuAsync(
        IPage page,
        CancellationToken cancellationToken,
        int timeoutMs = 20_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsDownloadPdfSubmenuVisibleAsync(page).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(350, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// PressReader daralkhaleej: the PDF options flyout often opens on hover, not the first click.
    /// </summary>
    private static async Task<bool> RevealDownloadSubmenuAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await EnsurePageActionsPanelReadyAsync(page, source, cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(600, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (await TryRevealDownloadSubmenuOnceAsync(page, source, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await OpenPressReaderPageActionsPanelAsync(page, source, cancellationToken).ConfigureAwait(false);
            await Task.Delay(700, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> TryRevealDownloadSubmenuOnceAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        if (await IsDownloadPdfSubmenuVisibleAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        // daralkhaleej (image 1 → 2): left-click the تنزيل row (not just the label span).
        foreach (var useHover in new[] { true, false })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (useHover && await HoverDownloadRowInPageActionsPanelViaDomAsync(page).ConfigureAwait(false))
            {
                await Task.Delay(450, cancellationToken).ConfigureAwait(false);
            }

            if (await ActivateTanzilDownloadRowAsync(page, cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
                if (await IsDownloadPdfSubmenuVisibleAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }
        }

        if (await ClickTanzilTextInPageActionsMenuAsync(page, cancellationToken).ConfigureAwait(false))
        {
            await Task.Delay(900, cancellationToken).ConfigureAwait(false);
            if (await IsDownloadPdfSubmenuVisibleAsync(page).ConfigureAwait(false))
            {
                return true;
            }
        }

        if (await ClickDownloadRowInPageActionsPanelAsync(page).ConfigureAwait(false)
            || await ClickDownloadInPageActionsPanelViaDomAsync(page).ConfigureAwait(false))
        {
            await Task.Delay(900, cancellationToken).ConfigureAwait(false);
            if (await IsDownloadPdfSubmenuVisibleAsync(page).ConfigureAwait(false))
            {
                return true;
            }
        }

        var downloadRow = await FindDownloadButtonInActionsPanelAsync(page, source).ConfigureAwait(false);
        if (downloadRow is not null)
        {
            try
            {
                await downloadRow.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                await downloadRow.ClickAsync(new LocatorClickOptions { Timeout = 10_000, Force = true }).ConfigureAwait(false);
                await Task.Delay(900, cancellationToken).ConfigureAwait(false);
                if (await IsDownloadPdfSubmenuVisibleAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch
            {
                // fall through to shared wait
            }
        }

        if (await WaitForPressReaderDownloadSubmenuAsync(page, cancellationToken, 8_000).ConfigureAwait(false))
        {
            return true;
        }

        if (await ActivateTanzilDownloadRowAsync(page, cancellationToken).ConfigureAwait(false))
        {
            await Task.Delay(800, cancellationToken).ConfigureAwait(false);
        }

        return await WaitForPressReaderDownloadSubmenuAsync(page, cancellationToken, 10_000).ConfigureAwait(false);
    }

    /// <summary>
    /// Activates the main-menu تنزيل row (image 1 → image 2) using pointer events on the interactive row container.
    /// PressReader rows often use javascript:void(0) handlers that ignore Playwright text-span clicks.
    /// </summary>
    private static async Task<bool> ActivateTanzilDownloadRowAsync(IPage page, CancellationToken cancellationToken)
    {
        var coords = await page.EvaluateAsync<double[]?>(
            """
            () => {
              const panelMarkers = ['عرض النص', 'Text View', 'إلغاء', 'Cancel'];
              const submenuLabels = ['Download Issue as PDF', 'Download Page as PDF'];
              const vw = window.innerWidth;
              const vh = window.innerHeight;
              const isVisible = (el) => {
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) return false;
                const style = window.getComputedStyle(el);
                return style.visibility !== 'hidden' && style.display !== 'none' && parseFloat(style.opacity) >= 0.1;
              };
              const inMainMenu = (el) => {
                let node = el;
                for (let depth = 0; depth < 20 && node; depth++, node = node.parentElement) {
                  const text = node.innerText || '';
                  if (submenuLabels.some(l => text.includes(l))) return false;
                  if (panelMarkers.some(m => text.includes(m)) && text.includes('تنزيل')) return true;
                }
                return false;
              };
              const findRow = (labelEl) => {
                let node = labelEl;
                let best = labelEl;
                let bestArea = 0;
                for (let up = 0; up < 14 && node; up++, node = node.parentElement) {
                  const tag = (node.tagName || '').toLowerCase();
                  const role = node.getAttribute('role') || '';
                  const r = node.getBoundingClientRect();
                  if (r.height < 24 || r.width < 60) continue;
                  const area = r.width * r.height;
                  if ((tag === 'li' || role === 'menuitem' || tag === 'button' || tag === 'a' || role === 'button') && area > bestArea) {
                    best = node;
                    bestArea = area;
                  }
                }
                return best;
              };
              const activate = (row) => {
                const opts = { bubbles: true, cancelable: true, view: window };
                row.dispatchEvent(new PointerEvent('pointerdown', opts));
                row.dispatchEvent(new PointerEvent('pointerup', opts));
                row.dispatchEvent(new MouseEvent('mousedown', opts));
                row.dispatchEvent(new MouseEvent('mouseup', opts));
                row.dispatchEvent(new MouseEvent('click', opts));
                if (typeof row.click === 'function') row.click();
              };
              let best = null;
              let bestY = -1;
              for (const el of document.querySelectorAll('span, div, a, button, li, [role="menuitem"], label')) {
                const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                if (text !== 'تنزيل' && text !== 'Download') continue;
                if (!isVisible(el) || !inMainMenu(el)) continue;
                const r = el.getBoundingClientRect();
                const cx = r.x + r.width / 2;
                if (cx > vw * 0.72) continue;
                if (r.y > bestY) {
                  bestY = r.y;
                  best = el;
                }
              }
              if (!best) return null;
              const row = findRow(best);
              activate(row);
              const br = row.getBoundingClientRect();
              return [br.x + br.width / 2, br.y + br.height / 2];
            }
            """).ConfigureAwait(false);

        if (coords is { Length: >= 2 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var x = (float)coords[0];
            var y = (float)coords[1];
            await page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            await page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
            return true;
        }

        return await ClickDownloadInPageActionsPanelViaDomAsync(page).ConfigureAwait(false);
    }

    /// <summary>
    /// Left-click the تنزيل label in the main page-actions list (image 1 → image 2 submenu).
    /// </summary>
    private static async Task<bool> ClickTanzilTextInPageActionsMenuAsync(IPage page, CancellationToken cancellationToken)
    {
        if (await ClickTanzilTextInPageActionsMenuViaDomAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        foreach (var marker in PageActionsPanelMarkers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var panels = page.Locator("div, section, article, ul, [role='menu']")
                    .Filter(new LocatorFilterOptions { HasText = marker });
                var panelCount = await panels.CountAsync().ConfigureAwait(false);
                for (var p = panelCount - 1; p >= 0; p--)
                {
                    var panel = panels.Nth(p);
                    var tanzilLabels = panel.GetByText("تنزيل", new LocatorGetByTextOptions { Exact = true });
                    var labelCount = await tanzilLabels.CountAsync().ConfigureAwait(false);
                    for (var i = 0; i < labelCount; i++)
                    {
                        var label = tanzilLabels.Nth(i);
                        if (!await label.IsVisibleAsync().ConfigureAwait(false))
                        {
                            continue;
                        }

                        if (!await IsMainMenuTanzilLabelAsync(label).ConfigureAwait(false))
                        {
                            continue;
                        }

                        try
                        {
                            await label.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                            return true;
                        }
                        catch
                        {
                            var row = label.Locator("xpath=ancestor::li[1] | ancestor::*[@role='menuitem'][1] | ancestor::button[1] | ancestor::a[1]");
                            if (await row.CountAsync().ConfigureAwait(false) > 0)
                            {
                                await row.First.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // try next marker
            }
        }

        return false;
    }

    private static Task<bool> IsMainMenuTanzilLabelAsync(ILocator label) =>
        label.EvaluateAsync<bool>(
            """
            (el) => {
              const submenuLabels = ['Download Issue as PDF', 'Download Page as PDF'];
              let node = el;
              for (let depth = 0; depth < 18 && node; depth++, node = node.parentElement) {
                const text = node.innerText || '';
                if (submenuLabels.some(l => text.includes(l))) return false;
                if (text.includes('عرض النص') && text.includes('تنزيل')) return true;
              }
              return false;
            }
            """);

    private static Task<bool> ClickTanzilTextInPageActionsMenuViaDomAsync(IPage page) =>
        page.EvaluateAsync<bool>(
            """
            () => {
              const panelMarkers = ['عرض النص', 'Text View'];
              const submenuLabels = ['Download Issue as PDF', 'Download Page as PDF'];
              const vw = window.innerWidth;
              const isVisible = (el) => {
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) return false;
                const cx = r.x + r.width / 2;
                if (cx > vw * 0.82) return false;
                const style = window.getComputedStyle(el);
                return style.visibility !== 'hidden' && style.display !== 'none' && parseFloat(style.opacity) >= 0.1;
              };
              const inMainMenu = (el) => {
                let node = el;
                for (let depth = 0; depth < 18 && node; depth++, node = node.parentElement) {
                  const text = node.innerText || '';
                  if (submenuLabels.some(l => text.includes(l))) return false;
                  if (panelMarkers.some(m => text.includes(m)) && text.includes('تنزيل')) return true;
                }
                return false;
              };
              const clickRow = (el) => {
                let best = el;
                let bestArea = 0;
                let node = el;
                for (let up = 0; up < 12 && node; up++, node = node.parentElement) {
                  const tag = (node.tagName || '').toLowerCase();
                  const role = node.getAttribute('role') || '';
                  const r = node.getBoundingClientRect();
                  if (r.height < 24 || r.width < 60) continue;
                  const area = r.width * r.height;
                  if ((tag === 'li' || role === 'menuitem' || tag === 'button' || tag === 'a' || role === 'button') && area > bestArea) {
                    best = node;
                    bestArea = area;
                  }
                }
                const opts = { bubbles: true, cancelable: true, view: window };
                best.dispatchEvent(new PointerEvent('pointerdown', opts));
                best.dispatchEvent(new PointerEvent('pointerup', opts));
                best.dispatchEvent(new MouseEvent('click', opts));
                best.click();
                return true;
              };
              let best = null;
              let bestY = -1;
              for (const el of document.querySelectorAll('span, div, a, button, li, [role="menuitem"], label')) {
                const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                if (text !== 'تنزيل') continue;
                if (!isVisible(el) || !inMainMenu(el)) continue;
                const r = el.getBoundingClientRect();
                if (r.y > bestY) {
                  bestY = r.y;
                  best = el;
                }
              }
              if (!best) return false;
              return clickRow(best);
            }
            """);

    private static async Task<bool> EnsurePageActionsPanelReadyAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
            {
                return true;
            }

            await DismissLoginDialogIfPresentAsync(page).ConfigureAwait(false);
            await EnsureEditionReaderViewAsync(page, cancellationToken).ConfigureAwait(false);
            await Task.Delay(400, cancellationToken).ConfigureAwait(false);

            // Manual daralkhaleej flow: right-click the spread opens the page-actions menu (image 1).
            if (await RightClickNewspaperSpreadAsync(page, source, cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }

            if (await ClickNewspaperSpreadAsync(page, source, cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }

            if (await ClickThreeDotPageActionsMenuAsync(page, cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }

            await OpenPressReaderPageActionsPanelAsync(page, source, cancellationToken).ConfigureAwait(false);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            await WaitForPressReaderPageActionsPanelAsync(page, cancellationToken, 8_000).ConfigureAwait(false);
        }

        return await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false);
    }

    /// <summary>Both عرض النص (or sibling action) and تنزيل are visible in the reader overlay (daralkhaleej docks the menu on the left).</summary>
    private static Task<bool> IsPageActionsPanelReadyAsync(IPage page) =>
        IsPageActionsPanelVisibleInDomAsync(page);

    private static readonly string[] PageActionsPanelMarkers =
    [
        "عرض النص",
        "Text View",
        "إشارة مرجعية",
        "Save to Collection",
        "مشاركة",
        "نسخ"
    ];

    private static readonly Regex DownloadRowLabelRegex = new(@"^(تنزيل|Download)(\s|[>›»]|$)", RegexOptions.Compiled);

    /// <summary>
    /// daralkhaleej Arabic UI: nested submenu after تنزيل uses English PDF row labels (per screenshots).
    /// </summary>
    private static readonly string[] DownloadSubmenuVisibleLabels =
    [
        "Download Issue as PDF",
        "Download Page as PDF",
        "تنزيل العدد بصيغة PDF",
        "تنزيل العدد كملف PDF",
        "تنزيل الصفحة بصيغة PDF",
        "تنزيل العدد",
        "Download Issue"
    ];

    /// <summary>Nested Download menu is open when Page or Issue PDF rows are visible (EN or AR).</summary>
    private static async Task<bool> IsDownloadPdfSubmenuVisibleAsync(IPage page)
    {
        foreach (var label in DownloadSubmenuVisibleLabels)
        {
            try
            {
                var exact = page.GetByText(label, new PageGetByTextOptions { Exact = true });
                if (await exact.CountAsync().ConfigureAwait(false) > 0
                    && await exact.First.IsVisibleAsync().ConfigureAwait(false))
                {
                    return true;
                }

                var partial = page.GetByText(label, new PageGetByTextOptions { Exact = false });
                if (await partial.CountAsync().ConfigureAwait(false) > 0
                    && await partial.First.IsVisibleAsync().ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch
            {
                // try next
            }
        }

        return await page.EvaluateAsync<bool>(
            """
            () => {
              const body = document.body?.innerText ?? '';
              if (/download\s+issue\s+as\s+pdf/i.test(body) || /download\s+page\s+as\s+pdf/i.test(body)) return true;
              if (/تنزيل\s+العدد/i.test(body) && /pdf/i.test(body)) return true;
              const nodes = document.querySelectorAll('button, a, li, [role="button"], [role="menuitem"], div, span');
              let issueVisible = false;
              let pageVisible = false;
              for (const el of nodes) {
                const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                if (!text || text.length > 120) continue;
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) continue;
                const style = window.getComputedStyle(el);
                if (style.visibility === 'hidden' || style.display === 'none' || parseFloat(style.opacity) < 0.1) continue;
                if (/download\s+issue\s+as\s+pdf/i.test(text)) issueVisible = true;
                if (/download\s+page\s+as\s+pdf/i.test(text)) pageVisible = true;
                if (/تنزيل/i.test(text) && /العدد|الإصدار|النسخة/i.test(text) && /pdf/i.test(text)) issueVisible = true;
              }
              if (issueVisible || pageVisible) return true;
              // Flyout header (image 2): centered panel titled تنزيل with PDF rows beneath.
              for (const panel of document.querySelectorAll('div, section, [role="dialog"]')) {
                const text = panel.innerText || '';
                if (!text.includes('تنزيل')) continue;
                if (!/download\s+issue\s+as\s+pdf/i.test(text) && !/download\s+page\s+as\s+pdf/i.test(text)) continue;
                const r = panel.getBoundingClientRect();
                if (r.width < 120 || r.height < 80) continue;
                const cx = r.x + r.width / 2;
                if (cx > window.innerWidth * 0.15 && cx < window.innerWidth * 0.85) return true;
              }
              return false;
            }
            """);
    }

    /// <summary>Page-actions overlay is open (عرض النص + تنزيل in center, not sidebar promos).</summary>
    private static Task<bool> IsPageActionsPanelOpenAsync(IPage page) =>
        IsPageActionsPanelReadyAsync(page);

    /// <summary>
    /// Clicks the Download/تنزيل row in the page-actions panel (opens nested PDF menu — image1 → image2).
    /// </summary>
    private static async Task<bool> ClickDownloadRowInPageActionsPanelAsync(IPage page)
    {
        foreach (var marker in PageActionsPanelMarkers)
        {
            try
            {
                var panels = page.Locator("div, section, article, [role='dialog']").Filter(new LocatorFilterOptions { HasText = marker });
                var panelCount = await panels.CountAsync().ConfigureAwait(false);
                for (var p = panelCount - 1; p >= 0; p--)
                {
                    var panel = panels.Nth(p);
                    var downloadRows = panel.GetByText("تنزيل", new LocatorGetByTextOptions { Exact = true });
                    var rowCount = await downloadRows.CountAsync().ConfigureAwait(false);
                    if (rowCount == 0)
                    {
                        downloadRows = panel.GetByText("Download", new LocatorGetByTextOptions { Exact = true });
                        rowCount = await downloadRows.CountAsync().ConfigureAwait(false);
                    }

                    if (rowCount == 0)
                    {
                        downloadRows = panel.Locator("[role='menuitem'], li, button, a").Filter(new LocatorFilterOptions
                        {
                            HasTextRegex = new Regex("^(تنزيل|Download)\\b")
                        });
                        rowCount = await downloadRows.CountAsync().ConfigureAwait(false);
                    }

                    if (rowCount > 0)
                    {
                        await downloadRows.Last.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch
            {
                // try next marker
            }
        }

        return await page.EvaluateAsync<bool>(
            """
            () => {
              const panelMarkers = ['عرض النص', 'Text View', 'إشارة مرجعية', 'مشاركة', 'نسخ'];
              const nodes = document.querySelectorAll('button, a, li, [role="button"], [role="menuitem"], div, span');
              for (const el of nodes) {
                const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                if (text !== 'تنزيل' && text !== 'Download') continue;
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) continue;
                if (r.x + r.width / 2 > window.innerWidth * 0.78) continue;
                const style = window.getComputedStyle(el);
                if (style.visibility === 'hidden' || style.display === 'none' || parseFloat(style.opacity) < 0.1) continue;
                let parent = el;
                for (let depth = 0; depth < 14 && parent; depth++, parent = parent.parentElement) {
                  const pt = (parent.innerText || '');
                  if (!panelMarkers.some(m => pt.includes(m))) continue;
                  let clickTarget = el;
                  for (let up = 0; up < 6 && clickTarget; up++) {
                    const tag = (clickTarget.tagName || '').toLowerCase();
                    const role = clickTarget.getAttribute('role') || '';
                    if (tag === 'button' || tag === 'a' || role === 'button' || role === 'menuitem' || tag === 'li') {
                      clickTarget.click();
                      return true;
                    }
                    clickTarget = clickTarget.parentElement;
                  }
                  el.click();
                  return true;
                }
              }
              return false;
            }
            """);
    }

    private static async Task EnsureEditionReaderViewAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 30_000 }).ConfigureAwait(false);
        }
        catch
        {
            // continue
        }

        await Task.Delay(1_500, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> ClickDownloadIssueAsPdfOptionAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var issueRow = page.GetByText("Download Issue as PDF", new PageGetByTextOptions { Exact = true });
            if (await issueRow.CountAsync().ConfigureAwait(false) > 0 && await issueRow.First.IsVisibleAsync().ConfigureAwait(false))
            {
                await issueRow.First.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                return true;
            }
        }
        catch
        {
            // try other labels
        }

        if (await ClickDownloadIssueAsPdfOptionViaDomAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        foreach (var label in DownloadIssueAsPdfTextLabels)
        {
            try
            {
                var byText = page.GetByText(label, new PageGetByTextOptions { Exact = true });
                var count = await byText.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < count; i++)
                {
                    var item = byText.Nth(i);
                    if (await item.IsVisibleAsync().ConfigureAwait(false))
                    {
                        await item.ClickAsync(new LocatorClickOptions { Timeout = 8_000 }).ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch
            {
                // try next label
            }
        }

        return await WaitAndClickIssuePdfSelectorsAsync(page, DownloadIssueAsPdfOptionSelectors, 8_000, cancellationToken).ConfigureAwait(false);
    }

    private static Task<bool> ClickDownloadIssueAsPdfOptionViaDomAsync(IPage page) =>
        page.EvaluateAsync<bool>(
            """
            () => {
              const nodes = document.querySelectorAll('button, a, li, [role="button"], [role="menuitem"], div, span');
              for (const el of nodes) {
                const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                if (!text || text.length > 120) continue;
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) continue;
                const style = window.getComputedStyle(el);
                if (style.visibility === 'hidden' || style.display === 'none' || parseFloat(style.opacity) < 0.1) continue;
                if (!isIssuePdfLabel(text)) continue;
                let best = el;
                let bestArea = 0;
                let node = el;
                for (let up = 0; up < 10 && node; up++, node = node.parentElement) {
                  const tag = (node.tagName || '').toLowerCase();
                  const role = node.getAttribute('role') || '';
                  const br = node.getBoundingClientRect();
                  if (br.height < 20) continue;
                  const area = br.width * br.height;
                  if ((tag === 'li' || role === 'menuitem' || tag === 'button' || tag === 'a') && area > bestArea) {
                    best = node;
                    bestArea = area;
                  }
                }
                const opts = { bubbles: true, cancelable: true, view: window };
                best.dispatchEvent(new PointerEvent('pointerdown', opts));
                best.dispatchEvent(new PointerEvent('pointerup', opts));
                best.dispatchEvent(new MouseEvent('click', opts));
                best.click();
                return true;
              }
              return false;
              function isIssuePdfLabel(t) {
                if (/download\s+page\s+as\s+pdf/i.test(t)) return false;
                if (/تنزيل\s+الصفحة/i.test(t) && /pdf/i.test(t)) return false;
                if (/download\s+issue\s+as\s+pdf/i.test(t)) return true;
                if (/تنزيل/i.test(t) && /العدد|الإصدار|النسخة|عدد/i.test(t) && /pdf/i.test(t)) return true;
                return false;
              }
            }
            """);

    private static async Task<bool> WaitForPdfDownloadConfirmDialogAsync(IPage page, CancellationToken cancellationToken)
    {
        var dialog = page.Locator(
            "[role='dialog']:has-text('PDF Download'), [role='dialog']:has-text('تنزيل PDF'), " +
            "[role='dialog']:has-text('personal use'), [role='dialog']:has-text('الاستخدام الشخصي'), " +
            "[role='dialog']:has-text('حقوق النشر'), .modal:has-text('PDF Download'), .modal:has-text('تنزيل PDF')");
        try
        {
            await dialog.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 20_000
            }).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return await page.EvaluateAsync<bool>(
                """
                () => {
                  const body = document.body?.innerText ?? '';
                  return body.includes('personal use') || body.includes('الاستخدام الشخصي')
                    || body.includes('PDF Download') || body.includes('تنزيل PDF');
                }
                """);
        }
    }

    private static async Task<bool> ClickPdfDownloadConfirmButtonAsync(IPage page, CancellationToken cancellationToken)
    {
        if (await ClickPdfDownloadConfirmViaDomAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        var dialog = page.Locator(
            "[role='dialog']:has-text('PDF Download'), [role='dialog']:has-text('تنزيل PDF'), " +
            "[role='dialog']:has-text('personal use'), [role='dialog']:has-text('الاستخدام الشخصي')");
        if (await dialog.CountAsync().ConfigureAwait(false) > 0)
        {
            foreach (var label in DownloadIssueAsPdfTextLabels)
            {
                try
                {
                    var inDialog = dialog.First.Locator($"button:has-text('{label}')");
                    if (await inDialog.CountAsync().ConfigureAwait(false) > 0)
                    {
                        var btn = inDialog.Last;
                        if (await btn.IsVisibleAsync().ConfigureAwait(false))
                        {
                            await btn.ClickAsync(new LocatorClickOptions { Timeout = 15_000 }).ConfigureAwait(false);
                            return true;
                        }
                    }
                }
                catch
                {
                    // try next label
                }
            }
        }

        return await WaitAndClickIssuePdfSelectorsAsync(page, PdfDownloadConfirmSelectors, 10_000, cancellationToken).ConfigureAwait(false);
    }

    private static Task<bool> ClickPdfDownloadConfirmViaDomAsync(IPage page) =>
        page.EvaluateAsync<bool>(
            """
            () => {
              const dialogs = document.querySelectorAll("[role='dialog'], .modal");
              for (const dialog of dialogs) {
                const style = window.getComputedStyle(dialog);
                if (style.display === 'none' || style.visibility === 'hidden') continue;
                const dt = (dialog.innerText || '');
                if (!/PDF Download|تنزيل PDF|personal use|الاستخدام الشخصي|حقوق النشر/i.test(dt)) continue;
                const buttons = dialog.querySelectorAll('button, a, [role="button"]');
                for (const el of buttons) {
                  const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                  if (!text) continue;
                  if (/download\s+page\s+as\s+pdf/i.test(text)) continue;
                  if (/تنزيل\s+الصفحة/i.test(text) && /pdf/i.test(text)) continue;
                  if (/download\s+issue\s+as\s+pdf/i.test(text)
                      || (/تنزيل/i.test(text) && /العدد|الإصدار|النسخة|عدد/i.test(text) && /pdf/i.test(text))) {
                    el.click();
                    return true;
                  }
                }
              }
              return false;
            }
            """);

    private static async Task<bool> WaitAndClickIssuePdfSelectorsAsync(
        IPage page,
        IReadOnlyList<string> selectors,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var selector in selectors)
            {
                try
                {
                    var locator = page.Locator(selector);
                    var count = await locator.CountAsync().ConfigureAwait(false);
                    for (var i = 0; i < count; i++)
                    {
                        var item = locator.Nth(i);
                        if (!await item.IsVisibleAsync().ConfigureAwait(false))
                        {
                            continue;
                        }

                        var text = (await item.InnerTextAsync().ConfigureAwait(false) ?? string.Empty).Trim();
                        if (IsDownloadPagePdfLabel(text))
                        {
                            continue;
                        }

                        if (!IsDownloadIssuePdfLabel(text))
                        {
                            continue;
                        }

                        await item.ClickAsync(new LocatorClickOptions { Timeout = 5_000 }).ConfigureAwait(false);
                        return true;
                    }
                }
                catch
                {
                    // try next selector
                }
            }

            await Task.Delay(350, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static bool IsDownloadPagePdfLabel(string text)
    {
        foreach (var label in DownloadPageAsPdfTextLabels)
        {
            if (text.Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return text.Contains("تنزيل الصفحة", StringComparison.Ordinal) && text.Contains("PDF", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDownloadIssuePdfLabel(string text)
    {
        if (IsDownloadPagePdfLabel(text))
        {
            return false;
        }

        foreach (var label in DownloadIssueAsPdfTextLabels)
        {
            if (text.Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return text.Contains("تنزيل", StringComparison.Ordinal)
               && (text.Contains("العدد", StringComparison.Ordinal)
                   || text.Contains("الإصدار", StringComparison.Ordinal)
                   || text.Contains("النسخة", StringComparison.Ordinal))
               && text.Contains("PDF", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> OpenPressReaderPageActionsPanelAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        if (await RightClickNewspaperSpreadAsync(page, source, cancellationToken).ConfigureAwait(false)
            && await WaitForPressReaderPageActionsPanelAsync(page, cancellationToken, 10_000).ConfigureAwait(false))
        {
            return true;
        }

        if (await ClickNewspaperSpreadAsync(page, source, cancellationToken).ConfigureAwait(false)
            && await WaitForPressReaderPageActionsPanelAsync(page, cancellationToken, 10_000).ConfigureAwait(false))
        {
            return true;
        }

        if (await ClickThreeDotPageActionsMenuAsync(page, cancellationToken).ConfigureAwait(false))
        {
            await Task.Delay(600, cancellationToken).ConfigureAwait(false);
            if (await WaitForPressReaderPageActionsPanelAsync(page, cancellationToken, 8_000).ConfigureAwait(false))
            {
                return true;
            }
        }

        foreach (var selector in BuildSelectorList(source.ContextMenuSelector, DefaultPageActionsMenuSelectors))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var locator = page.Locator(selector);
                var count = await locator.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < count; i++)
                {
                    var item = locator.Nth(i);
                    if (!await item.IsVisibleAsync().ConfigureAwait(false))
                    {
                        continue;
                    }

                    if (!await IsLikelyPageActionsMenuTriggerAsync(page, item).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await item.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                    if (await WaitForPressReaderPageActionsPanelAsync(page, cancellationToken, 8_000).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // try next selector
            }
        }

        return false;
    }

    /// <summary>
    /// Left-click the newspaper spread — daralkhaleej opens the page-actions menu (image 1) on the left edge.
    /// </summary>
    private static async Task<bool> ClickNewspaperSpreadAsync(IPage page, NewsSource source, CancellationToken cancellationToken)
    {
        foreach (var selector in BuildSelectorList(source.NewspaperCanvasSelector, DefaultCanvasSelectors))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var locator = page.Locator(selector);
                var count = await locator.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < count; i++)
                {
                    var item = locator.Nth(i);
                    if (!await item.IsVisibleAsync().ConfigureAwait(false))
                    {
                        continue;
                    }

                    await item.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                    await Task.Delay(700, cancellationToken).ConfigureAwait(false);
                    if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // try next selector
            }
        }

        var viewport = page.ViewportSize;
        if (viewport is null)
        {
            return false;
        }

        // Click on the visible newspaper pages (center-left), not the far-right app promo sidebar.
        var points = new (double X, double Y)[]
        {
            (0.42, 0.48),
            (0.45, 0.52),
            (0.38, 0.50),
            (0.50, 0.50),
            (0.35, 0.45),
            (0.48, 0.42),
            (0.40, 0.55)
        };

        foreach (var (rx, ry) in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var x = (float)(viewport.Width * rx);
                var y = (float)(viewport.Height * ry);
                await page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
                await Task.Delay(700, cancellationToken).ConfigureAwait(false);
                if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false)
                    || await IsPressReaderPageActionsPanelVisibleAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch
            {
                // try next point
            }
        }

        return false;
    }

    /// <summary>
    /// Right-click on the newspaper page image (fallback when left-click does not open actions).
    /// </summary>
    private static async Task<bool> RightClickNewspaperSpreadAsync(IPage page, NewsSource source, CancellationToken cancellationToken)
    {
        var viewport = page.ViewportSize;
        if (viewport is null)
        {
            return false;
        }

        // Center spread: on the main page image (not the right sidebar promos).
        var points = new (double X, double Y)[]
        {
            (0.50, 0.55),
            (0.50, 0.50),
            (0.48, 0.58),
            (0.52, 0.52),
            (0.45, 0.55),
            (0.55, 0.55),
            (0.50, 0.62),
            (0.50, 0.45)
        };

        foreach (var (rx, ry) in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var x = (int)(viewport.Width * rx);
                var y = (int)(viewport.Height * ry);
                await page.Mouse.ClickAsync(x, y, new MouseClickOptions { Button = MouseButton.Right }).ConfigureAwait(false);
                await Task.Delay(700, cancellationToken).ConfigureAwait(false);
                if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false)
                    || await IsPressReaderPageActionsPanelVisibleAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch
            {
                // try next point
            }
        }

        return false;
    }

    /// <summary>
    /// Clicks the ⋮ control centered below the active newspaper page (daralkhaleej carousel).
    /// </summary>
    private static async Task<bool> ClickThreeDotPageActionsMenuAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var clicked = await page.EvaluateAsync<bool>(
                """
                () => {
                  const vw = window.innerWidth;
                  const vh = window.innerHeight;
                  const cx = vw * 0.5;
                  const targetY = vh * 0.72;
                  const nodes = document.querySelectorAll('button, [role="button"]');
                  let best = null;
                  let bestDist = 1e12;
                  for (const el of nodes) {
                    const r = el.getBoundingClientRect();
                    if (r.width < 6 || r.height < 6 || r.width > 72 || r.height > 72) continue;
                    const mx = r.x + r.width / 2;
                    const my = r.y + r.height / 2;
                    if (Math.abs(mx - cx) > vw * 0.12) continue;
                    if (my < vh * 0.48 || my > vh * 0.80) continue;
                    const style = window.getComputedStyle(el);
                    if (style.visibility === 'hidden' || style.display === 'none' || style.opacity === '0') continue;
                    const dist = (mx - cx) ** 2 + (my - targetY) ** 2;
                    if (dist < bestDist) {
                      bestDist = dist;
                      best = el;
                    }
                  }
                  if (!best) return false;
                  best.click();
                  return true;
                }
                """).ConfigureAwait(false);
            if (clicked)
            {
                await Task.Delay(600, cancellationToken).ConfigureAwait(false);
                return await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false)
                       || await IsPressReaderPageActionsPanelVisibleAsync(page).ConfigureAwait(false);
            }
        }
        catch
        {
            // fall through to coordinate click
        }

        var viewport = page.ViewportSize;
        if (viewport is null)
        {
            return false;
        }

        var fallbackPoints = new (double X, double Y)[]
        {
            (0.50, 0.68),
            (0.50, 0.72),
            (0.50, 0.64),
            (0.50, 0.76)
        };

        foreach (var (rx, ry) in fallbackPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var x = (int)(viewport.Width * rx);
                var y = (int)(viewport.Height * ry);
                await page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
                await Task.Delay(700, cancellationToken).ConfigureAwait(false);
                if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false)
                    || await IsPressReaderPageActionsPanelVisibleAsync(page).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch
            {
                // try next point
            }
        }

        return false;
    }

    /// <summary>Ignore header chrome and far-right promos; keep toolbar ⋮ under the spread.</summary>
    private static async Task<bool> IsLikelyPageActionsMenuTriggerAsync(IPage page, ILocator trigger)
    {
        try
        {
            var box = await trigger.BoundingBoxAsync().ConfigureAwait(false);
            var viewport = page.ViewportSize;
            if (box is null || viewport is null)
            {
                return true;
            }

            var centerX = box.X + (box.Width / 2);
            var centerY = box.Y + (box.Height / 2);
            return centerX > viewport.Width * 0.22
                   && centerX < viewport.Width * 0.78
                   && centerY > viewport.Height * 0.40
                   && centerY < viewport.Height * 0.82;
        }
        catch
        {
            return true;
        }
    }

    private static async Task<bool> WaitForPressReaderPageActionsPanelAsync(
        IPage page,
        CancellationToken cancellationToken,
        int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false))
            {
                return true;
            }

            if (await IsPressReaderPageActionsPanelVisibleAsync(page).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(350, cancellationToken).ConfigureAwait(false);
        }

        return await IsPageActionsPanelReadyAsync(page).ConfigureAwait(false);
    }

    private static async Task<bool> IsPressReaderPageActionsPanelVisibleAsync(IPage page)
    {
        if (await IsPageActionsPanelVisibleInDomAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        foreach (var label in PageActionsPanelTextLabels)
        {
            try
            {
                var byText = page.GetByText(label, new PageGetByTextOptions { Exact = true });
                var count = await byText.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < count; i++)
                {
                    if (await byText.Nth(i).IsVisibleAsync().ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // try next label
            }
        }

        foreach (var indicator in PageActionsPanelIndicators)
        {
            try
            {
                var loc = page.Locator(indicator);
                var count = await loc.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < count; i++)
                {
                    if (await loc.Nth(i).IsVisibleAsync().ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // try next
            }
        }

        foreach (var label in new[] { "تنزيل", "Download" })
        {
            try
            {
                var byRole = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label });
                var count = await byRole.CountAsync().ConfigureAwait(false);
                for (var i = 0; i < count; i++)
                {
                    var item = byRole.Nth(i);
                    if (!await item.IsVisibleAsync().ConfigureAwait(false))
                    {
                        continue;
                    }

                    if (await IsDownloadActionInOverlayAsync(page, item, label, strictSidebarFilter: true).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // try next label
            }
        }

        return false;
    }

    private static async Task<ILocator?> FindDownloadButtonInActionsPanelAsync(IPage page, NewsSource source)
    {
        var selectors = BuildSelectorList(
            source.DownloadMenuItemSelector,
            DarAlKhaleejDownloadMenuSelectors,
            DefaultDownloadMenuSelectors);
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector);
            var count = await locator.CountAsync().ConfigureAwait(false);
            for (var i = 0; i < count; i++)
            {
                var item = locator.Nth(i);
                try
                {
                    if (!await item.IsVisibleAsync().ConfigureAwait(false))
                    {
                        continue;
                    }

                    if (await IsDownloadActionInOverlayAsync(page, item, null, strictSidebarFilter: false).ConfigureAwait(false))
                    {
                        return item;
                    }
                }
                catch
                {
                    // try next
                }
            }
        }

        foreach (var role in new[] { AriaRole.Menuitem, AriaRole.Button, AriaRole.Link })
        {
            foreach (var label in new[] { "تنزيل", "Download" })
            {
                try
                {
                    var byRole = page.GetByRole(role, new PageGetByRoleOptions { Name = label });
                    var count = await byRole.CountAsync().ConfigureAwait(false);
                    for (var i = 0; i < count; i++)
                    {
                        var item = byRole.Nth(i);
                        if (await item.IsVisibleAsync().ConfigureAwait(false)
                            && await IsDownloadActionInOverlayAsync(page, item, label, strictSidebarFilter: false).ConfigureAwait(false))
                        {
                            return item;
                        }
                    }
                }
                catch
                {
                    // try next label
                }
            }
        }

        try
        {
            var menus = page.Locator("[role='menu'], [role='dialog']");
            var menuCount = await menus.CountAsync().ConfigureAwait(false);
            for (var m = 0; m < menuCount; m++)
            {
                var menu = menus.Nth(m);
                var downloadInMenu = menu.GetByText("تنزيل", new LocatorGetByTextOptions { Exact = true });
                if (await downloadInMenu.CountAsync().ConfigureAwait(false) > 0
                    && await downloadInMenu.First.IsVisibleAsync().ConfigureAwait(false))
                {
                    return downloadInMenu.First;
                }

                downloadInMenu = menu.GetByText("Download", new LocatorGetByTextOptions { Exact = true });
                if (await downloadInMenu.CountAsync().ConfigureAwait(false) > 0
                    && await downloadInMenu.First.IsVisibleAsync().ConfigureAwait(false))
                {
                    return downloadInMenu.First;
                }
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            var byText = page.GetByText("تنزيل", new PageGetByTextOptions { Exact = true });
            var count = await byText.CountAsync().ConfigureAwait(false);
            for (var i = 0; i < count; i++)
            {
                var item = byText.Nth(i);
                if (await item.IsVisibleAsync().ConfigureAwait(false)
                    && await IsDownloadActionInOverlayAsync(page, item, "تنزيل", strictSidebarFilter: false).ConfigureAwait(false))
                {
                    return item;
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static readonly string[] PageActionsPanelTextLabels =
    [
        "تنزيل",
        "عرض النص",
        "إشارة مرجعية",
        "مشاركة",
        "نسخ",
        "Download",
        "Text View"
    ];

    /// <summary>
    /// PressReader overlays sometimes omit roles/ARIA; scan visible DOM text (daralkhaleej Arabic UI).
    /// </summary>
    private static Task<bool> IsPageActionsPanelVisibleInDomAsync(IPage page) =>
        page.EvaluateAsync<bool>(
            """
            () => {
              const downloadRe = /^(تنزيل|Download)(\s|[>›»]|$)/;
              const otherLabels = ['عرض النص', 'إشارة مرجعية', 'التعليق', 'مشاركة', 'نسخ', 'استماع', 'Text View', 'Save to Collection', 'Print', 'إلغاء', 'Cancel'];
              const vw = window.innerWidth;
              const vh = window.innerHeight;
              let downloadVisible = false;
              let otherVisible = false;
              for (const el of document.querySelectorAll('button, a, [role="button"], [role="menuitem"], li, div, span')) {
                const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                if (!text || text.length > 80) continue;
                if (/download\s+the\s+app/i.test(text)) continue;
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) continue;
                const cx = r.x + r.width / 2;
                const cy = r.y + r.height / 2;
                if (cx > vw * 0.82 || cy < vh * 0.05 || cy > vh * 0.96) continue;
                const style = window.getComputedStyle(el);
                if (style.visibility === 'hidden' || style.display === 'none' || parseFloat(style.opacity) < 0.1) continue;
                if (downloadRe.test(text)) downloadVisible = true;
                if (otherLabels.some(l => text === l || text.startsWith(l))) otherVisible = true;
              }
              return downloadVisible && otherVisible;
            }
            """);

    private static Task<bool> HoverDownloadRowInPageActionsPanelViaDomAsync(IPage page) =>
        InteractWithDownloadRowInPageActionsPanelViaDomAsync(page, hoverOnly: true);

    private static Task<bool> ClickDownloadInPageActionsPanelViaDomAsync(IPage page) =>
        InteractWithDownloadRowInPageActionsPanelViaDomAsync(page, hoverOnly: false);

    private static async Task<bool> InteractWithDownloadRowInPageActionsPanelViaDomAsync(IPage page, bool hoverOnly)
    {
        var coords = await page.EvaluateAsync<double[]?>(
            """
            () => {
              const panelMarkers = ['عرض النص', 'Text View', 'إشارة مرجعية', 'Save to Collection'];
              const downloadRe = /^(تنزيل|Download)(\s|[>›»]|$)/;
              const vw = window.innerWidth;
              const vh = window.innerHeight;
              const isVisible = (el) => {
                const r = el.getBoundingClientRect();
                if (r.width < 1 || r.height < 1) return false;
                const style = window.getComputedStyle(el);
                return style.visibility !== 'hidden' && style.display !== 'none' && parseFloat(style.opacity) >= 0.1;
              };
              const inReaderOverlay = (r) => {
                const cx = r.x + r.width / 2;
                const cy = r.y + r.height / 2;
                return cx < vw * 0.82 && cy > vh * 0.05 && cy < vh * 0.96;
              };
              const hasPanelMarker = (container) => {
                const body = container.innerText || '';
                return panelMarkers.some(m => body.includes(m));
              };
              const submenuLabels = ['Download Issue as PDF', 'Download Page as PDF'];
              const inMainMenu = (el) => {
                let node = el;
                for (let depth = 0; depth < 18 && node; depth++, node = node.parentElement) {
                  const text = node.innerText || '';
                  if (submenuLabels.some(l => text.includes(l))) return false;
                  if (panelMarkers.some(m => text.includes(m)) && text.includes('تنزيل')) return true;
                }
                return false;
              };
              let best = null;
              let bestY = -1;
              const containers = document.querySelectorAll('[role="menu"], [role="dialog"], div, section, article, ul');
              for (const container of containers) {
                if (!hasPanelMarker(container)) continue;
                const cr = container.getBoundingClientRect();
                if (!inReaderOverlay(cr)) continue;
                for (const el of container.querySelectorAll('button, a, li, [role="button"], [role="menuitem"], div, span')) {
                  const text = (el.innerText || '').trim().replace(/\s+/g, ' ');
                  if (text !== 'تنزيل' && !downloadRe.test(text)) continue;
                  if (/download\s+the\s+app/i.test(text)) continue;
                  if (!isVisible(el) || !inMainMenu(el)) continue;
                  const r = el.getBoundingClientRect();
                  if (!inReaderOverlay(r)) continue;
                  if (r.y > bestY) {
                    bestY = r.y;
                    best = el;
                  }
                }
              }
              if (!best) return null;
              let clickTarget = best;
              for (let up = 0; up < 10 && clickTarget; up++) {
                const tag = (clickTarget.tagName || '').toLowerCase();
                const role = clickTarget.getAttribute('role') || '';
                const rh = clickTarget.getBoundingClientRect().height;
                if ((tag === 'li' || role === 'menuitem' || tag === 'button' || tag === 'a') && rh >= 20) {
                  const br = clickTarget.getBoundingClientRect();
                  return [br.x + br.width / 2, br.y + br.height / 2];
                }
                clickTarget = clickTarget.parentElement;
              }
              const br = best.getBoundingClientRect();
              return [br.x + br.width / 2, br.y + br.height / 2];
            }
            """).ConfigureAwait(false);

        if (coords is null || coords.Length < 2)
        {
            return false;
        }

        var x = (float)coords[0];
        var y = (float)coords[1];
        await page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
        if (hoverOnly)
        {
            return true;
        }

        await page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Ignore "Download the App" / far-right sidebar promos; keep the reader page-actions تنزيل row (left dock on daralkhaleej).
    /// </summary>
    private static async Task<bool> IsDownloadActionInOverlayAsync(
        IPage page,
        ILocator downloadControl,
        string? expectedLabel,
        bool strictSidebarFilter)
    {
        try
        {
            var text = NormalizeDownloadRowText(await downloadControl.InnerTextAsync().ConfigureAwait(false));
            if (text.Contains("download the app", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var isDownload = DownloadRowLabelRegex.IsMatch(text);
            if (expectedLabel is not null)
            {
                isDownload = text.Equals(expectedLabel, StringComparison.OrdinalIgnoreCase)
                             || (expectedLabel == "تنزيل" && text.Contains("تنزيل", StringComparison.Ordinal))
                             || DownloadRowLabelRegex.IsMatch(text);
            }

            if (!isDownload)
            {
                return false;
            }

            var box = await downloadControl.BoundingBoxAsync().ConfigureAwait(false);
            var viewport = page.ViewportSize;
            if (box is null || viewport is null)
            {
                return true;
            }

            var centerX = box.X + (box.Width / 2);
            var centerY = box.Y + (box.Height / 2);
            if (centerX > viewport.Width * 0.82 || centerY < viewport.Height * 0.05 || centerY > viewport.Height * 0.96)
            {
                return false;
            }

            // Exclude far-right "Download the App" promos; allow left-docked page-actions menu.
            return !strictSidebarFilter || centerX < viewport.Width * 0.78;
        }
        catch
        {
            return true;
        }
    }

    private static string NormalizeDownloadRowText(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? string.Empty : Regex.Replace(raw.Trim(), @"\s+", " ");

    private async Task<PortalEditionDownloadStepResult> SavePressReaderPdfAsync(
        PortalAutomationSession session,
        IDownload download,
        CancellationToken cancellationToken)
    {
        var source = session.Source;
        var editionDir = PortalStoragePaths.BuildEditionRelativeDirectory(source.Name, storageOptions.Value);
        var editionFileRel = $"{editionDir}/{PortalStoragePaths.PressReaderEditionFileName}";
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            await download.SaveAsAsync(tempPath).ConfigureAwait(false);
            var bytes = await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
            var minKb = source.MinimumPdfSizeKb > 0 ? source.MinimumPdfSizeKb : 100;
            var validation = PortalPdfFileValidator.Validate(bytes, requirePdfContent: true, minKb);
            if (!validation.Valid)
            {
                return new PortalEditionDownloadStepResult(false, validation.FailureReason!, "NotPdf");
            }

            await fileStorage.WriteAsync(editionFileRel, bytes, cancellationToken).ConfigureAwait(false);
            if (session.DownloadJobId is null)
            {
                return new PortalEditionDownloadStepResult(
                    true,
                    "PDF downloaded (test run; no job record).",
                    null,
                    null,
                    editionFileRel,
                    Convert.ToHexString(SHA256.HashData(bytes)),
                    bytes.LongLength);
            }

            var fileId = await PortalStoragePaths.CreateDownloadedFileAsync(
                db,
                session.DownloadJobId.Value,
                source.EditionUrl ?? source.BaseUrl,
                editionFileRel,
                bytes,
                validation.ContentType,
                cancellationToken).ConfigureAwait(false);

            source.LastSavedPdfPath = editionFileRel.Replace(Path.DirectorySeparatorChar, '/');
            source.LastPdfDownloadedAt = DateTimeOffset.UtcNow;

            return new PortalEditionDownloadStepResult(
                true,
                "PressReader edition PDF saved.",
                null,
                fileId,
                editionFileRel,
                Convert.ToHexString(SHA256.HashData(bytes)),
                bytes.LongLength);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed saving PressReader PDF for source {SourceId}", source.Id);
            return new PortalEditionDownloadStepResult(false, ex.Message, "SaveFailed");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static IReadOnlyList<string> BuildSelectorList(string? configured, params string[][] defaults)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            list.Add(configured.Trim());
        }

        foreach (var group in defaults)
        {
            foreach (var s in group)
            {
                if (!list.Contains(s, StringComparer.Ordinal))
                {
                    list.Add(s);
                }
            }
        }

        return list;
    }
}
