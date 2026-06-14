using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Entities.Market;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MIP.Aws.Persistence.Identity;

/// <summary>Idempotent seed for GFH multi-listing instruments and exchange web provider configs.</summary>
public sealed class MarketGfhInstrumentsSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketDataOptions> marketOptions,
    ILogger<MarketGfhInstrumentsSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        await EnsureInstrumentAsync(db, "GFH_BH", "GFH", "GFH Financial Group", "Bahrain Bourse", "BH", "BHD", 0, cancellationToken).ConfigureAwait(false);
        await EnsureInstrumentAsync(db, "GFH_KW", "GFH", "GFH Financial Group", "Boursa Kuwait", "KW", "KWD", 1, cancellationToken).ConfigureAwait(false);
        await EnsureInstrumentAsync(db, "GFH_DFM", "GFH", "GFH Financial Group", "Dubai Financial Market", "AE", "AED", 2, cancellationToken).ConfigureAwait(false);
        await EnsureInstrumentAsync(db, "GFH_ADX", "GFH", "GFH Financial Group", "Abu Dhabi Securities Exchange", "AE", "AED", 3, cancellationToken).ConfigureAwait(false);

        var capture = marketOptions.Value.GfhShareCapture;
        await EnsureProviderConfigAsync(db, MarketDataProviderType.BoursaKuwait, "BoursaKuwait", capture.BoursaKuwait, cancellationToken).ConfigureAwait(false);
        await EnsureProviderConfigAsync(db, MarketDataProviderType.DubaiFinancialMarket, "DFM", capture.DubaiFinancialMarket, cancellationToken).ConfigureAwait(false);
        await EnsureProviderConfigAsync(db, MarketDataProviderType.AbuDhabiSecuritiesExchange, "ADX", capture.AbuDhabiSecuritiesExchange, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("GFH market instruments and exchange provider configs seeded.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureInstrumentAsync(
        IApplicationDbContext db,
        string internalCode,
        string symbol,
        string name,
        string exchange,
        string country,
        string currency,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var existing = await db.MarketInstruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol && i.Exchange == exchange && !i.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            db.MarketInstruments.Add(new MarketInstrument
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                Name = name,
                Exchange = exchange,
                Country = country,
                Currency = currency,
                InstrumentType = MarketInstrumentType.Equity,
                IsActive = true,
                IsGfhStock = true,
                IsFeaturedForExecutiveReport = true,
                DisplayOrder = displayOrder,
                Notes = internalCode,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.IsGfhStock = true;
            existing.IsFeaturedForExecutiveReport = true;
            existing.IsActive = true;
            existing.Notes = internalCode;
            existing.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureProviderConfigAsync(
        IApplicationDbContext db,
        MarketDataProviderType provider,
        string name,
        ExchangeWebPageOptions options,
        CancellationToken cancellationToken)
    {
        var existing = await db.MarketDataProviderConfigs
            .FirstOrDefaultAsync(c => c.Name == name && !c.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        var optionsJson = JsonSerializer.Serialize(options);
        if (existing is null)
        {
            db.MarketDataProviderConfigs.Add(new MarketDataProviderConfig
            {
                Id = Guid.NewGuid(),
                Name = name,
                Provider = provider,
                BaseUrl = options.SourceUrl,
                OptionsJson = optionsJson,
                IsActive = options.Enabled,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Provider = provider;
            existing.BaseUrl = options.SourceUrl;
            existing.OptionsJson = optionsJson;
            existing.IsActive = options.Enabled;
            existing.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
