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

        // Check if request matches our base path
        if (!path.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
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

            // Resolve services from DI
            var cache = context.RequestServices.GetRequiredService<HybridCache>();
            var optionsMonitor = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenApiAggregationOptions>>();

            // Use HybridCache with automatic stampede protection
            var cacheKey = $"openapi_spec_{serviceName}";
            var tags = new[] { "openapi_spec", $"service:{serviceName}" };

            var options = optionsMonitor.CurrentValue;
            var entryOptions = new HybridCacheEntryOptions
            {
                Expiration = options.AggregatedSpecCacheDuration,
                LocalCacheExpiration = options.AggregatedSpecCacheDuration
            };

            // Use wrapper to serialize OpenApiDocument as JSON string for caching
            var wrapper = await cache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    var doc = await AggregateServiceSpecificationAsync(context.RequestServices, serviceName, cancel);
                    return doc == null ? null : await OpenApiDocumentCacheWrapper.FromDocumentAsync(doc, cancel);
                },
                entryOptions,
                tags,
                context.RequestAborted
            );

            var aggregatedDoc = wrapper == null ? null : await wrapper.ToDocumentAsync(context.RequestAborted);

            if (aggregatedDoc == null)
            {
                LogServiceNotFound(serviceName);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"Service '{serviceName}' not found or failed to aggregate");
                return;
            }

            LogAggregationSuccess(serviceName);
            await WriteOpenApiResponse(context, aggregatedDoc, explicitFormat);
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
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <param name="serviceName">The name of the service to aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<OpenApiDocument?> AggregateServiceSpecificationAsync(
        IServiceProvider serviceProvider,
        string serviceName,
        CancellationToken cancellationToken)
    {
        LogStartingAggregation(serviceName);

        var serviceAnalyzer = serviceProvider.GetRequiredService<IServiceSpecificationAnalyzer>();
        var documentMerger = serviceProvider.GetRequiredService<IOpenApiMerger>();

        var serviceSpec = FindServiceSpecification(serviceAnalyzer, serviceName);
        if (serviceSpec == null)
        {
            LogServiceSpecNotFound(serviceName);
            return null;
        }

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
    /// Processes all routes for a service specification.
    /// </summary>
    private async Task<List<OpenApiDocument>> ProcessServiceRoutesAsync(
        IServiceProvider serviceProvider,
        ServiceSpecification serviceSpec,
        CancellationToken cancellationToken)
    {
        var processedDocuments = new List<OpenApiDocument>();

        // Resolve services once outside the loop for better performance
        var documentFetcher = serviceProvider.GetRequiredService<IOpenApiDocumentFetcher>();
        var reachabilityAnalyzer = serviceProvider.GetRequiredService<IPathReachabilityAnalyzer>();
        var documentPruner = serviceProvider.GetRequiredService<IOpenApiDocumentPruner>();
        var schemaRenamer = serviceProvider.GetRequiredService<ISchemaRenamer>();

        foreach (var routeMapping in serviceSpec.Routes)
        {
            try
            {
                var document = await ProcessSingleRouteAsync(
                    documentFetcher,
                    reachabilityAnalyzer,
                    documentPruner,
                    schemaRenamer,
                    routeMapping,
                    cancellationToken);

                if (document != null)
                {
                    processedDocuments.Add(document);
                    LogRouteProcessed(routeMapping.Route.RouteId);
                }
            }
            catch (Exception ex)
            {
                LogRouteProcessingError(routeMapping.Route.RouteId, ex);
            }
        }

        return processedDocuments;
    }

    /// <summary>
    /// Processes a single route to produce an OpenAPI document.
    /// </summary>
    private async Task<OpenApiDocument?> ProcessSingleRouteAsync(
        IOpenApiDocumentFetcher documentFetcher,
        IPathReachabilityAnalyzer reachabilityAnalyzer,
        IOpenApiDocumentPruner documentPruner,
        ISchemaRenamer schemaRenamer,
        RouteClusterMapping routeMapping,
        CancellationToken cancellationToken)
    {
        var clusterId = routeMapping.Cluster.ClusterId;
        LogProcessingRoute(routeMapping.Route.RouteId, clusterId);

        var baseUrl = GetClusterBaseUrl(routeMapping, clusterId);
        if (baseUrl == null)
        {
            return null;
        }

        var openApiPath = routeMapping.ClusterOpenApiConfig.OpenApiPath ?? "/swagger/v1/swagger.json";
        var document = await documentFetcher.FetchDocumentAsync(baseUrl, openApiPath, cancellationToken);

        if (document == null)
        {
            LogFetchFailed(clusterId);
            return null;
        }

        LogFetchedDocument(document.Paths?.Count ?? 0);

        var prunedDocument = PruneDocumentPaths(reachabilityAnalyzer, documentPruner, document, routeMapping, clusterId);
        if (prunedDocument == null)
        {
            return null;
        }

        return ApplySchemaPrefix(schemaRenamer, prunedDocument, routeMapping, clusterId);
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
    /// Prunes unreachable paths from the document.
    /// </summary>
    private OpenApiDocument? PruneDocumentPaths(
        IPathReachabilityAnalyzer reachabilityAnalyzer,
        IOpenApiDocumentPruner documentPruner,
        OpenApiDocument document,
        RouteClusterMapping routeMapping,
        string clusterId)
    {
        var reachabilityResult = reachabilityAnalyzer.AnalyzePathReachability(document, routeMapping);
        LogPathReachability(reachabilityResult.ReachablePaths.Count, reachabilityResult.UnreachablePaths.Count);

        var prunedDocument = documentPruner.PruneDocument(document, reachabilityResult);

        if (prunedDocument == null)
        {
            LogDocumentEmpty(clusterId);
            return null;
        }

        LogPrunedDocument(prunedDocument.Paths?.Count ?? 0);
        return prunedDocument;
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
        await streamWriter.FlushAsync();

        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(context.Response.Body);
        await context.Response.Body.FlushAsync();
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
        if (subPath.EndsWith("/openapi.json", StringComparison.OrdinalIgnoreCase))
        {
            var serviceName = subPath[..^"/openapi.json".Length];
            return (serviceName, "json");
        }

        if (subPath.EndsWith("/openapi.yaml", StringComparison.OrdinalIgnoreCase))
        {
            var serviceName = subPath[..^"/openapi.yaml".Length];
            return (serviceName, "yaml");
        }

        if (subPath.EndsWith("/openapi.yml", StringComparison.OrdinalIgnoreCase))
        {
            var serviceName = subPath[..^"/openapi.yml".Length];
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
