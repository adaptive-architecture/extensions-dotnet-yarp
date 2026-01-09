namespace AdaptArch.Extensions.Yarp.OpenApi.Configuration;

/// <summary>
/// OpenAPI configuration for a YARP route, stored as JSON in route metadata under "Ada.OpenApi" key.
/// </summary>
public class AdaOpenApiRouteConfig
{
    /// <summary>
    /// Gets or sets the human-readable name for the service specification.
    /// </summary>
    /// <remarks>
    /// Routes with the same ServiceName will be grouped together into a single OpenAPI specification.
    /// This name is used in the service discovery endpoint and in the URL path.
    /// If not specified, the cluster ID will be used as the service name.
    /// </remarks>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include this route in the aggregated OpenAPI documentation.
    /// </summary>
    /// <remarks>
    /// Defaults to true. Set to false to exclude a specific route from the aggregation
    /// even if its cluster has OpenAPI configuration.
    /// </remarks>
    public bool Enabled { get; set; } = true;
}
