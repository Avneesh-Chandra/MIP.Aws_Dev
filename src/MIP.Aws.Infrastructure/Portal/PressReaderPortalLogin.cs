using System.Text.RegularExpressions;
using MIP.Aws.Domain.Entities;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>
/// PressReader / branded library sign-in (daralkhaleej.pressreader.com).
/// Branded editions use a lock key icon on the left to open the Sign in modal.
/// </summary>
public static class PressReaderPortalLogin
{
    /// <summary>daralkhaleej.pressreader.com Arabic login modal (البريد الإلكتروني / كلمة المرور / تسجيل الدخول).</summary>
    private static readonly string[] DarAlKhaleejEmailSelectors =
    [
        "input[placeholder*='البريد الإلكتروني']",
        "input[placeholder*='البريد']",
        "input[type='email']",
        "input[name='email']",
        "input[autocomplete='username']",
        "input[autocomplete='email']"
    ];

    private static readonly string[] DarAlKhaleejPasswordSelectors =
    [
        "input[placeholder*='كلمة المرور']",
        "input[type='password']",
        "input[name='password']",
        "input[autocomplete='current-password']"
    ];

    private static readonly string[] DarAlKhaleejSubmitSelectors =
    [
        "[role='dialog'] button:has-text('تسجيل الدخول')",
        ".modal button:has-text('تسجيل الدخول')",
        "button:has-text('تسجيل الدخول')",
        "button[type='submit']"
    ];

    private static readonly string[] EmailSelectors =
    [
        "input[placeholder='Username']",
        "input[placeholder*='Username' i]",
        "#email",
        "input[type='email']",
        "input[name='email']",
        "input[name='login']",
        "input[name='username']",
        "input[autocomplete='username']"
    ];

    private static readonly string[] PasswordSelectors =
    [
        "input[placeholder='Password']",
        "input[placeholder*='Password' i]",
        "#password",
        "input[type='password']",
        "input[name='password']",
        "input[autocomplete='current-password']"
    ];

    private static readonly string[] SubmitSelectors =
    [
        "[role='dialog'] button:has-text('Sign in')",
        ".modal button:has-text('Sign in')",
        "button:has-text('Sign in')",
        "button[type='submit']",
        "button.sign-in",
        "button:has-text('Log in')",
        "input[type='submit']"
    ];

    /// <summary>
    /// Lock / key control on the left of the branded PressReader reader (opens Sign in modal).
    /// </summary>
    private static readonly string[] BrandedKeyIconSelectors =
    [
        "button[aria-label*='unlock' i]",
        "button[aria-label*='sign in' i]",
        "[class*='lock'] button",
        "[class*='Lock'] button",
        "[class*='unlock'] button",
        "[class*='Unlock'] button",
        "[class*='key'] button",
        "[class*='Key'] button",
        "button:has(svg[class*='key'])",
        "button:has(svg path[d*='M'])"
    ];

    private static readonly string[] BrandedHeaderSignInSelectors =
    [
        "button:has-text('تسجيل الدخول')",
        "a:has-text('تسجيل الدخول')",
        "[aria-label*='تسجيل الدخول']",
        "button:has-text('Sign in')",
        "a:has-text('Sign in')",
        "[aria-label*='Sign in' i]"
    ];

    public static bool IsPressReaderSource(NewsSource source)
    {
        static bool HasPressReader(Uri? u) =>
            u is not null &&
            u.Host.Contains("pressreader.com", StringComparison.OrdinalIgnoreCase);

        return string.Equals(source.ConnectorKey, "news.pressreader", StringComparison.OrdinalIgnoreCase)
               || HasPressReader(TryUri(source.LoginUrl))
               || HasPressReader(TryUri(source.EditionUrl))
               || HasPressReader(TryUri(source.BaseUrl));
    }

    public static async Task<(bool Success, string Message, string? FailureCode)> TryLoginAsync(
        IPage page,
        NewsSource source,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (IsBrandedDarAlKhaleejSource(source))
        {
            return await TryBrandedDarAlKhaleejLoginAsync(page, source, username, password, cancellationToken).ConfigureAwait(false);
        }

        return await TryGenericPressReaderLoginAsync(page, source, username, password, cancellationToken).ConfigureAwait(false);
    }

