using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Transforms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Analysis;

/// <summary>
/// Analyzes OpenAPI paths to determine which are reachable through YARP route configurations.
/// </summary>
public interface IPathReachabilityAnalyzer
{
    /// <summary>
    /// Analyzes an OpenAPI document to determine which paths are reachable through the given route mapping.
    /// </summary>
    /// <param name="document">The OpenAPI document to analyze.</param>
    /// <param name="routeMapping">The route-to-cluster mapping configuration.</param>
    /// <returns>Analysis result containing reachable and unreachable paths.</returns>
    PathReachabilityResult AnalyzePathReachability(OpenApiDocument document, RouteClusterMapping routeMapping);

    /// <summary>
    /// Analyzes multiple route mappings for the same service and determines overall path reachability.
    /// </summary>
    /// <param name="document">The OpenAPI document to analyze.</param>
    /// <param name="routeMappings">Collection of route-to-cluster mappings for the service.</param>
    /// <returns>Analysis result containing reachable and unreachable paths across all routes.</returns>
    PathReachabilityResult AnalyzePathReachability(OpenApiDocument document, IEnumerable<RouteClusterMapping> routeMappings);
}

/// <summary>
/// Result of path reachability analysis.
/// </summary>
public sealed class PathReachabilityResult
{
    /// <summary>
    /// Gets or sets the paths that are reachable through YARP routes.
    /// Key is the gateway path (as seen by clients at the gateway), value contains the operations and route info.
    /// </summary>
    public required Dictionary<string, ReachablePathInfo> ReachablePaths { get; init; }

    /// <summary>
    /// Gets or sets the paths that are not reachable through any YARP route.
    /// Key is the backend path (from the backend service's OpenAPI doc), value contains reason and operations.
    /// </summary>
    public required Dictionary<string, UnreachablePathInfo> UnreachablePaths { get; init; }

    /// <summary>
    /// Gets or sets warnings generated during analysis (e.g., non-analyzable transforms).
    /// </summary>
    public required List<string> Warnings { get; init; }
}

/// <summary>
/// Information about a reachable path.
/// </summary>
public sealed class ReachablePathInfo
{
    /// <summary>
    /// Gets or sets the backend path from the backend service's OpenAPI document.
    /// </summary>
    public required string BackendPath { get; init; }

    /// <summary>
    /// Gets or sets the gateway path as exposed by YARP (client-facing path).
    /// </summary>
    public required string GatewayPath { get; init; }

    /// <summary>
    /// Gets or sets the HTTP operations available at this path.
    /// </summary>
    public required Dictionary<HttpMethod, OpenApiOperation> Operations { get; init; }

    /// <summary>
    /// Gets or sets the route ID that makes this path reachable.
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// Gets or sets the transform analysis for this route.
    /// </summary>
    public required RouteTransformAnalysis TransformAnalysis { get; init; }
}

/// <summary>
/// Information about an unreachable path.
/// </summary>
public sealed class UnreachablePathInfo
{
    /// <summary>
    /// Gets or sets the backend path from the backend service's OpenAPI document.
    /// </summary>
    public required string BackendPath { get; init; }

    /// <summary>
    /// Gets or sets the reason why this path is unreachable.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets or sets the HTTP operations that would be available if the path were reachable.
    /// </summary>
    public required Dictionary<HttpMethod, OpenApiOperation> Operations { get; init; }
}

/// <summary>
/// Context for path reachability analysis operations.
/// </summary>
internal sealed record AnalysisContext(
    OpenApiAggregationOptions Options,
    Dictionary<string, ReachablePathInfo> ReachablePaths,
    List<string> Warnings,
    Dictionary<HttpMethod, OpenApiOperation> Operations);

