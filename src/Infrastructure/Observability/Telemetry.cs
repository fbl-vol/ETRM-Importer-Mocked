using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Observability;

/// <summary>
/// Central telemetry configuration for distributed tracing and metrics.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "ETRM.Importer.Mock";
    
    // ActivitySource for distributed tracing
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    
    // Meter for metrics
    public static readonly Meter Meter = new(ServiceName, "1.0.0");
    
    // Counters
    public static readonly Counter<long> TradesGenerated = Meter.CreateCounter<long>(
        "etrm.trades.generated",
        "trades",
        "Number of trades generated");
    
    public static readonly Counter<long> PricesGenerated = Meter.CreateCounter<long>(
        "etrm.prices.generated",
        "prices",
        "Number of EOD prices generated");
    
    public static readonly Counter<long> FilesUploaded = Meter.CreateCounter<long>(
        "etrm.files.uploaded",
        "files",
        "Number of files uploaded to S3");
    
    public static readonly Counter<long> EventsPublished = Meter.CreateCounter<long>(
        "etrm.events.published",
        "events",
        "Number of events published to NATS");
    
    // Histograms
    public static readonly Histogram<double> FileUploadDuration = Meter.CreateHistogram<double>(
        "etrm.file.upload.duration",
        "seconds",
        "Duration of file upload operations");
    
    public static readonly Histogram<double> EventPublishDuration = Meter.CreateHistogram<double>(
        "etrm.event.publish.duration",
        "seconds",
        "Duration of event publish operations");
    
    public static readonly Histogram<long> FileSize = Meter.CreateHistogram<long>(
        "etrm.file.size",
        "bytes",
        "Size of uploaded files");
    
    public static readonly Histogram<int> BatchSize = Meter.CreateHistogram<int>(
        "etrm.trade.batch.size",
        "trades",
        "Number of trades per batch");
}
