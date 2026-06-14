using MIP.Aws.Domain.Entities.Market;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class MarketInstrumentConfiguration : IEntityTypeConfiguration<MarketInstrument>
{
    public void Configure(EntityTypeBuilder<MarketInstrument> builder)
    {
        builder.ToTable("MarketInstruments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Exchange).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Sector).HasMaxLength(128);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.InstrumentType).HasConversion<int>();
        builder.HasIndex(x => new { x.Symbol, x.Exchange }).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder });
        builder.HasIndex(x => new { x.IsFeaturedForExecutiveReport, x.IsActive });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class MarketPriceSnapshotConfiguration : IEntityTypeConfiguration<MarketPriceSnapshot>
{
    public void Configure(EntityTypeBuilder<MarketPriceSnapshot> builder)
    {
        builder.ToTable("MarketPriceSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(512);
        builder.Property(x => x.SourceUrl).HasMaxLength(2048);
        builder.Property(x => x.RawPayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.MarketStatus).HasMaxLength(64);
        builder.Property(x => x.NormalizationNotes).HasMaxLength(2000);
        builder.Property(x => x.AnalystNote).HasMaxLength(2000);
        builder.Property(x => x.SourceProvider).HasConversion<int>();

        builder.Property(x => x.Open).HasPrecision(18, 6);
        builder.Property(x => x.High).HasPrecision(18, 6);
        builder.Property(x => x.Low).HasPrecision(18, 6);
        builder.Property(x => x.Close).HasPrecision(18, 6);
        builder.Property(x => x.PreviousClose).HasPrecision(18, 6);
        builder.Property(x => x.Change).HasPrecision(18, 6);
        builder.Property(x => x.ChangePercent).HasPrecision(9, 4);
        builder.Property(x => x.Volume).HasPrecision(20, 4);

        builder.HasOne(x => x.Instrument)
            .WithMany(x => x.Snapshots)
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ImportJob)
            .WithMany(x => x.ImportedSnapshots)
            .HasForeignKey(x => x.ImportJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.InstrumentId, x.TradeDate, x.SourceProvider }).IsUnique();
        builder.HasIndex(x => x.TradeDate);
        builder.HasIndex(x => new { x.IsVolatile, x.TradeDate });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class MarketDataImportJobConfiguration : IEntityTypeConfiguration<MarketDataImportJob>
{
    public void Configure(EntityTypeBuilder<MarketDataImportJob> builder)
    {
        builder.ToTable("MarketDataImportJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ProviderConfigName).HasMaxLength(128);
        builder.Property(x => x.OriginalFileName).HasMaxLength(512);
        builder.Property(x => x.StoredArtifactKey).HasMaxLength(1024);
        builder.Property(x => x.InitiatedByEmail).HasMaxLength(320);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.RowErrorsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.Provider, x.StartedAt });
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class MarketDataProviderConfigConfiguration : IEntityTypeConfiguration<MarketDataProviderConfig>
{
    public void Configure(EntityTypeBuilder<MarketDataProviderConfig> builder)
    {
        builder.ToTable("MarketDataProviderConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(2048);
        builder.Property(x => x.AuthSecretReference).HasMaxLength(256);
        builder.Property(x => x.Schedule).HasMaxLength(128);
        builder.Property(x => x.Provider).HasConversion<int>();
        builder.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LastFailureMessage).HasMaxLength(2000);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.IsActive });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class MarketMovementSummaryConfiguration : IEntityTypeConfiguration<MarketMovementSummary>
{
    public void Configure(EntityTypeBuilder<MarketMovementSummary> builder)
    {
        builder.ToTable("MarketMovementSummaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).HasMaxLength(64).IsRequired();
        builder.Property(x => x.MaxGainSymbol).HasMaxLength(32);
        builder.Property(x => x.MaxLossSymbol).HasMaxLength(32);
        builder.Property(x => x.AverageChangePercent).HasPrecision(9, 4);
        builder.Property(x => x.MaxGainPercent).HasPrecision(9, 4);
        builder.Property(x => x.MaxLossPercent).HasPrecision(9, 4);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Commentary).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.TradeDate, x.Scope }).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
