namespace MIP.Aws.Application.Configuration;

/// <summary>Development / staging guardrails to prevent accidental bulk mail.</summary>
public sealed class EmailSafetyOptions
{
    public const string SectionName = "EmailSafety";

    public bool Enabled { get; set; }

    public string RedirectAllTo { get; set; } = string.Empty;

    public string[] AllowedDomains { get; set; } = [];

    public string PrefixSubject { get; set; } = "[GFH-MIP-TEST]";
}
