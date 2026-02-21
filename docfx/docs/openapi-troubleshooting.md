# Troubleshooting

Common issues and solutions when working with the OpenAPI aggregation extension.

## No Services Listed

**Symptom**: `GET /api-docs` returns an empty array `[]`.

**Possible causes**:

1. **Missing route metadata**: Ensure YARP routes have the `Ada.OpenApi` metadata key with a `serviceName` value.

   ```json
   "Metadata": {
     "Ada.OpenApi": "{\"serviceName\":\"My Service\",\"enabled\":true}"
   }
   ```

2. **Routes disabled**: Check that `enabled` is `true` (or omitted, since it defaults to `true`).

3. **Invalid JSON in metadata**: The `Ada.OpenApi` value must be valid JSON. Verify with a JSON linter.

## Empty or Missing Paths in Aggregated Spec

**Symptom**: The aggregated spec has fewer paths than expected or no paths at all.

**Possible causes**:

1. **Path reachability**: Only paths that match the YARP route's `Match.Path` pattern are included. Check that the route pattern covers the downstream API paths.

   For example, if the route matches `/api/users/{**catch-all}` but the downstream API has paths under `/users/`, those paths won't be reachable.

2. **Non-analyzable transforms**: Routes with complex transforms may have their paths excluded. Check the `NonAnalyzableStrategy` setting:

   ```csharp
   options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.IncludeWithWarning;
   ```

3. **Missing cluster metadata**: Ensure the cluster has `Ada.OpenApi` metadata with the correct `openApiPath`:

   ```json
   "Metadata": {
     "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\"}"
   }
   ```

## Downstream Service Not Reachable

**Symptom**: The aggregated spec is empty or returns an error for a specific service.

**Possible causes**:

1. **Service not running**: Verify the downstream service is running and accessible from the gateway.

   ```bash
   curl http://localhost:5001/swagger/v1/swagger.json
   ```

2. **Wrong OpenAPI path**: The default path is `/swagger/v1/swagger.json`. If your service uses a different path, configure it in the cluster metadata:

   ```json
   "Ada.OpenApi": "{\"openApiPath\":\"/api/v1/openapi.json\"}"
   ```

3. **Fetch timeout**: If the downstream service is slow, increase the timeout:

   ```csharp
   options.DefaultFetchTimeoutMs = 30_000; // 30 seconds
   ```

4. **Failure caching**: After a failed fetch, the failure is cached for `FailureCacheDuration`. Invalidate the cache to retry immediately:

   ```bash
   curl -X POST http://localhost:5000/admin/cache/invalidate/my-service
   ```

## Schema Name Collisions

**Symptom**: Schema definitions are overwritten or missing in the aggregated spec.

**Solution**: Configure a unique `prefix` for each cluster to avoid collisions:

```json
{
  "user-service": {
    "Metadata": {
      "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"UserService\"}"
    }
  },
  "product-service": {
    "Metadata": {
      "Ada.OpenApi": "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"ProductService\"}"
    }
  }
}
```

## Stale Cached Responses

**Symptom**: The aggregated spec doesn't reflect recent changes to a downstream service.

**Solutions**:

1. **Wait for cache expiry**: Cached entries expire after `CacheDuration` (default 5 minutes).

2. **Invalidate the cache programmatically**:

   ```csharp
   await cacheInvalidator.InvalidateServiceAsync("my-service");
   ```

3. **Invalidate via admin endpoint** (if configured):

   ```bash
   curl -X POST http://localhost:5000/admin/cache/invalidate-all
   ```

4. **Reduce cache duration for development**:

   ```csharp
   options.CacheDuration = TimeSpan.FromSeconds(30);
   options.AggregatedSpecCacheDuration = TimeSpan.FromSeconds(30);
   ```

## Service Name Not Found (404)

**Symptom**: `GET /api-docs/my-service` returns 404.

**Possible causes**:

1. **Case sensitivity**: Service names support both original format and kebab-case. Try both:

   ```bash
   curl http://localhost:5000/api-docs/User%20Management
   curl http://localhost:5000/api-docs/user-management
   ```

2. **Service name mismatch**: Verify the exact `serviceName` in the route metadata matches what you're requesting. Check `GET /api-docs` for the list of available names.

## Large Specifications Truncated

**Symptom**: The aggregated spec appears incomplete.

**Solution**: Increase the maximum cache payload size:

```csharp
options.MaximumCachePayloadBytes = 5 * 1024 * 1024; // 5 MB
```

## Transform Warnings in Logs

**Symptom**: Log warnings about non-analyzable transforms.

**Explanation**: Some YARP transforms cannot be statically analyzed. The warnings indicate which routes have transforms that may affect path reachability analysis.

**Options**:

- **Accept the warnings**: The default `IncludeWithWarning` strategy includes all paths and logs warnings.
- **Suppress warnings**: Set `LogTransformWarnings = false` if you've verified the output is correct.
- **Switch strategy**: Use `ExcludeWithWarning` for stricter accuracy.

```csharp
options.LogTransformWarnings = false;
// or
options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.ExcludeWithWarning;
```

## Concurrent Fetch Limits

**Symptom**: Some services' specs are not fetched when there are many downstream services.

**Solution**: Increase the concurrent fetch limit:

```csharp
options.MaxConcurrentFetches = 20; // Default is 10
```

## Related Documentation

- [Configuration](openapi-configuration.md) - Full configuration reference
- [Caching](openapi-caching.md) - Cache behavior and invalidation
- [Advanced Topics](openapi-advanced.md) - Path analysis, schema renaming, transforms
