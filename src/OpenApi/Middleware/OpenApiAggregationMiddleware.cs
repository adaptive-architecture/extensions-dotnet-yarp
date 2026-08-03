using System.Text;
using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Caching;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using AdaptArch.Extensions.Yarp.OpenApi.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Merging;
using AdaptArch.Extensions.Yarp.OpenApi.Pruning;
using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Middleware;

/// <summary>
/// Middleware that aggregates OpenAPI specifications from downstream services and exposes them via REST endpoints.
/// </summary>
public sealed partial class OpenApiAggregationMiddleware
{
    private const string OpenApiJsonSuffix = "/openapi.json";
    private const string OpenApiYamlSuffix = "/openapi.yaml";
    private const string OpenApiYmlSuffix = "/openapi.yml";
    private const string InternalServerErrorMessage = "Internal server error";
    private const int MaxServiceNameLength = 256;

    private readonly RequestDelegate _next;
    private readonly string _basePath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiAggregationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="basePath">The base path for OpenAPI aggregation endpoints.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenApiAggregationMiddleware(
        RequestDelegate next,
        string basePath,
        ILogger<OpenApiAggregationMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _basePath = basePath;
        _logger = logger;
    }

    /// <summary>
    /// Processes HTTP requests and handles OpenAPI aggregation endpoints.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? String.Empty;

