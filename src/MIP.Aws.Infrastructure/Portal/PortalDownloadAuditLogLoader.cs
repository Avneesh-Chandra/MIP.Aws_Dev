using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>
/// Loads a small recent slice of portal audit rows. A full job can have tens of thousands of
/// Playwright console/network events; ordering those without a bound times out RDS.
/// </summary>
internal static class PortalDownloadAuditLogLoader
{
    public static IQueryable<PortalDownloadAuditLog> RecentForJob(
        IApplicationDbContext db,
        Guid downloadJobId,
        DateTimeOffset? notBefore = null)
    {
        var query = db.PortalDownloadAuditLogs.AsNoTracking()
            .Where(a => a.DownloadJobId == downloadJobId);

        if (notBefore is DateTimeOffset minCreatedAt)
        {
            query = query.Where(a => a.CreatedAt >= minCreatedAt);
        }

        return query
            .OrderByDescending(a => a.CreatedAt)
            .Take(PortalAuditArtifactResolver.MaxLogsPerJob);
    }
}
