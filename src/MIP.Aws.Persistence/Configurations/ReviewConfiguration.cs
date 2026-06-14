using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Entities.Review;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class ArticleReviewActionConfiguration : IEntityTypeConfiguration<ArticleReviewAction>
{
    public void Configure(EntityTypeBuilder<ArticleReviewAction> builder)
    {
        builder.ToTable("ArticleReviewActions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(4000);
        builder.Property(x => x.OverridesJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ExtractedArticleId, x.CreatedAt });
        builder.HasIndex(x => new { x.ActorUserId, x.CreatedAt });
        builder.HasOne(x => x.ExtractedArticle)
            .WithMany(x => x.ReviewActions)
            .HasForeignKey(x => x.ExtractedArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticleReviewCommentConfiguration : IEntityTypeConfiguration<ArticleReviewComment>
{
    public void Configure(EntityTypeBuilder<ArticleReviewComment> builder)
    {
        builder.ToTable("ArticleReviewComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AuthorEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.MentionedUserIds).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ExtractedArticleId, x.CreatedAt });
        builder.HasOne(x => x.ExtractedArticle)
            .WithMany(x => x.ReviewComments)
            .HasForeignKey(x => x.ExtractedArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment)
            .WithMany()
            .HasForeignKey(x => x.ParentCommentId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticleAnnotationConfiguration : IEntityTypeConfiguration<ArticleAnnotation>
{
    public void Configure(EntityTypeBuilder<ArticleAnnotation> builder)
    {
        builder.ToTable("ArticleAnnotations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(128);
        builder.Property(x => x.Note).HasMaxLength(4000);
        builder.Property(x => x.AnchorText).HasMaxLength(1000);
        builder.Property(x => x.AuthorEmail).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.ExtractedArticleId, x.CreatedAt });
        builder.HasOne(x => x.ExtractedArticle)
            .WithMany(x => x.Annotations)
            .HasForeignKey(x => x.ExtractedArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ReviewAssignmentConfiguration : IEntityTypeConfiguration<ReviewAssignment>
{
    public void Configure(EntityTypeBuilder<ReviewAssignment> builder)
    {
        builder.ToTable("ReviewAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssigneeEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.AssignedByEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.AssigneeUserId, x.Status });
        builder.HasIndex(x => new { x.ExtractedArticleId, x.Status });
        builder.HasOne(x => x.ExtractedArticle)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.ExtractedArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ExecutiveQueueItemConfiguration : IEntityTypeConfiguration<ExecutiveQueueItem>
{
    public void Configure(EntityTypeBuilder<ExecutiveQueueItem> builder)
    {
        builder.ToTable("ExecutiveQueueItems");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ExtractedArticleId).IsUnique();
        builder.HasIndex(x => new { x.Priority, x.DisplayOrder });
        builder.Property(x => x.EscalatedByEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExecutiveNote).HasMaxLength(4000);
        builder.Property(x => x.Recommendation).HasMaxLength(4000);
        builder.HasOne(x => x.ExtractedArticle)
            .WithOne(x => x.ExecutiveQueueItem)
            .HasForeignKey<ExecutiveQueueItem>(x => x.ExtractedArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PublishedToBrief)
            .WithMany()
            .HasForeignKey(x => x.PublishedToBriefId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ExecutiveBriefConfiguration : IEntityTypeConfiguration<ExecutiveBrief>
{
    public void Configure(EntityTypeBuilder<ExecutiveBrief> builder)
    {
        builder.ToTable("ExecutiveBriefs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Subtitle).HasMaxLength(512);
        builder.Property(x => x.IntroNarrative).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ClosingNotes).HasColumnType("nvarchar(max)");
        builder.Property(x => x.PublishedByEmail).HasMaxLength(256);
        builder.Property(x => x.RenderedPdfRelativePath).HasMaxLength(2048);
        builder.Property(x => x.RenderedHtmlRelativePath).HasMaxLength(2048);
        builder.HasIndex(x => new { x.Status, x.PublishedAt });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ExecutiveBriefItemConfiguration : IEntityTypeConfiguration<ExecutiveBriefItem>
{
    public void Configure(EntityTypeBuilder<ExecutiveBriefItem> builder)
    {
        builder.ToTable("ExecutiveBriefItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Commentary).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ExecutiveBriefId, x.DisplayOrder });
        builder.HasIndex(x => new { x.ExecutiveBriefId, x.ExtractedArticleId }).IsUnique();
        builder.HasOne(x => x.ExecutiveBrief)
            .WithMany(b => b.Items)
            .HasForeignKey(x => x.ExecutiveBriefId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ExtractedArticle)
            .WithMany()
            .HasForeignKey(x => x.ExtractedArticleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ExtractedArticleReviewFieldsConfiguration : IEntityTypeConfiguration<ExtractedArticle>
{
    public void Configure(EntityTypeBuilder<ExtractedArticle> builder)
    {
        builder.Property(x => x.AnalystHeadline).HasMaxLength(1024);
        builder.Property(x => x.AnalystSummary).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AnalystSentiment).HasMaxLength(32);
        builder.Property(x => x.AnalystTagsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.ReviewState);
        builder.HasIndex(x => new { x.ReviewState, x.AssignedReviewerId });
    }
}
