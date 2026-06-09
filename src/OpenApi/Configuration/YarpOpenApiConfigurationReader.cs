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
        var cluster = GetAllClusters().FirstOrDefault(c => c.ClusterId == clusterId);
        if (cluster == null)
        {
            return null;
        }

        return GetAdaConfig(cluster.Metadata,
            $"{_sectionName}:Clusters:{clusterId}:Metadata:{AdaOpenApiMetadataKey}",
            $"cluster '{clusterId}'", OpenApiJsonContext.Default.AdaOpenApiClusterConfig,
            s => s.Get<AdaOpenApiClusterConfig>());
    }

    /// <inheritdoc/>
    public AdaOpenApiRouteConfig? GetRouteOpenApiConfig(string routeId)
    {
        var route = GetAllRoutes().FirstOrDefault(r => r.RouteId == routeId);
        if (route == null)
        {
            return null;
        }

        return GetAdaConfig(route.Metadata,
            $"{_sectionName}:Routes:{routeId}:Metadata:{AdaOpenApiMetadataKey}",
            $"route '{routeId}'", OpenApiJsonContext.Default.AdaOpenApiRouteConfig,
            s => s.Get<AdaOpenApiRouteConfig>());
    }

    /// <inheritdoc/>
    public IEnumerable<ClusterConfig> GetAllClusters()
    {
        var config = _proxyConfigProvider.GetConfig();
        return config.Clusters ?? Enumerable.Empty<ClusterConfig>();
    }

    /// <inheritdoc/>
    public IEnumerable<RouteConfig> GetAllRoutes()
    {
        var config = _proxyConfigProvider.GetConfig();
        return config.Routes ?? Enumerable.Empty<RouteConfig>();
    }

    /// <inheritdoc/>
    public IEnumerable<(RouteConfig Route, AdaOpenApiRouteConfig AdaConfig)> GetAllRouteOpenApiConfigs()
    {
        foreach (var route in GetAllRoutes())
        {
            var adaConfig = GetAdaConfig(
                route.Metadata,
                $"{_sectionName}:Routes:{route.RouteId}:Metadata:{AdaOpenApiMetadataKey}",
                $"route '{route.RouteId}'",
                OpenApiJsonContext.Default.AdaOpenApiRouteConfig,
                s => s.Get<AdaOpenApiRouteConfig>());

            if (adaConfig != null)
            {
                yield return (route, adaConfig);
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<(ClusterConfig Cluster, AdaOpenApiClusterConfig AdaConfig)> GetAllClusterOpenApiConfigs()
    {
        foreach (var cluster in GetAllClusters())
        {
            var adaConfig = GetAdaConfig(
                cluster.Metadata,
                $"{_sectionName}:Clusters:{cluster.ClusterId}:Metadata:{AdaOpenApiMetadataKey}",
                $"cluster '{cluster.ClusterId}'",
                OpenApiJsonContext.Default.AdaOpenApiClusterConfig,
                s => s.Get<AdaOpenApiClusterConfig>());

            if (adaConfig != null)
            {
                yield return (cluster, adaConfig);
            }
        }
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
