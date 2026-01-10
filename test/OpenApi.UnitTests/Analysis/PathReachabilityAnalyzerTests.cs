using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Transforms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NSubstitute;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Analysis;

public class PathReachabilityAnalyzerTests
{
    private readonly IRouteTransformAnalyzer _transformAnalyzer;
    private readonly IOptionsMonitor<OpenApiAggregationOptions> _optionsMonitor;
    private readonly ILogger<PathReachabilityAnalyzer> _logger;
    private readonly PathReachabilityAnalyzer _analyzer;

    public PathReachabilityAnalyzerTests()
    {
        _transformAnalyzer = Substitute.For<IRouteTransformAnalyzer>();
        _optionsMonitor = Substitute.For<IOptionsMonitor<OpenApiAggregationOptions>>();
        _logger = NullLogger<PathReachabilityAnalyzer>.Instance;

        var options = new OpenApiAggregationOptions();
        _optionsMonitor.CurrentValue.Returns(options);

        _analyzer = new PathReachabilityAnalyzer(_transformAnalyzer, _optionsMonitor, _logger);
    }

    [Fact]
    public void AnalyzePathReachability_WithEmptyDocument_ReturnsEmptyResult()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Paths = []
        };
        var mapping = CreateTestMapping();

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Empty(result.ReachablePaths);
        Assert.Empty(result.UnreachablePaths);
    }

    [Fact]
    public void AnalyzePathReachability_WithNullPaths_ReturnsEmptyResult()
    {
        // Arrange
        var document = new OpenApiDocument();
        var mapping = CreateTestMapping();

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Empty(result.ReachablePaths);
        Assert.Empty(result.UnreachablePaths);
    }

    [Fact]
    public void AnalyzePathReachability_WithReachablePath_IncludesInResult()
    {
        // Arrange
        var document = CreateDocumentWithPath("/users", HttpMethod.Get);
        var mapping = CreateTestMapping();

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.Direct,
            IsAnalyzable = true
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);
        _transformAnalyzer.IsPathReachable(mapping.Route, "/users").Returns(true);
        _transformAnalyzer.MapBackendToGatewayPath(mapping.Route, "/users").Returns("/api/users");

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Single(result.ReachablePaths);
        Assert.Empty(result.UnreachablePaths);
        Assert.True(result.ReachablePaths.ContainsKey("/api/users"));

        var reachableInfo = result.ReachablePaths["/api/users"];
        Assert.Equal("/users", reachableInfo.BackendPath);
        Assert.Equal("/api/users", reachableInfo.GatewayPath);
        Assert.Single(reachableInfo.Operations);
        Assert.Contains(HttpMethod.Get, reachableInfo.Operations.Keys);
    }

    [Fact]
    public void AnalyzePathReachability_WithUnreachablePath_IncludesInUnreachable()
    {
        // Arrange
        var document = CreateDocumentWithPath("/admin", HttpMethod.Get);
        var mapping = CreateTestMapping();

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.Direct,
            IsAnalyzable = true
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);
        _transformAnalyzer.IsPathReachable(mapping.Route, "/admin").Returns(false);

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Empty(result.ReachablePaths);
        Assert.Single(result.UnreachablePaths);
        Assert.True(result.UnreachablePaths.ContainsKey("/admin"));

        var unreachableInfo = result.UnreachablePaths["/admin"];
        Assert.Equal("/admin", unreachableInfo.BackendPath);
        Assert.Contains("No YARP route", unreachableInfo.Reason);
    }

    [Fact]
    public void AnalyzePathReachability_WithMultiplePaths_AnalyzesAll()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/users"] = CreatePathItem(HttpMethod.Get),
                ["/posts"] = CreatePathItem(HttpMethod.Get),
                ["/admin"] = CreatePathItem(HttpMethod.Get)
            }
        };
        var mapping = CreateTestMapping();

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.PathPrefix,
            IsAnalyzable = true
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);

        // /users and /posts are reachable, /admin is not
        _transformAnalyzer.IsPathReachable(mapping.Route, "/users").Returns(true);
        _transformAnalyzer.IsPathReachable(mapping.Route, "/posts").Returns(true);
        _transformAnalyzer.IsPathReachable(mapping.Route, "/admin").Returns(false);

        _transformAnalyzer.MapBackendToGatewayPath(mapping.Route, "/users").Returns("/api/users");
        _transformAnalyzer.MapBackendToGatewayPath(mapping.Route, "/posts").Returns("/api/posts");

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Equal(2, result.ReachablePaths.Count);
        Assert.Single(result.UnreachablePaths);
        Assert.Contains("/api/users", result.ReachablePaths.Keys);
        Assert.Contains("/api/posts", result.ReachablePaths.Keys);
        Assert.Contains("/admin", result.UnreachablePaths.Keys);
    }

    [Fact]
    public void AnalyzePathReachability_WithMultipleOperations_IncludesAll()
    {
        // Arrange
        var pathItem = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation { OperationId = "getUser" },
                [HttpMethod.Post] = new OpenApiOperation { OperationId = "createUser" },
                [HttpMethod.Put] = new OpenApiOperation { OperationId = "updateUser" },
                [HttpMethod.Delete] = new OpenApiOperation { OperationId = "deleteUser" }
            }
        };

        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths { ["/users"] = pathItem }
        };
        var mapping = CreateTestMapping();

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.Direct,
            IsAnalyzable = true
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);
        _transformAnalyzer.IsPathReachable(mapping.Route, "/users").Returns(true);
        _transformAnalyzer.MapBackendToGatewayPath(mapping.Route, "/users").Returns("/users");

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        var reachableInfo = result.ReachablePaths["/users"];
        Assert.Equal(4, reachableInfo.Operations.Count);
        Assert.Contains(HttpMethod.Get, reachableInfo.Operations.Keys);
        Assert.Contains(HttpMethod.Post, reachableInfo.Operations.Keys);
        Assert.Contains(HttpMethod.Put, reachableInfo.Operations.Keys);
        Assert.Contains(HttpMethod.Delete, reachableInfo.Operations.Keys);
    }

    [Fact]
    public void AnalyzePathReachability_WithMultipleRoutes_FindsFirstReachable()
    {
        // Arrange
        var document = CreateDocumentWithPath("/users", HttpMethod.Get);

        var route1 = CreateTestRoute("route-1", "cluster-1", "/api/v1/{**catch-all}");
        var route2 = CreateTestRoute("route-2", "cluster-2", "/api/v2/{**catch-all}");

        var mapping1 = CreateTestMapping(route1, "cluster-1");
        var mapping2 = CreateTestMapping(route2, "cluster-2");

        var analysis1 = new RouteTransformAnalysis { TransformType = TransformType.Direct, IsAnalyzable = true };
        var analysis2 = new RouteTransformAnalysis { TransformType = TransformType.Direct, IsAnalyzable = true };

        _transformAnalyzer.AnalyzeRoute(route1).Returns(analysis1);
        _transformAnalyzer.AnalyzeRoute(route2).Returns(analysis2);

        // First route doesn't match, second does
        _transformAnalyzer.IsPathReachable(route1, "/users").Returns(false);
        _transformAnalyzer.IsPathReachable(route2, "/users").Returns(true);
        _transformAnalyzer.MapBackendToGatewayPath(route2, "/users").Returns("/api/v2/users");

        // Act
        var result = _analyzer.AnalyzePathReachability(document, [mapping1, mapping2]);

        // Assert
        Assert.Single(result.ReachablePaths);
        Assert.Contains("/api/v2/users", result.ReachablePaths.Keys);
        Assert.Equal("route-2", result.ReachablePaths["/api/v2/users"].RouteId);
    }

    [Fact]
    public void AnalyzePathReachability_WithNonAnalyzableTransform_IncludeWithWarning()
    {
        // Arrange
        var document = CreateDocumentWithPath("/users", HttpMethod.Get);
        var mapping = CreateTestMapping();

        var options = new OpenApiAggregationOptions
        {
            NonAnalyzableStrategy = NonAnalyzableTransformStrategy.IncludeWithWarning,
            LogTransformWarnings = true
        };
        _optionsMonitor.CurrentValue.Returns(options);

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.Unknown,
            IsAnalyzable = false
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Single(result.ReachablePaths);
        Assert.Empty(result.UnreachablePaths);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("non-analyzable", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzePathReachability_WithNonAnalyzableTransform_ExcludeWithWarning()
    {
        // Arrange
        var document = CreateDocumentWithPath("/users", HttpMethod.Get);
        var mapping = CreateTestMapping();

        var options = new OpenApiAggregationOptions
        {
            NonAnalyzableStrategy = NonAnalyzableTransformStrategy.ExcludeWithWarning,
            LogTransformWarnings = true
        };
        _optionsMonitor.CurrentValue.Returns(options);

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.Unknown,
            IsAnalyzable = false
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Empty(result.ReachablePaths);
        Assert.Single(result.UnreachablePaths);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void AnalyzePathReachability_WithNonAnalyzableTransform_SkipService()
    {
        // Arrange
        var document = CreateDocumentWithPath("/users", HttpMethod.Get);
        var mapping = CreateTestMapping();

        var options = new OpenApiAggregationOptions
        {
            NonAnalyzableStrategy = NonAnalyzableTransformStrategy.SkipService
        };
        _optionsMonitor.CurrentValue.Returns(options);

        var analysis = new RouteTransformAnalysis
        {
            TransformType = TransformType.Unknown,
            IsAnalyzable = false
        };

        _transformAnalyzer.AnalyzeRoute(mapping.Route).Returns(analysis);

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Empty(result.ReachablePaths);
        Assert.Empty(result.UnreachablePaths);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Skipping service", result.Warnings[0]);
    }

    [Fact]
    public void AnalyzePathReachability_WithPathItemWithoutOperations_Skips()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem() // No operations
            }
        };
        var mapping = CreateTestMapping();

        // Act
        var result = _analyzer.AnalyzePathReachability(document, mapping);

        // Assert
        Assert.Empty(result.ReachablePaths);
        Assert.Empty(result.UnreachablePaths);
    }

    // Helper methods
    private static RouteClusterMapping CreateTestMapping(RouteConfig route = null, string clusterId = null)
    {
        route ??= CreateTestRoute("test-route", clusterId ?? "test-cluster");

        return new RouteClusterMapping
        {
            Route = route,
            Cluster = new ClusterConfig
            {
                ClusterId = route.ClusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    { "dest1", new DestinationConfig { Address = "http://localhost:8080" } }
                }
            },
            RouteOpenApiConfig = new AdaOpenApiRouteConfig
            {
                ServiceName = "Test Service",
                Enabled = true
            },
            ClusterOpenApiConfig = new AdaOpenApiClusterConfig()
        };
    }

    private static RouteConfig CreateTestRoute(string routeId, string clusterId, string path = null)
    {
        return new RouteConfig
        {
            RouteId = routeId,
            ClusterId = clusterId,
            Match = new RouteMatch { Path = path ?? "/test/{**catch-all}" }
        };
    }

    private static OpenApiDocument CreateDocumentWithPath(string path, HttpMethod operationType)
    {
        return new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                [path] = CreatePathItem(operationType)
            }
        };
    }

    private static OpenApiPathItem CreatePathItem(HttpMethod operationType)
    {
        return new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [operationType] = new OpenApiOperation
                {
                    OperationId = $"{operationType.ToString().ToLowerInvariant()}Operation"
                }
            }
        };
    }
}