    public static bool IsBrandedDarAlKhaleejSource(NewsSource source)
    {
        static bool IsBrandedHost(Uri? u) =>
            u is not null &&
            u.Host.Contains("daralkhaleej.pressreader.com", StringComparison.OrdinalIgnoreCase);

        return IsBrandedHost(TryUri(source.EditionUrl))
               || IsBrandedHost(TryUri(source.BaseUrl))
               || IsBrandedHost(TryUri(source.LoginUrl));
    }

    private static async Task<(bool Success, string Message, string? FailureCode)> TryBrandedDarAlKhaleejLoginAsync(
        IPage page,
        NewsSource source,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var editionUrl = !string.IsNullOrWhiteSpace(source.EditionUrl)
            ? source.EditionUrl.Trim()
            : source.BaseUrl?.Trim();

        if (string.IsNullOrWhiteSpace(editionUrl))
        {
            return (false, "EditionUrl is required for daralkhaleej PressReader sources.", "EditionUrlMissing");
        }

        try
        {
            await page.GotoAsync(editionUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60_000 }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, $"Could not open PressReader edition: {ex.Message}", "NavigationFailed");
        }

        await DismissCookieBannerAsync(page).ConfigureAwait(false);

        if (await IsBrandedDarAlKhaleejLoggedInAsync(page, source).ConfigureAwait(false))
        {
            return (true, "Reusing existing Dar Al Khaleej PressReader subscriber session.", null);
        }

        if (!await IsLoginModalVisibleAsync(page).ConfigureAwait(false))
        {
            if (!await OpenBrandedSignInModalAsync(page, source, cancellationToken).ConfigureAwait(false))
            {
                return (false, "PressReader lock key icon or Sign in control not found on edition page.", "SelectorNotFound");
            }
        }

        if (!await WaitForSignInModalAsync(page, cancellationToken).ConfigureAwait(false))
        {
            return (false, "PressReader Sign in modal did not appear after opening login.", "SignInModalTimeout");
        }

        var emailSelectors = MergeSelectors(source.UsernameSelector, DarAlKhaleejEmailSelectors, EmailSelectors);
        var passwordSelectors = MergeSelectors(source.PasswordSelector, DarAlKhaleejPasswordSelectors, PasswordSelectors);
        var submitSelectors = MergeSelectors(source.SubmitSelector, DarAlKhaleejSubmitSelectors, SubmitSelectors);

        if (!await FillFirstVisibleInLoginDialogAsync(page, emailSelectors, username, cancellationToken).ConfigureAwait(false))
        {
            return (false, "PressReader email/username field not found in Sign in modal (Arabic or English).", "SelectorNotFound");
        }

        if (!await FillFirstVisibleInLoginDialogAsync(page, passwordSelectors, password, cancellationToken).ConfigureAwait(false))
        {
            return (false, "PressReader password field not found in Sign in modal.", "SelectorNotFound");
        }

        await UncheckStaySignedInAsync(page).ConfigureAwait(false);

        if (!await SubmitLoginDialogAsync(page, submitSelectors, cancellationToken).ConfigureAwait(false))
        {
            return (false, "PressReader Sign in button not found in modal.", "SelectorNotFound");
        }

        await Task.Delay(1_500, cancellationToken).ConfigureAwait(false);
        if (await TryGetLoginBlockerAsync(page).ConfigureAwait(false) is { } earlyBlocker)
        {
            return (false, earlyBlocker.Message, earlyBlocker.FailureCode);
        }

        if (!await WaitForBrandedDarAlKhaleejLoginSuccessAsync(page, source, cancellationToken).ConfigureAwait(false))
        {
            if (await TryGetLoginBlockerAsync(page).ConfigureAwait(false) is { } blocker)
            {
                return (false, blocker.Message, blocker.FailureCode);
            }

            return (false,
                "PressReader login did not complete. Re-save the encrypted password in News Sources (passwords are case-sensitive; e.g. Jumana@2026).",
                "LoginFailed");
        }