/// <summary>
/// Default implementation of <see cref="IPathReachabilityAnalyzer"/>.
/// Uses route transform analysis to determine if OpenAPI paths are reachable through YARP.
/// </summary>
public sealed partial class PathReachabilityAnalyzer : IPathReachabilityAnalyzer
{
    private readonly IRouteTransformAnalyzer _transformAnalyzer;
    private readonly IOptionsMonitor<OpenApiAggregationOptions> _optionsMonitor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathReachabilityAnalyzer"/> class.
    /// </summary>
    /// <param name="transformAnalyzer">The route transform analyzer.</param>
    /// <param name="optionsMonitor">The options monitor for configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public PathReachabilityAnalyzer(
        IRouteTransformAnalyzer transformAnalyzer,
        IOptionsMonitor<OpenApiAggregationOptions> optionsMonitor,
        ILogger<PathReachabilityAnalyzer> logger)
    {
        _transformAnalyzer = transformAnalyzer;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAPI document has no paths to analyze")]
    private partial void LogNoPathsToAnalyze();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Analyzing path reachability for {pathCount} paths across {routeCount} route(s)")]
    private partial void LogAnalyzingPaths(int pathCount, int routeCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Path {path} has no operations, skipping")]
    private partial void LogPathHasNoOperations(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{warning}")]
    private partial void LogTransformWarning(string warning);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Path {backendPath} is reachable via route {routeId} as {gatewayPath}")]
    private partial void LogPathReachable(string backendPath, string routeId, string gatewayPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Path {backendPath} is not reachable through any route")]
    private partial void LogPathNotReachable(string backendPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Path reachability analysis complete: {reachableCount} reachable, {unreachableCount} unreachable, {warningCount} warnings")]
    private partial void LogAnalysisComplete(int reachableCount, int unreachableCount, int warningCount);

    /// <inheritdoc/>
    public PathReachabilityResult AnalyzePathReachability(OpenApiDocument document, RouteClusterMapping routeMapping)
    {
        return AnalyzePathReachability(document, [routeMapping]);
    }

    /// <inheritdoc/>
    public PathReachabilityResult AnalyzePathReachability(OpenApiDocument document, IEnumerable<RouteClusterMapping> routeMappings)
    {
        var options = _optionsMonitor.CurrentValue;
        var reachablePaths = new Dictionary<string, ReachablePathInfo>(StringComparer.OrdinalIgnoreCase);
        var unreachablePaths = new Dictionary<string, UnreachablePathInfo>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        if (document?.Paths == null || document.Paths.Count == 0)
        {
            LogNoPathsToAnalyze();
            return new PathReachabilityResult
            {
                ReachablePaths = reachablePaths,
                UnreachablePaths = unreachablePaths,
                Warnings = warnings
            };
        }

        var mappingsList = routeMappings.ToList();
        LogAnalyzingPaths(document.Paths.Count, mappingsList.Count);

        foreach (var (backendPath, pathItem) in document.Paths)
        {
            if (String.IsNullOrWhiteSpace(backendPath))
            {
                continue;
            }

            var operations = GetOperations((OpenApiPathItem)pathItem);
            if (operations.Count == 0)
            {
                LogPathHasNoOperations(backendPath);
                continue;
            }

            var earlyReturn = AnalyzeSinglePath(backendPath, operations, mappingsList, options, reachablePaths, unreachablePaths, warnings);
            if (earlyReturn != null) return earlyReturn;
        }

        LogAnalysisComplete(reachablePaths.Count, unreachablePaths.Count, warnings.Count);

        return new PathReachabilityResult
        {
            ReachablePaths = reachablePaths,
            UnreachablePaths = unreachablePaths,
            Warnings = warnings
        };
    }

    private PathReachabilityResult? AnalyzeSinglePath(
        string backendPath,
        Dictionary<HttpMethod, OpenApiOperation> operations,
        List<RouteClusterMapping> mappingsList,
        OpenApiAggregationOptions options,
        Dictionary<string, ReachablePathInfo> reachablePaths,
        Dictionary<string, UnreachablePathInfo> unreachablePaths,
        List<string> warnings)
    {
        bool foundReachableRoute = false;

        foreach (var mapping in mappingsList)
        {
            var analysis = _transformAnalyzer.AnalyzeRoute(mapping.Route);

            // Handle non-analyzable transforms according to strategy
            if (!analysis.IsAnalyzable)
            {
                var context = new AnalysisContext(options, reachablePaths, warnings, operations);
                var earlyReturn = HandleNonAnalyzable(backendPath, mapping, context, analysis, ref foundReachableRoute);
                if (earlyReturn != null) return earlyReturn;
                continue;
            }

            // Check if this backend path is reachable through this route
            if (_transformAnalyzer.IsPathReachable(mapping.Route, backendPath))
            {
                var gatewayPath = _transformAnalyzer.MapBackendToGatewayPath(mapping.Route, backendPath);
                if (!String.IsNullOrWhiteSpace(gatewayPath))
                {
                    AddReachablePath(reachablePaths, gatewayPath, backendPath, operations, mapping, analysis);
                    foundReachableRoute = true;
                    LogPathReachable(backendPath, mapping.Route.RouteId, gatewayPath);
                    break;
                }
            }
        }

        if (!foundReachableRoute)
        {
            unreachablePaths[backendPath] = new UnreachablePathInfo
            {
                BackendPath = backendPath,
                Reason = "No YARP route configuration makes this path accessible",
                Operations = operations
            };
            LogPathNotReachable(backendPath);
        }

        return null;
    }

    private static void AddReachablePath(
        Dictionary<string, ReachablePathInfo> reachablePaths,
        string gatewayPath,
        string backendPath,
        Dictionary<HttpMethod, OpenApiOperation> operations,
        RouteClusterMapping mapping,
        RouteTransformAnalysis analysis)
    {
        // If gateway path already exists, we may have multiple routes to same path
        // Keep the first one found
        if (!reachablePaths.ContainsKey(gatewayPath))
        {
            reachablePaths[gatewayPath] = new ReachablePathInfo
            {
                BackendPath = backendPath,
                GatewayPath = gatewayPath,
                Operations = operations,
                RouteId = mapping.Route.RouteId,
                TransformAnalysis = analysis
            };
        }
    }

    private PathReachabilityResult? HandleNonAnalyzable(
        string backendPath,
        RouteClusterMapping mapping,
        AnalysisContext context,
        RouteTransformAnalysis analysis,
        ref bool foundReachableRoute)
    {
        var warning = $"Route {mapping.Route.RouteId} has non-analyzable transforms for path {backendPath}";

        if (context.Options.LogTransformWarnings)
        {
            LogTransformWarning(warning);
        }

        switch (context.Options.NonAnalyzableStrategy)
        {
            case NonAnalyzableTransformStrategy.IncludeWithWarning:
                context.Warnings.Add(warning);
                // Treat as reachable with a warning
                AddReachablePath(context.ReachablePaths, backendPath, backendPath, context.Operations, mapping, analysis);
                foundReachableRoute = true;
                break;

            case NonAnalyzableTransformStrategy.ExcludeWithWarning:
                context.Warnings.Add(warning);
                // Skip this route, don't mark as reachable
                break;

            case NonAnalyzableTransformStrategy.SkipService:
                context.Warnings.Add($"Skipping service due to non-analyzable route {mapping.Route.RouteId}");
                // Return early with empty results
                return new PathReachabilityResult
                {
                    ReachablePaths = [],
                    UnreachablePaths = [],
                    Warnings = context.Warnings
                };
        }

        return null;
    }

    private static Dictionary<HttpMethod, OpenApiOperation> GetOperations(OpenApiPathItem pathItem)
    {
        var operations = new Dictionary<HttpMethod, OpenApiOperation>();

        if (pathItem.Operations == null)
        {
            return operations;
        }

        foreach (var (operationType, operation) in pathItem.Operations)
        {
            if (operation != null)
            {
                operations[operationType] = operation;
            }
        }

        return operations;
    }
}
