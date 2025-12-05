#!/bin/bash

# Script to create MinIO bucket for ETRM system

echo "Setting up MinIO for ETRM system..."

# Wait for MinIO to be ready
echo "Waiting for MinIO to be ready..."
until docker run --rm --network etrm-network minio/mc alias set local http://minio:9000 minioadmin minioadmin 2>/dev/null; do
    echo "Waiting for MinIO..."
    sleep 2
done

echo "MinIO is ready!"

# Create bucket
echo "Creating etrm-raw bucket..."
docker run --rm --network etrm-network minio/mc mb local/etrm-raw --ignore-existing

echo "MinIO setup complete!"
echo "You can access MinIO Console at http://localhost:9001"
echo "Username: minioadmin"
echo "Password: minioadmin"
