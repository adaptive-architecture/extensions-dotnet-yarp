namespace AdaptArch.Extensions.Yarp.OpenApi.Caching;

/// <summary>
/// Service for invalidating OpenAPI cache entries by service name, cluster, or globally.
/// </summary>
public interface IOpenApiCacheInvalidator
{
    /// <summary>
    /// Invalidates all cached OpenAPI documents for a specific service.
    /// </summary>
    /// <param name="serviceName">The service name to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached OpenAPI documents for a specific cluster.
    /// </summary>
    /// <param name="clusterId">The cluster ID to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateClusterAsync(string clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all OpenAPI cache entries (documents and aggregated specs).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
