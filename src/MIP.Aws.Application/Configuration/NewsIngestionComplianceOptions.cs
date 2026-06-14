namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Legal copy and throttling for licensed portal automation.
/// </summary>
public sealed class NewsIngestionComplianceOptions
{
    public const string SectionName = "NewsIngestion";

    /// <summary>
    /// Shown in configuration and logs; does not replace counsel review.
    /// </summary>
    public string LegalAutomationDisclaimer { get; set; } =
        "GFH MediaIntelligence may automate only subscriber portals using credentials explicitly provided by GFH. " +
        "Automation follows visible UI controls (for example publisher-provided download buttons). " +
        "The system does not bypass paywalls, CAPTCHA, MFA, or publisher rate limits, and does not scrape content beyond what the licensed portal exposes to the authenticated user.";

    public int PortalActionDelayMinMs { get; set; } = 400;

    public int PortalActionDelayMaxMs { get; set; } = 1200;
}
