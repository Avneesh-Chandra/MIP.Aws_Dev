using System.Globalization;
using System.Text.RegularExpressions;

namespace MIP.Aws.Application.Features.Operator;

/// <summary>Portal and API routes for operator PDF links (email + Download Monitor).</summary>
public static partial class OperatorLatestPdfLinks
{
    public const string PortalRoutePrefix = "/operator/sources";

    public static string BuildPortalPath(
        Guid sourceId,
        bool inline = true,
        Guid? fileId = null,
        DateOnly? editionDate = null)
    {
        var path = fileId is Guid id
            ? $"{PortalRoutePrefix}/{sourceId:D}/pdf/{id:D}"
            : $"{PortalRoutePrefix}/{sourceId:D}/latest-pdf";
        return path + BuildQuery(inline, editionDate);
    }

    public static string BuildPortalUrl(
        string portalBaseUrl,
        Guid sourceId,
        bool inline = true,
        Guid? fileId = null,
        DateOnly? editionDate = null) =>
        $"{NormalizePortalBaseUrl(portalBaseUrl)}{BuildPortalPath(sourceId, inline, fileId, editionDate)}";

    /// <summary>
    /// Email links must use the same scheme operators already use in the browser.
    /// Terraform defaults AdminPortalUrl to http://ALB, but DEV is opened as https://ALB —
    /// http links then miss the auth cookie and fail to open the PDF.
    /// </summary>
    public static string NormalizePortalBaseUrl(string? portalBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(portalBaseUrl))
        {
            return string.Empty;
        }

        var trimmed = portalBaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            return trimmed;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = uri.IsDefaultPort || uri.Port == 80 ? -1 : uri.Port
        };
        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static string BuildApiPath(
        Guid sourceId,
        bool inline = true,
        Guid? fileId = null,
        DateOnly? editionDate = null)
    {
        var path = fileId is Guid id
            ? $"/api/v1/operator/sources/{sourceId:D}/pdf/{id:D}"
            : $"/api/v1/operator/sources/{sourceId:D}/latest-pdf";
        return path + BuildQuery(inline, editionDate);
    }

    public static bool TryMapApiPathToPortal(string? path, string? queryString, out string portalPath)
    {
        portalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        Guid sourceId;
        Guid? fileId = null;
        var fileMatch = ApiPdfFilePathRegex().Match(trimmed);
        if (fileMatch.Success
            && Guid.TryParse(fileMatch.Groups["id"].Value, out sourceId)
            && Guid.TryParse(fileMatch.Groups["fileId"].Value, out var parsedFileId))
        {
            fileId = parsedFileId;
        }
        else
        {
            var latestMatch = ApiLatestPdfPathRegex().Match(trimmed);
            if (!latestMatch.Success || !Guid.TryParse(latestMatch.Groups["id"].Value, out sourceId))
            {
                return false;
            }
        }

        var inline = true;
        DateOnly? editionDate = null;
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            var qs = queryString.TrimStart('?');
            foreach (var part in qs.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = part[..eq];
                var value = Uri.UnescapeDataString(part[(eq + 1)..]);
                if (key.Equals("inline", StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(value, out var parsedInline))
                {
                    inline = parsedInline;
                }
                else if (key.Equals("edition", StringComparison.OrdinalIgnoreCase)
                         && DateOnly.TryParseExact(
                             value,
                             "yyyy-MM-dd",
                             CultureInfo.InvariantCulture,
                             DateTimeStyles.None,
                             out var parsedEdition))
                {
                    editionDate = parsedEdition;
                }
            }
        }

        portalPath = BuildPortalPath(sourceId, inline, fileId, editionDate);
        return true;
    }

    private static string BuildQuery(bool inline, DateOnly? editionDate)
    {
        var query = $"?inline={inline.ToString().ToLowerInvariant()}";
        if (editionDate is DateOnly edition)
        {
            query += $"&edition={edition:yyyy-MM-dd}";
        }

        return query;
    }

    [GeneratedRegex(
        @"^/api/v1/operator/sources/(?<id>[0-9a-fA-F-]{36})/latest-pdf/?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ApiLatestPdfPathRegex();

    [GeneratedRegex(
        @"^/api/v1/operator/sources/(?<id>[0-9a-fA-F-]{36})/pdf/(?<fileId>[0-9a-fA-F-]{36})/?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ApiPdfFilePathRegex();
}
