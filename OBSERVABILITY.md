# Observability Guide

This document provides detailed information about the observability features implemented in the ETRM Importer Mock service.

## Overview

The ETRM Importer Mock service is fully instrumented with OpenTelemetry for comprehensive observability. Every operation generates traces, metrics, and structured logs that can be visualized and queried through the integrated observability stack.

## Architecture

```
┌─────────────────────┐
│ ETRM.Importer.Mock  │
│  - Traces           │
│  - Metrics          │──┐
│  - Logs             │  │
└─────────────────────┘  │
                         │ OTLP/gRPC
                         ▼
              ┌──────────────────┐
              │ OTEL Collector   │
              │  - Receives      │
              │  - Processes     │
              │  - Routes        │
              └──────────────────┘
                      │
          ┌───────────┼───────────┐
          ▼           ▼           ▼
     ┌──────┐   ┌──────────┐  ┌──────┐
     │Tempo │   │Prometheus│  │ Loki │
     │      │   │          │  │      │
     └──────┘   └──────────┘  └──────┘
          │           │           │
          └───────────┼───────────┘
                      ▼
                ┌─────────┐
                │ Grafana │
                │         │
                └─────────┘
```

## Distributed Tracing (Tempo)

### What is Traced

Every operation in the service creates spans with detailed timing information:

1. **Trade Generation**: `generate.trades`
   - Batch size
   - Generation timestamp
   - Success/failure status

2. **EOD Price Generation**: `generate.eod_prices`
   - Number of prices generated
   - Trading period
   - Success/failure status

3. **File Upload**: `s3.upload`
   - S3 bucket name
   - Object key
   - File size
   - Content type
   - Upload duration

4. **Event Publishing**: `nats.publish`
   - NATS subject
   - Message size
   - Publish duration

5. **CSV Import**: `import.csv`
   - Import ID
   - File type
   - S3 key
   - Success/failure status

### Viewing Traces

1. Open Grafana at http://localhost:3000
2. Go to Explore
3. Select "Tempo" as the datasource
4. Use the search interface to find traces by:
   - Service name: `ETRM.Importer.Mock`
   - Operation name: e.g., `generate.trades`
   - Duration: e.g., `> 1s`
   - Status: `error` or `ok`

### Trace Attributes

Each span includes relevant attributes:
- `service.name`: Service identifier
- `batch.size`: Number of items in batch
- `file.type`: Type of file (trades.csv, eod-prices.csv)
- `import.id`: Unique import identifier
- `s3.bucket`, `s3.key`: S3 location
- `messaging.system`, `messaging.destination`: NATS details

## Metrics (Prometheus)

### Available Metrics

#### Counters

- **etrm.trades.generated** (trades)
  - Number of trades generated
  - Incremented with each batch

- **etrm.prices.generated** (prices)
  - Number of EOD prices generated
  - Incremented once per day

- **etrm.files.uploaded** (files)
  - Number of files uploaded to S3
  - Labels: `bucket`

- **etrm.events.published** (events)
  - Number of events published to NATS
  - Labels: `subject`

#### Histograms

- **etrm.file.upload.duration** (seconds)
  - Duration of S3 upload operations
  - Labels: `bucket`
  - Provides p50, p95, p99 percentiles

- **etrm.event.publish.duration** (seconds)
  - Duration of NATS publish operations
  - Labels: `subject`
  - Provides p50, p95, p99 percentiles

- **etrm.file.size** (bytes)
  - Size of uploaded files
  - Labels: `bucket`

- **etrm.trade.batch.size** (trades)
  - Number of trades per batch
  - Distribution of batch sizes

### Querying Metrics

Access Prometheus at http://localhost:9090 and use PromQL queries:

```promql
# Trade generation rate
rate(etrm_trades_generated_total[5m])

# Average batch size
avg(etrm_trade_batch_size_bucket)

# 95th percentile upload duration
histogram_quantile(0.95, rate(etrm_file_upload_duration_bucket[5m]))

# Total events published by subject
sum by (subject) (etrm_events_published_total)
```

### Grafana Dashboards

Create custom dashboards in Grafana to visualize:
- Trade generation trends over time
- Upload/publish performance
- Batch size distribution
- Error rates

## Structured Logging (Loki)

### Log Format

All logs are formatted as JSON using Serilog's Compact JSON format:

```json
{
  "@t": "2025-12-05T18:00:00.123Z",
  "@mt": "Generating {Count} trades",
  "@l": "Information",
  "Count": 5,
  "SourceContext": "ETRM.Importer.Mock.ImporterWorker",
  "ServiceName": "ETRM.Importer.Mock"
}
```

