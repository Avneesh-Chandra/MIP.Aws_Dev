using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Entities.Market;
using MIP.Aws.Domain.Entities.Executive;
using MIP.Aws.Domain.Entities.Review;
using MIP.Aws.Domain.Entities.Social;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Persistence;

public sealed class MediaIntelligenceDbContext
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>, IApplicationDbContext
{
    public MediaIntelligenceDbContext(DbContextOptions<MediaIntelligenceDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserAuditLog> UserAuditLogs => Set<UserAuditLog>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<NewsSource> NewsSources => Set<NewsSource>();

    public DbSet<SourceCategory> SourceCategories => Set<SourceCategory>();

    public DbSet<SourceCredential> SourceCredentials => Set<SourceCredential>();

    public DbSet<DownloadSchedule> DownloadSchedules => Set<DownloadSchedule>();

    public DbSet<DownloadJob> DownloadJobs => Set<DownloadJob>();

    public DbSet<DownloadedFile> DownloadedFiles => Set<DownloadedFile>();

    public DbSet<ExtractedArticle> ExtractedArticles => Set<ExtractedArticle>();

    public DbSet<ArticleEntity> ArticleEntities => Set<ArticleEntity>();

    public DbSet<AiSummary> AiSummaries => Set<AiSummary>();

    public DbSet<OcrProcessingJob> OcrProcessingJobs => Set<OcrProcessingJob>();

    public DbSet<OcrPageResult> OcrPageResults => Set<OcrPageResult>();

    public DbSet<ArticlePage> ArticlePages => Set<ArticlePage>();

    public DbSet<ArticleClassification> ArticleClassifications => Set<ArticleClassification>();

    public DbSet<ArticleSentiment> ArticleSentiments => Set<ArticleSentiment>();

    public DbSet<ArticleKeyword> ArticleKeywords => Set<ArticleKeyword>();

    public DbSet<MarketRate> MarketRates => Set<MarketRate>();

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    public DbSet<MailSettings> MailSettings => Set<MailSettings>();

    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

    public DbSet<ReportScheduleRecipient> ReportScheduleRecipients => Set<ReportScheduleRecipient>();

    public DbSet<ReportDeliveryLog> ReportDeliveryLogs => Set<ReportDeliveryLog>();

    public DbSet<IntelligenceAlert> IntelligenceAlerts => Set<IntelligenceAlert>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PortalDownloadAuditLog> PortalDownloadAuditLogs => Set<PortalDownloadAuditLog>();

    public DbSet<SourceIngestionAlert> SourceIngestionAlerts => Set<SourceIngestionAlert>();

    public DbSet<PortalManualLoginSession> PortalManualLoginSessions => Set<PortalManualLoginSession>();

    public DbSet<PdfEditionDownload> PdfEditionDownloads => Set<PdfEditionDownload>();

    public DbSet<PdfSelectorSuggestion> PdfSelectorSuggestions => Set<PdfSelectorSuggestion>();

    public DbSet<DownloadOperatorNote> DownloadOperatorNotes => Set<DownloadOperatorNote>();

    public DbSet<AdminInterventionNotification> AdminInterventionNotifications => Set<AdminInterventionNotification>();

    public DbSet<DownloadMonitorBatchRun> DownloadMonitorBatchRuns => Set<DownloadMonitorBatchRun>();

    public DbSet<SourceConfigurationVersion> SourceConfigurationVersions => Set<SourceConfigurationVersion>();

    public DbSet<SourceRecoveryAttempt> SourceRecoveryAttempts => Set<SourceRecoveryAttempt>();

    public DbSet<AutoAiRecoveryRun> AutoAiRecoveryRuns => Set<AutoAiRecoveryRun>();

    public DbSet<AutoAiDownloadRecoverySettings> AutoAiDownloadRecoverySettings => Set<AutoAiDownloadRecoverySettings>();

    public DbSet<SourceRecoveryKnowledgeEntry> SourceRecoveryKnowledgeEntries => Set<SourceRecoveryKnowledgeEntry>();

    public DbSet<ArticleReviewAction> ArticleReviewActions => Set<ArticleReviewAction>();

    public DbSet<ArticleReviewComment> ArticleReviewComments => Set<ArticleReviewComment>();

    public DbSet<ArticleAnnotation> ArticleAnnotations => Set<ArticleAnnotation>();

    public DbSet<ReviewAssignment> ReviewAssignments => Set<ReviewAssignment>();

    public DbSet<ExecutiveQueueItem> ExecutiveQueueItems => Set<ExecutiveQueueItem>();

    public DbSet<ExecutiveBrief> ExecutiveBriefs => Set<ExecutiveBrief>();

    public DbSet<ExecutiveBriefItem> ExecutiveBriefItems => Set<ExecutiveBriefItem>();

    public DbSet<DailyExecutiveBrief> DailyExecutiveBriefs => Set<DailyExecutiveBrief>();

    public DbSet<DailyExecutiveBriefMarketSnapshot> DailyExecutiveBriefMarketSnapshots => Set<DailyExecutiveBriefMarketSnapshot>();

    public DbSet<DailyExecutiveBriefItem> DailyExecutiveBriefItems => Set<DailyExecutiveBriefItem>();

    public DbSet<DailyExecutiveBriefEmailLog> DailyExecutiveBriefEmailLogs => Set<DailyExecutiveBriefEmailLog>();

    public DbSet<DailyExecutiveBriefSettings> DailyExecutiveBriefSettings => Set<DailyExecutiveBriefSettings>();

    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();

    public DbSet<SocialPostPlatform> SocialPostPlatforms => Set<SocialPostPlatform>();

    public DbSet<SocialPostAttachment> SocialPostAttachments => Set<SocialPostAttachment>();

    public DbSet<SocialPostApproval> SocialPostApprovals => Set<SocialPostApproval>();

    public DbSet<SocialPostPublishLog> SocialPostPublishLogs => Set<SocialPostPublishLog>();

    public DbSet<SocialPlatformAccount> SocialPlatformAccounts => Set<SocialPlatformAccount>();

    public DbSet<MarketInstrument> MarketInstruments => Set<MarketInstrument>();

    public DbSet<MarketPriceSnapshot> MarketPriceSnapshots => Set<MarketPriceSnapshot>();

    public DbSet<MarketDataImportJob> MarketDataImportJobs => Set<MarketDataImportJob>();

    public DbSet<MarketDataProviderConfig> MarketDataProviderConfigs => Set<MarketDataProviderConfig>();

    public DbSet<MarketMovementSummary> MarketMovementSummaries => Set<MarketMovementSummary>();

    /// <inheritdoc />
    DbSet<ApplicationUser> IApplicationDbContext.Users => Users;

    /// <inheritdoc />
    DbSet<ApplicationRole> IApplicationDbContext.Roles => Roles;

    /// <inheritdoc />
    DbSet<IdentityUserRole<Guid>> IApplicationDbContext.UserRoles => UserRoles;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaIntelligenceDbContext).Assembly);
        modelBuilder.Entity<ApplicationUser>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<ApplicationRole>().HasQueryFilter(r => !r.IsDeleted);
        ApplySoftDeleteFilters(modelBuilder);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(MediaIntelligenceDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, [modelBuilder]);
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditableEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
