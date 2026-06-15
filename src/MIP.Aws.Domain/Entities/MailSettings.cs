using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>Runtime mail provider settings (singleton row).</summary>
public sealed class MailSettings : AuditableEntity
{
    public string ActiveProvider { get; set; } = "AzureCommunicationServices";

    public bool DevelopmentSafetyEnabled { get; set; } = true;

    public string? RedirectAllTo { get; set; }

    public string SubjectPrefix { get; set; } = "[GFH-MIP-TEST]";

    public string? StatusEmailRecipient { get; set; }

    public bool? StatusEmailEnabled { get; set; }

    public bool? MailAutomationEnabled { get; set; }
}
