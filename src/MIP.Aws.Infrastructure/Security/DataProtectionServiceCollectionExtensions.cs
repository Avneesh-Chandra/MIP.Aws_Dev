using Amazon.S3;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Security;

public static class DataProtectionServiceCollectionExtensions
{
    public const string ApplicationName = "MIP.Aws";

    public static IServiceCollection AddMipAwsDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var builder = services.AddDataProtection()
            .SetApplicationName(ApplicationName);

        var aws = configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>() ?? new AwsOptions();
        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        if (string.Equals(storage.Provider, "S3", StringComparison.OrdinalIgnoreCase)
            && aws.S3.Enabled
            && !string.IsNullOrWhiteSpace(aws.S3.BucketName))
        {
            var prefix = $"{aws.S3.Prefix.TrimEnd('/')}/platform/dataprotection";
            services.AddSingleton<IXmlRepository>(sp =>
            {
                var s3 = sp.GetRequiredService<IAmazonS3>();
                var logger = sp.GetRequiredService<ILogger<S3XmlRepository>>();
                return new S3XmlRepository(s3, aws.S3.BucketName, prefix, logger);
            });

            services.AddOptions<KeyManagementOptions>()
                .Configure<IXmlRepository>((options, repository) => options.XmlRepository = repository);

            builder.SetApplicationName(ApplicationName);
            return services;
        }

        var keysPath = Path.Combine(environment.ContentRootPath, "App_Data", "dataprotection-keys");
        Directory.CreateDirectory(keysPath);
        builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        return services;
    }
}