### Log Levels

- **Information**: Normal operations (trade generation, uploads, events)
- **Warning**: Recoverable issues (retries, fallbacks)
- **Error**: Failures requiring attention
- **Fatal**: Critical failures causing service shutdown

### Querying Logs in Grafana

Use LogQL to query logs:

```logql
# All logs from importer
{service_name="ETRM.Importer.Mock"}

# Trade generation logs
{service_name="ETRM.Importer.Mock"} |= "Generating"

# Error logs only
{service_name="ETRM.Importer.Mock"} | json | @l="Error"

# Logs with specific import ID
{service_name="ETRM.Importer.Mock"} | json | ImportId="abc-123"

# Upload operations
{service_name="ETRM.Importer.Mock"} |= "Uploaded object"
```

### Log Context

Each log entry includes:
- Timestamp (`@t`)
- Message template (`@mt`)
- Log level (`@l`)
- Source context (class name)
- Service name
- Additional properties (ImportId, FileType, Duration, etc.)

## Correlation

Traces, metrics, and logs are correlated:

1. **Trace ID Propagation**: Each operation creates a trace with a unique ID
2. **Structured Properties**: ImportId, FileType, etc. appear in logs and trace attributes
3. **Timing Correlation**: Log timestamps align with span start/end times
4. **Error Correlation**: Exceptions are logged AND recorded in spans

### Example Workflow

1. Service generates 5 trades → Creates span `generate.trades` with `batch.size=5`
2. Trades converted to CSV → Logged at Information level
3. CSV uploaded to S3 → Creates span `s3.upload`, records `file.size` metric
4. Event published to NATS → Creates span `nats.publish`, records event metric
5. All operations logged with same ImportId for correlation

## Best Practices

### For Developers

1. **Use Structured Logging**: Always use template syntax
   ```csharp
   _logger.LogInformation("Generated {Count} trades", count);  // ✓
   _logger.LogInformation($"Generated {count} trades");        // ✗
   ```

2. **Add Span Attributes**: Include relevant context
   ```csharp
   activity?.SetTag("batch.size", batchSize);
   activity?.SetTag("import.id", importId);
   ```

3. **Record Exceptions**: Always add exceptions to spans
   ```csharp
   catch (Exception ex)
   {
       activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
       activity?.AddException(ex);
       throw;
   }
   ```

4. **Use Meaningful Metrics**: Record dimensions that matter
   ```csharp
   Telemetry.FilesUploaded.Add(1, 
       new KeyValuePair<string, object?>("bucket", bucketName));
   ```

### For Operations

1. **Set Retention Policies**: Configure data retention in Tempo, Prometheus, and Loki
2. **Monitor Cardinality**: Watch metric dimensions to avoid cardinality explosion
3. **Create Alerts**: Set up Prometheus alerts for critical metrics
4. **Regular Review**: Check dashboards regularly for anomalies
5. **Index Logs**: Configure Loki indexes for frequently queried fields

## Configuration

### Service Configuration (appsettings.json)

```json
{
  "Observability": {
    "ServiceName": "ETRM.Importer.Mock",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://localhost:4317",
    "EnableTracing": true,
    "EnableMetrics": true,
    "EnableLogging": true
  }
}
```

### Environment Variables

Override configuration via environment variables:

```bash
Observability__ServiceName=ETRM.Importer.Mock
Observability__OtlpEndpoint=http://otel-collector:4317
Observability__EnableTracing=true
```

## Troubleshooting

### No Traces in Tempo

1. Check OTEL Collector logs: `docker-compose logs otel-collector`
2. Verify service is sending to correct endpoint
3. Check Tempo configuration in `observability/tempo.yaml`
4. Verify network connectivity: `docker-compose ps`

### Missing Metrics in Prometheus

1. Check OTEL Collector metrics endpoint: http://localhost:8889/metrics
2. Verify Prometheus scrape configuration
3. Check for metric name mismatches (underscores vs dots)
4. Review OTEL Collector logs for export errors

### Logs Not Appearing in Loki

1. Verify logs are structured JSON format
2. Check Loki ingestion: `docker-compose logs loki`
3. Verify OTEL Collector Loki exporter configuration
4. Check Loki query syntax in Grafana

### High Memory Usage

1. Reduce trace sampling rate
2. Decrease metric reporting frequency
3. Configure log level filtering
4. Set retention policies

## Further Reading

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Grafana Tempo Guide](https://grafana.com/docs/tempo/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Loki Documentation](https://grafana.com/docs/loki/)
- [Serilog Documentation](https://serilog.net/)
