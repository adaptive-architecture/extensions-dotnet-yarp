# OpenAPI Aggregation - Extra Details

> This document contains deep technical implementation details for the OpenAPI Aggregation feature.  
> **Last Updated**: 2026-01-10

## Implementation Details

### OpenApiDocumentFetcher

**Purpose**: Fetches OpenAPI documents from downstream services with caching and fallback support.

**Responsibilities**:
- Fetch OpenAPI specs from HTTP endpoints
- Cache fetched documents to reduce downstream load
- Implement fallback paths for discovery
- Handle fetch failures gracefully

#### Algorithm

```
Given cluster address A and OpenAPI path P:
1. Generate cache key: K = A + P
2. Check IMemoryCache for key K
3. If cached and not expired:
   a. Return cached document
4. If not cached:
   a. Acquire semaphore (respecting MaxConcurrentFetches limit)
   b. Send HTTP GET to A + P
   c. If 200 OK:
      - Parse JSON response as OpenAPI document
      - Store in cache with sliding expiration = CacheDuration
      - Release semaphore
      - Return document
   d. If non-200 or error:
      - For each fallback path F in FallbackPaths:
        i. Send HTTP GET to A + F
        ii. If 200 OK, parse and cache
        iii. Return document
   e. If all attempts fail:
      - Release semaphore
      - Log error
      - Return null
5. Return fetched document (or null)
```

**Caching Strategy**:
- **Cache key**: `{clusterAddress}{openApiPath}` (e.g., `http://localhost:5001/swagger/v1/swagger.json`)
- **Expiration**: Sliding expiration based on `CacheDuration` option (default 5 minutes)
- **Eviction**: Automatic via `IMemoryCache` LRU eviction when memory pressure increases
- **Thread safety**: `IMemoryCache` is thread-safe; semaphore limits concurrent fetches

**Performance Characteristics**:
- **Cached retrieval**: O(1) dictionary lookup, typically <1ms
- **Fresh fetch**: O(network latency + parse time), typically 100-500ms for local services
- **Memory usage**: O(m × s) where m = number of services, s = average spec size (~200KB per service)

### RouteTransformAnalyzer

**Purpose**: Analyzes YARP route transforms to understand how external paths map to downstream paths.

**Supported Transforms**:
- **PathPattern**: Direct pattern matching with route values (e.g., `/api/{version}/users/{id}`)
- **PathPrefix**: Adds prefix to downstream path (e.g., add `/api` → `/api/users`)
- **PathRemovePrefix**: Removes prefix from external path (e.g., remove `/external` → `/users`)
- **PathSet**: Sets exact downstream path (ignores matched path)
- **No transforms**: 1:1 path mapping (external path = downstream path)

**Algorithm**:
```
Given external path P_ext and transforms T[]:
1. Initialize downstream path: P_down = P_ext
2. For each transform T in T[] (applied in sequence):
   a. If T is PathRemovePrefix with prefix X:
      - If P_down starts with X:
        - P_down = P_down without X prefix
   b. If T is PathPrefix with prefix Y:
      - P_down = Y + P_down
   c. If T is PathSet with path Z:
      - P_down = Z (replaces entire path)
   d. If T is PathPattern with pattern W:
      - Extract route values from P_ext using route match pattern
      - Apply route values to W
      - P_down = result
   e. If T is unrecognized/custom:
      - Return null (non-analyzable)
3. Return P_down (or null if analysis failed)
```

**Non-Analyzable Transforms**:
- Custom transforms not in the standard set
- Transforms with runtime-dependent logic (e.g., based on headers)
- Complex regex-based transforms
- Handled per `NonAnalyzableTransformStrategy` option

**Examples**:

Example 1 - PathRemovePrefix:
```
External path: /api/users/123
Transform: PathRemovePrefix("/api")
Result: /users/123
```

Example 2 - PathPattern:
```
External path: /users/123
Transform: PathPattern("/api/v1/users/{id}")
Route values: { id: "123" }
Result: /api/v1/users/123
```

Example 3 - Multi-stage:
```
External path: /external/api/users/123
Transforms:
  1. PathRemovePrefix("/external")
  2. PathPrefix("/internal")
Result after step 1: /api/users/123
Result after step 2: /internal/api/users/123
```

### PathReachabilityAnalyzer

**Purpose**: Determines which downstream paths from OpenAPI specs are reachable through YARP route configuration.

