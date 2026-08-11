using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

internal static class PortalChallengeDetector
{
    /// <summary>
    /// Detects an interactive CAPTCHA challenge that would block automation.
    /// Invisible / always-present widgets (e.g. reCAPTCHA v3, bare data-sitekey) are ignored —
    /// those caused false "CAPTCHA or MFA" failures after a successful login probe.
    /// </summary>
    public static async Task<bool> DetectCaptchaAsync(IPage page)
    {
        try
        {
            var candidates = page.Locator(
                "iframe[src*='recaptcha']:visible," +
                "iframe[title*='reCAPTCHA']:visible," +
                "iframe[src*='hcaptcha']:visible," +
                ".g-recaptcha:visible," +
                "[class*='h-captcha']:visible," +
                "#cf-turnstile:visible," +
                "div.cf-turnstile:visible");

            return await candidates.CountAsync().ConfigureAwait(false) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects a visible OTP/MFA input. Page marketing text alone is not enough — PressReader
    /// and news pages often mention verification without presenting a real challenge.
    /// </summary>
    public static async Task<bool> DetectMfaAsync(IPage page)
    {
        try
        {
            var otp = page.Locator(
                "input[autocomplete='one-time-code']:visible," +
                "input[name*='otp' i]:visible," +
                "input[id*='otp' i]:visible," +
                "input[name*='totp' i]:visible," +
                "input[id*='totp' i]:visible," +
                "input[name*='verificationcode' i]:visible," +
                "input[id*='verificationcode' i]:visible");

            return await otp.CountAsync().ConfigureAwait(false) > 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> DetectPaywallOrUpgradeAsync(IPage page)
    {
        try
        {
            var body = await page.InnerTextAsync("body").ConfigureAwait(false);
            return body.Contains("subscribe", StringComparison.OrdinalIgnoreCase)
                   && (body.Contains("upgrade", StringComparison.OrdinalIgnoreCase)
                       || body.Contains("trial", StringComparison.OrdinalIgnoreCase)
                       || body.Contains("premium", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
