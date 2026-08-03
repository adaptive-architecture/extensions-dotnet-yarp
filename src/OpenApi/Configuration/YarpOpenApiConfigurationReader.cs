using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AdaptArch.Extensions.Yarp.OpenApi.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.Configuration;
/// <summary>
/// Service for reading OpenAPI configuration from YARP cluster and route metadata.
/// </summary>
public interface IYarpOpenApiConfigurationReader
{
    /// <summary>
    /// Gets the OpenAPI configuration for a specific cluster from Ada.OpenApi metadata.
    /// </summary>
    /// <param name="clusterId">The cluster identifier.</param>
    /// <returns>The cluster OpenAPI configuration, or null if not found or invalid.</returns>
    AdaOpenApiClusterConfig? GetClusterOpenApiConfig(string clusterId);

    /// <summary>
    /// Gets the OpenAPI configuration for a specific route from Ada.OpenApi metadata.
    /// </summary>
    /// <param name="routeId">The route identifier.</param>
    /// <returns>The route OpenAPI configuration, or null if not found or invalid.</returns>
    AdaOpenApiRouteConfig? GetRouteOpenApiConfig(string routeId);

    /// <summary>
    /// Gets all clusters from the YARP configuration.
    /// </summary>
    /// <returns>Collection of all configured clusters.</returns>
    IEnumerable<ClusterConfig> GetAllClusters();

    /// <summary>
    /// Gets all routes from the YARP configuration.
    /// </summary>
    /// <returns>Collection of all configured routes.</returns>
    IEnumerable<RouteConfig> GetAllRoutes();

    /// <summary>
    /// Gets all route configurations that have Ada.OpenApi metadata.
    /// </summary>
    /// <returns>Collection of tuples containing route config and parsed Ada.OpenApi metadata.</returns>
    IEnumerable<(RouteConfig Route, AdaOpenApiRouteConfig AdaConfig)> GetAllRouteOpenApiConfigs();

    /// <summary>
    /// Gets all cluster configurations that have Ada.OpenApi metadata.
    /// </summary>
    /// <returns>Collection of tuples containing cluster config and parsed Ada.OpenApi metadata.</returns>
    IEnumerable<(ClusterConfig Cluster, AdaOpenApiClusterConfig AdaConfig)> GetAllClusterOpenApiConfigs();
}

/// <summary>
/// Implementation of YARP OpenAPI configuration reader.
/// </summary>
public sealed partial class YarpOpenApiConfigurationReader : IYarpOpenApiConfigurationReader
{
    private const string AdaOpenApiMetadataKey = "Ada.OpenApi";

    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly string _sectionName;
    private ConfigSnapshot? _snapshot;

    /// <summary>
    /// Parsed Ada.OpenApi metadata for a single YARP configuration snapshot.
    /// Rebuilt whenever YARP publishes a new <see cref="IProxyConfig"/> instance, so the
    /// per-lookup enumeration and JSON deserialization happen once per snapshot instead of
    /// on every request.
    /// </summary>
    private sealed record ConfigSnapshot(
        IProxyConfig Source,
        IReadOnlyList<RouteConfig> Routes,
        IReadOnlyList<ClusterConfig> Clusters,
        Dictionary<string, AdaOpenApiRouteConfig?> RouteConfigs,
        Dictionary<string, AdaOpenApiClusterConfig?> ClusterConfigs);

    /// <summary>
    /// Initializes a new instance of the <see cref="YarpOpenApiConfigurationReader"/> class.
    /// </summary>
    public YarpOpenApiConfigurationReader(
        IProxyConfigProvider proxyConfigProvider,
        ILogger<YarpOpenApiConfigurationReader> logger,
        IConfiguration configuration,
        IOptions<OpenApiAggregationOptions> options)
    {
        _proxyConfigProvider = proxyConfigProvider;
        _logger = logger;
        _configuration = configuration;
        _sectionName = options.Value.ReverseProxyConfigSectionName;
    }

    /// <inheritdoc/>
    public AdaOpenApiClusterConfig? GetClusterOpenApiConfig(string clusterId)
    {
        return GetSnapshot().ClusterConfigs.GetValueOrDefault(clusterId);
    }

    /// <inheritdoc/>
    public AdaOpenApiRouteConfig? GetRouteOpenApiConfig(string routeId)
    {
        return GetSnapshot().RouteConfigs.GetValueOrDefault(routeId);
    }

