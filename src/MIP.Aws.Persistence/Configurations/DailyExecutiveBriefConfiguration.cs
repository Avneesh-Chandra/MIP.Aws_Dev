using MIP.Aws.Domain.Entities.Executive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class DailyExecutiveBriefConfiguration : IEntityTypeConfiguration<DailyExecutiveBrief>
{
    public void Configure(EntityTypeBuilder<DailyExecutiveBrief> builder)
    {
        builder.ToTable("DailyExecutiveBriefs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.PdfStoragePath).HasMaxLength(1024);
        builder.Property(x => x.HtmlStoragePath).HasMaxLength(1024);
        builder.Property(x => x.LastFailureReason).HasMaxLength(2000);
        builder.HasIndex(x => x.BriefDate).IsUnique();
        builder.HasIndex(x => new { x.Status, x.BriefDate });
    }
}

public sealed class DailyExecutiveBriefMarketSnapshotConfiguration : IEntityTypeConfiguration<DailyExecutiveBriefMarketSnapshot>
{
    public void Configure(EntityTypeBuilder<DailyExecutiveBriefMarketSnapshot> builder)
    {
        builder.ToTable("DailyExecutiveBriefMarketSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Market).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Exchange).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Ticker).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ClosingPrice).HasMaxLength(64);
        builder.Property(x => x.PreviousClosingPrice).HasMaxLength(64);
        builder.Property(x => x.ChangePercent).HasMaxLength(32);
        builder.Property(x => x.VolumeTraded).HasMaxLength(64);
        builder.HasIndex(x => new { x.DailyExecutiveBriefId, x.DisplayOrder });
        builder.HasOne(x => x.DailyExecutiveBrief).WithMany(x => x.MarketSnapshots)
            .HasForeignKey(x => x.DailyExecutiveBriefId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DailyExecutiveBriefItemConfiguration : IEntityTypeConfiguration<DailyExecutiveBriefItem>
{
    public void Configure(EntityTypeBuilder<DailyExecutiveBriefItem> builder)
    {
        builder.ToTable("DailyExecutiveBriefItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Headline).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SourceName).HasMaxLength(256);
        builder.Property(x => x.SourceUrl).HasMaxLength(1024);
        builder.Property(x => x.SectionType).HasConversion<int>();
        builder.HasIndex(x => new { x.DailyExecutiveBriefId, x.SectionType, x.DisplayOrder });
        builder.HasOne(x => x.DailyExecutiveBrief).WithMany(x => x.Items)
            .HasForeignKey(x => x.DailyExecutiveBriefId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DailyExecutiveBriefEmailLogConfiguration : IEntityTypeConfiguration<DailyExecutiveBriefEmailLog>
{
    public void Configure(EntityTypeBuilder<DailyExecutiveBriefEmailLog> builder)
    {
        builder.ToTable("DailyExecutiveBriefEmailLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(512).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.DailyExecutiveBriefId);
        builder.HasOne(x => x.DailyExecutiveBrief).WithMany(x => x.EmailLogs)
            .HasForeignKey(x => x.DailyExecutiveBriefId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DailyExecutiveBriefSettingsConfiguration : IEntityTypeConfiguration<DailyExecutiveBriefSettings>
{
    public void Configure(EntityTypeBuilder<DailyExecutiveBriefSettings> builder)
    {
        builder.ToTable("DailyExecutiveBriefSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToRecipients).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CcRecipients).HasColumnType("nvarchar(max)");
        builder.Property(x => x.BccRecipients).HasColumnType("nvarchar(max)");
        builder.Property(x => x.SendTimeLocal).HasMaxLength(8);
        builder.Property(x => x.GenerateTimeLocal).HasMaxLength(8);
        builder.Property(x => x.TimeZoneId).HasMaxLength(64);
    }
}
