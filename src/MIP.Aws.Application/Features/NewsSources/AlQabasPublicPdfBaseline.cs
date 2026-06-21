namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Known-good public PDF settings for Kuwait Al Qabas (homepage archive link on d.alqabas.com).</summary>
public static class AlQabasPublicPdfBaseline
{
    public const string SourceName = "Kuwait - Al Qabas";
    public const string ConnectorKey = "news.alqabas";
    public const string BaseUrl = "https://alqabas.com/";
    public const string EditionUrl = "https://d.alqabas.com/archive";
    public const string PdfDiscoveryPageUrl = "https://alqabas.com/";
    public const string PdfLinkSelector = "a[href*='d.alqabas.com/archive']";
    public const string PdfLinkKeywords =
        "pdf,download,edition,e-paper,archive,تحميل,آخر عدد,PDF,العدد,d.alqabas.com";
}