    /// <inheritdoc/>
    public IEnumerable<ClusterConfig> GetAllClusters()
    {
        return GetSnapshot().Clusters;
    }

    /// <inheritdoc/>
    public IEnumerable<RouteConfig> GetAllRoutes()
    {
        return GetSnapshot().Routes;
    }

    /// <inheritdoc/>
    public IEnumerable<(RouteConfig Route, AdaOpenApiRouteConfig AdaConfig)> GetAllRouteOpenApiConfigs()
    {
        var snapshot = GetSnapshot();
        foreach (var route in snapshot.Routes)
        {
            if (snapshot.RouteConfigs.GetValueOrDefault(route.RouteId) is { } adaConfig)
            {
                yield return (route, adaConfig);
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<(ClusterConfig Cluster, AdaOpenApiClusterConfig AdaConfig)> GetAllClusterOpenApiConfigs()
    {
        var snapshot = GetSnapshot();
        foreach (var cluster in snapshot.Clusters)
        {
            if (snapshot.ClusterConfigs.GetValueOrDefault(cluster.ClusterId) is { } adaConfig)
            {
                yield return (cluster, adaConfig);
            }
        }
    }

    private ConfigSnapshot GetSnapshot()
    {
        var config = _proxyConfigProvider.GetConfig();
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot != null && ReferenceEquals(snapshot.Source, config))
        {
            return snapshot;
        }

        // Racing rebuilds are harmless: both produce equivalent snapshots for the same config.
        snapshot = BuildSnapshot(config);
        Volatile.Write(ref _snapshot, snapshot);
        return snapshot;
    }

    private ConfigSnapshot BuildSnapshot(IProxyConfig config)
    {
        var routes = config.Routes ?? [];
        var clusters = config.Clusters ?? [];

        var routeConfigs = new Dictionary<string, AdaOpenApiRouteConfig?>(routes.Count, StringComparer.Ordinal);
        foreach (var route in routes)
        {
            _ = routeConfigs.TryAdd(route.RouteId, GetAdaConfig(
                route.Metadata,
                $"{_sectionName}:Routes:{route.RouteId}:Metadata:{AdaOpenApiMetadataKey}",
                $"route '{route.RouteId}'",
                OpenApiJsonContext.Default.AdaOpenApiRouteConfig,
                s => s.Get<AdaOpenApiRouteConfig>()));
        }

        var clusterConfigs = new Dictionary<string, AdaOpenApiClusterConfig?>(clusters.Count, StringComparer.Ordinal);
        foreach (var cluster in clusters)
        {
            _ = clusterConfigs.TryAdd(cluster.ClusterId, GetAdaConfig(
                cluster.Metadata,
                $"{_sectionName}:Clusters:{cluster.ClusterId}:Metadata:{AdaOpenApiMetadataKey}",
                $"cluster '{cluster.ClusterId}'",
                OpenApiJsonContext.Default.AdaOpenApiClusterConfig,
                s => s.Get<AdaOpenApiClusterConfig>()));
        }

        return new ConfigSnapshot(config, routes, clusters, routeConfigs, clusterConfigs);
    }

    private T? GetAdaConfig<T>(
        IReadOnlyDictionary<string, string>? metadata,
        string configSectionPath,
        string contextDescription,
        JsonTypeInfo<T> jsonTypeInfo,
        Func<IConfigurationSection, T?> fromSection) where T : class
    {
        // Old format: string value already in the metadata dict
        if (metadata?.TryGetValue(AdaOpenApiMetadataKey, out var metadataJson) == true && metadataJson is not null)
        {
            return ParseMetadataFromJson(metadataJson, contextDescription, jsonTypeInfo);
        }

        // New format: native JSON object — bind directly from IConfiguration section
        return fromSection(_configuration.GetSection(configSectionPath));
    }

    private T? ParseMetadataFromJson<T>(
        string metadataJson,
        string contextDescription,
        JsonTypeInfo<T> jsonTypeInfo) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(metadataJson, jsonTypeInfo);
        }
        catch (JsonException ex)
        {
            LogMetadataDeserializationFailed(AdaOpenApiMetadataKey, contextDescription, metadataJson, ex);
            return null;
        }
    }

    // Source-generated logging methods
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize {MetadataKey} metadata for {Context}. JSON: {Json}")]
    private partial void LogMetadataDeserializationFailed(string metadataKey, string context, string json, Exception ex);
}
