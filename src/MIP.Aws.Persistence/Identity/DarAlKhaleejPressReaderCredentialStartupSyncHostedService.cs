using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Persistence.Identity;

/// <summary>
/// On startup, mirror PressReader credentials between UAE Al Khaleej editions so sibling rows stay in sync after deploy.
/// </summary>
public sealed class DarAlKhaleejPressReaderCredentialStartupSyncHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DarAlKhaleejPressReaderCredentialStartupSyncHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var sources = await db.NewsSources
            .Include(s => s.Credential)
            .Where(s => !s.IsDeleted
                        && s.SourceType == NewsSourceType.WebPortalLogin
                        && s.Credential != null
                        && s.Credential.ProtectedCredentialPayload != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var mirrored = 0;
        foreach (var source in sources)
        {
            if (!DarAlKhaleejPressReaderBaseline.IsPressReaderSource(
                    source.ConnectorKey,
                    source.PortalStrategyKey,
                    source.EditionUrl,
                    source.BaseUrl))
            {
                continue;
            }

            await DarAlKhaleejPressReaderCredentialSync.MirrorToSiblingEditionAsync(
                    db,
                    source,
                    cancellationToken)
                .ConfigureAwait(false);
            mirrored++;
        }

        if (db is DbContext context && context.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Mirrored Dar Al Khaleej PressReader credentials from {Count} source(s) to sibling edition(s).",
                mirrored);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
