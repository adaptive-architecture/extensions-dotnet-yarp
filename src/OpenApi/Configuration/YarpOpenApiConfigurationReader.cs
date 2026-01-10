
using System.Text.Json;
using Microsoft.Extensions.Logging;
using global::Yarp.ReverseProxy.Configuration;
using System.Text.Json.Serialization.Metadata;
using AdaptArch.Extensions.Yarp.OpenApi.Json;

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="YarpOpenApiConfigurationReader"/> class.
    /// </summary>
    public YarpOpenApiConfigurationReader(
        IProxyConfigProvider proxyConfigProvider,
        ILogger<YarpOpenApiConfigurationReader> logger)
    {
        _proxyConfigProvider = proxyConfigProvider;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YarpOpenApiConfigurationReader"/> class without logging.
    /// </summary>
    /// <remarks>Used for testing.</remarks>
    public YarpOpenApiConfigurationReader(
        IProxyConfigProvider proxyConfigProvider)
        : this(proxyConfigProvider, NullLogger<YarpOpenApiConfigurationReader>.Instance)
    {
    }

    /// <inheritdoc/>
    public AdaOpenApiClusterConfig? GetClusterOpenApiConfig(string clusterId)
    {
        var cluster = GetAllClusters().FirstOrDefault(c => c.ClusterId == clusterId);
        if (cluster?.Metadata == null)
        {
            return null;
        }

        return ParseMetadata(
            cluster.Metadata,
            AdaOpenApiMetadataKey,
            $"cluster '{clusterId}'",
            OpenApiJsonContext.Default.AdaOpenApiClusterConfig);
    }

    /// <inheritdoc/>
    public AdaOpenApiRouteConfig? GetRouteOpenApiConfig(string routeId)
    {
        var route = GetAllRoutes().FirstOrDefault(r => r.RouteId == routeId);
        if (route?.Metadata == null)
        {
            return null;
        }

        return ParseMetadata(
            route.Metadata,
            AdaOpenApiMetadataKey,
            $"route '{routeId}'",
            OpenApiJsonContext.Default.AdaOpenApiRouteConfig);
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
            if (route.Metadata == null)
            {
                continue;
            }

            var adaConfig = ParseMetadata(
                route.Metadata,
                AdaOpenApiMetadataKey,
                $"route '{route.RouteId}'",
                OpenApiJsonContext.Default.AdaOpenApiRouteConfig);

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
            if (cluster.Metadata == null)
            {
                continue;
            }

            var adaConfig = ParseMetadata(
                cluster.Metadata,
                AdaOpenApiMetadataKey,
                $"cluster '{cluster.ClusterId}'",
                OpenApiJsonContext.Default.AdaOpenApiClusterConfig);

            if (adaConfig != null)
            {
                yield return (cluster, adaConfig);
            }
        }
    }

    private T? ParseMetadata<T>(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string contextDescription,
        JsonTypeInfo<T> jsonTypeInfo) where T : class
    {
        if (!metadata.TryGetValue(key, out var metadataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(metadataJson, jsonTypeInfo);
        }
        catch (JsonException ex)
        {
            LogMetadataDeserializationFailed(key, contextDescription, metadataJson, ex);
            return null;
        }
    }

    // Source-generated logging methods
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize {MetadataKey} metadata for {Context}. JSON: {Json}")]
    private partial void LogMetadataDeserializationFailed(string metadataKey, string context, string json, Exception ex);
}

/// <summary>
/// Null logger implementation for testing.
/// </summary>
file class NullLogger<T> : ILogger<T>
{
    public static readonly ILogger<T> Instance = new NullLogger<T>();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
