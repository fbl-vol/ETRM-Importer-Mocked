# ETRM Importer - Mocked Implementation

A complete, dockerized C# solution demonstrating a microservices-based ETRM (Energy Trading and Risk Management) system with mocked data import, normalization, and position aggregation.

## Overview

This system consists of three separable C# services:

1. **ETRM.Importer.Mock** - Generates mocked ETRM export files, uploads them to S3-compatible object storage (MinIO locally, Hetzner S3 in production), and publishes import events via NATS
2. **ETRM.Normalizer** - Consumes import events, downloads raw files from S3, normalizes/parses data into domain DTOs, and persists them to TimescaleDB
3. **ETRM.PositionAggregator** - Aggregates trades into positions and persists them to TimescaleDB

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

**Terminal 2 - Run the Importer:**

```bash
cd src/ETRM.Importer.Mock
dotnet run
```

The importer will:
- Read sample files from `samples/` directory
- Upload them to MinIO S3
- Publish import events to NATS
- Exit when complete

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

## Project Structure

```
.
├── docker-compose.yml           # Infrastructure services
├── sql/
│   └── init.sql                 # TimescaleDB schema initialization
├── samples/
│   ├── sample-trades.csv        # Sample trade data
│   └── sample-eod-prices.csv    # Sample EOD price data
└── src/
    ├── Shared/                  # Shared library
    │   ├── DTOs/               # Domain objects (Trade, EndOfDaySettlementPrice, Position)
    │   └── Events/             # Event contracts (RawImportedEvent, etc.)
    ├── Infrastructure/         # Infrastructure library
    │   ├── Configuration/      # Configuration options
    │   ├── S3/                 # S3 client wrapper
    │   ├── NATS/               # NATS publisher/subscriber
    │   └── Database/           # Repository implementations
    ├── ETRM.Importer.Mock/     # Import service
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

The system includes sample CSV files in the `samples/` directory that are automatically imported when running the importer.

## Production Considerations

- Use Hetzner S3 for object storage
- Implement proper secret management (Azure Key Vault, AWS Secrets Manager, etc.)
- Add authentication/authorization
- Implement proper error handling and retry logic
- Add monitoring and alerting
- Consider scaling NATS with JetStream for persistence
- Set up TimescaleDB replication for high availability
- Add comprehensive logging with structured logging (Serilog, etc.)

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

## License

MIT License
