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
using Microsoft.Extensions.Options;

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
        var useS3Keys = string.Equals(storage.Provider, "S3", StringComparison.OrdinalIgnoreCase)
                        && aws.S3.Enabled
                        && !string.IsNullOrWhiteSpace(aws.S3.BucketName);

        if (useS3Keys)
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

            services.AddSingleton(new DataProtectionKeyStoreInfo(
                Mode: DataProtectionKeyStoreMode.S3,
                Location: $"s3://{aws.S3.BucketName}/{prefix}"));
            services.AddHostedService<DataProtectionKeyRingStartupHostedService>();
            return services;
        }

        if (!environment.IsDevelopment()
            && string.Equals(storage.Provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            // Production/staging with S3 newspaper storage but no usable S3 bucket name for keys:
            // container disk is ephemeral — encrypting credentials here guarantees CredentialsNeedReEntry
            // after the next redeploy.
            throw new InvalidOperationException(
                "Storage.Provider=S3 requires Aws:S3:Enabled=true and Aws:S3:BucketName so DataProtection " +
                "keys persist under {Prefix}/platform/dataprotection. Without this, portal passwords are " +
                "lost on every ECS redeploy.");
        }

        var keysPath = Path.Combine(environment.ContentRootPath, "App_Data", "dataprotection-keys");
        Directory.CreateDirectory(keysPath);
        builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddSingleton(new DataProtectionKeyStoreInfo(
            Mode: DataProtectionKeyStoreMode.FileSystem,
            Location: keysPath));
        services.AddHostedService<DataProtectionKeyRingStartupHostedService>();
        return services;
    }
}

public enum DataProtectionKeyStoreMode
{
    FileSystem = 0,
    S3 = 1
}

public sealed record DataProtectionKeyStoreInfo(DataProtectionKeyStoreMode Mode, string Location);

/// <summary>
/// Logs DataProtection key-store mode at startup and forces an early key-ring load so S3 access
/// failures surface before the first login probe.
/// </summary>
internal sealed class DataProtectionKeyRingStartupHostedService(
    DataProtectionKeyStoreInfo storeInfo,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<KeyManagementOptions> keyManagementOptions,
    ILogger<DataProtectionKeyRingStartupHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "DataProtection key store: {Mode} at {Location} (ApplicationName={ApplicationName}).",
            storeInfo.Mode,
            storeInfo.Location,
            DataProtectionServiceCollectionExtensions.ApplicationName);

        // Touch the key manager so S3 list/get runs at startup rather than on first Unprotect.
        var repository = keyManagementOptions.Value.XmlRepository;
        if (repository is not null)
        {
            var keys = repository.GetAllElements();
            logger.LogInformation(
                "DataProtection key ring ready ({Count} key document(s)). Portal credentials will survive restarts while this store remains available.",
                keys.Count);
        }
        else
        {
            // File-system persistence still works via PersistKeysToFileSystem; create a protector to warm the ring.
            _ = dataProtectionProvider.CreateProtector("MIP.Aws.DataProtection.StartupProbe");
            logger.LogInformation(
                "DataProtection file-system key ring warmed. Keep {Location} intact across redeploys or credentials will need re-entry.",
                storeInfo.Location);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
