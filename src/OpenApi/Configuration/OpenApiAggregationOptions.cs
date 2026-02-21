using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Configuration;

/// <summary>
/// Global configuration options for YARP OpenAPI aggregation.
/// </summary>
public class OpenApiAggregationOptions
{
    /// <summary>
    /// Gets or sets the default path to fetch OpenAPI documents from downstream services.
    /// </summary>
    /// <remarks>
    /// This value can be overridden per cluster via Ada.OpenApi metadata.
    /// Defaults to "/swagger/v1/swagger.json" (ASP.NET Core with Swashbuckle standard).
    /// </remarks>
    public string DefaultOpenApiPath { get; set; } = "/swagger/v1/swagger.json";

    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic discovery of OpenAPI documents from all clusters.
    /// </summary>
    /// <remarks>
    /// If false, only clusters with explicit Ada.OpenApi metadata will be included in aggregation.
    /// Defaults to true for easier setup.
    /// </remarks>
    public bool EnableAutoDiscovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the caching duration for fetched OpenAPI documents.
    /// </summary>
    /// <remarks>
    /// Cached documents are automatically refreshed after this duration expires.
    /// Defaults to 5 minutes.
    /// </remarks>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the cache duration for aggregated service specifications.
    /// </summary>
    /// <remarks>
    /// Controls how long aggregated OpenAPI specs are cached in the middleware.
    /// Defaults to 5 minutes.
    /// </remarks>
    public TimeSpan AggregatedSpecCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the cache duration for failed fetch attempts.
    /// </summary>
    /// <remarks>
    /// When fetching an OpenAPI document fails, cache the failure for this duration
    /// to avoid repeatedly hammering failing services.
    /// Defaults to 1 minute.
    /// </remarks>
    public TimeSpan FailureCacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the maximum payload size in bytes for cached entries.
    /// </summary>
    /// <remarks>
    /// OpenAPI documents larger than this will not be cached.
    /// Defaults to 1 MB.
    /// </remarks>
    public int MaximumCachePayloadBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of concurrent document fetches.
    /// </summary>
    /// <remarks>
    /// Limits the number of downstream services that can be queried simultaneously.
    /// Defaults to 10 concurrent fetches.
    /// </remarks>
    public int MaxConcurrentFetches { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default timeout for fetching OpenAPI documents (in milliseconds).
    /// </summary>
    /// <remarks>
    /// Can be overridden per cluster via Ada.OpenApi metadata.
    /// Defaults to 10 seconds.
    /// </remarks>
    public int DefaultFetchTimeoutMs { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the fallback paths to try if the configured OpenApiPath fails.
    /// </summary>
    /// <remarks>
    /// These paths are tried in order if the primary OpenApiPath returns a non-success status code.
    /// Useful for supporting services using different conventions.
    /// </remarks>
    public string[] FallbackPaths { get; set; } =
    [
        "/api/v1/openapi.json",
        "/openapi.json",
        "/docs/openapi.json",
        "/swagger/openapi.json"
    ];

    /// <summary>
    /// Gets or sets the strategy for handling non-analyzable YARP transforms.
    /// </summary>
    /// <remarks>
    /// Determines how the system behaves when encountering complex transforms
    /// that cannot be reliably analyzed for path reachability.
    /// Defaults to IncludeWithWarning for safety.
    /// </remarks>
    public NonAnalyzableTransformStrategy NonAnalyzableStrategy { get; set; } =
        NonAnalyzableTransformStrategy.IncludeWithWarning;

    /// <summary>
    /// Gets or sets a value indicating whether to log warnings for complex transform scenarios.
    /// </summary>
    /// <remarks>
    /// When true, warnings are logged when transforms cannot be fully analyzed.
    /// Defaults to true for better diagnostics.
    /// </remarks>
    public bool LogTransformWarnings { get; set; } = true;

    /// <summary>
    /// Gets or sets a delegate that configures the <c>info</c> block of the aggregated OpenAPI document.
    /// </summary>
    /// <remarks>
    /// The delegate receives the merged <see cref="OpenApiInfo"/> (with contact already populated
    /// from downstream services) and the current <see cref="HttpContext"/>, and returns a
    /// (possibly modified) <see cref="OpenApiInfo"/>.
    /// When <c>null</c> (the default), the merged info passes through as-is.
    /// </remarks>
    public Func<OpenApiInfo, HttpContext, OpenApiInfo>? ConfigureInfo { get; set; }

    /// <summary>
    /// Gets or sets a delegate that configures the <c>servers</c> block of the aggregated OpenAPI document.
    /// </summary>
    /// <remarks>
    /// The delegate receives the current <see cref="HttpContext"/> and returns the list of
    /// <see cref="OpenApiServer"/> entries to set on the aggregated document.
    /// By default, injects the gateway's own URL derived from the incoming request.
    /// </remarks>
    public Func<HttpContext, IList<OpenApiServer>> ConfigureServers { get; set; } = context =>
    [
        new OpenApiServer
        {
            Url = $"{context.Request.Scheme}://{context.Request.Host}",
            Description = "API Gateway"
        }
    ];
}

/// <summary>
/// Strategy for handling YARP transforms that cannot be reliably analyzed.
/// </summary>
public enum NonAnalyzableTransformStrategy
{
    /// <summary>
    /// Include all paths from the service with a warning logged.
    /// Conservative approach that ensures no content is accidentally excluded.
    /// </summary>
    IncludeWithWarning,

    /// <summary>
    /// Exclude all paths from the service with a warning logged.
    /// Stricter approach that only includes content with confirmed reachability.
    /// </summary>
    ExcludeWithWarning,

    /// <summary>
    /// Skip the entire service from aggregation.
    /// Most restrictive approach for maximum accuracy.
    /// </summary>
    SkipService
}
