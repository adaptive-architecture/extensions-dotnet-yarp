#!/bin/bash

set -e

PIDS=()
TMP_DIR=""

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

cleanup() {
  echo ""
  bash "$REPO_ROOT/pipeline/stop-all.sh"
  wait 2>/dev/null || true

  if [ -n "$TMP_DIR" ] && [ -d "$TMP_DIR" ]; then
    rm -rf "$TMP_DIR"
  fi

  echo "Cleanup complete."
}

trap cleanup EXIT

# Kill any stale services from a previous run
bash "$REPO_ROOT/pipeline/stop-all.sh"

USER_SERVICE="$REPO_ROOT/samples/OpenApiAggregation/UserService/UserService.csproj"
PRODUCT_SERVICE="$REPO_ROOT/samples/OpenApiAggregation/ProductService/ProductService.csproj"
GATEWAY="$REPO_ROOT/samples/OpenApiAggregation/Gateway/Gateway.csproj"

# Use repo-local temp dir to avoid Windows short paths (e.g. VALENT~1)
# which break Spectral's internal $ref resolution.
TMP_DIR="$REPO_ROOT/.lint-tmp"
rm -rf "$TMP_DIR"
mkdir -p "$TMP_DIR"

echo "Building solution..."
dotnet build "$REPO_ROOT/Extensions.Yarp.slnx" --nologo -v q
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

# Wait for Gateway to be ready
echo "Waiting for Gateway to be ready..."
MAX_RETRIES=30
RETRY=0
until curl -sf http://localhost:5000/api-docs > /dev/null 2>&1; do
  RETRY=$((RETRY + 1))
  if [ "$RETRY" -ge "$MAX_RETRIES" ]; then
    echo "ERROR: Gateway did not become ready after $MAX_RETRIES attempts"
    exit 1
  fi
  sleep 1
done
echo "Gateway is ready."
echo ""

# Fetch aggregated specs
echo "Fetching aggregated OpenAPI specs..."
curl -sf http://localhost:5000/api-docs/user-management -o "$TMP_DIR/user-management.json"
curl -sf http://localhost:5000/api-docs/product-catalog -o "$TMP_DIR/product-catalog.json"
echo "Specs saved to $TMP_DIR"
echo ""

# Run Spectral linting
echo "Running Spectral lint..."
EXIT_CODE=0

npx --yes @stoplight/spectral-cli lint \
  "$TMP_DIR/user-management.json" \
  "$TMP_DIR/product-catalog.json" \
  --ruleset "$REPO_ROOT/.spectral.yaml" \
  --format stylish || EXIT_CODE=$?

echo ""
if [ "$EXIT_CODE" -ne 0 ]; then
  echo "Spectral lint FAILED (exit code $EXIT_CODE)"
else
  echo "Spectral lint PASSED"
fi

exit $EXIT_CODE
