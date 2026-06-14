using MIP.Aws.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>
/// Ensures operational commands only run against active (<see cref="Domain.Entities.NewsSource.IsEnabled"/>) sources.
/// </summary>
public static class NewsSourceGuard
{
    public static async Task EnsureEnabledAsync(
        IApplicationDbContext db,
        Guid newsSourceId,
        CancellationToken cancellationToken)
    {
        var row = await db.NewsSources.AsNoTracking()
            .Where(s => s.Id == newsSourceId && !s.IsDeleted)
            .Select(s => new { s.IsEnabled })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new InvalidOperationException("News source not found.");
        }

        if (!row.IsEnabled)
        {
            throw new InvalidOperationException(
                "This source is inactive. Set it to Active before running downloads, PDF discovery, or extraction.");
        }
    }
}
