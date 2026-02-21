# Advanced Topics

This page covers the internal processing pipeline in detail, including path reachability analysis, schema renaming, YARP transform analysis, and content negotiation.

## Path Reachability Analysis

Not all paths defined in a downstream service's OpenAPI specification are necessarily reachable through the gateway. The `IPathReachabilityAnalyzer` determines which paths are accessible based on the YARP route configuration.

### How It Works

Given a YARP route with `Match.Path = "/api/users/{**catch-all}"`, the analyzer determines that only paths starting with `/api/users/` in the downstream service's spec are reachable through this route.

```
Downstream service spec:
  /api/users           ✅ Reachable (matches /api/users/{**catch-all})
  /api/users/{id}      ✅ Reachable (matches /api/users/{**catch-all})
  /api/admin/users     ❌ Not reachable (no matching route)
  /internal/health     ❌ Not reachable (no matching route)
```

### Multiple Routes Per Service

When multiple routes point to the same service (same `serviceName`), all their path patterns are combined:

```json
{
  "Routes": {
    "users-route": {
      "Match": { "Path": "/api/users/{**catch-all}" },
      "Metadata": { "Ada.OpenApi": "{\"serviceName\":\"User Service\"}" }
    },
    "users-admin-route": {
      "Match": { "Path": "/api/admin/users/{**catch-all}" },
      "Metadata": { "Ada.OpenApi": "{\"serviceName\":\"User Service\"}" }
    }
  }
}
```

In this case, both `/api/users/*` and `/api/admin/users/*` paths are considered reachable.

## Document Pruning

After path analysis, the `IOpenApiDocumentPruner` removes unreachable content from the specification:

### Path Pruning

Paths that are not reachable through any gateway route are removed from the aggregated specification.

### Component Pruning

After paths are removed, components (schemas, parameters, request bodies, responses, etc.) that are no longer referenced by any remaining path are also pruned. This keeps the aggregated specification clean and focused.

```
Before pruning:
  Paths: /api/users, /api/users/{id}, /internal/health
  Schemas: User, UserList, HealthCheck, ErrorResponse

After pruning (assuming /internal/health is unreachable):
  Paths: /api/users, /api/users/{id}
  Schemas: User, UserList, ErrorResponse  (HealthCheck removed - unreferenced)
```

## Schema Renaming

When merging specifications from multiple services, schema name collisions are common. For example, both a User Service and a Product Service might define a `PaginatedResponse` or `ErrorResponse` schema.

### How Prefixing Works

The `ISchemaRenamer` applies a service-specific prefix (configured via the cluster's `prefix` metadata) to all schema names and their references:

```
Cluster metadata: { "prefix": "UserService" }

Before renaming:
  Schema: User
  Schema: PaginatedResponse

After renaming:
  Schema: UserServiceUser
  Schema: UserServicePaginatedResponse
```

### Configuring Prefixes

Set the prefix in the cluster's `Ada.OpenApi` metadata:

```json
{
  "Clusters": {
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
}
```

### What Gets Renamed

The renamer updates:

- Schema names in `components/schemas`
- All `$ref` references pointing to renamed schemas

This ensures the merged specification is internally consistent after renaming.

## Transform Analysis

YARP supports [request transforms](https://microsoft.github.io/reverse-proxy/articles/transforms.html) that modify the request path before forwarding to the downstream service. The `IRouteTransformAnalyzer` attempts to statically analyze these transforms to correctly map gateway paths to downstream paths.

### Analyzable Transforms

Transforms that can be statically analyzed include:

- **PathPrefix**: Adds a prefix to the request path
- **PathRemovePrefix**: Removes a prefix from the request path
- **PathPattern**: Replaces the path with a pattern
- **PathSet**: Sets the path to a fixed value

### Non-Analyzable Transforms

Some transforms cannot be statically analyzed because they depend on runtime values:

- Custom transform implementations
- Transforms that use request headers or query parameters
- Complex conditional transforms

### Handling Non-Analyzable Transforms

Configure how the aggregation handles routes with non-analyzable transforms:

```csharp
builder.Services.AddYarpOpenApiAggregation(options =>
{
    // Include paths but log a warning (default - safest option)
    options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.IncludeWithWarning;

    // Exclude paths and log a warning (stricter)
    options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.ExcludeWithWarning;

    // Skip the entire service
    options.NonAnalyzableStrategy = NonAnalyzableTransformStrategy.SkipService;

    // Enable/disable warning logs
    options.LogTransformWarnings = true;
});
```

## Content Negotiation

The aggregation middleware supports multiple output formats:

### JSON (Default)

```bash
# Via Accept header
curl -H "Accept: application/json" http://localhost:5000/api-docs/user-management

# Via explicit path
curl http://localhost:5000/api-docs/user-management/openapi.json
```

### YAML

```bash
# Via explicit path
curl http://localhost:5000/api-docs/user-management/openapi.yaml
curl http://localhost:5000/api-docs/user-management/openapi.yml
```

### Service Name Resolution

Service names in the URL support both their original format and kebab-case:

```bash
# Both of these work for a service named "User Management"
curl http://localhost:5000/api-docs/User%20Management
curl http://localhost:5000/api-docs/user-management
```

## Document Merging

The `IOpenApiMerger` combines multiple pruned and renamed specifications into a final aggregated document.

### Merge Behavior

- **Paths**: All paths from all services are combined into a single paths object
- **Schemas**: All renamed schemas are combined into `components/schemas`
- **Tags**: All tags are combined and deduplicated
- **Info**: Can be customized via `ConfigureInfo` delegate
- **Servers**: Can be customized via `ConfigureServers` delegate

### Handling Conflicts

If two services expose the same gateway path (e.g., both have `/api/health`), the later service's paths will overwrite the earlier one's. To avoid this, ensure your YARP route configuration gives each service distinct path prefixes.

## Service Listing

The `GET /api-docs` endpoint returns a JSON object listing all available services:

```bash
curl http://localhost:5000/api-docs
```

```json
{
  "services": [
    { "name": "User Management", "url": "/api-docs/user-management" },
    { "name": "Product Management", "url": "/api-docs/product-management" }
  ],
  "count": 2
}
```

Use the service names (or their kebab-case equivalents) to request individual aggregated specifications.

## Related Documentation

- [Configuration](openapi-configuration.md) - Full configuration reference
- [Caching](openapi-caching.md) - Cache behavior and invalidation
- [Troubleshooting](openapi-troubleshooting.md) - Common issues and solutions
