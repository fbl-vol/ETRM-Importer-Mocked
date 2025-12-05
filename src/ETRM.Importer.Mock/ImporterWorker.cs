using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Infrastructure.NATS;
using Infrastructure.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Events;

namespace ETRM.Importer.Mock;

public class ImporterWorker : BackgroundService
{
    private readonly IS3Client _s3Client;
    private readonly INatsPublisher _natsPublisher;
    private readonly ILogger<ImporterWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ImporterWorker(
        IS3Client s3Client,
        INatsPublisher natsPublisher,
        ILogger<ImporterWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _s3Client = s3Client;
        _natsPublisher = natsPublisher;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("ETRM Importer starting at: {time}", DateTimeOffset.Now);

            // Find project root (go up from src/ETRM.Importer.Mock to repository root)
            var projectDir = Directory.GetCurrentDirectory();
            var repoRoot = Path.GetFullPath(Path.Combine(projectDir, "..", ".."));
            
            // Import trades
            var tradesPath = Path.Combine(repoRoot, "samples", "sample-trades.csv");
            await ImportFileAsync(tradesPath, "trades.csv", stoppingToken);

            // Import EOD prices
            var eodPath = Path.Combine(repoRoot, "samples", "sample-eod-prices.csv");
            await ImportFileAsync(eodPath, "eod-prices.csv", stoppingToken);

            _logger.LogInformation("ETRM Importer completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during import");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private async Task ImportFileAsync(string filePath, string fileType, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}, skipping", filePath);
            return;
        }

        var importId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var fileName = Path.GetFileName(filePath);
        var objectKey = $"imports/{timestamp:yyyy}/{timestamp:MM}/{timestamp:dd}/{importId}/{fileName}";

        _logger.LogInformation("Importing file: {FilePath} as {FileType}", filePath, fileType);

        // Read file and calculate checksum
        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
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
                ["originalFileName"] = fileName
            }
        };

        await _natsPublisher.PublishAsync("etrm.raw.imported", @event, cancellationToken);
        _logger.LogInformation("Published import event for {FileType} with ImportId: {ImportId}", fileType, importId);
    }

    private static string CalculateSha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
