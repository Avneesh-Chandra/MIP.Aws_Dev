using Amazon;
using Amazon.S3;
using Amazon.SecretsManager;
using Amazon.SimpleEmailV2;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Abstractions.Secrets;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using MIP.Aws.Infrastructure.Reporting;
using MIP.Aws.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MIP.Aws.Infrastructure;

public static class AwsServiceCollectionExtensions
{
    public static IServiceCollection AddMipAwsCloudServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AwsOptions>(configuration.GetSection(AwsOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        var aws = configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>() ?? new AwsOptions();
        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        var region = RegionEndpoint.GetBySystemName(string.IsNullOrWhiteSpace(aws.Region) ? "us-east-1" : aws.Region);

        if (string.Equals(storage.Provider, "S3", StringComparison.OrdinalIgnoreCase) && aws.S3.Enabled)
        {
            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(region));
            services.AddSingleton<IFileStorageService, AwsS3FileStorageService>();
        }
        else
        {
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        }

        if (string.Equals(email.Provider, "AwsSes", StringComparison.OrdinalIgnoreCase) && aws.Ses.Enabled)
        {
            services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ => new AmazonSimpleEmailServiceV2Client(region));
            services.AddSingleton<AwsSesEmailSender>();
            services.AddSingleton<IReportEmailTransport>(sp => sp.GetRequiredService<AwsSesEmailSender>());
            services.AddSingleton<IReportEmailSender, ReportEmailDispatcher>();
        }
        else if (string.Equals(email.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IReportEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IReportEmailSender, MockReportEmailSender>();
        }

        if (aws.SecretsManager.Enabled)
        {
            services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient(region));
            services.AddSingleton<ISecretStore, AwsSecretsManagerSecretStore>();
        }
        else
        {
            services.AddSingleton<ISecretStore, LocalDataProtectionSecretStore>();
        }

        return services;
    }
}
