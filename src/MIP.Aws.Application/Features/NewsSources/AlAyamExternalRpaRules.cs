using System.Security.Cryptography;
using System.Text;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Shared Al Ayam RPA ingest rules (API key, skip Playwright, waiting message).</summary>
public static class AlAyamExternalRpaRules
{
    public const string ApiKeyHeaderName = "X-Rpa-Api-Key";

    public const string WaitingForUploadMessage = "Waiting for RPA upload of today's Al Ayam PDF.";

    public static bool ShouldSkipPublisherFetch(AlAyamOutboundClientOptions options, NewsSource source) =>
        options.UseExternalRpa && AlAyamPublicPdfBaseline.IsSource(source);

    public static bool IsAllowedSource(Guid sourceId, Guid? configuredAllowedSourceId) =>
        configuredAllowedSourceId is null
        || configuredAllowedSourceId == Guid.Empty
        || configuredAllowedSourceId == sourceId;

    public static bool IsWaitingForUpload(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Contains(WaitingForUploadMessage, StringComparison.OrdinalIgnoreCase);

    public static bool ApiKeysMatch(string? configured, string? provided)
    {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(configured);
        var actual = Encoding.UTF8.GetBytes(provided);
        if (expected.Length != actual.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
