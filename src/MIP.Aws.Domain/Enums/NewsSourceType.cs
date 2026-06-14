namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Ingestion channel for a publisher. Public web types honor robots.txt when paired with <see cref="ContentAcquisitionMode.PublicWebWithRobotsRespect"/>.
/// </summary>
public enum NewsSourceType
{
    /// <summary>RSS or Atom feed URL.</summary>
    Rss = 1,

    /// <summary>Public HTML pages (no subscriber session).</summary>
    PublicHtml = 2,

    /// <summary>Direct public PDF URL.</summary>
    PublicPdf = 3,

    /// <summary>Legacy API-style connector (extensibility).</summary>
    Api = 4,

    /// <summary>Licensed subscriber web portal; Playwright automation with stored credentials only.</summary>
    WebPortalLogin = 5,

    /// <summary>Artifacts supplied manually; automated download is disabled.</summary>
    ManualUpload = 6
}
