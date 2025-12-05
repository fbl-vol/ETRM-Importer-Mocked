#!/bin/bash

# Demo script for ETRM Importer System
# This script demonstrates the complete flow of the ETRM system

set -e

echo "================================================="
echo "ETRM Importer System - Demo Script"
echo "================================================="
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running. Please start Docker first."
    exit 1
fi

# Start Docker Compose services
echo "Step 1: Starting Docker Compose services..."
docker compose up -d
echo "Waiting for services to be ready..."
sleep 15
echo "✓ Services started"
echo ""

# Create MinIO bucket
echo "Step 2: Creating MinIO bucket..."
docker run --rm --network etrm-importer-mocked_etrm-network minio/mc mb minio/etrm-raw --insecure 2>/dev/null || echo "✓ Bucket already exists"
echo "✓ MinIO bucket ready"
echo ""

# Build all projects
echo "Step 3: Building all projects..."
dotnet build ETRM.sln > /dev/null
echo "✓ Build complete"
echo ""

# Run the Normalizer in background
echo "Step 4: Starting ETRM Normalizer..."
cd src/ETRM.Normalizer
dotnet run &
NORMALIZER_PID=$!
cd ../..
sleep 5
echo "✓ Normalizer started (PID: $NORMALIZER_PID)"
echo ""

# Run the Importer
echo "Step 5: Running ETRM Importer..."
cd src/ETRM.Importer.Mock
dotnet run
cd ../..
echo "✓ Import complete"
echo ""

# Wait a bit for processing
echo "Waiting for data to be processed..."
sleep 5

# Run the Position Aggregator
echo "Step 6: Running Position Aggregator..."
cd src/ETRM.PositionAggregator
dotnet run
cd ../..
echo "✓ Aggregation complete"
echo ""

# Query the database
echo "Step 7: Verifying data in TimescaleDB..."
echo ""
echo "--- Trades Count ---"
docker exec etrm-timescaledb psql -U postgres -d etrm -c "SELECT COUNT(*) FROM trades;"
echo ""
echo "--- Trades Summary ---"
docker exec etrm-timescaledb psql -U postgres -d etrm -c "SELECT trade_id, contract_id, customer_id, volume, side, currency FROM trades ORDER BY trade_id;"
echo ""
echo "--- Positions Summary ---"
docker exec etrm-timescaledb psql -U postgres -d etrm -c "SELECT position_id, contract_id, customer_id, volume, side, currency FROM positions ORDER BY position_id;"
echo ""

# Stop the normalizer
kill $NORMALIZER_PID 2>/dev/null || true

echo "================================================="
echo "Demo Complete!"
echo "================================================="
echo ""
echo "Services are still running. You can:"
echo "  - Access MinIO Console: http://localhost:9001 (minioadmin/minioadmin)"
echo "  - Access Adminer: http://localhost:8080 (postgres/postgres/etrm)"
echo "  - Query database: docker exec -it etrm-timescaledb psql -U postgres -d etrm"
echo ""
echo "To stop all services: docker compose down"
echo ""
