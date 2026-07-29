namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Microsoft Graph application permissions for outbound mail (client credentials).
/// Requires Entra app permission <c>Mail.Send</c> (application) with admin consent,
/// and the ability to send as <see cref="SenderMailbox"/>.
/// </summary>
public sealed class MicrosoftGraphOptions
{
    public const string SectionName = "MicrosoftGraph";

    public bool Enabled { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret value. Prefer Secrets Manager / user-secrets — never commit.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Mailbox UPN used as the sender (e.g. gfhnews@gfh.com).</summary>
    public string SenderMailbox { get; set; } = string.Empty;

    /// <summary>Optional friendly name for From display (informational; Graph uses mailbox identity).</summary>
    public string SenderDisplayName { get; set; } = "GFH News";

    /// <summary>Save a copy in the sender mailbox Sent Items folder.</summary>
    public bool SaveToSentItems { get; set; } = true;

    public int MaxSendAttempts { get; set; } = 4;

    public int TimeoutSeconds { get; set; } = 60;

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(SenderMailbox);
}
