# Caching

The OpenAPI aggregation extension uses `Microsoft.Extensions.Caching.Hybrid` (`HybridCache`) for high-performance caching of both fetched and aggregated OpenAPI documents. This page covers how caching works and how to invalidate cached entries.

## Cache Architecture

The caching layer operates at two levels:

1. **Document Cache**: Caches raw OpenAPI documents fetched from downstream services
2. **Aggregated Spec Cache**: Caches the final merged and processed specification for each service

```
Request for /api-docs/user-management
    │
    ▼
┌───────────────────────┐
│ Aggregated Spec Cache │──── Hit ──▶ Return cached spec
└──────────┬────────────┘
           │ Miss
           ▼
┌───────────────────────┐
│   Document Cache      │──── Hit ──▶ Use cached document
└──────────┬────────────┘
           │ Miss
           ▼
┌───────────────────────┐
│   HTTP Fetch from     │
│ downstream service    │
└───────────────────────┘
```

## Cache Configuration

Configure cache durations when registering services:

```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    // How long fetched documents are cached (default: 5 minutes)
    options.CacheDuration = TimeSpan.FromMinutes(5);

    // How long aggregated specs are cached (default: 5 minutes)
    options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(5);

    // How long failures are cached to avoid repeated failing requests (default: 1 minute)
    options.FailureCacheDuration = TimeSpan.FromMinutes(1);

    // Maximum size of a single cached entry (default: 1 MB)
    options.MaximumCachePayloadBytes = 2 * 1024 * 1024;
});
```

### Cache Duration Guidelines

| Scenario | Recommended Durations | Rationale |
|----------|----------------------|-----------|
| **Production (stable APIs)** | 10-30 minutes | APIs change infrequently, reduce downstream load |
| **Production (frequent deploys)** | 2-5 minutes | Balance freshness with performance |
| **Development** | 30 seconds - 1 minute | See changes quickly during development |
| **Failure cache** | 30 seconds - 2 minutes | Avoid hammering failing services, but recover quickly |

## Cache Invalidation

### IOpenApiCacheInvalidator Interface

The `IOpenApiCacheInvalidator` interface provides programmatic cache invalidation:

```csharp
public interface IOpenApiCacheInvalidator
{
    /// <summary>
    /// Invalidate cache for a specific service by name.
    /// </summary>
    Task InvalidateServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cache for a specific cluster by ID.
    /// </summary>
    Task InvalidateClusterAsync(string clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all cached OpenAPI documents and aggregated specs.
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
```

### Using Cache Invalidation

Inject `IOpenApiCacheInvalidator` via dependency injection:

```csharp
public class DeploymentNotificationHandler
{
    private readonly IOpenApiCacheInvalidator _cacheInvalidator;

    public DeploymentNotificationHandler(IOpenApiCacheInvalidator cacheInvalidator)
    {
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task OnServiceDeployed(string serviceName)
    {
        // Invalidate the cache for the deployed service
        await _cacheInvalidator.InvalidateServiceAsync(serviceName);
    }

    public async Task OnFullDeployment()
    {
        // Invalidate all cached specs
        await _cacheInvalidator.InvalidateAllAsync();
    }
}
```

### Admin Endpoints

A common pattern is to expose cache invalidation through admin endpoints:

```csharp
app.MapPost("/admin/cache/invalidate/{serviceName}", async (
    string serviceName,
    IOpenApiCacheInvalidator cacheInvalidator) =>
{
    await cacheInvalidator.InvalidateServiceAsync(serviceName);
    return Results.Ok(new { message = $"Cache invalidated for service: {serviceName}" });
});

app.MapPost("/admin/cache/invalidate-all", async (
    IOpenApiCacheInvalidator cacheInvalidator) =>
{
    await cacheInvalidator.InvalidateAllAsync();
    return Results.Ok(new { message = "All caches invalidated" });
});
```

You can then invalidate from the command line:

```bash
# Invalidate a specific service
curl -X POST http://localhost:5000/admin/cache/invalidate/user-management

# Invalidate all caches
curl -X POST http://localhost:5000/admin/cache/invalidate-all
```

## Tag-Based Invalidation

The cache implementation uses tag-based invalidation internally. Cache entries are tagged with:

- `service:{serviceName}` - For per-service invalidation
- `cluster:{clusterId}` - For per-cluster invalidation
- A global tag for full invalidation

This allows precise invalidation without clearing unrelated entries.

## Failure Caching

When a downstream service is unreachable or returns an error, the failure is cached for `FailureCacheDuration`. This prevents the gateway from repeatedly attempting to contact a failing service on every request.

```csharp
// Short failure cache for quick recovery
options.FailureCacheDuration = TimeSpan.FromSeconds(30);

// Longer failure cache if downstream services have slow recovery
options.FailureCacheDuration = TimeSpan.FromMinutes(5);
```

After the failure cache expires, the next request will attempt to fetch the document again.

## Related Documentation

- [Configuration](openapi-configuration.md) - Full configuration reference
- [Getting Started](openapi-getting-started.md) - Basic setup guide
- [Troubleshooting](openapi-troubleshooting.md) - Debugging cache issues
