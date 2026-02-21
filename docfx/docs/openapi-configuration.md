# Configuration

The OpenAPI aggregation extension provides comprehensive configuration through `OpenApiAggregationOptions`, YARP route metadata, and YARP cluster metadata.

## OpenApiAggregationOptions

Configure aggregation behavior when registering services:

```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    // Configure options here
});
```

### Cache Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CacheDuration` | `TimeSpan` | 5 minutes | How long fetched OpenAPI documents are cached |
| `AggregatedSpecCacheDuration` | `TimeSpan` | 5 minutes | How long aggregated (merged) specifications are cached |
| `FailureCacheDuration` | `TimeSpan` | 1 minute | How long fetch failures are cached to avoid repeated failing requests |
| `MaximumCachePayloadBytes` | `int` | 1,048,576 (1 MB) | Maximum size of a single cached payload in bytes |

```csharp
options.CacheDuration = TimeSpan.FromMinutes(10);
options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(10);
options.FailureCacheDuration = TimeSpan.FromMinutes(2);
options.MaximumCachePayloadBytes = 2 * 1024 * 1024; // 2 MB
```

### Fetching Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultOpenApiPath` | `string` | `/swagger/v1/swagger.json` | Default path to the OpenAPI document on downstream services |
| `MaxConcurrentFetches` | `int` | 10 | Maximum number of concurrent HTTP requests to downstream services |
| `DefaultFetchTimeoutMs` | `int` | 10,000 (10 sec) | Timeout in milliseconds for each downstream fetch request |
| `FallbackPaths` | `string[]` | See below | Additional paths to try if the primary path fails |

**Default fallback paths:**

```csharp
options.FallbackPaths = [
    "/api/v1/openapi.json",
    "/openapi.json",
    "/docs/openapi.json",
    "/swagger/openapi.json"
];
```

### Discovery Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableAutoDiscovery` | `bool` | `true` | Automatically discover services from all YARP clusters |

### Transform Handling

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `NonAnalyzableStrategy` | `NonAnalyzableTransformStrategy` | `IncludeWithWarning` | How to handle routes with transforms that cannot be statically analyzed |
| `LogTransformWarnings` | `bool` | `true` | Whether to log warnings for non-analyzable transforms |

#### NonAnalyzableTransformStrategy Enum

| Value | Description |
|-------|-------------|
| `IncludeWithWarning` | Include the paths in the aggregated spec and log a warning. This is the safest default as it avoids hiding APIs. |
| `ExcludeWithWarning` | Exclude the paths from the aggregated spec and log a warning. Use this when you prefer strict accuracy. |
| `SkipService` | Skip the entire service from aggregation. Use this for services that should only appear when fully analyzable. |

```csharp
options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.ExcludeWithWarning;
options.LogTransformWarnings = true;
```

### Customization Delegates

| Property | Type | Description |
|----------|------|-------------|
| `ConfigureInfo` | `Func<OpenApiInfo, HttpContext, OpenApiInfo>?` | Customize the info section of the aggregated specification |
| `ConfigureServers` | `Func<HttpContext, IList<OpenApiServer>>` | Provide custom server entries for the aggregated specification |

#### Customizing Info

```csharp
options.ConfigureInfo = (info, context) =>
{
    info.Title = "My API Gateway";
    info.Description = "Aggregated API documentation for all services";
    info.Version = "1.0.0";
    info.Contact = new OpenApiContact
    {
        Name = "API Support",
        Email = "api-support@example.com"
    };
    return info;
};
```

#### Customizing Servers

```csharp
options.ConfigureServers = context =>
{
    return
    [
        new OpenApiServer
        {
            Url = $"{context.Request.Scheme}://{context.Request.Host}",
            Description = "API Gateway"
        }
    ];
};
```

## YARP Route Metadata

Routes are configured with the `Ada.OpenApi` metadata key containing a JSON string.

### AdaOpenApiRouteConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceName` | `string?` | `null` | Human-readable service name for grouping routes |
| `Enabled` | `bool` | `true` | Whether this route participates in aggregation |

### Example

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
      "users-admin-route": {
        "ClusterId": "user-service",
        "Match": {
          "Path": "/api/admin/users/{**catch-all}"
        },
        "Metadata": {
          "Ada.OpenApi": "{\"serviceName\":\"User Management\",\"enabled\":true}"
        }
      }
    }
  }
}
```

Both routes above share the same `serviceName`, so they are grouped together into a single "User Management" aggregated specification.

### Disabling a Route

Set `enabled` to `false` to exclude a route from aggregation without removing its YARP configuration:

```json
{
  "Ada.OpenApi": "{\"serviceName\":\"User Management\",\"enabled\":false}"
}
```

## YARP Cluster Metadata

Clusters are configured with the `Ada.OpenApi` metadata key containing a JSON string.

### AdaOpenApiClusterConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `OpenApiPath` | `string` | `/swagger/v1/swagger.json` | Path to the OpenAPI spec on the downstream service |
| `Prefix` | `string?` | `null` | Prefix for schema and tag names to avoid collisions |

### Example

```json
{
  "ReverseProxy": {
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
      }
    }
  }
}
```

### Schema Prefix

The `prefix` property is used to avoid naming collisions when multiple downstream services have schemas with the same name. For example, if both a User Service and a Product Service define a `PaginatedResponse` schema, the prefix ensures they become `UserServicePaginatedResponse` and `ProductServicePaginatedResponse` in the aggregated spec.

## Complete Configuration Example

```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    // Caching
    options.CacheDuration = TimeSpan.FromMinutes(5);
    options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(5);
    options.FailureCacheDuration = TimeSpan.FromMinutes(1);
    options.MaximumCachePayloadBytes = 2 * 1024 * 1024;

    // Fetching
    options.DefaultOpenApiPath = "/swagger/v1/swagger.json";
    options.MaxConcurrentFetches = 10;
    options.DefaultFetchTimeoutMs = 10_000;
    options.FallbackPaths =
    [
        "/api/v1/openapi.json",
        "/openapi.json"
    ];

    // Transforms
    options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.IncludeWithWarning;
    options.LogTransformWarnings = true;

    // Customization
    options.ConfigureInfo = (info, context) =>
    {
        info.Title = "My Gateway API";
        info.Version = "2.0.0";
        return info;
    };

    options.ConfigureServers = context =>
    [
        new OpenApiServer
        {
            Url = $"{context.Request.Scheme}://{context.Request.Host}",
            Description = "Gateway"
        }
    ];
});
```

## Related Documentation

- [Getting Started](openapi-getting-started.md) - Quick setup guide
- [Caching](openapi-caching.md) - Cache behavior and invalidation
- [Advanced Topics](openapi-advanced.md) - Path analysis, schema renaming, transforms
