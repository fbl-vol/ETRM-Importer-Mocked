using ETRM.PositionAggregator;
using Infrastructure.Configuration;
using Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));

builder.Services.AddSingleton<ITradeRepository, TradeRepository>();
builder.Services.AddSingleton<IPositionRepository, PositionRepository>();
builder.Services.AddHostedService<AggregatorWorker>();

var host = builder.Build();
await host.RunAsync();