**Algorithm**:
```
Given downstream path P_down and routes R[]:
1. Initialize: reachable = false
2. For each route R in R[]:
   a. Get external pattern: R.Match.Path
   b. Get transforms: R.Transforms
   c. For each possible external path that matches R.Match.Path:
      i. Apply RouteTransformAnalyzer to get downstream path
      ii. If downstream path == P_down:
         - reachable = true
         - Record R as matching route
3. Return reachability verdict + list of matching routes
```

**Matching Logic**:
- **Exact match**: `/api/users` matches only `/api/users`
- **Wildcard match**: `/api/users/{**catch-all}` matches `/api/users/*` (any suffix)
- **Parameter match**: `/api/users/{id}` matches `/api/users/123` (single segment)
- **No match**: `/admin/users` with routes for `/api/**` → unreachable

**Edge Cases**:
- **Multiple routes to same path**: All routes recorded, path marked as reachable
- **Overlapping wildcards**: Most specific route preferred for analysis
- **Ambiguous transforms**: Conservative approach marks as reachable with warning

**Optimization**: Pre-computes route patterns to avoid repeated parsing.

### SchemaRenamer

**Purpose**: Renames schemas to avoid collisions during merging and updates all `$ref` references throughout the document.

**Algorithm**:
```
Given OpenAPI document D and prefix P:
1. Build schema inventory:
   - Collect all schema names from D.components.schemas
   - Create rename map: Map<string, string>
2. For each schema S in inventory:
   a. Original name: N_orig = S.name
   b. New name: N_new = P + N_orig
   c. Add to rename map: rename_map[N_orig] = N_new
3. Rename schema definitions:
   - For each schema in D.components.schemas:
     - Rename key from N_orig to N_new
4. Update all references recursively:
   - Call WalkAndRename(D.root, rename_map)
5. Return updated document

WalkAndRename(node, rename_map):
  If node is object:
    For each property (key, value) in node:
      If key == "$ref" and value is string:
        - Extract schema name from ref (e.g., "#/components/schemas/User" → "User")
        - If schema name in rename_map:
          - Update value to new ref with new name
      Else:
        - WalkAndRename(value, rename_map)
  Else if node is array:
    For each item in node:
      - WalkAndRename(item, rename_map)
```

**$ref Formats Handled**:
- `#/components/schemas/User` → `#/components/schemas/UserServiceUser`
- Also handles refs in: allOf, oneOf, anyOf, not, properties, items, additionalProperties

**Recursive Walking**: Visits all objects and arrays in the document tree, ensuring no reference is missed.

**Collision Handling**:
- Prefixes prevent most collisions
- If prefix itself causes collision (e.g., two services with prefix "UserService"), logged as warning
- Recommendation: Use unique prefixes per cluster

### OpenApiDocumentPruner

**Purpose**: Removes paths, schemas, and components that are unreachable or unused to keep specs clean and focused.

**Two-Phase Algorithm**:

**Phase 1: Path Pruning**
```
Given document D and reachability map R:
1. Create list of paths to remove: paths_to_remove = []
2. For each path P in D.paths:
   a. If P not in R (not reachable):
      - Add P to paths_to_remove
3. For each path in paths_to_remove:
   - Remove from D.paths
```

**Phase 2: Component Pruning (Schema, Response, Parameter)**
```
Given pruned document D:
1. Build reference graph:
   a. Initialize: in_use = Set()
   b. Scan all paths, operations, parameters, responses for $ref
   c. For each $ref found:
      - Extract referenced component name
      - Add to in_use set
      - Recursively scan referenced component for more $refs
2. Prune unused components:
   a. For each schema S in D.components.schemas:
      - If S not in in_use:
        - Remove S from D.components.schemas
   b. Repeat for:
      - D.components.responses
      - D.components.parameters
      - D.components.examples
      - D.components.requestBodies
      - D.components.headers
```

**Graph Traversal**: Depth-first search from paths to find all reachable components. Handles circular references by tracking visited nodes.

**Example**:
```
Schemas defined: User, Address, Country, AdminSettings
Paths use: User, Address
User references: Address
Address references: Country

Result:
- User: ✅ in use (referenced by path)
- Address: ✅ in use (referenced by User)
- Country: ✅ in use (referenced by Address)
- AdminSettings: ❌ unused (removed)
```

### OpenApiMerger

