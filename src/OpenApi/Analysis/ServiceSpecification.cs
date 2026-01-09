using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.Analysis;

/// <summary>
/// Represents a single OpenAPI service specification composed of one or more YARP routes.
/// Routes are grouped by their ServiceName metadata to create unified API documentation.
/// </summary>
public sealed class ServiceSpecification
{
    /// <summary>
    /// The unique service name that groups these routes together.
    /// This name is used as the identifier in the API docs endpoint (e.g., /api-docs/{serviceName}).
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Collection of route-to-cluster mappings that belong to this service.
    /// Each mapping includes the route configuration, its associated cluster, and OpenAPI metadata.
    /// </summary>
    public required IReadOnlyList<RouteClusterMapping> Routes { get; init; }
}

/// <summary>
/// Represents a YARP route paired with its target cluster and OpenAPI configuration.
/// This mapping provides all information needed to fetch and transform OpenAPI specs for a single route.
/// </summary>
public sealed class RouteClusterMapping
{
    /// <summary>
    /// The YARP route configuration including path patterns, transforms, and metadata.
    /// </summary>
    public required RouteConfig Route { get; init; }

    /// <summary>
    /// The YARP cluster configuration this route targets, including destination addresses and metadata.
    /// </summary>
    public required ClusterConfig Cluster { get; init; }

    /// <summary>
    /// OpenAPI-specific configuration from the route's metadata (Ada.OpenApi key).
    /// Contains ServiceName and Enabled flag.
    /// </summary>
    public required Configuration.AdaOpenApiRouteConfig RouteOpenApiConfig { get; init; }

    /// <summary>
    /// OpenAPI-specific configuration from the cluster's metadata (Ada.OpenApi key).
    /// Contains OpenApiPath and Prefix for schema collision avoidance.
    /// </summary>
    public required Configuration.AdaOpenApiClusterConfig ClusterOpenApiConfig { get; init; }
}
