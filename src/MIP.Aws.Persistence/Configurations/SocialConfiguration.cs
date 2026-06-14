using MIP.Aws.Domain.Entities.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class SocialPostConfiguration : IEntityTypeConfiguration<SocialPost>
{
    public void Configure(EntityTypeBuilder<SocialPost> builder)
    {
        builder.ToTable("SocialPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.InternalNotes).HasMaxLength(4000);
        builder.Property(x => x.LastFailureReason).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.ExecutiveBriefId);
        builder.HasIndex(x => x.ExtractedArticleId);
        builder.Property(x => x.ComplianceRiskLevel).HasMaxLength(32);
        builder.Property(x => x.ComplianceIssuesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.SourceType).HasConversion<int>();
    }
}

public sealed class SocialPostPlatformConfiguration : IEntityTypeConfiguration<SocialPostPlatform>
{
    public void Configure(EntityTypeBuilder<SocialPostPlatform> builder)
    {
        builder.ToTable("SocialPostPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.ExternalPostId).HasMaxLength(256);
        builder.Property(x => x.ExternalPostUrl).HasMaxLength(1024);
        builder.Property(x => x.Platform).HasConversion<int>();
        builder.Property(x => x.ContentVariant).HasConversion<int>();
        builder.Property(x => x.Headline).HasMaxLength(512);
        builder.Property(x => x.CallToAction).HasMaxLength(512);
        builder.Property(x => x.HashtagsJson).HasMaxLength(2000);
        builder.Property(x => x.MentionsJson).HasMaxLength(2000);
        builder.Property(x => x.ComplianceNotes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.SocialPostId, x.ContentVariant }).IsUnique();
        builder.HasOne(x => x.SocialPost).WithMany(x => x.Platforms).HasForeignKey(x => x.SocialPostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SocialPlatformAccount).WithMany().HasForeignKey(x => x.SocialPlatformAccountId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class SocialPostAttachmentConfiguration : IEntityTypeConfiguration<SocialPostAttachment>
{
    public void Configure(EntityTypeBuilder<SocialPostAttachment> builder)
    {
        builder.ToTable("SocialPostAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(512);
        builder.Property(x => x.StoragePath).HasMaxLength(1024);
        builder.Property(x => x.AttachmentKind).HasMaxLength(64);
        builder.Property(x => x.PreviewUrl).HasMaxLength(1024);
        builder.HasOne(x => x.SocialPost).WithMany(x => x.Attachments).HasForeignKey(x => x.SocialPostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SocialPostApprovalConfiguration : IEntityTypeConfiguration<SocialPostApproval>
{
    public void Configure(EntityTypeBuilder<SocialPostApproval> builder)
    {
        builder.ToTable("SocialPostApprovals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Comments).HasMaxLength(4000);
        builder.HasOne(x => x.SocialPost).WithMany(x => x.Approvals).HasForeignKey(x => x.SocialPostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SocialPostPublishLogConfiguration : IEntityTypeConfiguration<SocialPostPublishLog>
{
    public void Configure(EntityTypeBuilder<SocialPostPublishLog> builder)
    {
        builder.ToTable("SocialPostPublishLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(256);
        builder.Property(x => x.ExternalPostUrl).HasMaxLength(1024);
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.Property(x => x.Platform).HasConversion<int>();
        builder.HasOne(x => x.SocialPost).WithMany(x => x.PublishLogs).HasForeignKey(x => x.SocialPostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SocialPlatformAccountConfiguration : IEntityTypeConfiguration<SocialPlatformAccount>
{
    public void Configure(EntityTypeBuilder<SocialPlatformAccount> builder)
    {
        builder.ToTable("SocialPlatformAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Handle).HasMaxLength(128);
        builder.Property(x => x.AccountEmail).HasMaxLength(256);
        builder.Property(x => x.EnvironmentName).HasMaxLength(64);
        builder.Property(x => x.ExternalAccountId).HasMaxLength(256);
        builder.Property(x => x.ConnectionStatus).HasMaxLength(64);
        builder.Property(x => x.Scopes).HasMaxLength(512);
        builder.Property(x => x.LastConnectionError).HasMaxLength(2000);
        builder.Property(x => x.ProtectedTokenPayload).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProtectedRefreshTokenPayload).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LastTestOutcome).HasMaxLength(512);
        builder.Property(x => x.ConnectionHealth).HasMaxLength(64);
        builder.Property(x => x.OAuthState).HasMaxLength(256);
        builder.Property(x => x.Platform).HasConversion<int>();
        builder.Property(x => x.AccountType).HasConversion<int>();
        builder.HasIndex(x => new { x.Platform, x.IsEnabled });
    }
}
