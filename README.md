# ETRM Importer - Mocked Implementation

A complete, dockerized C# solution demonstrating a microservices-based ETRM (Energy Trading and Risk Management) system with mocked data import, normalization, and position aggregation. **Now featuring full observability with distributed tracing, metrics, and structured logging!**

## Overview

This system consists of three separable C# services:

1. **ETRM.Importer.Mock** - **Continuous service** that realistically generates and publishes trade data throughout the day and EOD prices once daily. Uploads to S3-compatible object storage (MinIO locally, Hetzner S3 in production), and publishes import events via NATS
2. **ETRM.Normalizer** - Consumes import events, downloads raw files from S3, normalizes/parses data into domain DTOs, and persists them to TimescaleDB
3. **ETRM.PositionAggregator** - Aggregates trades into positions and persists them to TimescaleDB

### ✨ Key Features

- **Realistic Trade Generation**: Continuous mock trade generation with configurable intervals and batch sizes
- **Business Hours Pattern**: Increased trade frequency during business hours (8:00-17:00 UTC)
- **Daily EOD Prices**: End-of-day settlement prices published once per day at 16:00 UTC
- **Full Observability**: Complete telemetry stack with Tempo, Prometheus, Loki, and Grafana
- **Distributed Tracing**: Track requests across services with OpenTelemetry
- **Metrics Collection**: Monitor trade generation, file uploads, and event publishing
- **Structured Logging**: JSON-formatted logs for easy querying in Loki

## Architecture

```
┌─────────────────────┐
│ ETRM.Importer.Mock  │
│  - Mock CSV files   │──┐
│  - Upload to S3     │  │
│  - Publish events   │  │
└─────────────────────┘  │
                         │
                         ▼
                    ┌────────┐
                    │  NATS  │
                    │ Events │
                    └────────┘
                         │
                         ▼
┌─────────────────────────────┐
│   ETRM.Normalizer           │
│  - Subscribe to events      │
│  - Download from S3         │──┐
│  - Parse & Normalize        │  │
│  - Persist to TimescaleDB   │  │
└─────────────────────────────┘  │
                                 │
                                 ▼
                        ┌────────────────┐
                        │  TimescaleDB   │
                        │  - trades      │
                        │  - eod_prices  │
                        │  - positions   │
                        └────────────────┘
                                 │
                                 ▼
┌──────────────────────────────────┐
│  ETRM.PositionAggregator         │
│  - Read trades from DB           │
│  - Aggregate by dimensions       │
│  - Persist positions to DB       │
└──────────────────────────────────┘
```

## Prerequisites

- Docker and Docker Compose
- .NET 8.0 SDK or later
- (Optional) psql or database admin tool for querying TimescaleDB

## Quick Start

### 1. Start the Infrastructure

Start TimescaleDB, NATS, and MinIO:

```bash
docker-compose up -d
```

This will start:
- **TimescaleDB** on port 5432 (initialized with schema from `sql/init.sql`)
- **NATS** on port 4222
- **MinIO** on ports 9000 (API) and 9001 (Console)
- **Adminer** on port 8080 for database administration
- **Tempo** on port 3200 (distributed tracing backend)
- **Prometheus** on port 9090 (metrics collection)
- **Loki** on port 3100 (log aggregation)
- **Grafana** on port 3000 (observability dashboards)
- **OpenTelemetry Collector** on port 4317/4318 (telemetry pipeline)

### 2. Create MinIO Bucket

Access MinIO Console at http://localhost:9001 (credentials: minioadmin/minioadmin) and create a bucket named `etrm-raw`, or use the MinIO CLI:

```bash
docker run --rm -it --network etrm-network minio/mc alias set local http://minio:9000 minioadmin minioadmin
docker run --rm -it --network etrm-network minio/mc mb local/etrm-raw
```

### 3. Run the Services

**Terminal 1 - Start the Normalizer (it must be running before importing):**

```bash
cd src/ETRM.Normalizer
dotnet run
```

**Terminal 2 - Run the Importer (Continuous Service):**

```bash
cd src/ETRM.Importer.Mock
dotnet run
```

The importer will:
- Run continuously, generating realistic mock trade data
- Generate trades at random intervals (configurable, default 30-300 seconds)
- Generate smaller batches during off-hours, larger during business hours
- Generate EOD prices once per day at 16:00 UTC
- Upload all generated data to MinIO S3
- Publish import events to NATS
- Send telemetry (traces, metrics, logs) to the observability stack

The normalizer will:
- Listen for import events
- Download files from S3
- Parse and normalize the data
- Insert into TimescaleDB

**Terminal 3 - Run the Position Aggregator:**

```bash
cd src/ETRM.PositionAggregator
dotnet run
```

The aggregator will:
- Query all trades from TimescaleDB
- Aggregate by contract, customer, book, trader, department, product type, currency, and side
- Upsert positions into the database
- Exit when complete

