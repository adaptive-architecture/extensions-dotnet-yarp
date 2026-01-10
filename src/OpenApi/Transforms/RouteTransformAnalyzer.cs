
using global::Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.Transforms;
/// <summary>
/// Service for analyzing YARP route transforms to determine path mappings.
/// </summary>
public interface IRouteTransformAnalyzer
{
    /// <summary>
    /// Analyzes a YARP route to determine its transform patterns.
    /// </summary>
    /// <param name="route">The route configuration to analyze.</param>
    /// <returns>Analysis result containing transform information.</returns>
    RouteTransformAnalysis AnalyzeRoute(RouteConfig route);

    /// <summary>
    /// Determines if a backend path is reachable through a specific route.
    /// </summary>
    /// <param name="route">The route configuration.</param>
    /// <param name="backendPath">The path from the backend service.</param>
    /// <returns>True if the path is reachable; otherwise, false.</returns>
    bool IsPathReachable(RouteConfig route, string backendPath);

    /// <summary>
    /// Maps a backend path to the gateway path that would reach it.
    /// </summary>
    /// <param name="route">The route configuration.</param>
    /// <param name="backendPath">The path from the backend service.</param>
    /// <returns>The gateway path (client-facing), or null if not mappable.</returns>
    string? MapBackendToGatewayPath(RouteConfig route, string backendPath);
}

/// <summary>
/// Implementation of route transform analyzer.
/// </summary>
public class RouteTransformAnalyzer : IRouteTransformAnalyzer
{
    /// <inheritdoc/>
    public RouteTransformAnalysis AnalyzeRoute(RouteConfig route)
    {
        var analysis = new RouteTransformAnalysis
        {
            RouteId = route.RouteId,
            MatchPattern = route.Match.Path ?? "/"
        };

        if (route.Transforms?.Any() != true)
        {
            analysis.TransformType = TransformType.Direct;
            analysis.IsAnalyzable = true;
            return analysis;
        }

        // Analyze each transform
        foreach (var transform in route.Transforms)
        {
            var transformInfo = AnalyzeTransform(transform);
            analysis.Transforms.Add(transformInfo);

            if (!transformInfo.IsAnalyzable)
            {
                analysis.IsAnalyzable = false;
                analysis.Warnings.Add($"Transform type '{transformInfo.Type}' cannot be fully analyzed");
            }
        }

        // Set overall transform type based on primary transform
        analysis.TransformType = analysis.Transforms.Count > 0
            ? analysis.Transforms[0].Type
            : TransformType.Direct;

        analysis.IsAnalyzable = analysis.Transforms.All(t => t.IsAnalyzable);

        return analysis;
    }

    /// <inheritdoc/>
    public bool IsPathReachable(RouteConfig route, string backendPath)
    {
        var gatewayPath = MapBackendToGatewayPath(route, backendPath);
        return gatewayPath != null;
    }

    /// <inheritdoc/>
    public string? MapBackendToGatewayPath(RouteConfig route, string backendPath)
    {
        var analysis = AnalyzeRoute(route);
        if (!analysis.IsAnalyzable)
        {
            // Conservative: assume reachable if we can't analyze
            return null;
        }

        var currentPath = backendPath;

        // Apply transforms in reverse to map backend -> gateway
        foreach (var transformInfo in analysis.Transforms)
        {
            currentPath = ApplyReverseTransform(currentPath, transformInfo, route.Match.Path ?? "/");
            if (currentPath == null)
            {
                return null;
            }
        }

        // If no transforms, backend path equals gateway path (direct mapping)
        if (analysis.Transforms.Count == 0)
        {
            return backendPath;
        }

        // After reversing transforms, currentPath should already be the gateway path
        return currentPath;
    }

    private static TransformInfo AnalyzeTransform(IReadOnlyDictionary<string, string> transform)
    {
        var transformInfo = new TransformInfo
        {
            Config = transform
        };

        // Identify transform type by checking which key is present
        if (transform.ContainsKey("PathPattern"))
        {
            transformInfo.Type = TransformType.PathPattern;
            transformInfo.IsAnalyzable = true;
        }
        else if (transform.ContainsKey("PathPrefix"))
        {
            transformInfo.Type = TransformType.PathPrefix;
            transformInfo.IsAnalyzable = true;
        }
        else if (transform.ContainsKey("PathRemovePrefix"))
        {
            transformInfo.Type = TransformType.PathRemovePrefix;
            transformInfo.IsAnalyzable = true;
        }
        else if (transform.ContainsKey("PathSet"))
        {
            transformInfo.Type = TransformType.PathSet;
            transformInfo.IsAnalyzable = true;
        }
        else
        {
            transformInfo.Type = TransformType.Unknown;
            transformInfo.IsAnalyzable = false;
        }

        return transformInfo;
    }

    private static string? ApplyReverseTransform(string downstreamPath, TransformInfo transformInfo, string routePattern)
    {
        return transformInfo.Type switch
        {
            TransformType.PathPattern => ReversePathPattern(downstreamPath, transformInfo, routePattern),
            TransformType.PathPrefix => ReversePathPrefix(downstreamPath, transformInfo, routePattern),
            TransformType.PathRemovePrefix => ReversePathRemovePrefix(downstreamPath, transformInfo),
            TransformType.PathSet => ReversePathSet(downstreamPath, transformInfo),
            _ => null
        };
    }

