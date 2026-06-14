using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Portal;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>
/// Canonical URL identity for news sources. Licensed portal editions are unique by <see cref="NewsSource.EditionUrl"/>,
/// not the shared publisher host in <see cref="NewsSource.BaseUrl"/>.
/// </summary>
public static class NewsSourceUrlRules
{
    public static string Normalize(string url) => url.Trim().TrimEnd('/');

    public static string ResolveBaseUrl(
        NewsSourceType sourceType,
        string baseUrl,
        string? editionUrl,
        string? portalStrategyKey)
    {
        var trimmedBase = baseUrl.Trim();
        if (sourceType == NewsSourceType.WebPortalLogin
            && !string.IsNullOrWhiteSpace(editionUrl)
            && string.Equals(
                PortalFieldMapper.NormalizeStrategyKey(portalStrategyKey),
                PortalStrategyKeys.PressReader,
                StringComparison.Ordinal))
        {
            return editionUrl.Trim();
        }

        return trimmedBase;
    }

    public static string GetUniquenessKey(
        NewsSourceType sourceType,
        string baseUrl,
        string? editionUrl,
        string? portalStrategyKey)
    {
        if (sourceType == NewsSourceType.WebPortalLogin && !string.IsNullOrWhiteSpace(editionUrl))
        {
            return Normalize(editionUrl);
        }

        return Normalize(baseUrl);
    }

    /// <summary>
    /// All URL identities that should be treated as the same licensed portal edition.
    /// PressReader rows may store the edition path on <see cref="NewsSource.BaseUrl"/> only.
    /// </summary>
    public static IReadOnlyList<string> GetIdentityKeys(
        NewsSourceType sourceType,
        string baseUrl,
        string? editionUrl,
        string? portalStrategyKey)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GetUniquenessKey(sourceType, baseUrl, editionUrl, portalStrategyKey)
        };

        if (sourceType == NewsSourceType.WebPortalLogin)
        {
            if (!string.IsNullOrWhiteSpace(editionUrl))
            {
                keys.Add(Normalize(editionUrl));
            }

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                keys.Add(Normalize(baseUrl));
            }

            var resolved = ResolveBaseUrl(sourceType, baseUrl, editionUrl, portalStrategyKey);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                keys.Add(Normalize(resolved));
            }
        }

        return keys.ToList();
    }

    public static bool IdentityChanged(
        NewsSourceType oldType,
        string oldBaseUrl,
        string? oldEditionUrl,
        string? oldPortalStrategyKey,
        NewsSourceType newType,
        string newBaseUrl,
        string? newEditionUrl,
        string? newPortalStrategyKey)
    {
        var oldKeys = GetIdentityKeys(oldType, oldBaseUrl, oldEditionUrl, oldPortalStrategyKey);
        var newKeys = GetIdentityKeys(newType, newBaseUrl, newEditionUrl, newPortalStrategyKey);
        if (oldKeys.Count != newKeys.Count)
        {
            return true;
        }

        return oldKeys.Except(newKeys, StringComparer.OrdinalIgnoreCase).Any()
               || newKeys.Except(oldKeys, StringComparer.OrdinalIgnoreCase).Any();
    }

    public static async Task EnsureUniqueAsync(
        IApplicationDbContext db,
        Guid? excludeId,
        NewsSourceType sourceType,
        string baseUrl,
        string? editionUrl,
        string? portalStrategyKey,
        CancellationToken cancellationToken)
    {
        var keys = GetIdentityKeys(sourceType, baseUrl, editionUrl, portalStrategyKey);
        var query = db.NewsSources.AsNoTracking().Where(x => !x.IsDeleted);
        if (excludeId is Guid id)
        {
            query = query.Where(x => x.Id != id);
        }

        var candidates = await query
            .Select(x => new { x.SourceType, x.BaseUrl, x.EditionUrl, x.PortalStrategyKey })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var duplicate = candidates.Any(x =>
            GetIdentityKeys(x.SourceType, x.BaseUrl, x.EditionUrl, x.PortalStrategyKey)
                .Any(candidateKey => keys.Contains(candidateKey, StringComparer.OrdinalIgnoreCase)));

        if (duplicate)
        {
            throw sourceType == NewsSourceType.WebPortalLogin && !string.IsNullOrWhiteSpace(editionUrl)
                ? new InvalidOperationException("Another news source already uses this edition URL.")
                : new InvalidOperationException("Another news source already uses this base URL.");
        }
    }
}
