using System.Net;
using MIP.Aws.Application.Compliance;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Compliance;

public sealed class PublisherComplianceGate(IHttpClientFactory httpClientFactory) : IPublisherComplianceGate
{
    public async Task<ComplianceEvaluation> EvaluateAsync(Uri resource, ContentAcquisitionMode mode, CancellationToken cancellationToken)
    {
        if (mode is ContentAcquisitionMode.LicensedFeedOrApi
            or ContentAcquisitionMode.PartnerManagedConnector
            or ContentAcquisitionMode.LicensedWebPortalSubscriber)
        {
            return new ComplianceEvaluation(true, "LICENSED_CHANNEL", "Licensed acquisition path; robots.txt checks are not required.");
        }

        var client = httpClientFactory.CreateClient("compliance");
        var robotsUri = new Uri(resource.GetLeftPart(UriPartial.Authority) + "/robots.txt");

        using var response = await client.GetAsync(robotsUri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ComplianceEvaluation(true, "ROBOTS_MISSING", "robots.txt not present; treating as allow-all per common crawler practice.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new ComplianceEvaluation(false, "ROBOTS_UNAVAILABLE", $"robots.txt returned {(int)response.StatusCode}; defaulting to deny for public-web mode.");
        }

        var robotsBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var path = resource.AbsolutePath;
        foreach (var line in robotsBody.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rule = line["Disallow:".Length..].Trim();
            if (string.IsNullOrEmpty(rule))
            {
                continue;
            }

            if (rule == "/")
            {
                return new ComplianceEvaluation(false, "ROBOTS_DISALLOW_ALL", "robots.txt disallows all paths for this host.");
            }

            if (path.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
            {
                return new ComplianceEvaluation(false, "ROBOTS_DISALLOW_PATH", $"robots.txt disallows path prefix '{rule}'.");
            }
        }

        return new ComplianceEvaluation(true, "ROBOTS_ALLOW", "robots.txt evaluation passed with conservative rules.");
    }
}
