#!/bin/bash

set -e

PIDS=()

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

cleanup() {
  echo ""
  bash "$REPO_ROOT/pipeline/stop-all.sh"
  wait 2>/dev/null || true
}

trap cleanup EXIT

# Kill any stale services from a previous run
bash "$REPO_ROOT/pipeline/stop-all.sh"

USER_SERVICE="$REPO_ROOT/samples/OpenApiAggregation/UserService/UserService.csproj"
PRODUCT_SERVICE="$REPO_ROOT/samples/OpenApiAggregation/ProductService/ProductService.csproj"
GATEWAY="$REPO_ROOT/samples/OpenApiAggregation/Gateway/Gateway.csproj"

echo "Building solution..."
dotnet build "$REPO_ROOT/Extensions.Yarp.slnx" --nologo
echo ""

echo "Starting UserService on port 5001..."
dotnet run --no-build --project "$USER_SERVICE" &
PIDS+=($!)

sleep 2

echo "Starting ProductService on port 5002..."
dotnet run --no-build --project "$PRODUCT_SERVICE" &
PIDS+=($!)

sleep 2

echo "Starting Gateway on port 5000..."
dotnet run --no-build --project "$GATEWAY" &
PIDS+=($!)

sleep 3

echo ""
echo "===================================="
echo "  All services are running!"
echo "===================================="
echo ""
echo "Services:"
echo "  UserService:    http://localhost:5001/swagger"
echo "  ProductService: http://localhost:5002/swagger"
echo "  Gateway:        http://localhost:5000/swagger"
echo ""
echo "OpenAPI Aggregation Endpoints:"
echo "  List services:        http://localhost:5000/api-docs"
echo "  User Management API:  http://localhost:5000/api-docs/user-management"
echo "  Product Catalog API:  http://localhost:5000/api-docs/product-catalog"
echo ""
echo "Proxied API Requests:"
echo "  GET  http://localhost:5000/api/users"
echo "  GET  http://localhost:5000/api/products"
echo ""
echo "Press Ctrl+C to stop all services..."
echo ""

wait
