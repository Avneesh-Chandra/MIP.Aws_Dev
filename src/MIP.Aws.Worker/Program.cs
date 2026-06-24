using MIP.Aws.Application;
using MIP.Aws.Application.Scheduling;
using MIP.Aws.Infrastructure;
using MIP.Aws.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMipAwsInfrastructure(builder.Configuration, builder.Environment, enableHangfireProcessing: true);

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
