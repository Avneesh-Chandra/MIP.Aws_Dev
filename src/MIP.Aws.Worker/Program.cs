using MIP.Aws.Application;
using MIP.Aws.Application.Scheduling;
using MIP.Aws.Infrastructure;
using MIP.Aws.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);

// Create empty SQL catalogs before Hangfire UseSqlServerStorage connects (first-boot / empty RDS).
await DatabaseBootstrap.EnsureSqlCatalogsBeforeHangfireIfRequestedAsync(
    builder.Environment,
    builder.Configuration).ConfigureAwait(false);

builder.Services.AddMipAwsInfrastructure(builder.Configuration, builder.Environment, enableHangfireProcessing: true);

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
