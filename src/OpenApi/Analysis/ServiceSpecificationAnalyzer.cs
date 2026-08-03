using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.Analysis;

/// <summary>
/// Analyzes YARP configuration to group routes by service name and create service specifications.
/// </summary>
public interface IServiceSpecificationAnalyzer
{
    /// <summary>
    /// Analyzes the current YARP configuration and groups routes by their ServiceName metadata.
    /// </summary>
    /// <returns>Collection of service specifications, each containing routes for a single service.</returns>
    IReadOnlyList<ServiceSpecification> AnalyzeServices();
}

/// <summary>
/// Default implementation of <see cref="IServiceSpecificationAnalyzer"/>.
/// Groups YARP routes by their Ada.OpenApi ServiceName metadata to create unified service specifications.
/// </summary>
public sealed partial class ServiceSpecificationAnalyzer : IServiceSpecificationAnalyzer
{
    private readonly IYarpOpenApiConfigurationReader _configReader;
    private readonly IOptionsMonitor<OpenApiAggregationOptions> _optionsMonitor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceSpecificationAnalyzer"/> class.
    /// </summary>
    /// <param name="configReader">The YARP OpenAPI configuration reader.</param>
    /// <param name="optionsMonitor">The options monitor for configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public ServiceSpecificationAnalyzer(
        IYarpOpenApiConfigurationReader configReader,
        IOptionsMonitor<OpenApiAggregationOptions> optionsMonitor,
        ILogger<ServiceSpecificationAnalyzer> logger)
    {
        _configReader = configReader;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Route {routeId} has no Ada.OpenApi metadata, skipping")]
    private partial void LogRouteNoMetadata(string routeId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Route {routeId} has OpenAPI disabled, skipping")]
    private partial void LogRouteDisabled(string routeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {routeId} has empty ServiceName in Ada.OpenApi metadata, skipping")]
    private partial void LogRouteEmptyServiceName(string routeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {routeId} has no ClusterId assigned, skipping")]
    private partial void LogRouteNoClusterId(string routeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {routeId} references cluster {clusterId} which does not exist, skipping")]
    private partial void LogRouteInvalidCluster(string routeId, string clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Route {routeId} added to service '{serviceName}'")]
    private partial void LogRouteAddedToService(string routeId, string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service specification created: '{serviceName}' with {routeCount} route(s)")]
    private partial void LogServiceSpecificationCreated(string serviceName, int routeCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Route {routeId} skipped: cluster {clusterId} has no Ada.OpenApi metadata and auto-discovery is disabled")]
    private partial void LogRouteClusterNotDiscoverable(string routeId, string clusterId);

    /// <inheritdoc/>
    public IReadOnlyList<ServiceSpecification> AnalyzeServices()
    {
        var options = _optionsMonitor.CurrentValue;
        var routes = _configReader.GetAllRoutes().ToList();
        var clusters = _configReader.GetAllClusters().ToList();

        // Build cluster lookup dictionary
        var clusterLookup = clusters.ToDictionary(c => c.ClusterId, StringComparer.OrdinalIgnoreCase);

        // Group routes by service name
        var serviceGroups = new Dictionary<string, List<RouteClusterMapping>>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in routes)
        {
            var mapping = TryCreateMapping(route, clusterLookup, options);
            if (mapping == null)
            {
                continue;
            }

            // Add to service group
            var serviceName = mapping.RouteOpenApiConfig.ServiceName!;
            if (!serviceGroups.TryGetValue(serviceName, out var mappings))
            {
                mappings = [];
                serviceGroups[serviceName] = mappings;
            }

            mappings.Add(mapping);
            LogRouteAddedToService(route.RouteId, serviceName);
        }

        // Create service specifications
        var specifications = new List<ServiceSpecification>(serviceGroups.Count);
        foreach (var (serviceName, mappings) in serviceGroups)
        {
            var spec = new ServiceSpecification
            {
                ServiceName = serviceName,
                Routes = mappings
            };
            specifications.Add(spec);
            LogServiceSpecificationCreated(serviceName, mappings.Count);
        }

        return specifications;
    }

    /// <summary>
    /// Validates a single route against its metadata and cluster; returns the
    /// route-to-cluster mapping or null when the route does not participate in aggregation.
    /// </summary>
    private RouteClusterMapping? TryCreateMapping(
        RouteConfig route,
        Dictionary<string, ClusterConfig> clusterLookup,
        OpenApiAggregationOptions options)
    {
        // Read route OpenAPI config
        var routeConfig = _configReader.GetRouteOpenApiConfig(route.RouteId);
        if (routeConfig == null)
        {
            LogRouteNoMetadata(route.RouteId);
            return null;
        }

        // Skip disabled routes
        if (!routeConfig.Enabled)
        {
            LogRouteDisabled(route.RouteId);
            return null;
        }

        // Validate service name
        if (String.IsNullOrWhiteSpace(routeConfig.ServiceName))
        {
            LogRouteEmptyServiceName(route.RouteId);
            return null;
        }

        // Lookup cluster
        if (String.IsNullOrWhiteSpace(route.ClusterId))
        {
            LogRouteNoClusterId(route.RouteId);
            return null;
        }

        if (!clusterLookup.TryGetValue(route.ClusterId, out var cluster))
        {
            LogRouteInvalidCluster(route.RouteId, route.ClusterId);
            return null;
        }

        // Read cluster OpenAPI config (or use defaults when auto-discovery is enabled)
        var clusterConfig = _configReader.GetClusterOpenApiConfig(route.ClusterId);
        if (clusterConfig == null)
        {
            if (!options.EnableAutoDiscovery)
            {
                LogRouteClusterNotDiscoverable(route.RouteId, route.ClusterId);
                return null;
            }

            clusterConfig = new AdaOpenApiClusterConfig();
        }

        return new RouteClusterMapping
        {
            Route = route,
            Cluster = cluster,
            RouteOpenApiConfig = routeConfig,
            ClusterOpenApiConfig = clusterConfig
        };
    }
}
