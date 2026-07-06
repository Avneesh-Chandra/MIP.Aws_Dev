using System.Net;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Email-client-safe HTML anchors (Gmail often strips href from button-styled links).</summary>
public static class EmailHtmlLinkFormatter
{
    public const int MaxMailToHrefLength = 2048;

    public static string Link(string? href, string label)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return WebUtility.HtmlEncode(label);
        }

        return $"<a href=\"{EncodeHrefAttribute(href)}\" target=\"_blank\" rel=\"noopener noreferrer\" style=\"color:#1d4ed8;text-decoration:underline;font-size:12px;font-weight:600;\">{WebUtility.HtmlEncode(label)}</a>";
    }

    internal static string EncodeHrefAttribute(string href)
    {
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            // Gmail/Outlook: keep & between mailto query params; HtmlEncode breaks many clients.
            return href.Replace("\"", "%22", StringComparison.Ordinal);
        }

        return WebUtility.HtmlEncode(href);
    }
}
