#!/bin/bash
# Bash script to run all three services concurrently for testing
# Usage: ./run-all-services.sh

set -e

echo "===================================="
echo "Starting YARP OpenAPI Aggregation Demo"
echo "===================================="
echo ""

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "Stopping all services..."
    
    # Kill all background jobs
    if [ ! -z "$USER_PID" ] && kill -0 $USER_PID 2>/dev/null; then
        kill $USER_PID 2>/dev/null || true
    fi
    
    if [ ! -z "$PRODUCT_PID" ] && kill -0 $PRODUCT_PID 2>/dev/null; then
        kill $PRODUCT_PID 2>/dev/null || true
    fi
    
    if [ ! -z "$GATEWAY_PID" ] && kill -0 $GATEWAY_PID 2>/dev/null; then
        kill $GATEWAY_PID 2>/dev/null || true
    fi
    
    echo "All services stopped."
    exit 0
}

# Register cleanup function
trap cleanup SIGINT SIGTERM EXIT

# Start UserService
echo "[1/3] Starting UserService on http://localhost:5001..."
cd "$SCRIPT_DIR/UserService"
dotnet run > /dev/null 2>&1 &
USER_PID=$!

sleep 2

# Start ProductService
echo "[2/3] Starting ProductService on http://localhost:5002..."
cd "$SCRIPT_DIR/ProductService"
dotnet run > /dev/null 2>&1 &
PRODUCT_PID=$!

sleep 2

# Start Gateway
echo "[3/3] Starting Gateway on http://localhost:5000..."
cd "$SCRIPT_DIR/Gateway"
dotnet run > /dev/null 2>&1 &
GATEWAY_PID=$!

sleep 3

echo ""
echo "===================================="
echo "All services are running!"
echo "===================================="
echo ""
echo "Services:"
echo "  • UserService:    http://localhost:5001/swagger"
echo "  • ProductService: http://localhost:5002/swagger"
echo "  • Gateway:        http://localhost:5000"
echo ""
echo "OpenAPI Aggregation Endpoints:"
echo "  • List services:        http://localhost:5000/api-docs"
echo "  • User Management API:  http://localhost:5000/api-docs/user-management"
echo "  • Product Catalog API:  http://localhost:5000/api-docs/product-catalog"
echo ""
echo "Proxied API Requests:"
echo "  • GET  http://localhost:5000/api/users"
echo "  • GET  http://localhost:5000/api/products"
echo ""
echo "Press Ctrl+C to stop all services..."
echo ""

# Wait for any process to exit or for user to press Ctrl+C
wait
