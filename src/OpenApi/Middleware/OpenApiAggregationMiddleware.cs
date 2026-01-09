using System.Text;
using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using AdaptArch.Extensions.Yarp.OpenApi.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Merging;
using AdaptArch.Extensions.Yarp.OpenApi.Pruning;
using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Middleware;

/// <summary>
/// Middleware that aggregates OpenAPI specifications from downstream services and exposes them via REST endpoints.
/// </summary>
public sealed partial class OpenApiAggregationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _basePath;
    private readonly ILogger _logger;
    private readonly IYarpOpenApiConfigurationReader _configReader;
    private readonly IServiceSpecificationAnalyzer _serviceAnalyzer;
    private readonly IOpenApiDocumentFetcher _documentFetcher;
    private readonly IPathReachabilityAnalyzer _reachabilityAnalyzer;
    private readonly IOpenApiDocumentPruner _documentPruner;
    private readonly ISchemaRenamer _schemaRenamer;
    private readonly IOpenApiMerger _documentMerger;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiAggregationMiddleware"/> class.
    /// </summary>
    public OpenApiAggregationMiddleware(
        RequestDelegate next,
        string basePath,
        ILogger<OpenApiAggregationMiddleware> logger,
        IYarpOpenApiConfigurationReader configReader,
        IServiceSpecificationAnalyzer serviceAnalyzer,
        IOpenApiDocumentFetcher documentFetcher,
        IPathReachabilityAnalyzer reachabilityAnalyzer,
        IOpenApiDocumentPruner documentPruner,
        ISchemaRenamer schemaRenamer,
        IOpenApiMerger documentMerger,
        IMemoryCache cache)
    {
        _next = next;
        _basePath = basePath;
        _logger = logger;
        _configReader = configReader;
        _serviceAnalyzer = serviceAnalyzer;
        _documentFetcher = documentFetcher;
        _reachabilityAnalyzer = reachabilityAnalyzer;
        _documentPruner = documentPruner;
        _schemaRenamer = schemaRenamer;
        _documentMerger = documentMerger;
        _cache = cache;
    }

    /// <summary>
    /// Processes HTTP requests and handles OpenAPI aggregation endpoints.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? String.Empty;

        // Check if request matches our base path
        if (!path.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Extract the service name from the path (if present)
        var subPath = path.Substring(_basePath.Length).TrimStart('/');

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

            // Analyze services from YARP configuration
            var serviceSpecs = _serviceAnalyzer.AnalyzeServices();
            var serviceNames = serviceSpecs.Select(s => s.ServiceName).Distinct().ToList();

            LogFoundServices(serviceNames.Count, String.Join(", ", serviceNames));

            // Return JSON response
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;

            var response = new ServiceListResponse
            {
                Services = serviceNames,
                Count = serviceNames.Count
            };

            var json = JsonSerializer.Serialize(response, OpenApiJsonContext.Default.ServiceListResponse);

            await context.Response.WriteAsync(json);
        }
        catch (Exception ex)
        {
            LogServiceListRequestError(ex);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    /// <summary>
    /// Handles requests for a specific service's aggregated OpenAPI specification.
    /// GET /api-docs/{serviceName}
    /// </summary>
    private async Task HandleServiceSpecRequest(HttpContext context, string serviceName)
    {
        try
        {
            LogHandlingSpecRequest(serviceName);

            // Normalize service name (URL decode and normalize case)
            serviceName = Uri.UnescapeDataString(serviceName);

            // Check cache first
            var cacheKey = $"openapi_spec_{serviceName}";
            if (_cache.TryGetValue<OpenApiDocument>(cacheKey, out var cachedDoc) && cachedDoc != null)
            {
                LogReturningCachedSpec(serviceName);
                await WriteOpenApiResponse(context, cachedDoc);
                return;
            }

            // Aggregate the OpenAPI spec for this service
            var aggregatedDoc = await AggregateServiceSpecificationAsync(serviceName);

            if (aggregatedDoc == null)
            {
                LogServiceNotFound(serviceName);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"Service '{serviceName}' not found or failed to aggregate");
                return;
            }

            // Cache the result for 5 minutes
            _cache.Set(cacheKey, aggregatedDoc, TimeSpan.FromMinutes(5));

            LogAggregationSuccess(serviceName);
            await WriteOpenApiResponse(context, aggregatedDoc);
        }
        catch (Exception ex)
        {
            LogSpecRequestError(serviceName, ex);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    /// <summary>
    /// Aggregates the OpenAPI specification for a specific service.
    /// </summary>
    private async Task<OpenApiDocument?> AggregateServiceSpecificationAsync(string serviceName)
    {
        LogStartingAggregation(serviceName);

        // Analyze services and find matching service
        // Support both exact match and kebab-case URL matching
        var serviceSpecs = _serviceAnalyzer.AnalyzeServices();
        var serviceSpec = serviceSpecs.FirstOrDefault(s =>
            String.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase) ||
            String.Equals(ToKebabCase(s.ServiceName), serviceName, StringComparison.OrdinalIgnoreCase));

        if (serviceSpec == null)
        {
            LogServiceSpecNotFound(serviceName);
            return null;
        }

        LogFoundRoutes(serviceSpec.Routes.Count, serviceName);

        // Process each route: fetch, analyze, prune, rename
        var processedDocuments = new List<OpenApiDocument>();

        foreach (var routeMapping in serviceSpec.Routes)
        {
            try
            {
                var routeId = routeMapping.Route.RouteId;
                var clusterId = routeMapping.Cluster.ClusterId;

                LogProcessingRoute(routeId, clusterId);

                // Get base URL from cluster destinations (use first destination)
                var destination = routeMapping.Cluster.Destinations?.FirstOrDefault().Value;
                if (destination == null)
                {
                    LogNoDestinations(clusterId);
                    continue;
                }

                var baseUrl = destination.Address?.TrimEnd('/');
                if (String.IsNullOrWhiteSpace(baseUrl))
                {
                    LogNoDestinationAddress(clusterId);
                    continue;
                }

                // Get OpenAPI path from cluster metadata
                var openApiPath = routeMapping.ClusterOpenApiConfig.OpenApiPath ?? "/swagger/v1/swagger.json";

                // Fetch OpenAPI document
                var document = await _documentFetcher.FetchDocumentAsync(baseUrl, openApiPath);
                if (document == null)
                {
                    LogFetchFailed(clusterId);
                    continue;
                }

                LogFetchedDocument(document.Paths?.Count ?? 0);

                // Analyze path reachability
                var reachabilityResult = _reachabilityAnalyzer.AnalyzePathReachability(document, routeMapping);
                LogPathReachability(reachabilityResult.ReachablePaths.Count, reachabilityResult.UnreachablePaths.Count);

                // Prune unreachable paths and unused components
                var prunedDocument = _documentPruner.PruneDocument(document, reachabilityResult);

                if (prunedDocument == null)
                {
                    LogDocumentEmpty(clusterId);
                    continue;
                }

                LogPrunedDocument(prunedDocument.Paths?.Count ?? 0);

                // Apply schema prefix if configured
                var finalDocument = prunedDocument;
                var prefix = routeMapping.ClusterOpenApiConfig.Prefix;
                if (!String.IsNullOrWhiteSpace(prefix))
                {
                    LogApplyingPrefix(prefix);
                    finalDocument = _schemaRenamer.ApplyPrefix(prunedDocument, prefix);

                    if (finalDocument == null)
                    {
                        LogPrefixFailed(clusterId);
                        continue;
                    }
                }

                processedDocuments.Add(finalDocument);
                LogRouteProcessed(routeId);
            }
            catch (Exception ex)
            {
                LogRouteProcessingError(routeMapping.Route.RouteId, ex);
                // Continue with other routes
            }
        }

        if (processedDocuments.Count == 0)
        {
            LogNoDocumentsProcessed(serviceName);
            return null;
        }

        LogDocumentsProcessed(processedDocuments.Count, serviceName);

        // Merge all processed documents into one
        var mergedDocument = _documentMerger.MergeDocuments(processedDocuments, serviceName);

        if (mergedDocument == null)
        {
            LogMergeFailed(serviceName);
            return null;
        }

        LogMergeSuccess(processedDocuments.Count, serviceName);

        return mergedDocument;
    }

    /// <summary>
    /// Writes the OpenAPI document to the HTTP response, supporting content negotiation.
    /// </summary>
    private static async Task WriteOpenApiResponse(HttpContext context, OpenApiDocument document)
    {
        // Set status code BEFORE writing response body
        context.Response.StatusCode = 200;

        // Determine output format based on Accept header
        var acceptHeader = context.Request.Headers["Accept"].ToString();
        var isYaml = acceptHeader.Contains("yaml", StringComparison.OrdinalIgnoreCase);

        if (isYaml)
        {
            // Serialize as YAML
            context.Response.ContentType = "application/yaml";
            await using var memoryStream = new MemoryStream();
            await using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
            var yamlWriter = new OpenApiYamlWriter(streamWriter);
            document.SerializeAsV3(yamlWriter);
            await streamWriter.FlushAsync();
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(context.Response.Body);
        }
        else
        {
            // Serialize as JSON (default)
            context.Response.ContentType = "application/json";
            await using var memoryStream = new MemoryStream();
            await using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
            var jsonWriter = new OpenApiJsonWriter(streamWriter);
            document.SerializeAsV3(jsonWriter);
            await streamWriter.FlushAsync();
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(context.Response.Body);
        }

        await context.Response.Body.FlushAsync();
    }

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
        return value.Trim()
            .Replace(" ", "-")
            .Replace("_", "-")
            .ToLowerInvariant();
    }

    // Source-generated logging methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling service list request")]
    private partial void LogHandlingServiceListRequest();

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} services: {Services}")]
    private partial void LogFoundServices(int count, string services);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling service list request")]
    private partial void LogServiceListRequestError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling OpenAPI spec request for service: {ServiceName}")]
    private partial void LogHandlingSpecRequest(string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Returning cached OpenAPI spec for service: {ServiceName}")]
    private partial void LogReturningCachedSpec(string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service not found or failed to aggregate: {ServiceName}")]
    private partial void LogServiceNotFound(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully aggregated OpenAPI spec for service: {ServiceName}")]
    private partial void LogAggregationSuccess(string serviceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling OpenAPI spec request for service: {ServiceName}")]
    private partial void LogSpecRequestError(string serviceName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting aggregation for service: {ServiceName}")]
    private partial void LogStartingAggregation(string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service specification not found: {ServiceName}")]
    private partial void LogServiceSpecNotFound(string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {RouteCount} routes for service: {ServiceName}")]
    private partial void LogFoundRoutes(int routeCount, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing route {RouteId} for cluster {ClusterId}")]
    private partial void LogProcessingRoute(string routeId, string clusterId);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully processed route {RouteId}")]
    private partial void LogRouteProcessed(string routeId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing route {RouteId}")]
    private partial void LogRouteProcessingError(string routeId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No documents were successfully processed for service: {ServiceName}")]
    private partial void LogNoDocumentsProcessed(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {DocumentCount} documents for service: {ServiceName}")]
    private partial void LogDocumentsProcessed(int documentCount, string serviceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to merge documents for service: {ServiceName}")]
    private partial void LogMergeFailed(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully merged {DocumentCount} documents for service: {ServiceName}")]
    private partial void LogMergeSuccess(int documentCount, string serviceName);
}
