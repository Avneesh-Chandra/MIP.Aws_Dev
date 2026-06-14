using System.Text.RegularExpressions;
using MIP.Aws.Domain.Entities;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Portal;

internal static class GenericPortalLogin
{
    public static async Task<PortalLoginStepResult> TryLoginAsync(
        IPage page,
        NewsSource source,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.GotoAsync(source.LoginUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new PortalLoginStepResult(false, $"Could not open login page: {ex.Message}", "NavigationFailed");
        }

        if (await PortalChallengeDetector.DetectCaptchaAsync(page).ConfigureAwait(false))
        {
            return new PortalLoginStepResult(false, "CAPTCHA detected on the login page.", "CaptchaOnLoginPage");
        }

        if (await PortalChallengeDetector.DetectMfaAsync(page).ConfigureAwait(false))
        {
            return new PortalLoginStepResult(false, "MFA or OTP challenge detected on the login page.", "MfaOnLoginPage");
        }

        try
        {
            await page.FillAsync(source.UsernameSelector!, username).ConfigureAwait(false);
            await page.FillAsync(source.PasswordSelector!, password).ConfigureAwait(false);
            await page.ClickAsync(source.SubmitSelector!).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new PortalLoginStepResult(false, "Timeout while interacting with login form.", "SelectorTimeout");
        }
        catch (Exception ex)
        {
            return new PortalLoginStepResult(false, $"Login form interaction failed: {ex.Message}", "SelectorNotFound");
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 90_000 }).ConfigureAwait(false);
        }
        catch
        {
            // SPA may not reach network idle
        }

        if (await PortalChallengeDetector.DetectCaptchaAsync(page).ConfigureAwait(false))
        {
            return new PortalLoginStepResult(false, "CAPTCHA appeared after submitting credentials.", "CaptchaAfterLogin");
        }

        if (await PortalChallengeDetector.DetectMfaAsync(page).ConfigureAwait(false))
        {
            return new PortalLoginStepResult(false, "MFA challenge appeared after submitting credentials.", "MfaAfterLogin");
        }

        if (!string.IsNullOrWhiteSpace(source.LoginSuccessSelector))
        {
            try
            {
                await page.WaitForSelectorAsync(
                    source.LoginSuccessSelector!,
                    new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 60_000 }).ConfigureAwait(false);
            }
            catch
            {
                return new PortalLoginStepResult(false, "LoginSuccessSelector not found within timeout.", "LoginSuccessSelectorTimeout");
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
                return new PortalLoginStepResult(false, "SuccessUrlPattern did not match after login.", "SuccessUrlMismatch");
            }
        }

        return new PortalLoginStepResult(true, string.Empty, null);
    }
}
