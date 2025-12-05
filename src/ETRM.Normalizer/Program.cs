using ETRM.Normalizer;
using Infrastructure.Configuration;
using Infrastructure.Database;
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
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));

builder.Services.AddSingleton<IS3Client, S3Client>();
builder.Services.AddSingleton<INatsSubscriber, NatsSubscriber>();
builder.Services.AddSingleton<ITradeRepository, TradeRepository>();
builder.Services.AddSingleton<IEodPriceRepository, EodPriceRepository>();
builder.Services.AddHostedService<NormalizerWorker>();

var host = builder.Build();
await host.RunAsync();
