# Security & Performance Review — AdaptArch.Extensions.Yarp.OpenApi

**Date**: 2026-08-02
**Scope**: `src/OpenApi` (the `src/Auth` module is an empty placeholder and was excluded).
**Outcome**: all findings below were either fixed on the `security-performance-improvements` branch (with tests written first) or explicitly documented as accepted with rationale.

## Security findings

| # | Finding | Severity | Status |
|---|---------|----------|--------|
| S1 | 500 responses echoed `ex.Message` to the client (`OpenApiAggregationMiddleware`) | Medium | **Fixed** — generic `Internal server error` body; details stay in logs |
| S2 | Base path matched by prefix only: `/api-docsomething` was handled as `/api-docs` | Low | **Fixed** — the character after the base path must be a segment boundary |
| S3 | Unbounded recursion over downstream schemas (`SchemaRenamer`, `OpenApiDocumentPruner`): a cyclic or deeply nested inline schema caused an uncatchable `StackOverflowException`, killing the process | High | **Fixed** — renamer detects inline cycles and enforces a 256-level depth guard (catchable `InvalidOperationException`); pruner walks schemas iteratively with a visited set |
| S4 | No size limit on fetched downstream documents; a hostile/misbehaving service could exhaust gateway memory | Medium | **Fixed** — new `MaxDocumentSizeBytes` option (default 10 MB) enforced via `HttpClient.MaxResponseContentBufferSize` |
| S5 | Attacker-chosen cache keys: any `/api-docs/{name}` request created a cache entry (including cached `null`s for unknown names), enabling unbounded cache growth; case variants also multiplied entries | Medium | **Fixed** — service names are resolved against configured YARP services *before* the cache is touched (unknown → 404, no entry); keys use the canonical configured name; failed aggregations of known services are re-cached with `FailureCacheDuration` |
| S6 | Service name validation gaps: no length limit, no control-character filtering; 404 body echoed request input | Low | **Fixed** — 256-char limit, control characters rejected, responses no longer echo input |
| S7 | Culture-sensitive `StartsWith` in path logic (`RouteTransformAnalyzer`); case-insensitive schema-name map silently collided schemas differing only by case (`SchemaRenamer`) | Low | **Fixed** — ordinal comparisons; schema names treated as case-sensitive per OpenAPI semantics |
| S8 | No authentication/authorization on `/api-docs` and the sample's `/admin/cache/*` endpoints | Informational | **Documented** — the library intentionally leaves auth to the host; the sample and the docs (`docfx/docs/openapi-aggregation.md`, "Securing the Endpoints") now state that the middleware must be registered after auth and that admin endpoints need `RequireAuthorization` |

## Performance findings

| # | Finding | Status |
|---|---------|--------|
| P1 | `YarpOpenApiConfigurationReader` re-enumerated all routes/clusters and re-deserialized `Ada.OpenApi` metadata JSON on *every* lookup; `ServiceSpecificationAnalyzer.AnalyzeServices` made this quadratic per request | **Fixed** — metadata is parsed once per YARP config snapshot (invalidated by `IProxyConfig` instance identity) and served from dictionaries |
| P2 | `AnalyzeServices` ran twice per spec request (existence check + aggregation) | **Fixed** — the resolved `ServiceSpecification` is passed into the aggregation instead of re-analyzed |
| P3 | `PathReachabilityAnalyzer` analyzed each route's transforms up to 3× per path (`AnalyzeRoute` + `IsPathReachable` + `MapBackendToGatewayPath`) | **Fixed** — `RouteTransformAnalyzer.AnalyzeRoute` is memoized per `RouteConfig` instance (`ConditionalWeakTable`, so stale configs are collectable) and the reachability loop maps each path once |
| P4 | `OpenApiDocumentCacheWrapper` was serialized by HybridCache via reflection (breaking the AOT story of `OpenApiJsonContext`) and made an extra full UTF-8 byte copy on every cache read | **Fixed** — source-generated serializer registered via `AddSerializer`; parsing now uses `OpenApiDocument.Parse` on the cached string directly |
| P5 | Cluster documents were fetched sequentially | **Fixed** — fetches run in parallel bounded by `MaxConcurrentFetches` |
| P6 | `ToKebabCase` allocated 4 strings per service per request; responses lacked `Content-Length` | **Fixed** — memoized (keys are config-bounded service names); `Content-Length` set from the buffered body |
| P7 | Every cache hit still re-parses the cached JSON into an `OpenApiDocument` | **Accepted** — `Servers`/`Info` are mutated per request from `HttpContext`, so sharing one parsed instance across requests would race. Caching the parsed document per request scope was judged not worth the complexity at current traffic profiles; revisit if profiling shows the parse dominating |
| P8 | `WriteOpenApiResponse` buffers into a `MemoryStream` before writing | **Accepted** — the `Microsoft.OpenApi` writers are synchronous; writing directly to `Response.Body` would perform sync I/O (disallowed by Kestrel). Buffering also enables `Content-Length` |
| P9 | `ApplyReverseTransform` does chained `string.Replace` allocations per path×route | **Accepted** — micro-allocation; bounded by (paths × routes) per aggregation, which is already cached |

