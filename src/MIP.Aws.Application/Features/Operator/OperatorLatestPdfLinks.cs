using System.Text.RegularExpressions;

namespace MIP.Aws.Application.Features.Operator;

/// <summary>Portal and API routes for operator latest-PDF links (email + Download Monitor).</summary>
public static partial class OperatorLatestPdfLinks
{
    public const string PortalRoutePrefix = "/operator/sources";

    public static string BuildPortalPath(Guid sourceId, bool inline = true) =>
        $"{PortalRoutePrefix}/{sourceId:D}/latest-pdf?inline={inline.ToString().ToLowerInvariant()}";

    public static string BuildPortalUrl(string portalBaseUrl, Guid sourceId, bool inline = true) =>
        $"{portalBaseUrl.TrimEnd('/')}{BuildPortalPath(sourceId, inline)}";

    public static string BuildApiPath(Guid sourceId, bool inline = true) =>
        $"/api/v1/operator/sources/{sourceId:D}/latest-pdf?inline={inline.ToString().ToLowerInvariant()}";

    public static bool TryMapApiPathToPortal(string? path, string? queryString, out string portalPath)
    {
        portalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var match = ApiLatestPdfPathRegex().Match(path.Trim());
        if (!match.Success || !Guid.TryParse(match.Groups["id"].Value, out var sourceId))
        {
            return false;
        }

        var inline = true;
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            var qs = queryString.TrimStart('?');
            foreach (var part in qs.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith("inline=", StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(part["inline=".Length..], out var parsed))
                {
                    inline = parsed;
                }
            }
        }

        portalPath = BuildPortalPath(sourceId, inline);
        return true;
    }

    [GeneratedRegex(
        @"^/api/v1/operator/sources/(?<id>[0-9a-fA-F-]{36})/latest-pdf/?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ApiLatestPdfPathRegex();
}
