namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Declares how human-readable access rights are modeled for compliance and routing.
/// </summary>
public enum NewsSourceAccessMode
{
    Unspecified = 0,

    /// <summary>GFH-licensed subscriber portal; automation permitted only with valid credentials and explicit download UI.</summary>
    LicensedSubscriberPortal = 1,

    /// <summary>Open web (RSS/HTML/PDF) subject to robots and rate limits.</summary>
    PublicWeb = 2,

    /// <summary>Manual file drops only.</summary>
    ManualArtifactsOnly = 3
}
