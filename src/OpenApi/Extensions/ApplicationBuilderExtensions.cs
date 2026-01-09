using AdaptArch.Extensions.Yarp.OpenApi.Middleware;
using Microsoft.AspNetCore.Builder;

namespace AdaptArch.Extensions.Yarp.OpenApi.Extensions;

/// <summary>
/// Extension methods for registering YARP OpenAPI aggregation middleware in the application pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds YARP OpenAPI aggregation middleware to the application pipeline.
    /// This middleware exposes endpoints for discovering and retrieving aggregated OpenAPI specifications.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="basePath">The base path for OpenAPI endpoints. Defaults to "/api-docs".</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// The middleware exposes the following endpoints:
    /// <list type="bullet">
    /// <item><description>GET {basePath} - Returns a list of available service names</description></item>
    /// <item><description>GET {basePath}/{serviceName} - Returns the aggregated OpenAPI specification for the specified service</description></item>
    /// </list>
    /// The endpoints support content negotiation via the Accept header:
    /// <list type="bullet">
    /// <item><description>application/json - Returns OpenAPI spec in JSON format (default)</description></item>
    /// <item><description>application/yaml or text/yaml - Returns OpenAPI spec in YAML format</description></item>
    /// </list>
    /// </remarks>
    public static IApplicationBuilder UseYarpOpenApiAggregation(
        this IApplicationBuilder app,
        string basePath = "/api-docs")
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        // Ensure basePath starts with /
        if (!basePath.StartsWith('/'))
        {
            basePath = "/" + basePath;
        }

        // Remove trailing slash if present
        basePath = basePath.TrimEnd('/');

        app.UseMiddleware<OpenApiAggregationMiddleware>(basePath);

        return app;
    }
}
