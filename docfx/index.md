---
_layout: landing
---

# Extensions for dotnet YARP

A collection of extensions for [dotnet YARP](https://github.com/microsoft/reverse-proxy) (Yet Another Reverse Proxy) designed to enhance reverse proxy capabilities with authentication, authorization, and OpenAPI integration.

## Package Overview

### AdaptArch.Extensions.Yarp.OpenApi
**OpenAPI extensions** for YARP reverse proxy:
- **OpenAPI Aggregation**: Automatically fetch, merge, and serve aggregated OpenAPI specifications from downstream services
- **Path Reachability Analysis**: Only include API paths that are actually reachable through gateway routes
- **Schema Collision Avoidance**: Automatic prefixing of schema names to prevent naming conflicts
- **Component Pruning**: Remove unreferenced schemas and components after path pruning
- **Content Negotiation**: Serve aggregated specs in JSON or YAML format
- **HybridCache Integration**: High-performance caching with per-service and global invalidation
- **Transform Analysis**: Analyze YARP path transforms for correct path mapping

### AdaptArch.Extensions.Yarp.Auth
**Authentication and authorization extensions** for YARP reverse proxy:
- Authentication middleware integration
- Authorization policy enforcement

## Key Benefits

- **YARP Integration**: Seamless integration with Microsoft's YARP reverse proxy
- **Automatic Discovery**: Reads YARP configuration to discover downstream services
- **OpenAPI Support**: Automatic API documentation aggregation from multiple services
- **Production Ready**: High-performance caching, configurable timeouts, and failure resilience
- **Testable**: Fully unit tested with high code coverage
- **Modern .NET**: Built on .NET 10 with latest language features
- **Dependency Injection Ready**: Full integration with Microsoft.Extensions.DependencyInjection

## Quick Start

```csharp
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add OpenAPI aggregation
builder.Services.AddYarpOpenApiAggregation(options =>
{
    options.CacheDuration = TimeSpan.FromMinutes(5);
    options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(5);
    options.FailureCacheDuration = TimeSpan.FromMinutes(1);
});

var app = builder.Build();

// Serve aggregated OpenAPI specs at /api-docs
app.UseYarpOpenApiAggregation("/api-docs");

app.MapReverseProxy();
app.Run();
```

## Getting Started

1. **Install packages** based on your needs
2. **Configure YARP routes** with `Ada.OpenApi` metadata
3. **Register services** and add middleware
4. **Explore the documentation** for detailed usage examples
5. **Check the API reference** for complete method signatures

These extensions provide essential capabilities for building production-ready reverse proxy solutions with YARP.
