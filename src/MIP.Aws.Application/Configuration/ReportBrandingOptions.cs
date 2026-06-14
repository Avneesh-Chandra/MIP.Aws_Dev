namespace MIP.Aws.Application.Configuration;

/// <summary>Corporate branding applied to PDF/HTML reports.</summary>
public sealed class ReportBrandingOptions
{
    public const string SectionName = "ReportBranding";

    public string OrganizationName { get; set; } = "GFH Financial Group";

    public string ConfidentialityFooter { get; set; } =
        "Confidential — For authorized GFH personnel only. Contains licensed third-party content processed under GFH agreements.";

    public string PrimaryColorHex { get; set; } = "#0A2342";

    public string AccentColorHex { get; set; } = "#C5A572";
}
