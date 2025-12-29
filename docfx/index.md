---
_layout: landing
---

# Extensions for dotnet YARP

A collection of extensions for [dotnet YARP](https://github.com/microsoft/reverse-proxy) (Yet Another Reverse Proxy) designed to enhance reverse proxy capabilities with authentication, authorization, and OpenAPI integration.

## Package Overview

### AdaptArch.Extensions.Yarp.Auth
**Authentication and authorization extensions** for YARP reverse proxy:
- Authentication middleware integration
- Authorization policy enforcement
- Token validation and transformation
- Identity provider integration

### AdaptArch.Extensions.Yarp.OpenApi
**OpenAPI extensions** for YARP reverse proxy:
- OpenAPI specification aggregation
- API documentation routing
- Swagger UI integration
- Dynamic API discovery

## Key Benefits

✅ **YARP Integration**: Seamless integration with Microsoft's YARP reverse proxy  
✅ **Authentication Ready**: Built-in support for common authentication scenarios  
✅ **OpenAPI Support**: Automatic API documentation aggregation  
✅ **Production Ready**: Battle-tested patterns and practices  
✅ **Testable**: Fully unit tested with high code coverage  
✅ **Modern .NET**: Built on .NET 10 with latest language features  

## Getting Started

```bash
# Install packages
dotnet add package AdaptArch.Extensions.Yarp.Auth
dotnet add package AdaptArch.Extensions.Yarp.OpenApi
```

## Quick Start

```csharp
using AdaptArch.Extensions.Yarp.Auth;
using AdaptArch.Extensions.Yarp.OpenApi;

// Auth module
Console.WriteLine($"Module: {AuthModule.Name}");

// OpenApi module
Console.WriteLine($"Module: {OpenApiModule.Name}");
```

## Documentation

- [API Reference](api/index.html)
- [GitHub Repository](https://github.com/adaptive-architecture/extensions-dotnet-yarp)

These extensions provide essential capabilities for building production-ready reverse proxy solutions with YARP.
