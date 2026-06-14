namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Declares how content may legally be acquired for a source (configuration-driven compliance gate).
/// </summary>
public enum ContentAcquisitionMode
{
    LicensedFeedOrApi = 1,
    PublicWebWithRobotsRespect = 2,
    PartnerManagedConnector = 3,

    /// <summary>
    /// Licensed newspaper website accessed with GFH-provided subscriber credentials; robots.txt checks are not applied to authenticated portal flows.
    /// </summary>
    LicensedWebPortalSubscriber = 4
}