    private static string? ReversePathPattern(string downstreamPath, TransformInfo transformInfo, string routePattern)
    {
        if (!transformInfo.Config.TryGetValue("PathPattern", out var pattern))
        {
            return null;
        }

        // Extract the catch-all portion from the pattern
        // Pattern: "/users/{**catch-all}" -> downstream: "/users/123" -> should map to route pattern
        var patternWithoutCatchAll = pattern.Replace("/{**catch-all}", "").Replace("{**catch-all}", "");
        var routeWithoutCatchAll = routePattern.Replace("/{**catch-all}", "").Replace("{**catch-all}", "");

        if (downstreamPath.StartsWith(patternWithoutCatchAll))
        {
            var remainder = downstreamPath[patternWithoutCatchAll.Length..];
            return routeWithoutCatchAll + remainder;
        }

        return null;
    }

    private static string? ReversePathPrefix(string downstreamPath, TransformInfo transformInfo, string routePattern)
    {
        if (!transformInfo.Config.TryGetValue("PathPrefix", out var prefix))
        {
            return null;
        }

        // PathPrefix adds a prefix going forward, so reverse removes it
        if (downstreamPath.StartsWith(prefix))
        {
            var pathWithoutPrefix = downstreamPath[prefix.Length..];

            // Map the result back to the route pattern
            var routeBase = routePattern.Replace("/{**catch-all}", "").Replace("{**catch-all}", "");
            return routeBase + pathWithoutPrefix;
        }

        return null;
    }

    private static string? ReversePathRemovePrefix(string downstreamPath, TransformInfo transformInfo)
    {
        if (!transformInfo.Config.TryGetValue("PathRemovePrefix", out var prefix))
        {
            return null;
        }

        // PathRemovePrefix removes a prefix going forward, so reverse adds it back
        return prefix + downstreamPath;
    }

    private static string? ReversePathSet(string downstreamPath, TransformInfo transformInfo)
    {
        if (!transformInfo.Config.TryGetValue("PathSet", out var setPath))
        {
            return null;
        }

        // PathSet replaces the entire path
        // Can only reverse if downstream matches the set path
        return downstreamPath == setPath ? downstreamPath : null;
    }

    private static string? MapWithRoutePattern(string path, string fromPattern, string toPattern)
    {
        // Simple catch-all pattern matching
        var fromWithoutCatchAll = fromPattern.Replace("/{**catch-all}", "").Replace("{**catch-all}", "");
        var toWithoutCatchAll = toPattern.Replace("/{**catch-all}", "").Replace("{**catch-all}", "");

        // If path starts with the fromPattern prefix, map it to toPattern prefix
        if (fromWithoutCatchAll.Length == 0 || path.StartsWith(fromWithoutCatchAll))
        {
            var remainder = fromWithoutCatchAll.Length > 0
                ? path[fromWithoutCatchAll.Length..]
                : path;
            return toWithoutCatchAll + remainder;
        }

        return null;
    }
}

/// <summary>
/// Result of analyzing a route's transforms.
/// </summary>
public class RouteTransformAnalysis
{
    /// <summary>
    /// Gets or sets the route identifier.
    /// </summary>
    public string RouteId { get; set; } = String.Empty;

    /// <summary>
    /// Gets or sets the route match pattern.
    /// </summary>
    public string MatchPattern { get; set; } = String.Empty;

    /// <summary>
    /// Gets or sets the primary transform type.
    /// </summary>
    public TransformType TransformType { get; set; }

    /// <summary>
    /// Gets the list of analyzed transforms.
    /// </summary>
    public List<TransformInfo> Transforms { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the transforms can be reliably analyzed.
    /// </summary>
    public bool IsAnalyzable { get; set; } = true;

    /// <summary>
    /// Gets the list of warnings encountered during analysis.
    /// </summary>
    public List<string> Warnings { get; } = [];
}

/// <summary>
/// Information about a single transform.
/// </summary>
public class TransformInfo
{
    /// <summary>
    /// Gets or sets the transform type.
    /// </summary>
    public TransformType Type { get; set; }

    /// <summary>
    /// Gets or sets the transform configuration.
    /// </summary>
    public IReadOnlyDictionary<string, string> Config { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets a value indicating whether this transform can be analyzed.
    /// </summary>
    public bool IsAnalyzable { get; set; }
}

/// <summary>
/// Types of YARP transforms.
/// </summary>
public enum TransformType
{
    /// <summary>
    /// Direct mapping with no transforms.
    /// </summary>
    Direct,

    /// <summary>
    /// PathPattern transform using route template.
    /// </summary>
    PathPattern,

    /// <summary>
    /// PathPrefix transform adding a prefix.
    /// </summary>
    PathPrefix,

    /// <summary>
    /// PathRemovePrefix transform removing a prefix.
    /// </summary>
    PathRemovePrefix,

    /// <summary>
    /// PathSet transform replacing the entire path.
    /// </summary>
    PathSet,

    /// <summary>
    /// Unknown or unsupported transform type.
    /// </summary>
    Unknown
}