### 4. Verify the Data

Connect to the database:

```bash
docker exec -it etrm-timescaledb psql -U postgres -d etrm
```

Query the data:

```sql
-- Check trades
SELECT COUNT(*) FROM trades;
SELECT * FROM trades LIMIT 5;

-- Check EOD prices
SELECT COUNT(*) FROM eod_prices;
SELECT * FROM eod_prices LIMIT 5;

-- Check positions
SELECT COUNT(*) FROM positions;
SELECT * FROM positions;
```

Or use Adminer at http://localhost:8080:
- System: PostgreSQL
- Server: timescaledb
- Username: postgres
- Password: postgres
- Database: etrm

### 5. Access Observability Dashboards

**Grafana** - http://localhost:3000
- Pre-configured with Tempo, Prometheus, and Loki datasources
- View distributed traces, metrics dashboards, and logs
- No login required (anonymous access enabled for development)

**Prometheus** - http://localhost:9090
- Query and explore metrics directly
- View targets and their health status

**Tempo** - http://localhost:3200
- Direct access to trace backend (typically accessed via Grafana)

## Observability Features

### Distributed Tracing (Tempo)

Every operation is instrumented with OpenTelemetry spans:
- Trade generation and batch processing
- File uploads to S3
- Event publishing to NATS
- EOD price generation

View traces in Grafana to see the complete flow of each operation with timing information.

### Metrics (Prometheus)

The following metrics are collected:
- `etrm.trades.generated` - Number of trades generated
- `etrm.prices.generated` - Number of EOD prices generated
- `etrm.files.uploaded` - Number of files uploaded to S3
- `etrm.events.published` - Number of events published to NATS
- `etrm.file.upload.duration` - Duration of S3 upload operations
- `etrm.event.publish.duration` - Duration of NATS publish operations
- `etrm.file.size` - Size of uploaded files in bytes
- `etrm.trade.batch.size` - Number of trades per batch

Access metrics in Prometheus or create custom dashboards in Grafana.

### Structured Logging (Loki)

All logs are structured using Serilog with JSON formatting:
- Easily queryable in Grafana with Loki
- Correlated with traces using trace IDs
- Contains contextual information like ImportId, FileType, etc.

Query logs in Grafana using LogQL, for example:
```logql
{service_name="ETRM.Importer.Mock"} |= "Generating"
```

## Project Structure

```
.
├── docker-compose.yml           # Infrastructure services (including observability stack)
├── sql/
│   └── init.sql                 # TimescaleDB schema initialization
├── samples/
│   ├── sample-trades.csv        # Sample trade data (for reference)
│   └── sample-eod-prices.csv    # Sample EOD price data (for reference)
├── observability/               # Observability configuration
│   ├── tempo.yaml              # Tempo (tracing) configuration
│   ├── prometheus.yml          # Prometheus (metrics) configuration
│   ├── loki.yaml               # Loki (logs) configuration
│   ├── grafana-datasources.yml # Grafana datasource provisioning
│   └── otel-collector-config.yaml # OpenTelemetry Collector configuration
└── src/
    ├── Shared/                  # Shared library
    │   ├── DTOs/               # Domain objects (Trade, EndOfDaySettlementPrice, Position)
    │   └── Events/             # Event contracts (RawImportedEvent, etc.)
    ├── Infrastructure/         # Infrastructure library
    │   ├── Configuration/      # Configuration options
    │   ├── Observability/      # Telemetry (ActivitySource, Meter)
    │   ├── S3/                 # S3 client wrapper (instrumented)
    │   ├── NATS/               # NATS publisher/subscriber (instrumented)
    │   └── Database/           # Repository implementations
    ├── ETRM.Importer.Mock/     # Continuous import service
    │   └── Services/           # TradeGenerator, PriceGenerator
    ├── ETRM.Normalizer/        # Normalization service
    └── ETRM.PositionAggregator/ # Aggregation service
```

## Configuration

All services use appsettings.json and environment variables for configuration.

### Common Environment Variables

**S3 Configuration:**
```bash
S3__Endpoint=http://localhost:9000
S3__AccessKey=minioadmin
S3__SecretKey=minioadmin
S3__BucketName=etrm-raw
S3__ForcePathStyle=true
```

**NATS Configuration:**
```bash
NATS__Url=nats://localhost:4222
```

**Observability Configuration:**
```bash
Observability__ServiceName=ETRM.Importer.Mock
Observability__ServiceVersion=1.0.0
Observability__OtlpEndpoint=http://localhost:4317
Observability__EnableTracing=true
Observability__EnableMetrics=true
Observability__EnableLogging=true
```

**Importer Worker Configuration:**
```bash
ImporterWorker__MinTradeIntervalSeconds=30
ImporterWorker__MaxTradeIntervalSeconds=300
ImporterWorker__MinTradesPerBatch=1
ImporterWorker__MaxTradesPerBatch=10
ImporterWorker__EodPricePublishHour=16
ImporterWorker__UseBusinessHoursPattern=true
ImporterWorker__BusinessHoursFrequencyMultiplier=0.5
```