## Correctness findings

| # | Finding | Status |
|---|---------|--------|
| C1 | Declared-but-dead options: `DefaultOpenApiPath`, `EnableAutoDiscovery`, `FailureCacheDuration`, `MaxConcurrentFetches` had **zero** effect when configured | **Fixed** — all four are now honored. `AdaOpenApiClusterConfig.OpenApiPath` became nullable so the option can supply the default (breaking change for direct users of that class; config-file behavior unchanged) |
| C2 | `OpenApiMerger` aliased `source.Paths`/`source.Components` by reference in the single-document path | **Fixed** — collections are copied |
| C3 | `OpenApiDocumentFetcher` logged genuine client cancellation as a downstream timeout and swallowed it | **Fixed** — caller cancellation propagates; timeouts still log and fall through to fallback paths |
| C4 | Unused dependencies: `YamlDotNet` (YAML output actually uses `Microsoft.OpenApi`'s writer) and the legacy `Microsoft.AspNetCore.Http.Abstractions` 2.3.x package on a net10.0 project | **Fixed** — replaced with a `FrameworkReference` to `Microsoft.AspNetCore.App`; redundant `Configuration.Binder`/`Extensions.Http` package refs removed (NU1510) |
| C5 | Defensive null checks on non-nullable interface returns (`MergeDocuments`, `PruneDocument`, `ApplyPrefix`) look dead per annotations | **Accepted (kept)** — nullability is not enforced at runtime and these interfaces are public extension points; third-party implementations returning `null` fail cleanly instead of with a `NullReferenceException` |
| C6 | `IYarpOpenApiConfigurationReader.GetAllRouteOpenApiConfigs`/`GetAllClusterOpenApiConfigs` have no internal callers | **Accepted (kept)** — public API of a shipped package; now also served cheaply from the config snapshot |

## Behavioral changes to be aware of

- Unknown service names now return **404 `Service not found`** (previously a 500 caused by resolving cache dependencies, or a long-lived cached `null`).
- Cache keys and `service:{name}` invalidation tags now use the **canonical configured service name**, not the raw request path segment. Invalidation by original or kebab-case name via `IOpenApiCacheInvalidator` should pass the configured name.
- `AdaOpenApiClusterConfig.OpenApiPath` defaults to `null` instead of `/swagger/v1/swagger.json`; the effective default now comes from `OpenApiAggregationOptions.DefaultOpenApiPath` (same value out of the box).
- Cluster fetches for one service run in parallel (bounded by `MaxConcurrentFetches`); merge order remains deterministic.
- Setting `EnableAutoDiscovery = false` now actually restricts aggregation to clusters with explicit `Ada.OpenApi` metadata.

## Verification

- `dotnet build` clean with `TreatWarningsAsErrors` (no suppressions added).
- 297 unit tests + 96 integration tests pass; every behavioral fix landed with a test written first (TDD).
