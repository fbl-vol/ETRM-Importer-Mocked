using ETRM.Importer.Mock;
using ETRM.Importer.Mock.Services;
using Infrastructure.Configuration;
using Infrastructure.NATS;
using Infrastructure.Observability;
using Infrastructure.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "ETRM.Importer.Mock")
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// Configure options
builder.Services.Configure<S3Options>(builder.Configuration.GetSection("S3"));
builder.Services.Configure<NatsOptions>(builder.Configuration.GetSection("NATS"));
builder.Services.Configure<ObservabilityOptions>(builder.Configuration.GetSection("Observability"));
builder.Services.Configure<ImporterWorkerOptions>(builder.Configuration.GetSection("ImporterWorker"));

// Configure OpenTelemetry
var observabilityOptions = builder.Configuration
    .GetSection("Observability")
    .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(
        serviceName: observabilityOptions.ServiceName,
        serviceVersion: observabilityOptions.ServiceVersion);

// Add tracing
if (observabilityOptions.EnableTracing)
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder
                .SetResourceBuilder(resourceBuilder)
                .AddSource(Telemetry.ActivitySource.Name)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(observabilityOptions.OtlpEndpoint);
                });
        });
}

// Add metrics
if (observabilityOptions.EnableMetrics)
{
    builder.Services.AddOpenTelemetry()
        .WithMetrics(meterProviderBuilder =>
        {
            meterProviderBuilder
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(Telemetry.Meter.Name)
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(observabilityOptions.OtlpEndpoint);
                });
        });
}

// Register services
builder.Services.AddSingleton<IS3Client, S3Client>();
builder.Services.AddSingleton<INatsPublisher, NatsPublisher>();
builder.Services.AddSingleton<TradeGenerator>();
builder.Services.AddSingleton<PriceGenerator>();
builder.Services.AddHostedService<ImporterWorker>();

var host = builder.Build();

try
{
    Log.Information("Starting ETRM Importer Mock service");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
