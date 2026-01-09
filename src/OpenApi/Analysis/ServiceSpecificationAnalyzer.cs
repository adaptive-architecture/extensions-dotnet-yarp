using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceSpecificationAnalyzer"/> class.
    /// </summary>
    /// <param name="configReader">The YARP OpenAPI configuration reader.</param>
    /// <param name="logger">The logger instance.</param>
    public ServiceSpecificationAnalyzer(
        IYarpOpenApiConfigurationReader configReader,
        ILogger<ServiceSpecificationAnalyzer> logger)
    {
        _configReader = configReader;
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

    /// <inheritdoc/>
    public IReadOnlyList<ServiceSpecification> AnalyzeServices()
    {
        var routes = _configReader.GetAllRoutes().ToList();
        var clusters = _configReader.GetAllClusters().ToList();

        // Build cluster lookup dictionary
        var clusterLookup = clusters.ToDictionary(c => c.ClusterId, StringComparer.OrdinalIgnoreCase);

        // Group routes by service name
        var serviceGroups = new Dictionary<string, List<RouteClusterMapping>>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in routes)
        {
            // Read route OpenAPI config
            var routeConfig = _configReader.GetRouteOpenApiConfig(route.RouteId);
            if (routeConfig == null)
            {
                LogRouteNoMetadata(route.RouteId);
                continue;
            }

            // Skip disabled routes
            if (!routeConfig.Enabled)
            {
                LogRouteDisabled(route.RouteId);
                continue;
            }

            // Validate service name
            if (String.IsNullOrWhiteSpace(routeConfig.ServiceName))
            {
                LogRouteEmptyServiceName(route.RouteId);
                continue;
            }

            // Lookup cluster
            if (String.IsNullOrWhiteSpace(route.ClusterId))
            {
                LogRouteNoClusterId(route.RouteId);
                continue;
            }

            if (!clusterLookup.TryGetValue(route.ClusterId, out var cluster))
            {
                LogRouteInvalidCluster(route.RouteId, route.ClusterId);
                continue;
            }

            // Read cluster OpenAPI config (or use defaults)
            var clusterConfig = _configReader.GetClusterOpenApiConfig(route.ClusterId) ?? new AdaOpenApiClusterConfig();

            // Create mapping
            var mapping = new RouteClusterMapping
            {
                Route = route,
                Cluster = cluster,
                RouteOpenApiConfig = routeConfig,
                ClusterOpenApiConfig = clusterConfig
            };

            // Add to service group
            var serviceName = routeConfig.ServiceName;
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
}
