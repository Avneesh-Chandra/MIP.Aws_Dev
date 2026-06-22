using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Articles;
using MIP.Aws.Application.Abstractions.Caching;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Application.Abstractions.Browser;
using MIP.Aws.Application.Abstractions.Crawling;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Jobs;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Portal;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Abstractions.Secrets;
using MIP.Aws.Application.Abstractions.Security;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Abstractions.Telemetry;
using MIP.Aws.Application.Compliance;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Connectors;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Application.Scheduling;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Articles;
using MIP.Aws.Infrastructure.Caching;
using MIP.Aws.Infrastructure.Browser;
using MIP.Aws.Infrastructure.Compliance;
using MIP.Aws.Infrastructure.Connectors;
using MIP.Aws.Infrastructure.Crawling;
using MIP.Aws.Infrastructure.Download;
using MIP.Aws.Infrastructure.Intelligence.Recovery;
using MIP.Aws.Infrastructure.Jobs;
using MIP.Aws.Infrastructure.News;
using MIP.Aws.Infrastructure.News.EditionDiscovery;
using MIP.Aws.Infrastructure.News.PdfEdition;
using MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;
using MIP.Aws.Infrastructure.Operator;
using MIP.Aws.Infrastructure.Portal;
using MIP.Aws.Infrastructure.Scheduling;
using MIP.Aws.Infrastructure.Security;
using MIP.Aws.Infrastructure.Reporting;
using MIP.Aws.Infrastructure.Telemetry;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJwtServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        return services;
    }

    public static IServiceCollection AddMipAwsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool enableHangfireProcessing = true)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<ApplicationDisplayOptions>(configuration.GetSection(ApplicationDisplayOptions.SectionName));
        services.Configure<PdfEditionSchedulerOptions>(configuration.GetSection(PdfEditionSchedulerOptions.SectionName));
        services.Configure<MailAutomationOptions>(configuration.GetSection(MailAutomationOptions.SectionName));
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.Configure<EmailSafetyOptions>(configuration.GetSection(EmailSafetyOptions.SectionName));
        services.Configure<AutoAiDownloadRecoveryOptions>(configuration.GetSection(AutoAiDownloadRecoveryOptions.SectionName));
        services.Configure<AiSourceRecoveryOptions>(configuration.GetSection(AiSourceRecoveryOptions.SectionName));
        services.Configure<NewsIngestionComplianceOptions>(configuration.GetSection(NewsIngestionComplianceOptions.SectionName));
        services.Configure<HangfireQueueOptions>(configuration.GetSection(HangfireQueueOptions.SectionName));
        services.Configure<HangfireHostOptions>(configuration.GetSection(HangfireHostOptions.SectionName));
        services.Configure<AiSelectorSuggestionOptions>(configuration.GetSection(AiSelectorSuggestionOptions.SectionName));
        services.Configure<RedisCacheOptions>(configuration.GetSection(RedisCacheOptions.SectionName));
        services.AddSingleton<ICacheService, InMemoryCacheService>();

        services.AddMipAwsCloudServices(configuration);
        services.AddMipAwsDataProtection(configuration, environment);

        services.AddHttpClient(nameof(PdfEditionContentFetcher), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/pdf,*/*;q=0.8");
        });
        services.AddHttpClient(nameof(ResilientContentDownloader));
        services.AddHttpClient(nameof(EditionDiscoveryHtmlClient), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ar,en-US;q=0.9,en;q=0.8");
        });

        services.AddSingleton<ITelemetryService, NoopTelemetryService>();
        services.AddSingleton<IHeadlessBrowserService, PlaywrightHeadlessBrowserService>();
        services.AddHostedService<PlaywrightBrowserBootstrapHostedService>();
        services.AddSingleton<HtmlEditionStoryExtractor>();
        services.AddSingleton<IArticleExtractor, HtmlArticleExtractor>();
        services.AddScoped<IRobotsPolicyService, RobotsPolicyService>();
        services.AddSingleton<INewsCredentialProtector, NewsCredentialProtector>();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddSingleton<ILoginThrottleService, CacheBackedLoginThrottleService>();

        services.AddSingleton<IContentDownloader, ResilientContentDownloader>();
        services.AddSingleton<IPdfDownloader, PdfDownloader>();
        services.AddSingleton<IRssDownloader, RssDownloader>();
        services.AddSingleton<INewsSourceConnectorFactory, NewsSourceConnectorFactory>();
        services.AddSingleton<INewsSourceConnector, DefaultHtmlNewsSourceConnector>();
        services.AddSingleton<INewsSourceConnector, RssNewsSourceConnector>();
        services.AddSingleton<INewsSourceConnector, PdfNewsSourceConnector>();
        services.AddSingleton<PublisherEditionNewsConnector>();
        services.AddSingleton<EditionUrlDiscoveryRegistry>();
        services.AddSingleton<IEditionUrlDiscovery, AkhbarAlKhaleejEditionDiscovery>();
        services.AddSingleton<IEditionUrlDiscovery, AlAyamEditionDiscovery>();
        services.AddSingleton<IEditionUrlDiscovery, AlQabasEditionDiscovery>();
        services.AddSingleton<IEditionUrlDiscovery, AawsatEditionDiscovery>();
        services.AddSingleton<EditionDiscoveryHtmlClient>();

        services.AddScoped<IPublisherComplianceGate, PublisherComplianceGate>();
        services.AddSingleton<IDarAlKhaleejPressReaderSessionStore, DarAlKhaleejPressReaderSessionStore>();
        services.AddScoped<IWebPortalAutomationService, PlaywrightWebPortalAutomationService>();
        services.AddScoped<PressReaderDownloadStrategy>();
        services.AddScoped<GenericWebPortalDownloadStrategy>();
        services.AddScoped<IPortalDownloadStrategyResolver, PortalDownloadStrategyResolver>();

        services.AddSingleton<IPdfEditionDownloadProgressTracker, PdfEditionDownloadProgressTracker>();
        services.AddScoped<IPdfEditionDiscoveryService, PlaywrightPdfEditionDiscoveryService>();
        services.AddScoped<PdfEditionContentFetcher>();
        services.AddScoped<PdfEditionValidator>();
        services.AddScoped<IPdfEditionDownloadService, PdfEditionDownloadService>();
        services.AddScoped<IPdfDiscoveryPageCaptureService, PdfDiscoveryPageCaptureService>();
        services.AddScoped<IPdfSelectorSuggestionTestService, PdfSelectorSuggestionTestService>();
        services.AddScoped<IPdfEditionFailureArtifactService, PdfEditionFailureArtifactService>();
        services.AddScoped<PortalArtifactUrlBuilder>();
        services.AddScoped<IPdfEditionAdminNotificationService, PdfEditionAdminNotificationService>();
        services.AddScoped<IPdfSelectorSuggestionService, NoopPdfSelectorSuggestionService>();
        services.AddScoped<SourceRecoveryContextBuilder>();
        services.AddScoped<IDownloadManager, NewsDownloadManager>();
        services.AddSingleton<INewsDownloadJobScheduler, HangfireNewsDownloadJobScheduler>();
        services.AddSingleton<IIntelligenceJobScheduler, NoopIntelligenceJobScheduler>();
        services.AddScoped<IOperatorDownloadMonitorService, OperatorDownloadMonitorService>();
        services.AddSingleton<IDownloadMonitorBatchRunService, DownloadMonitorBatchRunService>();
        services.AddScoped<IDownloadMonitorDailyStatusEmailService, DownloadMonitorDailyStatusEmailService>();

        services.AddMipAwsAi(configuration, environment);
        services.AddScoped<ISourceRecoveryAnalysisService, SourceRecoveryService>();
        services.AddScoped<ISourceRecoveryOrchestrator, SourceRecoveryOrchestrator>();
        services.AddScoped<IAiRecoverySuggestionRanker, AiRecoverySuggestionRanker>();
        services.AddScoped<IAutoAiDownloadRecoveryOrchestrator, AutoAiDownloadRecoveryOrchestrator>();
        services.AddScoped<IAutoAiDownloadRecoveryEnqueueService, AutoAiDownloadRecoveryEnqueueService>();
        services.AddScoped<AutoAiDownloadRecoverySettingsProvider>();
        services.AddScoped<IAutoAiDownloadRecoverySettingsReader>(sp => sp.GetRequiredService<AutoAiDownloadRecoverySettingsProvider>());
        services.AddTransient<AutoAiDownloadRecoveryJob>();

        services.AddTransient<PdfEditionJobs>();
        services.AddTransient<NewsIngestionJobs>();
        services.AddTransient<DownloadMonitorScheduledJobs>();
        services.AddSingleton<IScheduledJobRegistry, HangfireScheduledJobRegistry>();

        services.AddScoped<IMailSettingsService, MailSettingsService>();
        services.AddScoped<IMailConfigStatusService, MailConfigStatusService>();
        services.AddSingleton<ReportEmailSafetyService>();
        services.AddSingleton<IAuditService, NoopAuditService>();
        services.AddMemoryCache();

        if (enableHangfireProcessing)
        {
            var hangfireConnection = configuration.GetConnectionString("Hangfire")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Hangfire requires a SQL connection string.");

            var queueOptions = configuration.GetSection(HangfireQueueOptions.SectionName).Get<HangfireQueueOptions>() ?? new HangfireQueueOptions();
            var hangfireHost = configuration.GetSection(HangfireHostOptions.SectionName).Get<HangfireHostOptions>() ?? new HangfireHostOptions();
            services.AddHangfire(cfg => cfg
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(5)
                }));

            if (hangfireHost.EnableJobServer)
            {
                services.AddHangfireServer(o =>
                {
                    o.Queues = queueOptions.Queues;
                    if (queueOptions.WorkerCount is > 0)
                    {
                        o.WorkerCount = queueOptions.WorkerCount.Value;
                    }
                });
            }
        }

        return services;
    }
}

internal sealed class NoopAuditService : IAuditService
{
    public Task RecordAdminActionAsync(string action, string resourceType, string? resourceId, object? details = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordReportDownloadAsync(Guid reportId, Guid? userId, string? ipAddress, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordAiProcessingAsync(Guid articleId, bool success, string? failureReason, TimeSpan duration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordOcrProcessingAsync(Guid downloadedFileId, bool success, string? failureReason, TimeSpan duration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class MockReportEmailSender(ILogger<MockReportEmailSender> logger) : IReportEmailSender
{
    public Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("MOCK EMAIL to {To} | {Subject}", string.Join(',', message.To), message.Subject);
        return Task.FromResult(new ReportEmailSendResult(
            true,
            "mock",
            "noreply@mipaws.local",
            message.To,
            [],
            null,
            null,
            null,
            EmailSendOutcome.Sent,
            DateTimeOffset.UtcNow));
    }

    public Task<int> RetryFailedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}

internal sealed class LocalDataProtectionSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.TryGetValue(key, out var v) ? v : null);

    public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _secrets[key] = value;
        return Task.CompletedTask;
    }
}
