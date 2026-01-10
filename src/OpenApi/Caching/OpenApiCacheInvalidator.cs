namespace AdaptArch.Extensions.Yarp.OpenApi.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of <see cref="IOpenApiCacheInvalidator"/>.
/// Provides tag-based cache invalidation for OpenAPI documents and aggregated specifications.
/// </summary>
internal sealed partial class OpenApiCacheInvalidator : IOpenApiCacheInvalidator
{
    private readonly HybridCache _cache;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiCacheInvalidator"/> class.
    /// </summary>
    /// <param name="cache">The hybrid cache instance.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenApiCacheInvalidator(HybridCache cache, ILogger<OpenApiCacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvalidateServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        LogInvalidatingService(serviceName);
        await _cache.RemoveByTagAsync($"service:{serviceName}", cancellationToken);
        LogServiceInvalidated(serviceName);
    }

    /// <inheritdoc/>
    public async Task InvalidateClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);

        LogInvalidatingCluster(clusterId);
        await _cache.RemoveByTagAsync($"cluster:{clusterId}", cancellationToken);
        LogClusterInvalidated(clusterId);
    }

    /// <inheritdoc/>
    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        LogInvalidatingAll();
        await _cache.RemoveByTagAsync("*", cancellationToken);
        LogAllInvalidated();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Invalidating OpenAPI cache for service: {ServiceName}")]
    private partial void LogInvalidatingService(string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully invalidated cache for service: {ServiceName}")]
    private partial void LogServiceInvalidated(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invalidating OpenAPI cache for cluster: {ClusterId}")]
    private partial void LogInvalidatingCluster(string clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully invalidated cache for cluster: {ClusterId}")]
    private partial void LogClusterInvalidated(string clusterId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalidating ALL OpenAPI cache entries")]
    private partial void LogInvalidatingAll();

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully invalidated all OpenAPI cache entries")]
    private partial void LogAllInvalidated();
}
