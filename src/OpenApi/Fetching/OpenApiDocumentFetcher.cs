using AdaptArch.Extensions.Yarp.OpenApi.Caching;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
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
    private readonly HybridCache _cache;
    private readonly IOptionsMonitor<OpenApiAggregationOptions> _optionsMonitor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiDocumentFetcher"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">The hybrid cache for storing fetched documents.</param>
    /// <param name="optionsMonitor">The options monitor for configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenApiDocumentFetcher(
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        IOptionsMonitor<OpenApiAggregationOptions> optionsMonitor,
        ILogger<OpenApiDocumentFetcher> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
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
        var cacheKey = $"{baseUrl}:{openApiPath}";
        var tags = new[] { "openapi", $"baseUrl:{baseUrl}" };

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = options.CacheDuration,
            LocalCacheExpiration = options.CacheDuration
        };

        // HybridCache provides automatic stampede protection
        // Use wrapper to serialize OpenApiDocument as JSON string
        var wrapper = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var doc = await FetchDocumentFromSourceAsync(baseUrl, openApiPath, cancel);
                return doc == null ? null : await OpenApiDocumentCacheWrapper.FromDocumentAsync(doc, cancel);
            },
            entryOptions,
            tags,
            cancellationToken
        );

        return wrapper == null ? null : await wrapper.ToDocumentAsync(cancellationToken);
    }

    private async Task<OpenApiDocument?> FetchDocumentFromSourceAsync(string baseUrl, string openApiPath, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

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

        if (document != null)
        {
            LogDocumentFetched(baseUrl, openApiPath);
        }
        else
        {
            LogFetchFailedAllPaths(baseUrl, openApiPath);
        }

        return document;
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
                LogParsingErrors(fullUrl, diagnostic.Errors);
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
    [LoggerMessage(Level = LogLevel.Information, Message = "Primary OpenAPI path {Path} failed for {BaseUrl}, trying fallback paths")]
    private partial void LogTryingFallbackPaths(string path, string baseUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully fetched OpenAPI document from fallback path {Path} for {BaseUrl}")]
    private partial void LogFallbackPathSuccess(string path, string baseUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully fetched OpenAPI document from {BaseUrl}{Path}")]
    private partial void LogDocumentFetched(string baseUrl, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenAPI document from {BaseUrl}{Path} (all paths attempted)")]
    private partial void LogFetchFailedAllPaths(string baseUrl, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching OpenAPI document from {Url}")]
    private partial void LogFetchingDocument(string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenAPI document from {Url}: HTTP {StatusCode}")]
    private partial void LogHttpError(string url, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAPI document from {Url} has {Errors} parsing errors")]
    private partial void LogParsingErrors(string url, IList<OpenApiError> errors);

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
