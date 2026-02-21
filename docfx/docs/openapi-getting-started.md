# Getting Started

This guide walks you through setting up OpenAPI aggregation in a YARP reverse proxy gateway.

## Prerequisites

- .NET 10.0 or later
- A YARP-based reverse proxy application
- Downstream services that expose OpenAPI specifications

## Installation

```bash
dotnet add package AdaptArch.Extensions.Yarp.OpenApi
```

## Basic Setup

### 1. Register Services

In your gateway's `Program.cs`, register the OpenAPI aggregation services:

```csharp
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add OpenAPI aggregation
builder.Services.AddYarpOpenApiAggregation();
```

### 2. Add Middleware

Add the aggregation middleware to your pipeline:

```csharp
var app = builder.Build();

// Add OpenAPI aggregation middleware
app.UseYarpOpenApiAggregation("/api-docs");

// Map YARP reverse proxy
app.MapReverseProxy();

app.Run();
```

### 3. Configure YARP Routes

Add `Ada.OpenApi` metadata to your YARP routes and clusters in `appsettings.json`:

```json
{
  "ReverseProxy": {
    "Routes": {
      "users-route": {
        "ClusterId": "user-service",
        "Match": {
          "Path": "/api/users/{**catch-all}"
        },
        "Metadata": {
          "Ada.OpenApi": "{\"serviceName\":\"User Management\",\"enabled\":true}"
        }
      },
      "products-route": {
        "ClusterId": "product-service",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        },
        "Metadata": {
          "Ada.OpenApi": "{\"serviceName\":\"Product Management\",\"enabled\":true}"
        }
      }
    },
    "Clusters": {
      "user-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5001/"
          }
        },
        "Metadata": {
          "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"UserService\"}"
        }
      },
      "product-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5002/"
          }
        },
        "Metadata": {
          "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"ProductService\"}"
        }
      }
    }
  }
}
```

### 4. Verify the Setup

Start your gateway and downstream services, then access:

- `GET /api-docs` - Lists available service names
- `GET /api-docs/user-management` - User Management aggregated spec (JSON)
- `GET /api-docs/product-management/openapi.yaml` - Product Management spec (YAML)

## Configuration Options

You can customize the aggregation behavior when registering services:

```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    // Cache durations
    options.CacheDuration = TimeSpan.FromMinutes(5);
    options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(5);
    options.FailureCacheDuration = TimeSpan.FromMinutes(1);

    // Fetching behavior
    options.MaxConcurrentFetches = 10;
    options.DefaultFetchTimeoutMs = 10_000;
    options.MaximumCachePayloadBytes = 2 * 1024 * 1024;

    // Default paths for finding OpenAPI specs
    options.DefaultOpenApiPath = "/swagger/v1/swagger.json";

    // How to handle non-analyzable transforms
    options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.IncludeWithWarning;
});
```

See the [Configuration](openapi-configuration.md) documentation for a complete reference.

## YARP Metadata Format

### Route Metadata

The `Ada.OpenApi` key on routes accepts a JSON string with:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `serviceName` | `string` | `null` | Human-readable name for grouping routes into a logical service |
| `enabled` | `bool` | `true` | Whether this route is included in the aggregation |

Routes with the same `serviceName` are grouped together into a single aggregated specification.

### Cluster Metadata

The `Ada.OpenApi` key on clusters accepts a JSON string with:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `openApiPath` | `string` | `/swagger/v1/swagger.json` | Path to the OpenAPI spec on the downstream service |
| `prefix` | `string` | `null` | Prefix applied to schema names to avoid collisions |

## Complete Example

Here is a minimal but complete gateway `Program.cs`:

```csharp
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddYarpOpenApiAggregation(options =>
{
    options.CacheDuration = TimeSpan.FromMinutes(5);
    options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(5);
    options.FailureCacheDuration = TimeSpan.FromMinutes(1);
    options.MaximumCachePayloadBytes = 2 * 1024 * 1024;
});

var app = builder.Build();

app.UseYarpOpenApiAggregation("/api-docs");
app.MapReverseProxy();

app.Run();
```

## Next Steps

- [Configuration](openapi-configuration.md) - Explore all configuration options
- [Caching](openapi-caching.md) - Learn about cache behavior and invalidation
- [Advanced Topics](openapi-advanced.md) - Path analysis, schema renaming, and more
- [Overview](openapi-aggregation.md) - Architecture and processing pipeline
- [Troubleshooting](openapi-troubleshooting.md) - Common issues and solutions
