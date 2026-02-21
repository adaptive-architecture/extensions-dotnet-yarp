# OpenAPI Aggregation

The OpenAPI Aggregation extension provides automatic aggregation of OpenAPI specifications from downstream services behind a YARP reverse proxy, presenting a unified API documentation experience through a single gateway endpoint.

## Overview

In a microservices architecture, each service typically exposes its own OpenAPI specification. When these services sit behind a YARP reverse proxy, consumers need a single, consolidated view of all available APIs. The OpenAPI Aggregation extension solves this by:

- **Fetching** OpenAPI specifications from each downstream service
- **Analyzing** YARP route configurations to determine reachable paths
- **Pruning** unreachable paths and unused components
- **Renaming** schemas to avoid naming collisions across services
- **Merging** multiple specifications into a single aggregated document
- **Caching** results for performance with configurable invalidation

## Architecture

```
                    ┌──────────────────────────────────────┐
                    │        API Gateway (YARP)            │
                    │                                      │
                    │  ┌────────────────────────────────┐  │
                    │  │ OpenAPI Aggregation Middleware │  │
                    │  │                                │  │
                    │  │  1. Read YARP configuration    │  │
                    │  │  2. Group routes by service    │  │
                    │  │  3. Fetch downstream specs     │  │
                    │  │  4. Analyze path reachability  │  │
                    │  │  5. Prune unreachable paths    │  │
                    │  │  6. Rename schemas (prefixing) │  │
                    │  │  7. Merge into single spec     │  │
                    │  │  8. Cache the result           │  │
                    │  └────────────────────────────────┘  │
                    │                                      │
                    └────────┬──────────────┬──────────────┘
                             │              │
                   ┌─────────▼──┐    ┌──────▼─────────┐
                   │ Service A  │    │   Service B    │
                   │ /swagger   │    │   /swagger     │
                   └────────────┘    └────────────────┘
```

## Key Features

- **Automatic Discovery**: Reads YARP route and cluster configuration to discover downstream services
- **Path Reachability Analysis**: Only includes API paths that are actually reachable through the gateway's route configuration
- **Schema Collision Avoidance**: Automatically prefixes schema names to prevent naming conflicts when merging specifications from multiple services
- **Component Pruning**: Removes unreferenced schemas, parameters, and other components after path pruning
- **Content Negotiation**: Serves aggregated specifications in both JSON and YAML formats
- **HybridCache Integration**: Uses `Microsoft.Extensions.Caching.Hybrid` for high-performance caching with configurable durations
- **Cache Invalidation**: Programmatic cache invalidation per service, per cluster, or globally
- **Transform Analysis**: Analyzes YARP path transforms to correctly map gateway paths to downstream paths
- **Configurable Info and Servers**: Customize the aggregated specification's info section and server list via delegates

## Processing Pipeline

The aggregation pipeline processes each service specification through the following stages:

| Stage | Component | Description |
|-------|-----------|-------------|
| 1. Configuration Reading | `IYarpOpenApiConfigurationReader` | Reads YARP routes and clusters with `Ada.OpenApi` metadata |
| 2. Service Grouping | `IServiceSpecificationAnalyzer` | Groups routes by service name to identify distinct services |
| 3. Document Fetching | `IOpenApiDocumentFetcher` | Retrieves OpenAPI specs from downstream services via HTTP |
| 4. Path Analysis | `IPathReachabilityAnalyzer` | Determines which API paths are reachable through gateway routes |
| 5. Transform Analysis | `IRouteTransformAnalyzer` | Analyzes YARP path transforms for correct path mapping |
| 6. Document Pruning | `IOpenApiDocumentPruner` | Removes unreachable paths and unused components |
| 7. Schema Renaming | `ISchemaRenamer` | Applies service-specific prefixes to avoid naming collisions |
| 8. Document Merging | `IOpenApiMerger` | Combines pruned and renamed specs into a single document |

## Exposed Endpoints

When registered with `app.UseYarpOpenApiAggregation("/api-docs")`, the middleware exposes:

| Endpoint | Description |
|----------|-------------|
| `GET /api-docs` | Lists all available service names |
| `GET /api-docs/{serviceName}` | Aggregated spec with content negotiation (JSON/YAML) |
| `GET /api-docs/{serviceName}/openapi.json` | Aggregated spec in JSON format |
| `GET /api-docs/{serviceName}/openapi.yaml` | Aggregated spec in YAML format |
| `GET /api-docs/{serviceName}/openapi.yml` | Aggregated spec in YAML format |

Service names support both their original format and kebab-case (e.g., both `User Management` and `user-management` work).

## Package Dependencies

The OpenAPI aggregation extension builds on:

- **[YARP](https://github.com/microsoft/reverse-proxy)** (v2.3.0) - Microsoft's reverse proxy library
- **[Microsoft.OpenApi](https://github.com/microsoft/OpenAPI.NET)** (v3.1.2) - OpenAPI document model
- **[Microsoft.Extensions.Caching.Hybrid](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid)** (v10.1.0) - High-performance caching
- **[YamlDotNet](https://github.com/aaubry/YamlDotNet)** (v16.3.0) - YAML serialization for OpenAPI output

## Module Identifier

The OpenApi module exposes a constant identifier:

```csharp
using AdaptArch.Extensions.Yarp.OpenApi;

Console.WriteLine($"Module: {OpenApiModule.Name}"); // Output: "OpenApi"
```

## Related Documentation

- [Getting Started](openapi-getting-started.md) - Installation and basic setup
- [Configuration](openapi-configuration.md) - Detailed configuration reference
- [Caching](openapi-caching.md) - Cache behavior and invalidation
- [Advanced Topics](openapi-advanced.md) - Path analysis, schema renaming, transforms
- [Troubleshooting](openapi-troubleshooting.md) - Common issues and solutions
