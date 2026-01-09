# YARP OpenAPI Aggregation Sample

This sample demonstrates the **YARP OpenAPI Aggregation Extension** in action, showing how to aggregate OpenAPI specifications from multiple downstream microservices into a unified API gateway.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    API Gateway (YARP)                   │
│                                                         │
│  - Routes requests to downstream services               │
│  - Aggregates OpenAPI specs                             │
│  - Exposes unified API documentation                    │
│                                                         │
│  GET /api-docs              → List all services         │
│  GET /api-docs/users        → User API spec             │
│  GET /api-docs/products     → Product API spec          │
└────────────┬──────────────────────┬─────────────────────┘
             │                      │
      ┌──────▼──────┐          ┌────▼──────┐
      │ UserService │          │  Product  │
      │             │          │  Service  │
      │ Port: 5001  │          │ Port: 5002│
      └─────────────┘          └───────────┘
```

## Sample Services

### 1. UserService (Port 5001)
A simple REST API for managing users with the following endpoints:
- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create a new user
- `DELETE /api/users/{id}` - Delete a user

**OpenAPI Spec**: `http://localhost:5001/swagger/v1/swagger.json`

### 2. ProductService (Port 5002)
A simple REST API for managing products with the following endpoints:
- `GET /api/products` - Get all products
- `GET /api/products/{id}` - Get product by ID
- `POST /api/products` - Create a new product
- `PUT /api/products/{id}` - Update a product
- `DELETE /api/products/{id}` - Delete a product

**OpenAPI Spec**: `http://localhost:5002/swagger/v1/swagger.json`

### 3. Gateway (Port 5000)
YARP reverse proxy with OpenAPI aggregation that:
- Proxies requests to downstream services
- Fetches and caches OpenAPI specs
- Analyzes path reachability through YARP transforms
- Prunes unreachable paths
- Applies schema prefixes to avoid naming collisions
- Merges specs into unified documentation

**Aggregated API Docs**: 
- `http://localhost:5000/api-docs` - List available services
- `http://localhost:5000/api-docs/user-management` - Aggregated User API
- `http://localhost:5000/api-docs/product-catalog` - Aggregated Product API

## Running the Sample

### Prerequisites
- .NET 10.0 SDK or later
- Terminal/PowerShell/Bash (depending on your OS)

### Quick Start - Run All Services

Choose the appropriate script for your platform:

#### Windows (Batch Script - Recommended)
```cmd
cd samples/OpenApiAggregation
.\run-all-services.cmd
```
This opens each service in a separate window. Close the windows to stop services.

#### Windows (PowerShell)
```powershell
cd samples/OpenApiAggregation
.\run-all-services.ps1
```

#### Linux/Mac (Bash)
```bash
cd samples/OpenApiAggregation
chmod +x run-all-services.sh
./run-all-services.sh
```
Press Ctrl+C to stop all services.

### Manual Start - Individual Services

If you prefer to run services manually in separate terminals:

**Terminal 1 - UserService:**
```bash
cd samples/OpenApiAggregation/UserService
dotnet run
```
The service will start on `http://localhost:5001`

**Terminal 2 - ProductService:**
```bash
cd samples/OpenApiAggregation/ProductService
dotnet run
```
The service will start on `http://localhost:5002`

**Terminal 3 - Gateway:**
```bash
cd samples/OpenApiAggregation/Gateway
dotnet run
```
The gateway will start on `http://localhost:5000`

## Testing the Aggregation

### 1. View Available Services
```bash
curl http://localhost:5000/api-docs
```

**Response:**
```json
{
  "services": ["User Management", "Product Catalog"],
  "count": 2
}
```

### 2. Get Aggregated User API Spec
```bash
curl http://localhost:5000/api-docs/user-management
```

Returns the complete OpenAPI 3.0 specification for the User Management service with:
- All reachable paths through YARP
- Schema names prefixed with "UserService"
- Proper external paths (as seen by clients)

### 3. Get Aggregated Product API Spec (YAML)
```bash
curl -H "Accept: application/yaml" http://localhost:5000/api-docs/product-catalog
```

Returns the OpenAPI specification in YAML format.

### 4. Make Proxied Requests
```bash
# Get all users (proxied through gateway)
curl http://localhost:5000/api/users

# Get all products (proxied through gateway)
curl http://localhost:5000/api/products

# Create a new user
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"username":"newuser","email":"newuser@example.com","fullName":"New User"}'
```

## Gateway Configuration

The gateway is configured in `appsettings.json` with YARP routes and OpenAPI metadata:

