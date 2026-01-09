using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using AdaptArch.Extensions.Yarp.OpenApi.Merging;
using AdaptArch.Extensions.Yarp.OpenApi.Pruning;
using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using AdaptArch.Extensions.Yarp.OpenApi.Transforms;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdaptArch.Extensions.Yarp.OpenApi.Extensions;

/// <summary>
/// Extension methods for registering YARP OpenAPI aggregation services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds YARP OpenAPI aggregation services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional configuration action for <see cref="OpenApiAggregationOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYarpOpenApiAggregation(
        this IServiceCollection services,
        Action<OpenApiAggregationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register memory cache if not already registered
        services.TryAddSingleton<IMemoryCache, MemoryCache>();

        // Register HTTP client factory if not already registered
        services.AddHttpClient();

        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }

        // Register configuration and analysis services
        services.TryAddSingleton<IYarpOpenApiConfigurationReader, YarpOpenApiConfigurationReader>();
        services.TryAddSingleton<IRouteTransformAnalyzer, RouteTransformAnalyzer>();
        services.TryAddSingleton<IServiceSpecificationAnalyzer, ServiceSpecificationAnalyzer>();

        // Register OpenAPI processing services
        services.TryAddSingleton<IOpenApiDocumentFetcher, OpenApiDocumentFetcher>();
        services.TryAddSingleton<IPathReachabilityAnalyzer, PathReachabilityAnalyzer>();
        services.TryAddSingleton<IOpenApiDocumentPruner, OpenApiDocumentPruner>();
        services.TryAddSingleton<ISchemaRenamer, SchemaRenamer>();
        services.TryAddSingleton<IOpenApiMerger, OpenApiMerger>();

        return services;
    }
}
