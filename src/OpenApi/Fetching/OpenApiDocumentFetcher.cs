using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Fetching;

/// <summary>
/// Service for fetching OpenAPI documents from downstream services.
/// </summary>
public interface IOpenApiDocumentFetcher
{
    /// <summary>
    /// Fetches an OpenAPI document from the specified base URL and path.
    /// Results are cached according to the configured cache duration.
    /// </summary>
    /// <param name="baseUrl">The base URL of the service (e.g., "http://localhost:8080").</param>
    /// <param name="openApiPath">The path to the OpenAPI document (e.g., "/swagger/v1/swagger.json").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OpenAPI document if successful, null if fetch failed.</returns>
    Task<OpenApiDocument?> FetchDocumentAsync(string baseUrl, string openApiPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IOpenApiDocumentFetcher"/>.
/// Fetches OpenAPI documents from HTTP endpoints with memory caching and fallback path support.
/// </summary>
public sealed partial class OpenApiDocumentFetcher : IOpenApiDocumentFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<OpenApiAggregationOptions> _optionsMonitor;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiDocumentFetcher"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">The memory cache for storing fetched documents.</param>
    /// <param name="optionsMonitor">The options monitor for configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenApiDocumentFetcher(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsMonitor<OpenApiAggregationOptions> optionsMonitor,
        ILogger<OpenApiDocumentFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        var options = _optionsMonitor.CurrentValue;
        _concurrencySemaphore = new SemaphoreSlim(options.MaxConcurrentFetches, options.MaxConcurrentFetches);
    }

    /// <inheritdoc/>
    public async Task<OpenApiDocument?> FetchDocumentAsync(string baseUrl, string openApiPath, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL cannot be null or whitespace.", nameof(baseUrl));
        }

        if (String.IsNullOrWhiteSpace(openApiPath))
        {
            throw new ArgumentException("OpenAPI path cannot be null or whitespace.", nameof(openApiPath));
        }

        var options = _optionsMonitor.CurrentValue;
        var cacheKey = $"openapi:{baseUrl}:{openApiPath}";

        // Check cache first
        if (_cache.TryGetValue<OpenApiDocument>(cacheKey, out var cachedDocument))
        {
            LogCacheHit(baseUrl, openApiPath);
            return cachedDocument;
        }

        // Limit concurrent fetches
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring semaphore
            if (_cache.TryGetValue(cacheKey, out cachedDocument))
            {
                LogCacheHitAfterSemaphore(baseUrl, openApiPath);
                return cachedDocument;
            }

            // Try primary path first
            var document = await TryFetchFromPathAsync(baseUrl, openApiPath, cancellationToken);

            // If primary path failed, try fallback paths
            if (document == null && options.FallbackPaths.Length > 0)
            {
                LogTryingFallbackPaths(openApiPath, baseUrl);
                foreach (var fallbackPath in options.FallbackPaths)
                {
                    document = await TryFetchFromPathAsync(baseUrl, fallbackPath, cancellationToken);
                    if (document != null)
                    {
                        LogFallbackPathSuccess(fallbackPath, baseUrl);
                        break;
                    }
                }
            }

            // Cache the result (even if null, to avoid repeated failed fetches)
            if (document != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = options.CacheDuration,
                    Size = 1 // Simple size calculation for memory management
                };
                _cache.Set(cacheKey, document, cacheEntryOptions);
                LogDocumentCached(baseUrl, openApiPath, options.CacheDuration);
            }
            else
            {
                // Cache failures for a shorter duration to avoid hammering failing services
                var failureCacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                    Size = 1
                };
                _cache.Set(cacheKey, (OpenApiDocument?)null, failureCacheOptions);
                LogFetchFailedAllPaths(baseUrl, openApiPath);
            }

            return document;
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private async Task<OpenApiDocument?> TryFetchFromPathAsync(string baseUrl, string path, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var fullUrl = CombineUrl(baseUrl, path);

        try
        {
            LogFetchingDocument(fullUrl);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(options.DefaultFetchTimeoutMs);

            using var response = await httpClient.GetAsync(fullUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                LogHttpError(fullUrl, (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var (document, diagnostic) = await OpenApiDocument.LoadAsync(stream, cancellationToken: cancellationToken);
            if (diagnostic?.Errors?.Count > 0)
            {
                LogParsingErrors(fullUrl, String.Join(", ", diagnostic.Errors.Select(e => e.ToString())));
            }
            if (document == null)
            {
                LogParsingFailed(fullUrl);
                return null;
            }

            LogFetchSuccess(fullUrl);
            return document;
        }
        catch (TaskCanceledException ex)
        {
            LogTimeout(fullUrl, ex);
            return null;
        }
        catch (HttpRequestException ex)
        {
            LogHttpRequestError(fullUrl, ex);
            return null;
        }
        catch (Exception ex)
        {
            LogUnexpectedError(fullUrl, ex);
            return null;
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        // Ensure base URL doesn't end with slash and path starts with slash
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.StartsWith('/') ? path : $"/{path}";
        return trimmedBase + trimmedPath;
    }

    // Source-generated logging methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "OpenAPI document cache hit for {BaseUrl}{Path}")]
    private partial void LogCacheHit(string baseUrl, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OpenAPI document cache hit (after semaphore) for {BaseUrl}{Path}")]
    private partial void LogCacheHitAfterSemaphore(string baseUrl, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Primary OpenAPI path {Path} failed for {BaseUrl}, trying fallback paths")]
    private partial void LogTryingFallbackPaths(string path, string baseUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully fetched OpenAPI document from fallback path {Path} for {BaseUrl}")]
    private partial void LogFallbackPathSuccess(string path, string baseUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenAPI document cached for {BaseUrl}{Path} (expires in {Duration})")]
    private partial void LogDocumentCached(string baseUrl, string path, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenAPI document from {BaseUrl}{Path} (all paths attempted)")]
    private partial void LogFetchFailedAllPaths(string baseUrl, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching OpenAPI document from {Url}")]
    private partial void LogFetchingDocument(string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenAPI document from {Url}: HTTP {StatusCode}")]
    private partial void LogHttpError(string url, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAPI document from {Url} has parsing errors: {Errors}")]
    private partial void LogParsingErrors(string url, string errors);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse OpenAPI document from {Url}")]
    private partial void LogParsingFailed(string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully fetched and parsed OpenAPI document from {Url}")]
    private partial void LogFetchSuccess(string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeout fetching OpenAPI document from {Url}")]
    private partial void LogTimeout(string url, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP error fetching OpenAPI document from {Url}")]
    private partial void LogHttpRequestError(string url, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error fetching OpenAPI document from {Url}")]
    private partial void LogUnexpectedError(string url, Exception ex);
}
