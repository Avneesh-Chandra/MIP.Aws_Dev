namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Direct SMTP outbound configuration. This is the testing / fallback channel used when
/// Microsoft Graph application permissions are not yet provisioned. When <see cref="Enabled"/>
/// is true the SMTP service replaces the Graph mail sender at the DI layer.
/// </summary>
/// <remarks>
/// For Microsoft 365 the standard values are <c>Host=smtp.office365.com</c>, <c>Port=587</c>,
/// <c>UseStartTls=true</c>. The mailbox must have <em>SMTP AUTH</em> enabled and, if the tenant
/// enforces multi-factor auth, an App Password is required (Basic Auth via SMTP is blocked
/// otherwise). Never commit the password to git — supply it through environment, Key Vault,
/// or App Service application settings.
/// </remarks>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Master kill-switch. When true the SMTP sender wins over Graph.</summary>
    public bool Enabled { get; set; }

    /// <summary>SMTP relay host (e.g. <c>smtp.office365.com</c>, <c>smtp-mail.outlook.com</c>, <c>smtp.gmail.com</c>).</summary>
    public string Host { get; set; } = "smtp.office365.com";

    /// <summary>SMTP port (587 for STARTTLS, 465 for implicit TLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>If true, the client issues STARTTLS after the plain handshake (port 587).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>If true, the client opens an implicit TLS socket (port 465).</summary>
    public bool UseSsl { get; set; }

    /// <summary>SMTP AUTH user. For Microsoft 365 this is the mailbox UPN.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>SMTP AUTH password or App Password. Inject through environment / Key Vault — never commit.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The "From" address of outbound mail. Usually equals <see cref="Username"/>.</summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>Alias for SenderEmail (Azure App Service: Smtp__FromEmail).</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Optional friendly name attached to the "From" header.</summary>
    public string SenderDisplayName { get; set; } = "GFH Media Intelligence";

    /// <summary>Alias for SenderDisplayName (Azure App Service: Smtp__FromName).</summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>Per-attempt SMTP timeout (seconds).</summary>
    public int TimeoutSeconds { get; set; } = 60;

    public string ResolvedFromEmail =>
        !string.IsNullOrWhiteSpace(FromEmail) ? FromEmail.Trim()
        : !string.IsNullOrWhiteSpace(SenderEmail) ? SenderEmail.Trim()
        : Username.Trim();

    public string ResolvedFromName =>
        !string.IsNullOrWhiteSpace(FromName) ? FromName.Trim()
        : !string.IsNullOrWhiteSpace(SenderDisplayName) ? SenderDisplayName.Trim()
        : "GFH Media Intelligence";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(ResolvedFromEmail);
}
