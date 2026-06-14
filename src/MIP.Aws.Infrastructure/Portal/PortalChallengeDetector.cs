using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

internal static class PortalChallengeDetector
{
    public static async Task<bool> DetectCaptchaAsync(IPage page)
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

    public static async Task<bool> DetectMfaAsync(IPage page)
    {
        try
        {
            var otp = page.Locator("input[autocomplete='one-time-code'],input[name*='otp' i],input[id*='otp' i]");
            if (await otp.CountAsync().ConfigureAwait(false) > 0)
            {
                return true;
            }

            var body = await page.InnerTextAsync("body").ConfigureAwait(false);
            if (body.Contains("two-factor", StringComparison.OrdinalIgnoreCase)
                || body.Contains("two step", StringComparison.OrdinalIgnoreCase)
                || body.Contains("verification code", StringComparison.OrdinalIgnoreCase)
                || body.Contains("authenticator", StringComparison.OrdinalIgnoreCase))
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