```json
{
  "ReverseProxy": {
    "Routes": {
      "users-route": {
        "ClusterId": "user-service",
        "Match": { "Path": "/api/users/{**catch-all}" },
        "Metadata": {
          "Ada.OpenApi": "{\"serviceName\":\"User Management\",\"enabled\":true}"
        }
      },
      "products-route": {
        "ClusterId": "product-service",
        "Match": { "Path": "/api/products/{**catch-all}" },
        "Metadata": {
          "Ada.OpenApi": "{\"serviceName\":\"Product Catalog\",\"enabled\":true}"
        }
      }
    },
    "Clusters": {
      "user-service": {
        "Metadata": {
          "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"UserService\"}"
        },
        "Destinations": {
          "destination1": { "Address": "http://localhost:5001/" }
        }
      },
      "product-service": {
        "Metadata": {
          "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"ProductService\"}"
        },
        "Destinations": {
          "destination1": { "Address": "http://localhost:5002/" }
        }
      }
    }
  }
}
```

## Key Features Demonstrated

### 1. Path Transform Analysis
The extension analyzes YARP path transforms to determine which downstream paths are reachable:
- Routes with `PathPattern`, `PathPrefix`, `PathRemovePrefix` transforms
- Direct routing without transforms
- Complex multi-stage transforms

### 2. Schema Collision Avoidance
Each service's schemas are prefixed to avoid naming conflicts:
- User service: `User` → `UserServiceUser`
- Product service: `Product` → `ProductServiceProduct`
- All `$ref` references are automatically updated

### 3. Path Pruning
Only paths that are reachable through YARP routes are included:
- Downstream path: `/api/users/{id}`
- YARP route match: `/api/users/{**catch-all}`
- Result: ✅ Included (reachable)

If a downstream API has `/admin/users`, but no YARP route matches it:
- Result: ❌ Excluded (unreachable)

### 4. Component Pruning
Unused schemas, responses, and parameters are automatically removed:
- If `UserAddress` schema is not referenced by any reachable path → Removed
- Keeps the OpenAPI spec clean and focused

### 5. Metadata Aggregation
The merged specification includes:
- Combined servers from all services (deduplicated)
- Merged tags (deduplicated by name)
- Aggregated security requirements
- Combined description

### 6. Caching
OpenAPI specs are cached for 5 minutes to reduce overhead:
- Downstream specs are fetched once per cache duration
- Aggregated specs are cached after processing
- Configurable cache duration

## Extension Points

### Custom Transform Analyzers
Add support for custom YARP transforms:
```csharp
services.AddSingleton<IRouteTransformAnalyzer, CustomTransformAnalyzer>();
```

### Custom Schema Renaming
Implement custom prefix strategies:
```csharp
services.AddSingleton<ISchemaRenamer, CustomSchemaRenamer>();
```

### Custom Merging Logic
Override merge behavior for specific component types:
```csharp
services.AddSingleton<IOpenApiMerger, CustomOpenApiMerger>();
```

## Configuration Options

```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    options.CacheDuration = TimeSpan.FromMinutes(10);
    options.DefaultOpenApiPath = "/swagger/v1/swagger.json";
    options.FallbackPaths = new[] { "/openapi.json", "/swagger.json" };
});
```

## Troubleshooting

### Issue: "Service not found"
- Verify the service name in route metadata matches exactly
- Check that routes have `Ada.OpenApi` metadata with `enabled: true`

### Issue: "Failed to fetch OpenAPI document"
- Ensure downstream services are running
- Verify the `openApiPath` in cluster metadata is correct
- Check network connectivity between gateway and services

### Issue: "Paths not appearing in aggregated spec"
- Verify YARP route patterns match the downstream paths
- Check transform analysis logs for unreachable paths
- Ensure path patterns use wildcard captures correctly

### Issue: "Schema name conflicts"
- Ensure each cluster has a unique `prefix` in metadata
- Use PascalCase prefixes for best compatibility

## Learn More

- **YARP Documentation**: https://microsoft.github.io/reverse-proxy/
- **OpenAPI Specification**: https://swagger.io/specification/
- **Extension Source**: `../../src/OpenApi/`
- **Test Suite**: `../../test/OpenApi.UnitTests/`

## Project Status

- ✅ Core implementation complete (100%)
- ✅ Unit tests complete (85 tests, 100% pass rate)
- ⚠️ Sample application (in progress)
- ⏳ Integration tests (planned)
- ⏳ Performance benchmarks (planned)

## Contributing

This sample demonstrates the capabilities of the YARP OpenAPI Aggregation Extension. To extend or modify:

1. Fork the repository
2. Make your changes
3. Add tests for new functionality
4. Submit a pull request

## License

This sample is part of the `adaptive-architecture/extensions-dotnet-yarp` repository.
