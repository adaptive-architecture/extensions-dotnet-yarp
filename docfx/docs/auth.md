# Auth

The Auth extension provides authentication and authorization capabilities for YARP reverse proxy.

## Overview

The `AdaptArch.Extensions.Yarp.Auth` package provides authentication and authorization extensions for YARP. It is designed to integrate with YARP's request pipeline to enforce security policies at the gateway level.

## Installation

```bash
dotnet add package AdaptArch.Extensions.Yarp.Auth
```

## Module Identifier

The Auth module exposes a constant identifier:

```csharp
using AdaptArch.Extensions.Yarp.Auth;

Console.WriteLine($"Module: {AuthModule.Name}"); // Output: "Auth"
```

## Status & Roadmap

This module is currently in early development. The package provides foundational building blocks for gateway-level authentication with YARP. Additional authentication and authorization features are planned for future releases.

## Related Documentation

- [OpenAPI Aggregation](openapi-aggregation.md) - API documentation aggregation for YARP
- [API Reference](../api/index.html) - Complete API documentation
