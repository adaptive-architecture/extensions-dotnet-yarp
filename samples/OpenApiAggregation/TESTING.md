# Testing the YARP OpenAPI Aggregation Sample

This document provides instructions for testing the OpenAPI aggregation functionality.

## Prerequisites

- .NET 10.0 SDK installed
- All sample projects built successfully (see main README.md)

## Running the Services

You have several options for running the services:

### Option 1: Batch Script (Windows - Recommended)

```cmd
cd samples/OpenApiAggregation
.\run-all-services.cmd
```

This will start all three services in separate windows. Close each window to stop the respective service.

### Option 2: PowerShell Script (Windows)

```powershell
cd samples/OpenApiAggregation
.\run-all-services.ps1
```

This will start all three services concurrently and display their URLs. Press Ctrl+C to stop all services.

**Note:** If you encounter PowerShell language mode errors, use the batch script instead.

### Option 3: Bash Script (Linux/Mac)

```bash
cd samples/OpenApiAggregation
chmod +x run-all-services.sh
./run-all-services.sh
```

This will start all three services in the background. Press Ctrl+C to stop all services.

### Option 4: Manual - Separate Terminals (All Platforms)

**Terminal 1 - UserService:**
```bash
cd samples/OpenApiAggregation/UserService
dotnet run
```

**Terminal 2 - ProductService:**
```bash
cd samples/OpenApiAggregation/ProductService
dotnet run
```

**Terminal 3 - Gateway:**
```bash
cd samples/OpenApiAggregation/Gateway
dotnet run
```



## Service URLs

Once all services are running:

| Service | URL | Swagger UI |
|---------|-----|------------|
| UserService | http://localhost:5001 | http://localhost:5001/swagger |
| ProductService | http://localhost:5002 | http://localhost:5002/swagger |
| Gateway | http://localhost:5000 | N/A |

## Testing the Aggregation

### 1. List Available Services

```bash
curl http://localhost:5000/api-docs
```

**Expected Response:**
```json
{
  "services": [
    "user-management",
    "product-catalog"
  ],
  "count": 2
}
```

### 2. Get User Management API Specification

```bash
curl http://localhost:5000/api-docs/user-management
```

**Expected Response:** A merged OpenAPI document containing:
- Paths for `/api/users`, `/api/users/{id}`
- Schemas prefixed with `UserService` (e.g., `UserServiceUser`)
- Only reachable paths included (based on YARP route configuration)

**Get as YAML:**
```bash
curl http://localhost:5000/api-docs/user-management?format=yaml
```

### 3. Get Product Catalog API Specification

```bash
curl http://localhost:5000/api-docs/product-catalog
```

**Expected Response:** A merged OpenAPI document containing:
- Paths for `/api/products`, `/api/products/{id}`
- Schemas prefixed with `ProductService` (e.g., `ProductServiceProduct`)
- Only reachable paths included

### 4. Test Proxied Requests

Test that the gateway correctly proxies requests to downstream services:

**Get all users:**
```bash
curl http://localhost:5000/api/users
```

**Get all products:**
```bash
curl http://localhost:5000/api/products
```

**Create a new user:**
```bash
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name": "John Doe", "email": "john@example.com"}'
```

**Create a new product:**
```bash
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name": "Laptop", "price": 999.99, "description": "High-performance laptop"}'
```

## Verification Checklist

Use this checklist to verify the aggregation is working correctly:

- [ ] All three services start without errors
- [ ] UserService Swagger UI accessible at http://localhost:5001/swagger
- [ ] ProductService Swagger UI accessible at http://localhost:5002/swagger
- [ ] Gateway list endpoint returns both services
- [ ] User Management aggregated spec accessible
- [ ] Product Catalog aggregated spec accessible
- [ ] Schema prefixes applied correctly (`UserService`, `ProductService`)
- [ ] Only reachable paths included in aggregated specs
- [ ] Gateway successfully proxies GET requests to UserService
- [ ] Gateway successfully proxies GET requests to ProductService
- [ ] Gateway successfully proxies POST requests to both services

