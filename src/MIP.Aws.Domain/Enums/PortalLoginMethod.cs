namespace MIP.Aws.Domain.Enums;

/// <summary>
/// How the portal expects authentication to be performed during automation.
/// </summary>
/// <remarks>
/// Modes <see cref="ManualOtpAssisted"/> and <see cref="ManualBrowserSession"/> NEVER run an
/// unattended login. They require an authorized GFH operator to complete the challenge
/// (mobile OTP, e-mail OTP, authenticator app, paper token, …) in a headed Chromium window.
/// The platform captures only the resulting session state. No OTP value is ever stored,
/// logged, or transmitted to the application server in plaintext.
/// </remarks>
public enum PortalLoginMethod
{
    /// <summary>Fill username/password fields via CSS selectors and submit.</summary>
    FormCssSelectors = 1,

    /// <summary>Rely primarily on URL pattern after redirect to detect success (selectors optional).</summary>
    SuccessUrlPattern = 2,

    /// <summary>
    /// Username + manual OTP completion. The platform pre-fills the username via the configured
    /// selector but the human operator must type the SMS/e-mail/authenticator code themselves.
    /// Downloads only proceed if the publisher exposes an explicit download control and
    /// <c>IsDownloadAllowed</c> is true.
    /// </summary>
    ManualOtpAssisted = 3,

    /// <summary>
    /// Fully operator-driven login: the platform opens the portal in a headed browser, the
    /// operator performs the entire authentication flow (including OTP/MFA), and the platform
    /// captures only the resulting Playwright storage-state for the licensed download replay.
    /// </summary>
    ManualBrowserSession = 4
}
