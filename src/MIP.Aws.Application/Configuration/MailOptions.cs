namespace MIP.Aws.Application.Configuration;

public enum MailActiveProvider
{
    AzureCommunicationServices,
    MicrosoftGraph,
    Smtp
}

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public MailActiveProvider ActiveProvider { get; set; } = MailActiveProvider.AzureCommunicationServices;

    public bool DevelopmentSafetyEnabled { get; set; } = true;

    public string RedirectAllTo { get; set; } = string.Empty;

    public string SubjectPrefix { get; set; } = "[GFH-MIP-TEST]";

    public string[] AllowedDomains { get; set; } = [];
}
