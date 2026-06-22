using MIP.Aws.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MIP.Aws.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SourceCategoryConfiguration : IEntityTypeConfiguration<SourceCategory>
{
    public void Configure(EntityTypeBuilder<SourceCategory> builder)
    {
        builder.ToTable("SourceCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class NewsSourceConfiguration : IEntityTypeConfiguration<NewsSource>
{
    public void Configure(EntityTypeBuilder<NewsSource> builder)
    {
        builder.ToTable("NewsSources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ConnectorKey).HasMaxLength(128);
        builder.Property(x => x.DefaultLanguage).HasMaxLength(16);
        builder.Property(x => x.Country).HasMaxLength(128);
        builder.Property(x => x.LoginUrl).HasMaxLength(2048);
        builder.Property(x => x.EditionUrl).HasMaxLength(2048);
        builder.Property(x => x.LogoutUrl).HasMaxLength(2048);
        builder.Property(x => x.PortalUsername).HasMaxLength(256);
        builder.Property(x => x.UsernameSelector).HasMaxLength(512);
        builder.Property(x => x.PasswordSelector).HasMaxLength(512);
        builder.Property(x => x.SubmitSelector).HasMaxLength(512);
        builder.Property(x => x.DownloadSelector).HasMaxLength(512);
        builder.Property(x => x.PortalStrategyKey).HasMaxLength(64);
        builder.Property(x => x.LoginIconSelector).HasMaxLength(512);
        builder.Property(x => x.NewspaperCanvasSelector).HasMaxLength(512);
        builder.Property(x => x.ContextMenuSelector).HasMaxLength(512);
        builder.Property(x => x.DownloadMenuItemSelector).HasMaxLength(512);
        builder.Property(x => x.LoginSuccessSelector).HasMaxLength(512);
        builder.Property(x => x.SuccessUrlPattern).HasMaxLength(512);
        builder.Property(x => x.OtpInstructions).HasMaxLength(2000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.PdfDiscoveryPageUrl).HasMaxLength(2048);
        builder.Property(x => x.PdfDownloadSelector).HasMaxLength(512);
        builder.Property(x => x.PdfLinkSelector).HasMaxLength(512);
        builder.Property(x => x.PdfLinkKeywords).HasMaxLength(2000);
        builder.Property(x => x.PdfDatePattern).HasMaxLength(256);
        builder.Property(x => x.LastPdfUrl).HasMaxLength(2048);
        builder.Property(x => x.LastSavedPdfPath).HasMaxLength(2048);
        builder.Property(x => x.LastInternalReportPath).HasMaxLength(2048);
        builder.Property(x => x.PdfDiscoveryMode).HasConversion<int>();
        builder.Property(x => x.PdfDownloadExpectedAction).HasConversion<int>();
        builder.Property(x => x.PdfLinkExpectedAction).HasConversion<int>();
        builder.Property(x => x.LastPdfDiscoveryOutcome).HasConversion<int>();
        builder.HasIndex(x => new { x.TenantId, x.BaseUrl });
        builder.HasOne(x => x.SourceCategory).WithMany(x => x.NewsSources).HasForeignKey(x => x.SourceCategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class PortalManualLoginSessionConfiguration : IEntityTypeConfiguration<PortalManualLoginSession>
{
    public void Configure(EntityTypeBuilder<PortalManualLoginSession> builder)
    {
        builder.ToTable("PortalManualLoginSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedByEmail).HasMaxLength(320);
        builder.Property(x => x.SessionArtifactRelativePath).HasMaxLength(2048);
        builder.Property(x => x.FailureCode).HasMaxLength(128);
        builder.Property(x => x.FailureReason).HasMaxLength(4000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.NewsSourceId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.NewsSourceId, x.ExpiresAt });
        builder.HasOne(x => x.NewsSource).WithMany(x => x.ManualLoginSessions).HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SourceCredentialConfiguration : IEntityTypeConfiguration<SourceCredential>
{
    public void Configure(EntityTypeBuilder<SourceCredential> builder)
    {
        builder.ToTable("SourceCredentials");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.NewsSourceId).IsUnique();
        builder.Property(x => x.KeyVaultSecretName).HasMaxLength(256);
        builder.Property(x => x.ApiKeyHeaderName).HasMaxLength(128);
        builder.Property(x => x.ProtectedCredentialPayload).HasColumnType("nvarchar(max)");
        builder.HasOne(x => x.NewsSource).WithOne(x => x.Credential).HasForeignKey<SourceCredential>(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class DownloadScheduleConfiguration : IEntityTypeConfiguration<DownloadSchedule>
{
    public void Configure(EntityTypeBuilder<DownloadSchedule> builder)
    {
        builder.ToTable("DownloadSchedules");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.NewsSourceId).IsUnique();
        builder.Property(x => x.CronExpression).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(128).IsRequired();
        builder.HasOne(x => x.NewsSource).WithOne(x => x.DownloadSchedule).HasForeignKey<DownloadSchedule>(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class DownloadJobConfiguration : IEntityTypeConfiguration<DownloadJob>
{
    public void Configure(EntityTypeBuilder<DownloadJob> builder)
    {
        builder.ToTable("DownloadJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Trigger).HasConversion<int>();
        builder.HasIndex(x => new { x.NewsSourceId, x.Status, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany(x => x.DownloadJobs).HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class AutoAiRecoveryRunConfiguration : IEntityTypeConfiguration<AutoAiRecoveryRun>
{
    public void Configure(EntityTypeBuilder<AutoAiRecoveryRun> builder)
    {
        builder.ToTable("AutoAiRecoveryRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Trigger).HasConversion<int>();
        builder.Property(x => x.TimelineJson).IsRequired();
        builder.Property(x => x.ResultSummary).HasMaxLength(4000);
        builder.Property(x => x.SuccessfulOptionTitle).HasMaxLength(512);
        builder.HasIndex(x => new { x.NewsSourceId, x.CreatedAt });
        builder.HasIndex(x => x.FailedDownloadJobId);
        builder.HasOne(x => x.NewsSource).WithMany().HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FailedDownloadJob).WithMany().HasForeignKey(x => x.FailedDownloadJobId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.SourceRecoveryAttempt).WithMany().HasForeignKey(x => x.SourceRecoveryAttemptId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class AutoAiDownloadRecoverySettingsConfiguration : IEntityTypeConfiguration<AutoAiDownloadRecoverySettings>
{
    public void Configure(EntityTypeBuilder<AutoAiDownloadRecoverySettings> builder)
    {
        builder.ToTable("AutoAiDownloadRecoverySettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaximumRiskAllowed).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class DownloadedFileConfiguration : IEntityTypeConfiguration<DownloadedFile>
{
    public void Configure(EntityTypeBuilder<DownloadedFile> builder)
    {
        builder.ToTable("DownloadedFiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OriginalUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.BlobUri).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(128);
        builder.HasIndex(x => x.DownloadJobId);
        builder.HasOne(x => x.DownloadJob).WithMany(x => x.Files).HasForeignKey(x => x.DownloadJobId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class PdfEditionDownloadConfiguration : IEntityTypeConfiguration<PdfEditionDownload>
{
    public void Configure(EntityTypeBuilder<PdfEditionDownload> builder)
    {
        builder.ToTable("PdfEditionDownloads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.SavedPath).HasMaxLength(2048);
        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Sha256Hash).HasMaxLength(128);
        builder.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(4000);
        builder.Property(x => x.DiscoveryMethod).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.NewsSourceId, x.EditionDate, x.Status });
        builder.HasOne(x => x.NewsSource).WithMany(x => x.PdfEditionDownloads).HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DownloadJob).WithMany().HasForeignKey(x => x.DownloadJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DownloadedFile).WithMany().HasForeignKey(x => x.DownloadedFileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class PdfSelectorSuggestionConfiguration : IEntityTypeConfiguration<PdfSelectorSuggestion>
{
    public void Configure(EntityTypeBuilder<PdfSelectorSuggestion> builder)
    {
        builder.ToTable("PdfSelectorSuggestions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.HtmlSnapshotPath).HasMaxLength(2048);
        builder.Property(x => x.ScreenshotPath).HasMaxLength(2048);
        builder.Property(x => x.SuggestedSelector).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.TestFailureReason).HasMaxLength(4000);
        builder.Property(x => x.SelectorType).HasConversion<int>();
        builder.Property(x => x.Purpose).HasConversion<int>();
        builder.Property(x => x.ExpectedAction).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.NewsSourceId, x.Status, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany(x => x.PdfSelectorSuggestions).HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ExtractedArticleConfiguration : IEntityTypeConfiguration<ExtractedArticle>
{
    public void Configure(EntityTypeBuilder<ExtractedArticle> builder)
    {
        builder.ToTable("ExtractedArticles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Headline).HasMaxLength(512).IsRequired();
        builder.Property(x => x.CanonicalUrl).HasMaxLength(2048);
        builder.Property(x => x.ContentFingerprint).HasMaxLength(128);
        builder.Property(x => x.Author).HasMaxLength(256);
        builder.Property(x => x.Section).HasMaxLength(256);
        builder.Property(x => x.TagsJson).HasMaxLength(4000);
        builder.Property(x => x.GfhSignalsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.GfhContextExplanation).HasMaxLength(4000);
        builder.Property(x => x.ExecutiveBrief).HasMaxLength(4000);
        builder.Property(x => x.AiLastFailureDetail).HasMaxLength(512);
        builder.Property(x => x.RawContent).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CleanedContent).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.ContentFingerprint).HasFilter("[ContentFingerprint] IS NOT NULL");
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.PublishedAt);
        builder.HasIndex(x => new { x.IntelligenceStatus, x.GfhRelevanceTier });
        builder.HasOne(x => x.DownloadedFile).WithMany(x => x.Articles).HasForeignKey(x => x.DownloadedFileId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticleEntityConfiguration : IEntityTypeConfiguration<ArticleEntity>
{
    public void Configure(EntityTypeBuilder<ArticleEntity> builder)
    {
        builder.ToTable("ArticleEntities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => new { x.ExtractedArticleId, x.EntityType });
        builder.HasOne(x => x.ExtractedArticle).WithMany(x => x.Entities).HasForeignKey(x => x.ExtractedArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class AiSummaryConfiguration : IEntityTypeConfiguration<AiSummary>
{
    public void Configure(EntityTypeBuilder<AiSummary> builder)
    {
        builder.ToTable("AISummaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ModelName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.Sentiment).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SentimentRationale).HasMaxLength(4000);
        builder.Property(x => x.ExecutiveNarrative).HasMaxLength(8000);
        builder.Property(x => x.TopicsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.KeywordsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RiskSignalsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.OpportunitySignalsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.ExtractedArticleId);
        builder.HasOne(x => x.ExtractedArticle).WithMany(x => x.AiSummaries).HasForeignKey(x => x.ExtractedArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class MarketRateConfiguration : IEntityTypeConfiguration<MarketRate>
{
    public void Configure(EntityTypeBuilder<MarketRate> builder)
    {
        builder.ToTable("MarketRates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Exchange).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.Symbol, x.Exchange, x.TradeDate }).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.BlobUri).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ApprovedBy).HasMaxLength(320);
        builder.Property(x => x.FailureReason).HasMaxLength(4000);
        builder.Property(x => x.ContentType).HasMaxLength(256);
        builder.HasIndex(x => new { x.ReportDate, x.ReportType });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasMany(x => x.DeliveryLogs).WithOne(x => x.Report).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("EmailLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FromEmail).HasMaxLength(320);
        builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Cc).HasMaxLength(2000);
        builder.Property(x => x.Bcc).HasMaxLength(2000);
        builder.Property(x => x.Subject).HasMaxLength(512).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.MessageId).HasMaxLength(256);
        builder.Property(x => x.ProviderOperationId).HasMaxLength(256);
        builder.Property(x => x.OriginalRecipients).HasMaxLength(2000);
        builder.HasIndex(x => x.BriefId);
        builder.HasOne(x => x.Report).WithMany(x => x.EmailLogs).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ReportSchedule).WithMany().HasForeignKey(x => x.ReportScheduleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReportScheduleId);
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ResourceType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ResourceId).HasMaxLength(64);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => x.OccurredAt);
    }
}

public sealed class PortalDownloadAuditLogConfiguration : IEntityTypeConfiguration<PortalDownloadAuditLog>
{
    public void Configure(EntityTypeBuilder<PortalDownloadAuditLog> builder)
    {
        builder.ToTable("PortalDownloadAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKind).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(128);
        builder.Property(x => x.ScreenshotRelativePath).HasMaxLength(2048);
        builder.Property(x => x.HtmlSnapshotRelativePath).HasMaxLength(2048);
        builder.HasIndex(x => new { x.NewsSourceId, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany(x => x.PortalAuditLogs).HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DownloadJob).WithMany().HasForeignKey(x => x.DownloadJobId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SourceIngestionAlertConfiguration : IEntityTypeConfiguration<SourceIngestionAlert>
{
    public void Configure(EntityTypeBuilder<SourceIngestionAlert> builder)
    {
        builder.ToTable("SourceIngestionAlerts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.NewsSourceId, x.IsResolved, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany(x => x.IngestionAlerts).HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class OcrProcessingJobConfiguration : IEntityTypeConfiguration<OcrProcessingJob>
{
    public void Configure(EntityTypeBuilder<OcrProcessingJob> builder)
    {
        builder.ToTable("OcrProcessingJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000);
        builder.Property(x => x.ResultManifestRelativePath).HasMaxLength(2048);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.HasIndex(x => new { x.DownloadedFileId, x.Status, x.CreatedAt });
        builder.HasOne(x => x.DownloadedFile).WithMany(f => f.OcrJobs).HasForeignKey(x => x.DownloadedFileId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class OcrPageResultConfiguration : IEntityTypeConfiguration<OcrPageResult>
{
    public void Configure(EntityTypeBuilder<OcrPageResult> builder)
    {
        builder.ToTable("OcrPageResults");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PageText).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.PageJsonRelativePath).HasMaxLength(2048);
        builder.HasIndex(x => new { x.OcrProcessingJobId, x.PageNumber }).IsUnique();
        builder.HasOne(x => x.OcrProcessingJob).WithMany(x => x.Pages).HasForeignKey(x => x.OcrProcessingJobId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticlePageConfiguration : IEntityTypeConfiguration<ArticlePage>
{
    public void Configure(EntityTypeBuilder<ArticlePage> builder)
    {
        builder.ToTable("ArticlePages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Snippet).HasMaxLength(4000);
        builder.HasIndex(x => new { x.ExtractedArticleId, x.PageNumber });
        builder.HasOne(x => x.ExtractedArticle).WithMany(x => x.Pages).HasForeignKey(x => x.ExtractedArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticleClassificationConfiguration : IEntityTypeConfiguration<ArticleClassification>
{
    public void Configure(EntityTypeBuilder<ArticleClassification> builder)
    {
        builder.ToTable("ArticleClassifications");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ExtractedArticleId, x.Category });
        builder.HasOne(x => x.ExtractedArticle).WithMany(x => x.Classifications).HasForeignKey(x => x.ExtractedArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticleSentimentConfiguration : IEntityTypeConfiguration<ArticleSentiment>
{
    public void Configure(EntityTypeBuilder<ArticleSentiment> builder)
    {
        builder.ToTable("ArticleSentiments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Explanation).HasMaxLength(4000);
        builder.HasIndex(x => x.ExtractedArticleId).IsUnique();
        builder.HasOne(x => x.ExtractedArticle).WithMany(x => x.Sentiments).HasForeignKey(x => x.ExtractedArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ArticleKeywordConfiguration : IEntityTypeConfiguration<ArticleKeyword>
{
    public void Configure(EntityTypeBuilder<ArticleKeyword> builder)
    {
        builder.ToTable("ArticleKeywords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Keyword).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.ExtractedArticleId, x.Keyword });
        builder.HasOne(x => x.ExtractedArticle).WithMany(x => x.Keywords).HasForeignKey(x => x.ExtractedArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("ReportSchedules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TargetRoleName).HasMaxLength(128);
        builder.HasIndex(x => new { x.IsEnabled, x.NextRunUtc });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ReportScheduleRecipientConfiguration : IEntityTypeConfiguration<ReportScheduleRecipient>
{
    public void Configure(EntityTypeBuilder<ReportScheduleRecipient> builder)
    {
        builder.ToTable("ReportScheduleRecipients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.HasIndex(x => new { x.ReportScheduleId, x.Email }).IsUnique();
        builder.HasOne(x => x.ReportSchedule).WithMany(x => x.Recipients).HasForeignKey(x => x.ReportScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ReportDeliveryLogConfiguration : IEntityTypeConfiguration<ReportDeliveryLog>
{
    public void Configure(EntityTypeBuilder<ReportDeliveryLog> builder)
    {
        builder.ToTable("ReportDeliveryLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.RecipientsSnapshot).HasMaxLength(4000);
        builder.HasIndex(x => new { x.ReportScheduleId, x.StartedAt });
        builder.HasOne(x => x.ReportSchedule).WithMany(x => x.DeliveryLogs).HasForeignKey(x => x.ReportScheduleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Report).WithMany(x => x.DeliveryLogs).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class IntelligenceAlertConfiguration : IEntityTypeConfiguration<IntelligenceAlert>
{
    public void Configure(EntityTypeBuilder<IntelligenceAlert> builder)
    {
        builder.ToTable("IntelligenceAlerts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.IsAcknowledged, x.CreatedAt });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class DownloadOperatorNoteConfiguration : IEntityTypeConfiguration<DownloadOperatorNote>
{
    public void Configure(EntityTypeBuilder<DownloadOperatorNote> builder)
    {
        builder.ToTable("DownloadOperatorNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Note).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.NewsSourceId, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany().HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DownloadJob).WithMany().HasForeignKey(x => x.DownloadJobId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class DownloadMonitorBatchRunConfiguration : IEntityTypeConfiguration<DownloadMonitorBatchRun>
{
    public void Configure(EntityTypeBuilder<DownloadMonitorBatchRun> builder)
    {
        builder.ToTable("DownloadMonitorBatchRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HangfireJobId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.StartedAt);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class AdminInterventionNotificationConfiguration : IEntityTypeConfiguration<AdminInterventionNotification>
{
    public void Configure(EntityTypeBuilder<AdminInterventionNotification> builder)
    {
        builder.ToTable("AdminInterventionNotifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FailureReason).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(128);
        builder.Property(x => x.SuggestedAction).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.OperatorNote).HasMaxLength(4000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany().HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DownloadJob).WithMany().HasForeignKey(x => x.DownloadJobId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SourceConfigurationVersionConfiguration : IEntityTypeConfiguration<SourceConfigurationVersion>
{
    public void Configure(EntityTypeBuilder<SourceConfigurationVersion> builder)
    {
        builder.ToTable("SourceConfigurationVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.JsonConfiguration).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.NewsSourceId, x.VersionNumber });
        builder.HasIndex(x => new { x.NewsSourceId, x.Status });
        builder.HasOne(x => x.NewsSource).WithMany().HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        // Intentionally no FK to SourceRecoveryAttempts — avoids SQL Server multiple cascade paths.
        builder.Ignore(x => x.SourceRecoveryAttempt);
        builder.Property(x => x.SourceRecoveryAttemptId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SourceRecoveryAttemptConfiguration : IEntityTypeConfiguration<SourceRecoveryAttempt>
{
    public void Configure(EntityTypeBuilder<SourceRecoveryAttempt> builder)
    {
        builder.ToTable("SourceRecoveryAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FailureType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(128);
        builder.Property(x => x.FailureMessage).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AnalysisJson).IsRequired();
        builder.Property(x => x.ResultSummary).HasMaxLength(4000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.NewsSourceId, x.CreatedAt });
        builder.HasIndex(x => new { x.DownloadJobId, x.CreatedAt });
        builder.HasOne(x => x.NewsSource).WithMany().HasForeignKey(x => x.NewsSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DownloadJob).WithMany().HasForeignKey(x => x.DownloadJobId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CandidateVersion).WithMany().HasForeignKey(x => x.CandidateVersionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RollbackVersion).WithMany().HasForeignKey(x => x.RollbackVersionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RetryDownloadJob).WithMany().HasForeignKey(x => x.RetryDownloadJobId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.AutoAiRecoveryRunId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SourceRecoveryKnowledgeEntryConfiguration : IEntityTypeConfiguration<SourceRecoveryKnowledgeEntry>
{
    public void Configure(EntityTypeBuilder<SourceRecoveryKnowledgeEntry> builder)
    {
        builder.ToTable("SourceRecoveryKnowledgeEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FailureType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PortalStrategyKey).HasMaxLength(64);
        builder.Property(x => x.ConnectorKey).HasMaxLength(128);
        builder.Property(x => x.FieldName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OldSelector).HasMaxLength(512);
        builder.Property(x => x.NewSelector).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Strategy).HasConversion<int>();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.FailureType, x.PortalStrategyKey, x.FieldName });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