        return (true, string.Empty, null);
    }

    /// <summary>
    /// Signs out on daralkhaleej so server-side concurrent session slots are released after login probes.
    /// </summary>
    public static async Task<bool> TryReleaseDarAlKhaleejSessionAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        if (!IsBrandedDarAlKhaleejSource(source))
        {
            return false;
        }

        var editionUrl = !string.IsNullOrWhiteSpace(source.EditionUrl)
            ? source.EditionUrl.Trim()
            : source.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(editionUrl))
        {
            return false;
        }

        try
        {
            if (!page.Url.Contains(new Uri(editionUrl).Host, StringComparison.OrdinalIgnoreCase))
            {
                await page.GotoAsync(editionUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30_000 }).ConfigureAwait(false);
            }
        }
        catch
        {
            return false;
        }

        return await TryBrandedDarAlKhaleejLogoutAsync(page, cancellationToken).ConfigureAwait(false);
    }

    public static Task<bool> IsLoggedInForLogoutAsync(IPage page) => HasSubscriberAccountInTopBarAsync(page);

    public static async Task<(string Message, string FailureCode)?> TryGetLoginBlockerAsync(IPage page)
    {
        string body;
        try
        {
            body = await page.EvaluateAsync<string>("() => document.body?.innerText ?? ''").ConfigureAwait(false);
        }
        catch
        {
            return null;
        }

        if (body.Contains("تجاوزت عدد الجلسات", StringComparison.Ordinal)
            || body.Contains("الجلسات المتزامنة", StringComparison.Ordinal)
            || body.Contains("عدد الجلسات المتزامنة", StringComparison.Ordinal))
        {
            return (
                "PressReader blocked login: concurrent session limit exceeded for this subscriber. Close other browsers or tabs signed in with the same account (including an earlier login test), wait a few minutes, then retry.",
                "ConcurrentSessionsExceeded");
        }

        if (body.Contains("exceeded the number of simultaneous sessions", StringComparison.OrdinalIgnoreCase)
            || body.Contains("concurrent session", StringComparison.OrdinalIgnoreCase)
            || body.Contains("simultaneous session", StringComparison.OrdinalIgnoreCase)
            || body.Contains("too many sessions", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "PressReader blocked login: concurrent session limit exceeded. Close other active sessions for this subscriber, then retry.",
                "ConcurrentSessionsExceeded");
        }

        return null;
    }

    private static readonly string[] BrandedSignOutSelectors =
    [
        "button:has-text('تسجيل الخروج')",
        "a:has-text('تسجيل الخروج')",
        "[role='menuitem']:has-text('تسجيل الخروج')",
        "button:has-text('Sign out')",
        "a:has-text('Sign out')",
        "[role='menuitem']:has-text('Sign out')",
        "button:has-text('Log out')",
        "a:has-text('Log out')"
    ];

    /// <summary>
    /// Returns true when the subscriber chip (e.g. JM37955283) is no longer shown in the top bar.
    /// </summary>
    public static async Task<bool> TryBrandedDarAlKhaleejLogoutAsync(IPage page, CancellationToken cancellationToken)
    {
        if (!await HasSubscriberAccountInTopBarAsync(page).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            var signOutVisible = page.GetByText("تسجيل الخروج", new PageGetByTextOptions { Exact = true });
            if (await signOutVisible.CountAsync().ConfigureAwait(false) > 0
                && await signOutVisible.First.IsVisibleAsync().ConfigureAwait(false))
            {
                await signOutVisible.First.ClickAsync(new LocatorClickOptions { Timeout = 8_000 }).ConfigureAwait(false);
                await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
                return !await HasSubscriberAccountInTopBarAsync(page).ConfigureAwait(false);
            }
        }
        catch
        {
            // open account menu first
        }

        if (!await ClickSubscriberAccountMenuAsync(page, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await Task.Delay(900, cancellationToken).ConfigureAwait(false);

        try
        {
            var signOut = page.GetByText("تسجيل الخروج", new PageGetByTextOptions { Exact = true });
            if (await signOut.CountAsync().ConfigureAwait(false) > 0 && await signOut.First.IsVisibleAsync().ConfigureAwait(false))
            {
                await signOut.First.ClickAsync(new LocatorClickOptions { Timeout = 8_000 }).ConfigureAwait(false);
                await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
                return !await HasSubscriberAccountInTopBarAsync(page).ConfigureAwait(false);
            }
        }
        catch
        {
            // fall through
        }

        if (!await ClickFirstVisibleAsync(page, BrandedSignOutSelectors, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
        return !await HasSubscriberAccountInTopBarAsync(page).ConfigureAwait(false);
    }

    private static async Task<bool> ClickSubscriberAccountMenuAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var subscriberChip = page.Locator(
                "header button, header a, header [role='button'], header span, header div, [class*='user'] button, [class*='account'] button, [class*='profile'] button");
            var chipCount = await subscriberChip.CountAsync().ConfigureAwait(false);
            for (var i = chipCount - 1; i >= 0; i--)
            {
                var chip = subscriberChip.Nth(i);
                try
                {
                    if (!await chip.IsVisibleAsync().ConfigureAwait(false))
                    {
                        continue;
                    }

                    var text = (await chip.InnerTextAsync().ConfigureAwait(false) ?? string.Empty).Trim();
                    if (Regex.IsMatch(text, @"^[A-Z]{2}\d{5,}$") || text.Length == 1)
                    {
                        await chip.ClickAsync(new LocatorClickOptions { Timeout = 8_000 }).ConfigureAwait(false);
                        return true;
                    }
                }
                catch
                {
                    // try next
                }
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            var subscriber = page.Locator("button, a, [role='button'], [role='menuitem'], span, div")
                .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^[A-Z]{2}\\d{5,}$") });
            var count = await subscriber.CountAsync().ConfigureAwait(false);
            for (var i = 0; i < count; i++)
            {
                var item = subscriber.Nth(i);
                try
                {
                    if (!await item.IsVisibleAsync().ConfigureAwait(false))
                    {
                        continue;
                    }

                    var box = await item.BoundingBoxAsync().ConfigureAwait(false);
                    var viewport = page.ViewportSize;
                    if (box is not null && viewport is not null)
                    {
                        var centerX = box.X + (box.Width / 2);
                        var centerY = box.Y + (box.Height / 2);
                        if (centerX < viewport.Width * 0.50 || centerY > viewport.Height * 0.22)
                        {
                            continue;
                        }
                    }

                    await item.ClickAsync(new LocatorClickOptions { Timeout = 8_000 }).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    // try next match
                }
            }
        }
        catch
        {
            // fall through to DOM click
        }

        try
        {
            return await page.EvaluateAsync<bool>(
                """
                () => {
                  const pattern = /^[A-Z]{2}\d{5,}$/;
                  const vw = window.innerWidth;
                  const vh = window.innerHeight;
                  for (const el of document.querySelectorAll('button, a, [role="button"], span, div')) {
                    const text = (el.innerText || '').trim();
                    if (!pattern.test(text)) continue;
                    const r = el.getBoundingClientRect();
                    if (r.width < 1 || r.height < 1) continue;
                    const cx = r.x + r.width / 2;
                    const cy = r.y + r.height / 2;
                    if (cx < vw * 0.55 || cy > vh * 0.22) continue;
                    el.click();
                    return true;
                  }
                  return false;
                }
                """).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static Task<bool> HasSubscriberAccountInTopBarAsync(IPage page) =>
        page.EvaluateAsync<bool>(
            """
            () => {
              const pattern = /\b[A-Z]{2}\d{5,}\b/;
              const top = document.querySelector('header')?.innerText ?? '';
              if (pattern.test(top)) return true;
              const vw = window.innerWidth;
              const vh = window.innerHeight;
              for (const el of document.querySelectorAll('button, a, span, div, [role="button"]')) {
                const text = (el.innerText || '').trim();
                if (!pattern.test(text)) continue;
                const r = el.getBoundingClientRect();
                const cy = r.y + r.height / 2;
                const cx = r.x + r.width / 2;
                if (cx >= vw * 0.55 && cy <= vh * 0.22) return true;
              }
              return false;
            }
            """);

    private static async Task UncheckStaySignedInAsync(IPage page)
    {
        try
        {
            var checkbox = page.Locator(
                "[role='dialog'] input[type='checkbox'], .modal input[type='checkbox'], label:has-text('Stay signed in') input, label:has-text('Keep me logged in') input");
            var count = await checkbox.CountAsync().ConfigureAwait(false);
            for (var i = 0; i < count; i++)
            {
                var item = checkbox.Nth(i);
                if (!await item.IsVisibleAsync().ConfigureAwait(false))
                {
                    continue;
                }

                if (await item.IsCheckedAsync().ConfigureAwait(false))
                {
                    await item.UncheckAsync(new LocatorUncheckOptions { Timeout = 3_000 }).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // optional
        }
    }

    private static async Task<bool> OpenBrandedSignInModalAsync(IPage page, NewsSource? source, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(source?.LoginIconSelector))
        {
            try
            {
                await page.ClickAsync(source.LoginIconSelector!, new PageClickOptions { Timeout = 15_000 }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // fall through to defaults
            }
        }

        // Primary path: key icon on the left-center lock overlay (daralkhaleej branded reader).
        if (await ClickKeyIconOnLeftPanelAsync(page, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        foreach (var selector in BrandedHeaderSignInSelectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                continue;
            }

            try
            {
                await locator.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // try next
            }
        }

        return false;
    }

    private static async Task<bool> ClickKeyIconOnLeftPanelAsync(IPage page, CancellationToken cancellationToken)
    {
        var viewport = page.ViewportSize;
        if (viewport is not null)
        {
            var leftX = (int)(viewport.Width * 0.08);
            var centerY = viewport.Height / 2;
            try
            {
                await page.Mouse.ClickAsync(leftX, centerY).ConfigureAwait(false);
                await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                if (await page.Locator(
                        "input[placeholder*='البريد'], input[placeholder*='Username' i], input[type='email']")
                        .CountAsync() > 0)
                {
                    return true;
                }
            }
            catch
            {
                // fall through to selector-based click
            }
        }

        foreach (var selector in BrandedKeyIconSelectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

                    var box = await item.BoundingBoxAsync().ConfigureAwait(false);
                    if (box is null || box.Width <= 0)
                    {
                        continue;
                    }

                    // Prefer controls on the left half of the viewport (lock key placement).
                    if (viewport is not null && box.X > viewport.Width * 0.35)
                    {
                        continue;
                    }

                    await item.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    // try next match
                }
            }
        }

        return false;
    }

    private static async Task<bool> IsLoginModalVisibleAsync(IPage page)
    {
        var loginFields = page.Locator(
            "input[placeholder*='البريد'], input[placeholder*='Username' i], input[type='email']");
        if (await loginFields.CountAsync() == 0)
        {
            return false;
        }

        try
        {
            return await loginFields.First.IsVisibleAsync().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForSignInModalAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var modal = page.Locator(
            "[role='dialog']:has-text('تسجيل الدخول'), [role='dialog']:has-text('Sign in'), .modal:has-text('تسجيل الدخول'), .modal:has-text('Sign in')");
        try
        {
            await modal.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 20_000
            }).ConfigureAwait(false);
            return true;
        }
        catch
        {
            // Wait for email field (Arabic daralkhaleej or English PressReader).
            var emailField = page.Locator(
                "input[placeholder*='البريد'], input[placeholder='Username'], input[placeholder*='Username' i], input[type='email']").First;
            try
            {
                await emailField.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 15_000
                }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// After successful daralkhaleej login the header shows the subscriber id (e.g. JM37955283) instead of تسجيل الدخول.
    /// Requires positive proof — stale cookies or a closed login modal must not count as authenticated.
    /// </summary>
    public static async Task<bool> IsBrandedDarAlKhaleejLoggedInAsync(IPage page, NewsSource source)
    {
        if (await TryGetLoginBlockerAsync(page).ConfigureAwait(false) is not null)
        {
            return false;
        }

        if (await HasSubscriberAccountInTopBarAsync(page).ConfigureAwait(false))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(source.LoginSuccessSelector))
        {
            try
            {
                var custom = page.Locator(source.LoginSuccessSelector!).First;
                return await custom.CountAsync() > 0 && await custom.IsVisibleAsync().ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static async Task<bool> WaitForBrandedDarAlKhaleejLoginSuccessAsync(
        IPage page,
        NewsSource source,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryGetLoginBlockerAsync(page).ConfigureAwait(false) is not null)
            {
                return false;
            }

            if (await IsBrandedDarAlKhaleejLoggedInAsync(page, source).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 5_000 }).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        return await IsBrandedDarAlKhaleejLoggedInAsync(page, source).ConfigureAwait(false);
    }

    private static async Task<bool> SubmitLoginDialogAsync(IPage page, string[] submitSelectors, CancellationToken cancellationToken)
    {
        var dialogSubmit = submitSelectors
            .Select(s => s.StartsWith("[role='dialog']", StringComparison.Ordinal) ? s : $"[role='dialog'] {s}")
            .ToArray();

        if (await ClickFirstVisibleAsync(page, dialogSubmit, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await ClickFirstVisibleAsync(page, submitSelectors, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        try
        {
            var password = page.Locator("[role='dialog'] input[type='password']").First;
            if (await password.CountAsync() > 0)
            {
                await password.PressAsync("Enter", new LocatorPressOptions { Timeout = 5_000 }).ConfigureAwait(false);
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static async Task<bool> FillFirstVisibleInLoginDialogAsync(
        IPage page,
        string[] selectors,
        string value,
        CancellationToken cancellationToken)
    {
        var dialogScoped = selectors
            .Select(s => s.StartsWith("[role='dialog']", StringComparison.Ordinal) ? s : $"[role='dialog'] {s}")
            .ToArray();

        if (await FillFirstVisibleAsync(page, dialogScoped, value, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return await FillFirstVisibleAsync(page, selectors, value, cancellationToken).ConfigureAwait(false);
    }

    private static string[] MergeSelectors(string? configured, params string[][] defaults)
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

        return list.ToArray();
    }

    private static async Task<(bool Success, string Message, string? FailureCode)> TryGenericPressReaderLoginAsync(
        IPage page,
        NewsSource source,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var loginUri = !string.IsNullOrWhiteSpace(source.LoginUrl)
            ? new Uri(source.LoginUrl)
            : new Uri("https://www.pressreader.com/signin");

        try
        {
            await page.GotoAsync(loginUri.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, $"Could not open PressReader login page: {ex.Message}", "NavigationFailed");
        }

        await DismissCookieBannerAsync(page).ConfigureAwait(false);

        if (!await FillFirstVisibleAsync(page, EmailSelectors, username, cancellationToken).ConfigureAwait(false))
        {
            await TryOpenSignInFromEditionAsync(page, source, cancellationToken).ConfigureAwait(false);
            if (!await FillFirstVisibleAsync(page, EmailSelectors, username, cancellationToken).ConfigureAwait(false))
            {
                return (false, "PressReader email/username field not found.", "SelectorNotFound");
            }
        }

        if (!await FillFirstVisibleAsync(page, PasswordSelectors, password, cancellationToken).ConfigureAwait(false))
        {
            return (false, "PressReader password field not found.", "SelectorNotFound");
        }

        if (!await ClickFirstVisibleAsync(page, SubmitSelectors, cancellationToken).ConfigureAwait(false))
        {
            return (false, "PressReader sign-in button not found.", "SelectorNotFound");
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 90_000 }).ConfigureAwait(false);
        }
        catch
        {
            // SPA viewer may not reach network idle
        }

        if (page.Url.Contains("/signin", StringComparison.OrdinalIgnoreCase) &&
            await page.Locator(PasswordSelectors[0]).CountAsync() > 0)
        {
            return (false, "PressReader sign-in did not leave the login page (check credentials).", "LoginFailed");
        }

        if (!string.IsNullOrWhiteSpace(source.EditionUrl) &&
            !page.Url.Contains(new Uri(source.EditionUrl).Host, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await page.GotoAsync(source.EditionUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return (false, $"Login succeeded but edition navigation failed: {ex.Message}", "EditionNavigationFailed");
            }
        }

        return (true, string.Empty, null);
    }

    private static async Task TryOpenSignInFromEditionAsync(IPage page, NewsSource source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.EditionUrl))
        {
            return;
        }

        try
        {
            await page.GotoAsync(source.EditionUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        await OpenBrandedSignInModalAsync(page, source, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DismissCookieBannerAsync(IPage page)
    {
        var accept = page.Locator("button:has-text('Accept'), button:has-text('I agree'), #onetrust-accept-btn-handler").First;
        if (await accept.CountAsync() > 0)
        {
            try
            {
                await accept.ClickAsync(new LocatorClickOptions { Timeout = 3_000 }).ConfigureAwait(false);
            }
            catch
            {
                // optional
            }
        }
    }

    private static async Task<bool> FillFirstVisibleAsync(IPage page, string[] selectors, string value, CancellationToken cancellationToken)
    {
        foreach (var selector in selectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                continue;
            }

            try
            {
                if (!await locator.IsVisibleAsync().ConfigureAwait(false))
                {
                    continue;
                }

                await locator.FillAsync(value, new LocatorFillOptions { Timeout = 15_000 }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // try next selector
            }
        }

        return false;
    }

    private static async Task<bool> ClickFirstVisibleAsync(IPage page, string[] selectors, CancellationToken cancellationToken)
    {
        foreach (var selector in selectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                continue;
            }

            try
            {
                if (!await locator.IsVisibleAsync().ConfigureAwait(false))
                {
                    continue;
                }

                await locator.ClickAsync(new LocatorClickOptions { Timeout = 15_000 }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // try next selector
            }
        }

        return false;
    }

    private static Uri? TryUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}
