namespace MIP.Aws.Application.Configuration;

/// <summary>Microsoft Graph application permissions for outbound mail.</summary>
public sealed class MicrosoftGraphOptions
{
    public const string SectionName = "MicrosoftGraph";

    public bool Enabled { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Mailbox UPN used as the sender (must be licensed for Graph mail send).</summary>
    public string SenderMailbox { get; set; } = string.Empty;

    public int MaxSendAttempts { get; set; } = 4;
}