**Purpose**: Merges multiple OpenAPI documents into a single unified specification.

**Merging Strategy**:

```
Given documents D1, D2, ..., Dn:
1. Create empty result document R
2. Merge paths:
   - For each Di:
     - Add all paths from Di.paths to R.paths
     - If path already exists: log warning (shouldn't happen after renaming)
3. Merge components:
   - Merge D1.components.schemas ∪ D2.components.schemas ∪ ... ∪ Dn.components.schemas → R.components.schemas
   - Merge responses, parameters, examples similarly
   - Collisions: Log warning (should be prevented by prefixing)
4. Merge tags:
   - Union of all tags from D1.tags ∪ D2.tags ∪ ... ∪ Dn.tags
   - Deduplicate by tag name (first occurrence wins for description)
5. Merge servers:
   - Union of all server URLs
   - Deduplicate by server URL
6. Merge security:
   - Union of security schemes
   - Union of security requirements (OR logic)
7. Build info section:
   - Title: "Aggregated API - " + comma-separated service names
   - Description: Concatenated descriptions from all services
   - Version: "aggregated" or highest version number
8. Return R
```

**Conflict Resolution**:
- **Paths**: Should not conflict (different services expose different paths)
- **Schemas**: Conflicts prevented by prefixing (performed before merging)
- **Tags**: Deduplicated by name
- **Servers**: Deduplicated by URL

### OpenApiAggregationMiddleware

**Purpose**: ASP.NET Core middleware that serves aggregated OpenAPI specifications via HTTP endpoints.

**Endpoints**:
1. **List services**: `GET /api-docs`
   - Returns JSON: `{ "services": ["service1", "service2"], "count": 2 }`
2. **Get service spec (JSON)**: `GET /api-docs/{serviceName}`
   - Returns aggregated OpenAPI spec as JSON
3. **Get service spec (YAML)**: `GET /api-docs/{serviceName}?format=yaml`
   - Returns aggregated OpenAPI spec as YAML

**Request Processing**:
```
On HTTP request:
1. Match path: Is it /api-docs or /api-docs/{serviceName}?
2. If /api-docs:
   - Get list of service names from ServiceSpecificationAnalyzer
   - Return as JSON
3. If /api-docs/{serviceName}:
   a. Parse query string for format parameter (json/yaml, default: json)
   b. Call ServiceSpecificationAnalyzer to get ServiceSpecification for serviceName
   c. For each route in ServiceSpecification:
      i. Fetch OpenAPI doc from downstream (via OpenApiDocumentFetcher)
      ii. Analyze reachability (via PathReachabilityAnalyzer)
      iii. Prune unreachable paths (via OpenApiDocumentPruner)
      iv. Rename schemas (via SchemaRenamer)
      v. Prune unused components (via OpenApiDocumentPruner)
   d. Merge all processed docs (via OpenApiMerger)
   e. Serialize to requested format (JSON/YAML)
   f. Return with appropriate content-type header
4. If path doesn't match:
   - Call next middleware in pipeline
```

## Complex Scenarios

### Scenario 1: Multiple Routes with Different Transforms to Same Service

**Description**: A YARP gateway has multiple routes pointing to the same downstream service, each with different path transforms. This requires union logic to determine overall reachability.

**Example Configuration**:
```json
"route-1": {
  "ClusterId": "user-service",
  "Match": { "Path": "/api/users/{**catch-all}" },
  "Transforms": [],
  "Metadata": { "Ada.OpenApi": "{\"serviceName\":\"User Management\"}" }
},
"route-2": {
  "ClusterId": "user-service",
  "Match": { "Path": "/v2/users/{**catch-all}" },
  "Transforms": [
    { "PathRemovePrefix": "/v2" },
    { "PathPrefix": "/api" }
  ],
  "Metadata": { "Ada.OpenApi": "{\"serviceName\":\"User Management\"}" }
}
```

**Approach**:
1. Service analyzer groups both routes by `serviceName` ("User Management")
2. Path reachability analyzer evaluates each route independently
3. A downstream path `/api/users/123` is reachable via:
   - **route-1**: External path `/api/users/123` (no transforms)
   - **route-2**: External path `/v2/users/123` (after transforms)
4. Both routes recorded in reachability metadata
5. Path included in aggregated spec (union logic: reachable via at least one route)

**Result**: Downstream paths are included if reachable through *any* configured route to the service.

