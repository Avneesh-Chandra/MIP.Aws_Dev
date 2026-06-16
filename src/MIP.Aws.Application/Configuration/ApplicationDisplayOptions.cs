namespace MIP.Aws.Application.Configuration;

/// <summary>UI and email display preferences shared across Blazor and server-rendered reports.</summary>
public sealed class ApplicationDisplayOptions
{
    public const string SectionName = "Application";

    /// <summary>IANA time zone for all user-visible timestamps (default: Bahrain operations).</summary>
    public string DisplayTimeZoneId { get; set; } = "Asia/Bahrain";
}
