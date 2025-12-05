using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ETRM.Importer.Mock.Services;
using Infrastructure.Configuration;
using Infrastructure.NATS;
using Infrastructure.Observability;
using Infrastructure.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Shared.Events;

namespace ETRM.Importer.Mock;

public class ImporterWorker : BackgroundService
{
    private readonly IS3Client _s3Client;
    private readonly INatsPublisher _natsPublisher;
    private readonly ILogger<ImporterWorker> _logger;
    private readonly ImporterWorkerOptions _options;
    private readonly TradeGenerator _tradeGenerator;
    private readonly PriceGenerator _priceGenerator;
    private readonly Random _random = new();
    private DateTime _lastEodPriceDate = DateTime.MinValue;

    public ImporterWorker(
        IS3Client s3Client,
        INatsPublisher natsPublisher,
        ILogger<ImporterWorker> logger,
        IOptions<ImporterWorkerOptions> options,
        TradeGenerator tradeGenerator,
        PriceGenerator priceGenerator)
    {
        _s3Client = s3Client;
        _natsPublisher = natsPublisher;
        _logger = logger;
        _options = options.Value;
        _tradeGenerator = tradeGenerator;
        _priceGenerator = priceGenerator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ETRM Importer starting as continuous service at: {Time}", DateTimeOffset.Now);
        _logger.LogInformation("Configuration: TradeInterval={MinInterval}-{MaxInterval}s, BatchSize={MinBatch}-{MaxBatch}, EodHour={EodHour}UTC",
            _options.MinTradeIntervalSeconds, _options.MaxTradeIntervalSeconds,
            _options.MinTradesPerBatch, _options.MaxTradesPerBatch,
            _options.EodPricePublishHour);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                // Check if we should generate EOD prices
                if (now.Hour == _options.EodPricePublishHour && now.Date != _lastEodPriceDate)
                {
                    await GenerateAndPublishEodPricesAsync(now, stoppingToken);
                    _lastEodPriceDate = now.Date;
                }

                // Generate and publish trades
                await GenerateAndPublishTradesAsync(now, stoppingToken);

                // Calculate next interval
                var baseInterval = _random.Next(
                    _options.MinTradeIntervalSeconds,
                    _options.MaxTradeIntervalSeconds);

                // Apply business hours multiplier if enabled
                if (_options.UseBusinessHoursPattern && IsBusinessHours(now))
                {
                    baseInterval = (int)(baseInterval * _options.BusinessHoursFrequencyMultiplier);
                }

                _logger.LogInformation("Next trade batch in {Interval} seconds", baseInterval);
                await Task.Delay(TimeSpan.FromSeconds(baseInterval), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ETRM Importer stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in continuous import service");
            throw;
        }
    }

    private async Task GenerateAndPublishTradesAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("generate.trades", ActivityKind.Internal);
        
        try
        {
            var batchSize = _random.Next(_options.MinTradesPerBatch, _options.MaxTradesPerBatch + 1);
            activity?.SetTag("batch.size", batchSize);

            _logger.LogInformation("Generating {Count} trades", batchSize);

            var trades = _tradeGenerator.GenerateTrades(batchSize);
            var csv = _tradeGenerator.TradesToCsv(trades);

            Telemetry.TradesGenerated.Add(batchSize);
            Telemetry.BatchSize.Record(batchSize);

            await ImportCsvDataAsync(csv, "trades.csv", timestamp, cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to generate and publish trades");
            throw;
        }
    }

    private async Task GenerateAndPublishEodPricesAsync(DateTime timestamp, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("generate.eod_prices", ActivityKind.Internal);
        
        try
        {
            _logger.LogInformation("Generating EOD prices for {Date}", timestamp.Date);

            var prices = _priceGenerator.GeneratePrices(timestamp.Date);
            var csv = _priceGenerator.PricesToCsv(prices);

            activity?.SetTag("price.count", prices.Count);
            Telemetry.PricesGenerated.Add(prices.Count);

            await ImportCsvDataAsync(csv, "eod-prices.csv", timestamp, cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to generate and publish EOD prices");
            throw;
        }
    }

    private async Task ImportCsvDataAsync(string csvData, string fileType, DateTime timestamp, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("import.csv", ActivityKind.Internal);
        activity?.SetTag("file.type", fileType);

        var importId = Guid.NewGuid().ToString();
        var fileName = $"{Path.GetFileNameWithoutExtension(fileType)}-{timestamp:yyyyMMdd-HHmmss}.csv";
        var objectKey = $"imports/{timestamp:yyyy}/{timestamp:MM}/{timestamp:dd}/{importId}/{fileName}";

        activity?.SetTag("import.id", importId);
        activity?.SetTag("s3.key", objectKey);

        _logger.LogInformation("Importing generated data as {FileType} with ImportId: {ImportId}", fileType, importId);

        // Calculate checksum
        var fileBytes = Encoding.UTF8.GetBytes(csvData);
        var checksum = CalculateSha256(fileBytes);

        // Upload to S3
        using (var stream = new MemoryStream(fileBytes))
        {
            await _s3Client.UploadFileAsync(objectKey, stream, "text/csv", cancellationToken);
        }

        // Publish event
        var @event = new RawImportedEvent
        {
            ImportId = importId,
            Bucket = "etrm-raw",
            ObjectKey = objectKey,
            FileType = fileType,
            Format = "csv",
            Checksum = checksum,
            SizeBytes = fileBytes.Length,
            ImportedAt = timestamp,
            Metadata = new Dictionary<string, string>
            {
                ["sourceSystem"] = "MockedETRM",
                ["originalFileName"] = fileName,
                ["generatedData"] = "true"
            }
        };

        await _natsPublisher.PublishAsync("etrm.raw.imported", @event, cancellationToken);
        
        _logger.LogInformation("Published import event for {FileType} with ImportId: {ImportId}", fileType, importId);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static bool IsBusinessHours(DateTime dateTime)
    {
        return dateTime.Hour >= 8 && dateTime.Hour < 17;
    }

    private static string CalculateSha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
