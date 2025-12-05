using ETRM.Importer.Mock;
using Infrastructure.Configuration;
using Infrastructure.NATS;
using Infrastructure.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<S3Options>(builder.Configuration.GetSection("S3"));
builder.Services.Configure<NatsOptions>(builder.Configuration.GetSection("NATS"));

builder.Services.AddSingleton<IS3Client, S3Client>();
builder.Services.AddSingleton<INatsPublisher, NatsPublisher>();
builder.Services.AddHostedService<ImporterWorker>();

var host = builder.Build();
await host.RunAsync();