**Database Configuration:**
```bash
Database__Host=localhost
Database__Port=5432
Database__Username=postgres
Database__Password=postgres
Database__Database=etrm
```

For production with Hetzner S3, update the S3 configuration:
```bash
S3__Endpoint=https://your-bucket.fsn1.your-project.io
S3__AccessKey=your-access-key
S3__SecretKey=your-secret-key
S3__ForcePathStyle=false
```

## Data Models

### Trade
Represents a trade (future, swap, etc.) used to derive positions and P&L.

### EndOfDaySettlementPrice
End-of-day settlement price for a contract/customer at a given trading period.

### Position
Aggregated position on a contract for a customer/book. Positions are computed from trades grouped by:
- ContractId
- CustomerId
- BookId
- TraderId
- DepartmentId
- ProductType
- Currency
- Side

Volume is summed, and TimeUpdated is the maximum of all contributing trades.

## Event Schema

### etrm.raw.imported
Published when raw ETRM data has been imported and stored in S3.

```json
{
  "eventType": "ETRM.Raw.Imported",
  "importId": "uuid",
  "bucket": "etrm-raw",
  "objectKey": "imports/2025/12/05/import-id/filename.csv",
  "fileType": "trades.csv",
  "format": "csv",
  "checksum": "sha256-hex",
  "sizeBytes": 12345,
  "importedAt": "2025-12-05T10:00:00Z",
  "metadata": {
    "sourceSystem": "MockedETRM"
  }
}
```

## Database Schema

The system uses TimescaleDB with hypertables for time-series data:

- **trades** - Hypertable partitioned on `trade_date`
- **eod_prices** - Hypertable partitioned on `trading_period`
- **positions** - Regular table with unique constraint on aggregation dimensions

See `sql/init.sql` for complete schema.

## Building the Solution

```bash
dotnet build
```

## Development

### Adding New File Types

1. Add a new DTO in `src/Shared/DTOs/`
2. Create a corresponding repository in `src/Infrastructure/Database/`
3. Add parsing logic in `src/ETRM.Normalizer/NormalizerWorker.cs`
4. Update the database schema in `sql/init.sql`

### Testing Locally

The importer now runs as a continuous service generating realistic mock data:
- Trades are generated throughout the day at random intervals
- EOD prices are generated once per day at 16:00 UTC
- All data generation is observable through Grafana dashboards
- Sample CSV files in `samples/` directory are provided for reference only

### Customizing Mock Data Generation

Adjust the `ImporterWorker` configuration in `appsettings.json` to control:
- Trade generation frequency (min/max intervals)
- Batch size ranges
- EOD price publication time
- Business hours pattern activation

## Production Considerations

### Infrastructure
- Use Hetzner S3 for object storage
- Implement proper secret management (Azure Key Vault, AWS Secrets Manager, etc.)
- Add authentication/authorization
- Implement proper error handling and retry logic
- Consider scaling NATS with JetStream for persistence
- Set up TimescaleDB replication for high availability

### Observability
- **Already Implemented**: Full observability stack with Tempo, Prometheus, and Loki
- Configure persistent storage for observability data in production
- Set up retention policies for traces, metrics, and logs
- Create production-ready Grafana dashboards and alerts
- Configure alerting rules in Prometheus for critical metrics
- Set up log aggregation and archival for compliance
- Consider using managed observability services (Grafana Cloud, etc.) for scale

### Telemetry Best Practices
- All operations are already instrumented with spans, metrics, and structured logs
- Trace IDs are propagated across service boundaries
- Error traces include exception details
- Metrics include relevant dimensions (file type, bucket, subject, etc.)

## Troubleshooting

**Cannot connect to services:**
- Ensure Docker Compose services are running: `docker-compose ps`
- Check service logs: `docker-compose logs [service-name]`

**MinIO bucket not found:**
- Create the `etrm-raw` bucket via MinIO Console or CLI

**Database connection fails:**
- Wait for TimescaleDB to be fully initialized (check with `docker-compose logs timescaledb`)
- Verify connection string in appsettings.json

**NATS connection issues:**
- Ensure NATS is running: `docker-compose ps nats`
- Check NATS logs: `docker-compose logs nats`

**Observability services not accessible:**
- Check if all observability services are running: `docker-compose ps tempo prometheus loki grafana otel-collector`
- Verify OpenTelemetry Collector is receiving data: Check collector logs with `docker-compose logs otel-collector`
- If Grafana shows no data, verify datasource configuration in Grafana UI

**Importer service not generating data:**
- Check the importer logs for errors
- Verify configuration in `appsettings.json`
- Ensure MinIO bucket exists and is accessible
- Check NATS connection

## License

MIT License
