namespace MIP.Aws.Application.Security;

public static class BrowserNavigationHelper
{
    public static bool WantsHtmlPage(string? acceptHeader)
    {
        return string.IsNullOrWhiteSpace(acceptHeader)
               || acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