### Scenario 2: Schema Circular References

**Description**: Schemas reference each other in cycles (e.g., `User` → `Organization` → `User`). The renaming and pruning algorithms must handle this gracefully.

**Example**:
```json
"User": {
  "properties": {
    "id": { "type": "string" },
    "organization": { "$ref": "#/components/schemas/Organization" }
  }
},
"Organization": {
  "properties": {
    "id": { "type": "string" },
    "owner": { "$ref": "#/components/schemas/User" }
  }
}
```

**Approach**:
- **Schema renamer**: Renames both schemas to `UserServiceUser` and `UserServiceOrganization`
- **Reference rewriting**: Updates all `$ref` properties (doesn't follow references, just rewrites strings)
- **Component pruner**: Uses visited set to avoid infinite loops during graph traversal
- **Result**: Both schemas retained if `User` is reachable (both are transitively in use)

**Key insight**: Algorithms are $ref-aware (update references) but don't follow $ref chains during certain phases to avoid infinite loops.

### Scenario 3: Non-Analyzable Custom Transforms

**Description**: A route uses a custom YARP transform that isn't recognized by the standard `RouteTransformAnalyzer`.

**Example**:
```json
"Transforms": [
  { "CustomGeoTransform": "region=us-west" }
]
```

**Approach**:
1. Transform analyzer encounters unrecognized transform type
2. Returns `null` (analysis failed)
3. Behavior determined by `NonAnalyzableTransformStrategy` option:
   - **IncludeWithWarning**: Include all paths from service, log warning
   - **ExcludeWithWarning**: Exclude all paths from service, log warning
   - **SkipService**: Skip entire service from aggregation
4. Warning logged: "Unable to analyze transforms for route X; using strategy Y"

**Extensibility**: Users can implement `IRouteTransformAnalyzer` to handle custom transforms.

## Integration Points

### Integration with YARP

- **Configuration reading**: Accesses YARP config via `IProxyConfigProvider`
- **Metadata access**: Reads route and cluster metadata dictionaries
- **Configuration reload**: Listens for YARP config changes (future enhancement)
- **Non-intrusive**: Does not modify YARP's request processing pipeline

**Key YARP types used**:
- `RouteConfig`: Route definitions with match patterns, transforms, metadata
- `ClusterConfig`: Cluster definitions with destinations, metadata
- `ITransformBuilder`: Understanding of standard YARP transforms

### Integration with ASP.NET Core

- **DI container**: All services registered via `AddYarpOpenApiAggregation()`
- **Middleware pipeline**: Registered via `UseYarpOpenApiAggregation()`
- **Configuration system**: Uses `IOptions<OpenApiAggregationOptions>`
- **Logging**: Uses `ILogger<T>` for all logging
- **HTTP client factory**: Uses `IHttpClientFactory` for downstream requests

**Middleware placement**: Should be placed before `app.MapReverseProxy()` so aggregation endpoints aren't proxied.

### Integration with OpenAPI Ecosystem

**Output compatibility**:
- Produces standard **OpenAPI 3.0** JSON and YAML
- Compatible with:
  - Swagger UI
  - ReDoc
  - Postman
  - Insomnia
  - OpenAPI Generator (client generation)
  - NSwag (client generation)

**Input compatibility**:
- Accepts OpenAPI 3.0+ from downstream services
- Parses using `Microsoft.OpenApi` library (standard OpenAPI parser)

## Edge Cases and Corner Cases

### Edge Case 1: Empty Downstream Specs

**Scenario**: Downstream service returns a valid OpenAPI document with empty `paths: {}` object.

**Handling**: 
- Document fetched and cached normally
- No paths to analyze or prune
- Results in service with no exposed endpoints in aggregation
- Logged as informational message: "Service X has no paths defined"

### Edge Case 2: Malformed Downstream Specs

**Scenario**: Downstream service returns invalid JSON, non-OpenAPI content, or malformed OpenAPI.

**Handling**:
- Fetcher catches `JsonException` or `OpenApiException` during parsing
- Logs error with service details and exception message
- Service is skipped from aggregation (not included in merged result)
- Other services continue processing normally
- User sees warning in logs but aggregation proceeds for valid services

### Edge Case 3: All Paths Pruned (No Reachable Paths)

**Scenario**: After reachability analysis, all paths in a downstream spec are marked as unreachable.

**Handling**:
- All paths pruned, resulting in empty `paths: {}` object
- Service included in aggregation but with no paths
- Logged as warning: "Service X has no reachable paths through configured routes"
- Likely indicates misconfiguration (routes don't match service paths)

### Edge Case 4: Duplicate Route Metadata (Inconsistent Prefixes)

**Scenario**: Multiple routes with same `serviceName` point to clusters with different `prefix` values in metadata.

**Handling**:
- Service analyzer groups all routes by `serviceName`
- Uses prefix from first-encountered cluster
- Logs warning: "Inconsistent prefixes for service X: found [prefix1, prefix2]"
- Recommendation: Use consistent prefixes across all clusters for the same service

### Corner Case 1: Route with No Match Pattern

**Scenario**: Route configuration missing `Match.Path` (invalid config, but defensive handling).

**Handling**:
- Configuration reader logs error
- Route skipped from analysis
- Does not crash aggregation pipeline

### Corner Case 2: Cluster with No Destinations

**Scenario**: Cluster defined but has no destinations (invalid config).

**Handling**:
- Cannot determine cluster address for fetching
- Logs error: "Cluster X has no destinations"
- Service skipped from aggregation

## Performance Considerations

### Caching Effectiveness

**Best Case** (Cache hit):
- Response time: <10ms
- Downstream load: Zero (cache serves request)
- CPU: Minimal (cache lookup + serialization)

**Worst Case** (Cache miss, cold start):
- Response time: 100-500ms (network latency + parsing + processing)
- Downstream load: One HTTP GET per service
- CPU: JSON parsing + tree walking + merging

**Expected Cache Hit Ratio**: >95% in typical usage (OpenAPI specs rarely change)

**Cache warming**: Consider making initial requests to aggregation endpoints during application startup to warm cache.

### Memory Usage

**Per Service**:
- Raw OpenAPI JSON: ~50-500 KB (varies by API complexity)
- Parsed `OpenApiDocument` object graph: ~2-5× raw JSON size
- Cached duration: 5 minutes default (sliding expiration)
- **Total per service**: ~200 KB - 2 MB

**Total Memory** (for 10 services): ~2-20 MB (acceptable overhead)

**Memory management**: `IMemoryCache` automatically evicts entries under memory pressure using LRU policy.

### CPU Usage

**Expensive Operations**:
- **JSON parsing**: O(n) where n = document size (~10-50ms for typical specs)
- **Schema renaming**: O(s × r) where s = schema count, r = total $ref count (~5-20ms)
- **Path reachability**: O(p × r) where p = path count, r = route count (~1-10ms)
- **Component pruning**: O(c) where c = component count (~1-5ms)
- **Merging**: O(p1 + p2 + ... + pn) where pn = paths in document n (~5-15ms)

**Total processing time** (cold cache): 25-100ms for typical services

**Optimization**: Caching amortizes parsing and processing costs across many requests.

### Concurrency

**Thread Safety**:
- `IMemoryCache` is thread-safe (concurrent reads/writes safe)
- Middleware processes requests concurrently (ASP.NET Core handles this)
- Fetcher uses `SemaphoreSlim` to limit concurrent downstream requests (`MaxConcurrentFetches`)

**Cold Start Thundering Herd**:
- Scenario: Multiple requests arrive before cache warmed
- Without mitigation: All requests attempt to fetch simultaneously
- Mitigation: `SemaphoreSlim` limits to `MaxConcurrentFetches` (default: 10)
- Subsequent requests wait for first request to complete and populate cache

**Scalability**: Each gateway instance has its own cache (no shared state across instances).

## Extensibility Points

### Custom Transform Analyzers

**Interface**: `IRouteTransformAnalyzer`

**Use Case**: Support custom YARP transforms not in the standard set.

**Implementation**:
```csharp
public class CustomTransformAnalyzer : IRouteTransformAnalyzer
{
    public string? AnalyzeTransforms(
        string externalPath, 
        IReadOnlyList<IReadOnlyDictionary<string, string>> transforms)
    {
        foreach (var transform in transforms)
        {
            if (transform.ContainsKey("CustomGeoTransform"))
            {
                // Apply custom logic
                var region = transform["CustomGeoTransform"];
                return ApplyGeoTransform(externalPath, region);
            }
        }
        
        // Delegate to default analyzer for standard transforms
        return null; // or fallback implementation
    }
}

// Register:
services.AddSingleton<IRouteTransformAnalyzer, CustomTransformAnalyzer>();
```

### Custom Schema Renaming

**Interface**: `ISchemaRenamer`

**Use Case**: Apply different naming strategies (e.g., namespace-style, hash-based, version-aware).

**Example**:
```csharp
public class NamespaceSchemaRenamer : ISchemaRenamer
{
    public OpenApiDocument RenameSchemas(OpenApiDocument document, string prefix)
    {
        // Use namespace-style: User → Services.UserManagement.User
        // Implementation: build rename map, update schemas, walk document
        // Similar to default implementation but with namespace separators
    }
}
```

### Custom Document Fetching

**Interface**: `IOpenApiDocumentFetcher`

**Use Case**: Fetch from authenticated endpoints, non-HTTP sources, or databases.

**Example**:
```csharp
public class AuthenticatedFetcher : IOpenApiDocumentFetcher
{
    private readonly ITokenProvider _tokenProvider;
    
    public async Task<OpenApiDocument?> FetchAsync(
        string clusterAddress, 
        string openApiPath, 
        CancellationToken ct)
    {
        var token = await _tokenProvider.GetTokenAsync();
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        var response = await client.GetAsync(clusterAddress + openApiPath, ct);
        if (!response.IsSuccessStatusCode) return null;
        
        var json = await response.Content.ReadAsStringAsync(ct);
        var reader = new OpenApiStringReader();
        return reader.Read(json, out var diagnostic);
    }
}
```

## Troubleshooting

### Issue: Aggregated spec is empty (no paths)

**Symptoms**: GET `/api-docs/{service}` returns `{ "paths": {} }` or empty array.

**Possible Causes**:
1. No routes have matching `serviceName` in metadata
2. All paths marked as unreachable by reachability analyzer
3. Downstream service is unreachable or returning errors

**Solutions**:
1. Verify route metadata: `"Ada.OpenApi": "{\"serviceName\":\"MyService\",\"enabled\":true}"`
2. Enable debug logging: `"AdaptArch.Extensions.Yarp.OpenApi": "Debug"`
3. Check reachability analysis logs for warnings
4. Test downstream OpenAPI endpoint directly: `curl http://localhost:5001/swagger/v1/swagger.json`
5. Verify YARP route patterns match downstream paths

### Issue: Schema name collisions or errors

**Symptoms**: Unexpected behavior, errors about duplicate schema names, or `$ref` resolution failures.

**Possible Causes**:
- Missing or empty `prefix` in cluster metadata
- Multiple clusters with same prefix for different services
- Malformed `$ref` references in downstream specs

**Solutions**:
- Add unique `prefix` to each cluster: `"Ada.OpenApi": "{\"prefix\":\"UserService\"}"`
- Verify prefixes are unique across clusters serving different services
- Check downstream OpenAPI specs for validity
- Enable debug logging to see schema renaming process

### Issue: Slow first-request response time

**Symptoms**: First request to aggregated spec takes >1 second, but subsequent requests are fast.

**Possible Causes**:
- Cache expired or cold start (expected behavior)
- Slow downstream services
- Many services being fetched concurrently

**Solutions**:
- This is expected behavior on cold start (caching working as designed)
- Increase `CacheDuration` for longer cache retention: `options.CacheDuration = TimeSpan.FromMinutes(10)`
- Warm cache on startup: Make request to each service endpoint during app initialization
- Check downstream service performance (they may be slow to respond)
- Adjust `MaxConcurrentFetches` if overwhelming downstream services

### Issue: Paths missing from aggregated spec

**Symptoms**: Downstream service has paths that don't appear in aggregation, even though they should be reachable.

**Possible Causes**:
- Paths are unreachable through YARP route configuration
- YARP route pattern doesn't match downstream paths
- Complex transforms not analyzed correctly
- Path transform analysis returned null (non-analyzable)

**Solutions**:
- Verify YARP route patterns cover intended paths
- Use wildcard patterns for broad coverage: `/api/{**catch-all}`
- Check transform analysis logs for warnings about non-analyzable transforms
- Enable debug logging to see path reachability analysis: `"AdaptArch.Extensions.Yarp.OpenApi": "Debug"`
- Review `NonAnalyzableTransformStrategy` setting
- Implement custom `IRouteTransformAnalyzer` for custom transforms

---

**Document Version**: 1.0.0  
**Last Updated**: 2026-01-10