        // Check if request matches our base path; the character after the base path must be a
        // segment boundary so that e.g. "/api-docsomething" is not treated as "/api-docs".
        if (!path.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase)
            || (path.Length > _basePath.Length && path[_basePath.Length] != '/' && !_basePath.EndsWith('/')))
        {
            await _next(context);
            return;
        }

        // Extract the service name from the path (if present)
        var subPath = path[_basePath.Length..].TrimStart('/');

        if (String.IsNullOrEmpty(subPath))
        {
            // List all available services
            await HandleServiceListRequest(context);
        }
        else
        {
            // Return aggregated OpenAPI spec for specific service
            await HandleServiceSpecRequest(context, subPath);
        }
    }

    /// <summary>
    /// Handles requests for the list of available services.
    /// GET /api-docs
    /// </summary>
    private async Task HandleServiceListRequest(HttpContext context)
    {
        try
        {
            LogHandlingServiceListRequest();

            // Resolve service analyzer from DI
            var serviceAnalyzer = context.RequestServices.GetRequiredService<IServiceSpecificationAnalyzer>();

            // Analyze services from YARP configuration
            var serviceSpecs = serviceAnalyzer.AnalyzeServices();

            // Build service info list with URLs
            var services = serviceSpecs
                .Select(s => s.ServiceName)
                .Distinct()
                .Select(name => new ServiceInfo
                {
                    Name = name,
                    Url = $"{_basePath}/{ToKebabCase(name)}"
                })
                .ToList();

            LogFoundServices(services.Count, services);

            // Return JSON response
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;

            var response = new ServiceListResponse
            {
                Services = services,
                Count = services.Count
            };

            var json = JsonSerializer.Serialize(response, OpenApiJsonContext.Default.ServiceListResponse);

            await context.Response.WriteAsync(json, context.RequestAborted);
        }
        catch (Exception ex)
        {
            LogServiceListRequestError(ex);
            context.Response.StatusCode = 500;
            // Reporting the failure must not itself be cancelled: the token may already be
            // the cause of the exception we are reporting. The body stays generic so no
            // exception details leak to the client; details are in the log.
            await context.Response.WriteAsync(InternalServerErrorMessage, CancellationToken.None);
        }
    }

    /// <summary>
    /// Handles requests for a specific service's aggregated OpenAPI specification.
    /// Supports multiple URL patterns:
    /// - GET /api-docs/{serviceName}
    /// - GET /api-docs/{serviceName}/openapi.json
    /// - GET /api-docs/{serviceName}/openapi.yaml
    /// - GET /api-docs/{serviceName}/openapi.yml
    /// </summary>
    private async Task HandleServiceSpecRequest(HttpContext context, string subPath)
    {
        // Parse the subPath to extract service name and format
        var (serviceName, explicitFormat) = ParseServiceSpecPath(subPath);

        try
        {
            LogHandlingSpecRequest(serviceName);

            // Normalize service name (URL decode and normalize case)
            serviceName = Uri.UnescapeDataString(serviceName);

            // Validate service name to prevent path traversal and abusive input
            if (!IsValidServiceName(serviceName))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid service name", context.RequestAborted);
                return;
            }

            // Resolve the service against the configured YARP services before touching the
            // cache so that arbitrary request paths cannot create cache entries.
            var serviceAnalyzer = context.RequestServices.GetRequiredService<IServiceSpecificationAnalyzer>();
            var serviceSpec = FindServiceSpecification(serviceAnalyzer, serviceName);
            if (serviceSpec == null)
            {
                LogServiceNotFound(serviceName);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Service not found", context.RequestAborted);
                return;
            }

            // Resolve services from DI
            var cache = context.RequestServices.GetRequiredService<HybridCache>();
            var optionsMonitor = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenApiAggregationOptions>>();

            // Key the cache by the canonical (configured) service name so request-path
            // variants ("TestService", "testservice", …) share a single entry.
            var canonicalName = serviceSpec.ServiceName;
            var cacheKey = $"openapi_spec_{canonicalName}";
            var tags = new[] { "openapi_spec", $"service:{canonicalName}" };

            var options = optionsMonitor.CurrentValue;
            var entryOptions = new HybridCacheEntryOptions
            {
                Expiration = options.AggregatedSpecCacheDuration,
                LocalCacheExpiration = options.AggregatedSpecCacheDuration
            };

            // Use wrapper to serialize OpenApiDocument as JSON string for caching.
            // A wrapper without Json marks a failed aggregation (negative cache entry).
            var aggregationRan = false;
            var wrapper = await cache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    aggregationRan = true;
                    var doc = await AggregateServiceSpecificationAsync(context.RequestServices, serviceSpec, cancel);
                    return doc == null ? new OpenApiDocumentCacheWrapper() : await OpenApiDocumentCacheWrapper.FromDocumentAsync(doc, cancel);
                },
                entryOptions,
                tags,
                context.RequestAborted
            );

            var aggregatedDoc = wrapper == null ? null : await wrapper.ToDocumentAsync(context.RequestAborted);

            if (aggregatedDoc == null)
            {
                if (aggregationRan)
                {
                    // Replace the freshly cached failure with a short-lived entry so a broken
                    // downstream is retried after FailureCacheDuration instead of the full
                    // aggregated-spec duration. Not cancelled by the client: the entry must be
                    // written even if the request is aborted.
                    var failureEntryOptions = new HybridCacheEntryOptions
                    {
                        Expiration = options.FailureCacheDuration,
                        LocalCacheExpiration = options.FailureCacheDuration
                    };
                    await cache.SetAsync(cacheKey, new OpenApiDocumentCacheWrapper(), failureEntryOptions, tags, CancellationToken.None);
                }

                LogServiceNotFound(canonicalName);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Service not found or failed to aggregate", context.RequestAborted);
                return;
            }

            aggregatedDoc.Servers = [.. options.ConfigureServers(context)];

            if (options.ConfigureInfo != null)
            {
                aggregatedDoc.Info = options.ConfigureInfo(aggregatedDoc.Info, context);
            }

            LogAggregationSuccess(serviceName);
            await WriteOpenApiResponse(context, aggregatedDoc, explicitFormat);
        }
        catch (Exception ex)
        {
            LogSpecRequestError(serviceName, ex);
            context.Response.StatusCode = 500;
            // Reporting the failure must not itself be cancelled: the token may already be
            // the cause of the exception we are reporting. The body stays generic so no
            // exception details leak to the client; details are in the log.
            await context.Response.WriteAsync(InternalServerErrorMessage, CancellationToken.None);
        }
    }

    /// <summary>
    /// Aggregates the OpenAPI specification for a specific service.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <param name="serviceSpec">The already-resolved service specification to aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<OpenApiDocument?> AggregateServiceSpecificationAsync(
        IServiceProvider serviceProvider,
        ServiceSpecification serviceSpec,
        CancellationToken cancellationToken)
    {
        var serviceName = serviceSpec.ServiceName;
        LogStartingAggregation(serviceName);

        var documentMerger = serviceProvider.GetRequiredService<IOpenApiMerger>();

        LogFoundRoutes(serviceSpec.Routes.Count, serviceName);

        var processedDocuments = await ProcessServiceRoutesAsync(serviceProvider, serviceSpec, cancellationToken);

        if (processedDocuments.Count == 0)
        {
            LogNoDocumentsProcessed(serviceName);
            return null;
        }

        LogDocumentsProcessed(processedDocuments.Count, serviceName);

        var mergedDocument = documentMerger.MergeDocuments(processedDocuments, serviceName);

        if (mergedDocument == null)
        {
            LogMergeFailed(serviceName);
            return null;
        }

        LogMergeSuccess(processedDocuments.Count, serviceName);
        return mergedDocument;
    }

    /// <summary>
    /// Finds the service specification matching the given service name.
    /// </summary>
    private static ServiceSpecification? FindServiceSpecification(IServiceSpecificationAnalyzer serviceAnalyzer, string serviceName)
    {
        var serviceSpecs = serviceAnalyzer.AnalyzeServices();
        return serviceSpecs.FirstOrDefault(s =>
            String.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase) ||
            String.Equals(ToKebabCase(s.ServiceName), serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates a request-supplied service name: rejects path traversal sequences,
    /// separators, control characters, and abusive lengths.
    /// </summary>
    private static bool IsValidServiceName(string serviceName)
    {
        return !String.IsNullOrWhiteSpace(serviceName)
            && serviceName.Length <= MaxServiceNameLength
            && !serviceName.Contains("..", StringComparison.Ordinal)
            && !serviceName.Contains('/')
            && !serviceName.Contains('\\')
            && !serviceName.Any(Char.IsControl);
    }

    /// <summary>
    /// Services and options resolved once per aggregation and shared across cluster processing.
    /// </summary>
    private sealed record AggregationContext(
        IOpenApiDocumentFetcher DocumentFetcher,
        IPathReachabilityAnalyzer ReachabilityAnalyzer,
        IOpenApiDocumentPruner DocumentPruner,
        ISchemaRenamer SchemaRenamer,
        OpenApiAggregationOptions Options);

    /// <summary>
    /// Processes all routes for a service specification, grouped by cluster.
    /// Routes sharing the same cluster fetch the OpenAPI document once and analyze reachability across all routes.
    /// </summary>
    private async Task<List<OpenApiDocument>> ProcessServiceRoutesAsync(
        IServiceProvider serviceProvider,
        ServiceSpecification serviceSpec,
        CancellationToken cancellationToken)
    {
        // Resolve services once outside the loop for better performance
        var aggregation = new AggregationContext(
            serviceProvider.GetRequiredService<IOpenApiDocumentFetcher>(),
            serviceProvider.GetRequiredService<IPathReachabilityAnalyzer>(),
            serviceProvider.GetRequiredService<IOpenApiDocumentPruner>(),
            serviceProvider.GetRequiredService<ISchemaRenamer>(),
            serviceProvider.GetRequiredService<IOptionsMonitor<OpenApiAggregationOptions>>().CurrentValue);

        // Group routes by cluster to avoid fetching the same document multiple times
        var clusterGroups = serviceSpec.Routes.GroupBy(r => r.Cluster.ClusterId).ToList();

        // Process clusters in parallel, bounded by MaxConcurrentFetches
        using var fetchSemaphore = new SemaphoreSlim(Math.Max(1, aggregation.Options.MaxConcurrentFetches));

        var clusterTasks = clusterGroups.Select(async clusterGroup =>
        {
            await fetchSemaphore.WaitAsync(cancellationToken);
            try
            {
                return await ProcessClusterRoutesAsync(
                    aggregation,
                    clusterGroup.Key,
                    [.. clusterGroup],
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogClusterProcessingError(clusterGroup.Key, ex);
                return null;
            }
            finally
            {
                _ = fetchSemaphore.Release();
            }
        });

        var documents = await Task.WhenAll(clusterTasks);

        // Preserve cluster order for deterministic merging
        var processedDocuments = new List<OpenApiDocument>();
        for (var i = 0; i < clusterGroups.Count; i++)
        {
            if (documents[i] != null)
            {
                processedDocuments.Add(documents[i]!);
                LogClusterProcessed(clusterGroups[i].Key);
            }
        }

        return processedDocuments;
    }

    /// <summary>
    /// Processes all routes for a single cluster: fetches the document once,
    /// analyzes reachability across all routes, prunes, and applies prefix.
    /// </summary>
    private async Task<OpenApiDocument?> ProcessClusterRoutesAsync(
        AggregationContext aggregation,
        string clusterId,
        List<RouteClusterMapping> routeMappings,
        CancellationToken cancellationToken)
    {
        LogProcessingCluster(clusterId, routeMappings.Count);

        var firstMapping = routeMappings[0];
        var baseUrl = GetClusterBaseUrl(firstMapping, clusterId);
        if (baseUrl == null)
        {
            return null;
        }

        var openApiPath = firstMapping.ClusterOpenApiConfig.OpenApiPath;
        if (String.IsNullOrWhiteSpace(openApiPath))
        {
            openApiPath = aggregation.Options.DefaultOpenApiPath;
        }
        var document = await aggregation.DocumentFetcher.FetchDocumentAsync(baseUrl, openApiPath, cancellationToken);

        if (document == null)
        {
            LogFetchFailed(clusterId);
            return null;
        }

        LogFetchedDocument(document.Paths?.Count ?? 0);

        // Analyze reachability across all routes for this cluster at once
        var reachabilityResult = aggregation.ReachabilityAnalyzer.AnalyzePathReachability(document, routeMappings);
        LogPathReachability(reachabilityResult.ReachablePaths.Count, reachabilityResult.UnreachablePaths.Count);

        var prunedDocument = aggregation.DocumentPruner.PruneDocument(document, reachabilityResult);

        if (prunedDocument == null)
        {
            LogDocumentEmpty(clusterId);
            return null;
        }

        LogPrunedDocument(prunedDocument.Paths?.Count ?? 0);

        return ApplySchemaPrefix(aggregation.SchemaRenamer, prunedDocument, firstMapping, clusterId);
    }

    /// <summary>
    /// Gets the base URL from cluster destinations.
    /// </summary>
    private string? GetClusterBaseUrl(RouteClusterMapping routeMapping, string clusterId)
    {
        var destination = routeMapping.Cluster.Destinations?.FirstOrDefault().Value;
        if (destination == null)
        {
            LogNoDestinations(clusterId);
            return null;
        }

        var baseUrl = destination.Address?.TrimEnd('/');
        if (String.IsNullOrWhiteSpace(baseUrl))
        {
            LogNoDestinationAddress(clusterId);
            return null;
        }

        return baseUrl;
    }

    /// <summary>
    /// Applies schema prefix if configured.
    /// </summary>
    private OpenApiDocument? ApplySchemaPrefix(
        ISchemaRenamer schemaRenamer,
        OpenApiDocument document,
        RouteClusterMapping routeMapping,
        string clusterId)
    {
        var prefix = routeMapping.ClusterOpenApiConfig.Prefix;
        if (String.IsNullOrWhiteSpace(prefix))
        {
            return document;
        }

        LogApplyingPrefix(prefix);
        var prefixedDocument = schemaRenamer.ApplyPrefix(document, prefix);

        if (prefixedDocument == null)
        {
            LogPrefixFailed(clusterId);
            return null;
        }

        return prefixedDocument;
    }

    /// <summary>
    /// Writes the OpenAPI document to the HTTP response, supporting content negotiation.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="document">The OpenAPI document to write.</param>
    /// <param name="explicitFormat">Explicit format from URL (json/yaml), or null to use Accept header.</param>
    private static async Task WriteOpenApiResponse(HttpContext context, OpenApiDocument document, string? explicitFormat = null)
    {
        // Set status code BEFORE writing response body
        context.Response.StatusCode = 200;

        // Determine output format
        var isYaml = DetermineOutputFormat(context, explicitFormat);

        // Set content type
        context.Response.ContentType = isYaml ? "application/yaml" : "application/json";

        // Serialize document to response body
        await using var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);

        var writer = isYaml
            ? (IOpenApiWriter)new OpenApiYamlWriter(streamWriter)
            : new OpenApiJsonWriter(streamWriter);

        document.SerializeAsV3(writer);
        await streamWriter.FlushAsync(context.RequestAborted);

        memoryStream.Position = 0;
        context.Response.ContentLength = memoryStream.Length;
        await memoryStream.CopyToAsync(context.Response.Body, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    /// <summary>
    /// Determines whether to output YAML format based on explicit format or Accept header.
    /// </summary>
    private static bool DetermineOutputFormat(HttpContext context, string? explicitFormat)
    {
        if (!String.IsNullOrEmpty(explicitFormat))
        {
            // Use explicit format from URL
            return explicitFormat.Equals("yaml", StringComparison.OrdinalIgnoreCase);
        }

        // Fall back to Accept header content negotiation
        var acceptHeader = context.Request.Headers.Accept.ToString();
        return acceptHeader.Contains("yaml", StringComparison.OrdinalIgnoreCase);
    }

    // Only ever keyed by configured service names, so the cache is bounded by the
    // YARP configuration size.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> KebabCaseCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Converts a string to kebab-case (lowercase with hyphens).
    /// Example: "User Management" -> "user-management"
    /// </summary>
    private static string ToKebabCase(string value)
    {
        if (String.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Replace spaces and underscores with hyphens, then lowercase
        return KebabCaseCache.GetOrAdd(value, static v => v.Trim()
            .Replace(" ", "-")
            .Replace("_", "-")
            .ToLowerInvariant());
    }

    /// <summary>
    /// Parses the service spec path to extract service name and explicit format.
    /// Supports:
    /// - {serviceName} -> (serviceName, null)
    /// - {serviceName}/openapi.json -> (serviceName, "json")
    /// - {serviceName}/openapi.yaml -> (serviceName, "yaml")
    /// - {serviceName}/openapi.yml -> (serviceName, "yaml")
    /// </summary>
    private static (string ServiceName, string? Format) ParseServiceSpecPath(string subPath)
    {
        if (String.IsNullOrWhiteSpace(subPath))
        {
            return (String.Empty, null);
        }

        // Check if path ends with /openapi.{extension}
        if (subPath.EndsWith(OpenApiJsonSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var serviceName = subPath[..^OpenApiJsonSuffix.Length];
            return (serviceName, "json");
        }

        if (subPath.EndsWith(OpenApiYamlSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var serviceName = subPath[..^OpenApiYamlSuffix.Length];
            return (serviceName, "yaml");
        }

        if (subPath.EndsWith(OpenApiYmlSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var serviceName = subPath[..^OpenApiYmlSuffix.Length];
            return (serviceName, "yaml");
        }

        // No explicit format, return full path as service name
        return (subPath, null);
    }

    // Source-generated logging methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling service list request")]
    private partial void LogHandlingServiceListRequest();

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} services: {Services}")]
    private partial void LogFoundServices(int count, List<ServiceInfo>? services);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling service list request")]
    private partial void LogServiceListRequestError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling OpenAPI spec request for service: {ServiceName}")]
    private partial void LogHandlingSpecRequest(string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service not found or failed to aggregate: {ServiceName}")]
    private partial void LogServiceNotFound(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully aggregated OpenAPI spec for service: {ServiceName}")]
    private partial void LogAggregationSuccess(string serviceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling OpenAPI spec request for service: {ServiceName}")]
    private partial void LogSpecRequestError(string serviceName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting aggregation for service: {ServiceName}")]
    private partial void LogStartingAggregation(string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {RouteCount} routes for service: {ServiceName}")]
    private partial void LogFoundRoutes(int routeCount, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing cluster {ClusterId} with {RouteCount} route(s)")]
    private partial void LogProcessingCluster(string clusterId, int routeCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cluster {ClusterId} has no destinations, skipping")]
    private partial void LogNoDestinations(string clusterId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cluster {ClusterId} destination has no address, skipping")]
    private partial void LogNoDestinationAddress(string clusterId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenAPI document for cluster: {ClusterId}")]
    private partial void LogFetchFailed(string clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetched OpenAPI document with {PathCount} paths")]
    private partial void LogFetchedDocument(int pathCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Path reachability: {ReachableCount} reachable, {UnreachableCount} unreachable")]
    private partial void LogPathReachability(int reachableCount, int unreachableCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document became empty after pruning for cluster: {ClusterId}")]
    private partial void LogDocumentEmpty(string clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pruned document has {PathCount} paths")]
    private partial void LogPrunedDocument(int pathCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Applying schema prefix: {Prefix}")]
    private partial void LogApplyingPrefix(string prefix);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to apply schema prefix for cluster: {ClusterId}")]
    private partial void LogPrefixFailed(string clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully processed cluster {ClusterId}")]
    private partial void LogClusterProcessed(string clusterId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing cluster {ClusterId}")]
    private partial void LogClusterProcessingError(string clusterId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No documents were successfully processed for service: {ServiceName}")]
    private partial void LogNoDocumentsProcessed(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {DocumentCount} documents for service: {ServiceName}")]
    private partial void LogDocumentsProcessed(int documentCount, string serviceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to merge documents for service: {ServiceName}")]
    private partial void LogMergeFailed(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully merged {DocumentCount} documents for service: {ServiceName}")]
    private partial void LogMergeSuccess(int documentCount, string serviceName);
}
