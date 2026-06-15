using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

public class NewsSource : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public NewsSourceType SourceType { get; set; }

    public ContentAcquisitionMode AcquisitionMode { get; set; }

    /// <summary>High-level access classification for compliance routing.</summary>
    public NewsSourceAccessMode SourceAccessMode { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string? ConnectorKey { get; set; }

    public string? DefaultLanguage { get; set; }

    /// <summary>ISO country name or code for editorial grouping (optional).</summary>
    public string? Country { get; set; }

    /// <summary>When the last successful download completed for this source.</summary>
    public DateTimeOffset? LastDownloadAt { get; set; }

    /// <summary>Whether HTTP authentication headers or stored credentials may be required.</summary>
    public bool RequiresAuthentication { get; set; }

    /// <summary>Subscriber portal requires an interactive login before any download.</summary>
    public bool RequiresLogin { get; set; }

    /// <summary>Optional preferred interval between runs in minutes (Hangfire cron still authoritative).</summary>
    public int? DownloadFrequencyMinutes { get; set; }

    /// <summary>Use Playwright for JavaScript-rendered pages (higher cost; respect robots and licensing).</summary>
    public bool UseHeadlessBrowser { get; set; }

    public bool IsEnabled { get; set; } = true;

    public Guid? SourceCategoryId { get; set; }

    public SourceCategory? SourceCategory { get; set; }

    public SourceCredential? Credential { get; set; }

    public DownloadSchedule? DownloadSchedule { get; set; }

    public ICollection<DownloadJob> DownloadJobs { get; set; } = new List<DownloadJob>();

    // --- Licensed web portal automation (password never stored on this entity; use SourceCredential.ProtectedCredentialPayload) ---

    /// <summary>Full URL of the publisher login page.</summary>
    public string? LoginUrl { get; set; }

    /// <summary>URL of the edition / e-paper area after authentication.</summary>
    public string? EditionUrl { get; set; }

    /// <summary>Optional explicit logout URL to terminate the session after download.</summary>
    public string? LogoutUrl { get; set; }

    /// <summary>Portal username (not a secret). Password is stored encrypted on <see cref="SourceCredential"/>.</summary>
    public string? PortalUsername { get; set; }

    public PortalLoginMethod LoginMethod { get; set; } = PortalLoginMethod.FormCssSelectors;

    public string? UsernameSelector { get; set; }

    public string? PasswordSelector { get; set; }

    public string? SubmitSelector { get; set; }

    public string? DownloadSelector { get; set; }

    /// <summary>Portal automation strategy key (e.g. Generic, PressReader).</summary>
    public string? PortalStrategyKey { get; set; }

    /// <summary>CSS selector for the login/key icon (PressReader branded reader).</summary>
    public string? LoginIconSelector { get; set; }

    /// <summary>CSS selector for the newspaper reader canvas area (right-click / context menu target).</summary>
    public string? NewspaperCanvasSelector { get; set; }

    /// <summary>CSS selector to open the page context menu when a direct right-click is insufficient.</summary>
    public string? ContextMenuSelector { get; set; }

    /// <summary>CSS selector for the Download menu item in the portal context menu.</summary>
    public string? DownloadMenuItemSelector { get; set; }

    /// <summary>Seconds to wait for a browser download event after triggering Download.</summary>
    public int DownloadWaitTimeoutSeconds { get; set; } = 180;

    /// <summary>Optional selector that must appear when login succeeds.</summary>
    public string? LoginSuccessSelector { get; set; }

    /// <summary>Optional regex pattern applied to the current URL to detect successful login.</summary>
    public string? SuccessUrlPattern { get; set; }

    /// <summary>Publisher is known to present CAPTCHA; automated runs are blocked until manual remediation.</summary>
    public bool RequiresCaptcha { get; set; }

    /// <summary>GFH confirms this portal exposes a permitted download control for licensed subscribers.</summary>
    public bool IsDownloadAllowed { get; set; }

    /// <summary>Set when automation detects CAPTCHA/MFA or cannot proceed without human intervention.</summary>
    public bool RequiresManualAction { get; set; }

    // ───────── Manual-assisted MFA / OTP support ─────────

    /// <summary>The portal authenticates the operator with a second factor (mobile OTP, e-mail OTP, authenticator). Unattended login is forbidden.</summary>
    public bool RequiresMfa { get; set; }

    /// <summary>The portal challenges the operator with a one-time-passcode (SMS / e-mail / TOTP). Treat as a stricter form of <see cref="RequiresMfa"/> that the dashboard surfaces explicitly.</summary>
    public bool RequiresOtp { get; set; }

    /// <summary>Operator must complete an assisted-login session before any download can be triggered.</summary>
    public bool ManualLoginRequired { get; set; }

    /// <summary>Short HTML-safe instructions shown to the operator inside the assisted-login dialog (e.g. "Enter the OTP sent to the mobile-on-file. Do NOT share with any automated system.").</summary>
    public string? OtpInstructions { get; set; }

    /// <summary>How many minutes the captured Playwright session state is treated as valid for downloads (1..240). Defaults to 30 when null.</summary>
    public int? AssistedSessionTimeoutMinutes { get; set; }

    /// <summary>Internal notes (portal quirks, contacts, renewal dates).</summary>
    public string? Notes { get; set; }

    public ICollection<PortalDownloadAuditLog> PortalAuditLogs { get; set; } = new List<PortalDownloadAuditLog>();

    public ICollection<SourceIngestionAlert> IngestionAlerts { get; set; } = new List<SourceIngestionAlert>();

    public ICollection<PortalManualLoginSession> ManualLoginSessions { get; set; } = new List<PortalManualLoginSession>();

    // ───────── Public edition PDF discovery ─────────

    public bool PdfDiscoveryEnabled { get; set; }

    /// <summary>When true, AI selector suggestions may be requested for this source (also requires global AI/Bedrock configuration).</summary>
    public bool AiSelectorSuggestionEnabled { get; set; }

    /// <summary>When null, inherits global Auto AI Download Recovery setting.</summary>
    public bool? AutoAiRecoveryEnabled { get; set; }

    /// <summary>When true, public HTML article extraction may run for this source.</summary>
    public bool PublicHtmlExtractionEnabled { get; set; }

    /// <summary>When true, admins may generate an internal GFH article intelligence PDF (not an original newspaper PDF).</summary>
    public bool GenerateInternalReportAllowed { get; set; }

    public PdfDiscoveryMode PdfDiscoveryMode { get; set; } = PdfDiscoveryMode.Hybrid;

    public string? PdfDiscoveryPageUrl { get; set; }

    public string? PdfDownloadSelector { get; set; }

    public string? PdfLinkSelector { get; set; }

    public string? PdfLinkKeywords { get; set; }

    public string? PdfDatePattern { get; set; }

    public bool PreferTodayEdition { get; set; } = true;

    public bool PreferLatestEdition { get; set; } = true;

    public bool RequirePdfContentType { get; set; } = true;

    public int MinimumPdfSizeKb { get; set; } = 100;

    public DateTimeOffset? LastPdfDiscoveredAt { get; set; }

    public DateTimeOffset? LastPdfDownloadedAt { get; set; }

    public string? LastPdfUrl { get; set; }

    public string? LastSavedPdfPath { get; set; }

    public SourcePdfDiscoveryOutcome LastPdfDiscoveryOutcome { get; set; }

    public DateTimeOffset? LastPublicHtmlExtractedAt { get; set; }

    public string? LastInternalReportPath { get; set; }

    public PdfSelectorExpectedAction? PdfDownloadExpectedAction { get; set; }

    public PdfSelectorExpectedAction? PdfLinkExpectedAction { get; set; }

    public ICollection<PdfEditionDownload> PdfEditionDownloads { get; set; } = new List<PdfEditionDownload>();

    public ICollection<PdfSelectorSuggestion> PdfSelectorSuggestions { get; set; } = new List<PdfSelectorSuggestion>();
}
