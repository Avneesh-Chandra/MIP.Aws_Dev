using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Entities.Market;
using MIP.Aws.Domain.Entities.Executive;
using MIP.Aws.Domain.Entities.Review;
using MIP.Aws.Domain.Entities.Social;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<ApplicationRole> Roles { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<UserAuditLog> UserAuditLogs { get; }

    DbSet<Tenant> Tenants { get; }

    DbSet<NewsSource> NewsSources { get; }

    DbSet<SourceCategory> SourceCategories { get; }

    DbSet<SourceCredential> SourceCredentials { get; }

    DbSet<DownloadSchedule> DownloadSchedules { get; }

    DbSet<DownloadJob> DownloadJobs { get; }

    DbSet<DownloadedFile> DownloadedFiles { get; }

    DbSet<ExtractedArticle> ExtractedArticles { get; }

    DbSet<ArticleEntity> ArticleEntities { get; }

    DbSet<AiSummary> AiSummaries { get; }

    DbSet<OcrProcessingJob> OcrProcessingJobs { get; }

    DbSet<OcrPageResult> OcrPageResults { get; }

    DbSet<ArticlePage> ArticlePages { get; }

    DbSet<ArticleClassification> ArticleClassifications { get; }

    DbSet<ArticleSentiment> ArticleSentiments { get; }

    DbSet<ArticleKeyword> ArticleKeywords { get; }

    DbSet<MarketRate> MarketRates { get; }

    DbSet<Report> Reports { get; }

    DbSet<EmailLog> EmailLogs { get; }

    DbSet<MailSettings> MailSettings { get; }

    DbSet<ReportSchedule> ReportSchedules { get; }

    DbSet<ReportScheduleRecipient> ReportScheduleRecipients { get; }

    DbSet<ReportDeliveryLog> ReportDeliveryLogs { get; }

    DbSet<IntelligenceAlert> IntelligenceAlerts { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<PortalDownloadAuditLog> PortalDownloadAuditLogs { get; }

    DbSet<SourceIngestionAlert> SourceIngestionAlerts { get; }

    DbSet<PortalManualLoginSession> PortalManualLoginSessions { get; }

    DbSet<PdfEditionDownload> PdfEditionDownloads { get; }

    DbSet<PdfSelectorSuggestion> PdfSelectorSuggestions { get; }

    DbSet<DownloadOperatorNote> DownloadOperatorNotes { get; }

    DbSet<AdminInterventionNotification> AdminInterventionNotifications { get; }

    DbSet<DownloadMonitorBatchRun> DownloadMonitorBatchRuns { get; }

    DbSet<SourceConfigurationVersion> SourceConfigurationVersions { get; }

    DbSet<SourceRecoveryAttempt> SourceRecoveryAttempts { get; }

    DbSet<SourceRecoveryKnowledgeEntry> SourceRecoveryKnowledgeEntries { get; }

    DbSet<AutoAiRecoveryRun> AutoAiRecoveryRuns { get; }

    DbSet<AutoAiDownloadRecoverySettings> AutoAiDownloadRecoverySettings { get; }

    // ───────── Review workflow ─────────

    DbSet<ArticleReviewAction> ArticleReviewActions { get; }

    DbSet<ArticleReviewComment> ArticleReviewComments { get; }

    DbSet<ArticleAnnotation> ArticleAnnotations { get; }

    DbSet<ReviewAssignment> ReviewAssignments { get; }

    DbSet<ExecutiveQueueItem> ExecutiveQueueItems { get; }

    DbSet<ExecutiveBrief> ExecutiveBriefs { get; }

    DbSet<ExecutiveBriefItem> ExecutiveBriefItems { get; }

    DbSet<DailyExecutiveBrief> DailyExecutiveBriefs { get; }

    DbSet<DailyExecutiveBriefMarketSnapshot> DailyExecutiveBriefMarketSnapshots { get; }

    DbSet<DailyExecutiveBriefItem> DailyExecutiveBriefItems { get; }

    DbSet<DailyExecutiveBriefEmailLog> DailyExecutiveBriefEmailLogs { get; }

    DbSet<DailyExecutiveBriefSettings> DailyExecutiveBriefSettings { get; }

    // ───────── Social publishing ─────────

    DbSet<SocialPost> SocialPosts { get; }

    DbSet<SocialPostPlatform> SocialPostPlatforms { get; }

    DbSet<SocialPostAttachment> SocialPostAttachments { get; }

    DbSet<SocialPostApproval> SocialPostApprovals { get; }

    DbSet<SocialPostPublishLog> SocialPostPublishLogs { get; }

    DbSet<SocialPlatformAccount> SocialPlatformAccounts { get; }

    // ───────── Market data ─────────

    DbSet<MarketInstrument> MarketInstruments { get; }

    DbSet<MarketPriceSnapshot> MarketPriceSnapshots { get; }

    DbSet<MarketDataImportJob> MarketDataImportJobs { get; }

    DbSet<MarketDataProviderConfig> MarketDataProviderConfigs { get; }

    DbSet<MarketMovementSummary> MarketMovementSummaries { get; }

    /// <summary>Identity user-role join (read-only) used by the analyst directory lookup.</summary>
    DbSet<IdentityUserRole<Guid>> UserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
