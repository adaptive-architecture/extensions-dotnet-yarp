namespace AdaptArch.Extensions.Yarp.OpenApi.Configuration;

/// <summary>
/// OpenAPI configuration for a YARP cluster, stored as JSON in cluster metadata under "Ada.OpenApi" key.
/// </summary>
public class AdaOpenApiClusterConfig
{
    /// <summary>
    /// Gets or sets the path to the OpenAPI document on the downstream service.
    /// </summary>
    /// <remarks>
    /// Defaults to "/swagger/v1/swagger.json" if not specified.
    /// This is the standard path used by ASP.NET Core with Swashbuckle.
    /// </remarks>
    public string OpenApiPath { get; set; } = "/swagger/v1/swagger.json";

    /// <summary>
    /// Gets or sets the prefix to apply to schema and tag names to avoid collisions during merging.
    /// </summary>
    /// <remarks>
    /// When multiple services are merged into one specification, this prefix is added to all
    /// schema names and tags from this cluster's OpenAPI document.
    /// Example: With prefix "UserService", schema "User" becomes "UserServiceUser".
    /// If not specified, no prefix is applied (may cause naming collisions).
    /// </remarks>
    public string? Prefix { get; set; }
}