## Troubleshooting

### Services Won't Start

**Problem:** Port already in use

**Solution:** Check if another service is using ports 5000, 5001, or 5002:
```bash
# Windows
netstat -ano | findstr "5000 5001 5002"

# Linux/Mac
lsof -i :5000
lsof -i :5001
lsof -i :5002
```

Kill the process or change the ports in the `appsettings.json` files.

### Gateway Can't Fetch OpenAPI Specs

**Problem:** 502 Bad Gateway or timeouts

**Solution:**
1. Verify UserService and ProductService are running
2. Check the OpenAPI endpoints directly:
   - http://localhost:5001/swagger/v1/swagger.json
   - http://localhost:5002/swagger/v1/swagger.json
3. Review Gateway logs for errors

### Empty or Missing Paths

**Problem:** Aggregated spec has no paths or missing expected paths

**Solution:**
1. Check YARP route configuration in `Gateway/appsettings.json`
2. Ensure route metadata includes `Ada.OpenApi` with `serviceName` and `enabled: true`
3. Ensure cluster metadata includes `Ada.OpenApi` with `openApiPath` and `prefix`
4. Review Gateway logs for path reachability analysis messages

### Schema Conflicts

**Problem:** Schemas from different services have naming conflicts

**Solution:**
1. Verify schema prefixes in cluster metadata (`UserService`, `ProductService`)
2. Check that prefix is applied consistently across all schemas
3. Review Gateway logs for conflict resolution warnings

## Debugging

Enable debug logging in the Gateway to see detailed aggregation process:

**Gateway/appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AdaptArch.Extensions.Yarp.OpenApi": "Debug",
      "Yarp": "Information"
    }
  }
}
```

Debug logs will show:
- OpenAPI document fetching
- Path reachability analysis
- Schema renaming operations
- Document merging steps
- Cache hits/misses

## Performance Testing

### Cache Verification

The OpenAPI documents are cached for 5 minutes by default. Verify caching:

1. Make first request to aggregated spec (should fetch from downstream)
2. Make second request immediately (should use cache)
3. Check Gateway logs for cache hit messages

**Modify cache duration in Gateway/Program.cs:**
```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    options.CacheDuration = TimeSpan.FromMinutes(10); // Increase to 10 minutes
});
```

### Load Testing

Use a tool like `wrk` or `ab` (Apache Bench) to test performance:

```bash
# 10,000 requests with 100 concurrent connections
ab -n 10000 -c 100 http://localhost:5000/api-docs/user-management
```

Expected behavior:
- First request fetches from downstream (~100-200ms)
- Cached requests served in <10ms
- No errors under load

## Advanced Testing Scenarios

### Testing Path Transform Analysis

The aggregation uses YARP path transforms to determine which downstream paths are reachable. Test this:

1. **Add a new route** in Gateway `appsettings.json` that doesn't expose all UserService paths
2. Request the aggregated spec
3. Verify that only the paths reachable through the configured routes appear in the spec

### Testing Schema Pruning

Unused schemas should be pruned from the aggregated spec. Test this:

1. Add a complex schema to UserService that's not referenced by any exposed endpoint
2. Request the aggregated spec
3. Verify the unused schema is not included in the `components.schemas` section

### Testing Multiple Routes to Same Service

Multiple YARP routes can point to the same downstream service. Test this:

1. Add multiple routes in Gateway config pointing to UserService with different transforms
2. Request the aggregated spec
3. Verify all reachable paths are included (union of all route configurations)

## Cleanup

Stop all services:
- **PowerShell script:** Press `Ctrl+C`
- **Manual terminals:** Press `Ctrl+C` in each terminal
- **Background processes:** Run `kill $USER_PID $PRODUCT_PID $GATEWAY_PID`

## Next Steps

After successful testing:
1. Review the implementation in `src/OpenApi/`
2. Check unit tests in `test/OpenApi.UnitTests/`
3. Customize the sample to match your microservices architecture
4. Integrate with your existing YARP gateway configuration
