using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>
/// UAE Al Khaleej and Economy share one PressReader subscriber — mirror encrypted credentials to the sibling edition.
/// </summary>
public static class DarAlKhaleejPressReaderCredentialSync
{
    public static async Task MirrorToSiblingEditionAsync(
        IApplicationDbContext db,
        NewsSource updated,
        CancellationToken cancellationToken)
    {
        if (updated.Credential?.ProtectedCredentialPayload is null
            || string.IsNullOrWhiteSpace(updated.PortalUsername))
        {
            return;
        }

        if (!DarAlKhaleejPressReaderBaseline.IsPressReaderSource(
                updated.ConnectorKey,
                updated.PortalStrategyKey,
                updated.EditionUrl,
                updated.BaseUrl))
        {
            return;
        }

        var candidates = await db.NewsSources
            .Include(s => s.Credential)
            .Where(s => !s.IsDeleted
                        && s.Id != updated.Id
                        && s.SourceType == NewsSourceType.WebPortalLogin)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var siblings = candidates
            .Where(s => ContainsPressReaderHost(s.EditionUrl) || ContainsPressReaderHost(s.BaseUrl))
            .ToList();

        foreach (var sibling in siblings)
        {
            if (!DarAlKhaleejPressReaderBaseline.IsPressReaderSource(
                    sibling.ConnectorKey,
                    sibling.PortalStrategyKey,
                    sibling.EditionUrl,
                    sibling.BaseUrl))
            {
                continue;
            }

            sibling.PortalUsername = updated.PortalUsername;
            sibling.ModifiedAt = DateTimeOffset.UtcNow;

            if (sibling.Credential is null)
            {
                sibling.Credential = new SourceCredential
                {
                    Id = Guid.NewGuid(),
                    NewsSourceId = sibling.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.SourceCredentials.Add(sibling.Credential);
            }

            sibling.Credential.ProtectedCredentialPayload = updated.Credential.ProtectedCredentialPayload;
            sibling.Credential.ModifiedAt = DateTimeOffset.UtcNow;
        }
    }

    private static bool ContainsPressReaderHost(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.Contains("pressreader.com", StringComparison.OrdinalIgnoreCase);
}
