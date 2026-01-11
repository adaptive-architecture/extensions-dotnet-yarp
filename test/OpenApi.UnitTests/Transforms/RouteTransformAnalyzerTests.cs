using AdaptArch.Extensions.Yarp.OpenApi.Transforms;
using Yarp.ReverseProxy.Configuration;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Transforms;

public class RouteTransformAnalyzerTests
{
    [Fact]
    public void AnalyzeRoute_WithNoTransforms_ReturnsDirectMapping()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Transforms = null
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.AnalyzeRoute(route);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-route", result.RouteId);
        Assert.Equal("/api/users/{**catch-all}", result.MatchPattern);
        Assert.Equal(TransformType.Direct, result.TransformType);
        Assert.True(result.IsAnalyzable);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AnalyzeRoute_WithUnknownTransformType_ReturnsWarningAndNotAnalyzable()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "unknown-transform",
            Match = new RouteMatch { Path = "/api/unknown" },
            Transforms =
            [
                new Dictionary<string, string>
                    {
                        { "CustomTransform", "value" }
                    }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.AnalyzeRoute(route);

        // Assert
        Assert.False(result.IsAnalyzable);
        Assert.Contains("cannot be fully analyzed", result.Warnings[0]);
        Assert.Equal(TransformType.Unknown, result.Transforms[0].Type);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithUnanalyzableTransform_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "unknown-transform",
            Match = new RouteMatch { Path = "/api/unknown" },
            Transforms =
            [
                new Dictionary<string, string>
                    {
                        { "CustomTransform", "value" }
                    }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var gatewayPath = analyzer.MapBackendToGatewayPath(route, "/backend/path");

        // Assert
        Assert.Null(gatewayPath);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithPathRemovePrefix_MapsCorrectly()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "remove-prefix",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                    {
                        { "PathRemovePrefix", "/v1" }
                    }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var gatewayPath = analyzer.MapBackendToGatewayPath(route, "/users");

        // Assert
        Assert.Equal("/v1/users", gatewayPath);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithPathSet_MapsCorrectly()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "set-path",
            Match = new RouteMatch { Path = "/api/set" },
            Transforms =
            [
                new Dictionary<string, string>
                    {
                        { "PathSet", "/fixed/path" }
                    }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var gatewayPath = analyzer.MapBackendToGatewayPath(route, "/fixed/path");

        // Assert
        Assert.Equal("/fixed/path", gatewayPath);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithPathSet_NonMatching_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "set-path",
            Match = new RouteMatch { Path = "/api/set" },
            Transforms =
            [
                new Dictionary<string, string>
                    {
                        { "PathSet", "/fixed/path" }
                    }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var gatewayPath = analyzer.MapBackendToGatewayPath(route, "/other/path");

        // Assert
        Assert.Null(gatewayPath);
    }

    [Fact]
    public void AnalyzeRoute_WithNullMatchPath_UsesDefault()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "null-match-path",
            Match = new RouteMatch { Path = null },
            Transforms = null
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.AnalyzeRoute(route);

        // Assert
        Assert.Equal("/", result.MatchPattern);
    }

    [Fact]
    public void AnalyzeRoute_WithPathPattern_ReturnsAnalyzableResult()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPattern", "/users/{**catch-all}" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.AnalyzeRoute(route);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TransformType.PathPattern, result.TransformType);
        Assert.True(result.IsAnalyzable);
        Assert.Single(result.Transforms);
        Assert.Equal(TransformType.PathPattern, result.Transforms[0].Type);
    }

    [Fact]
    public void AnalyzeRoute_WithPathPrefix_ReturnsAnalyzableResult()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPrefix", "/v1" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.AnalyzeRoute(route);

        // Assert
        Assert.True(result.IsAnalyzable);
        Assert.Single(result.Transforms);
        Assert.Equal(TransformType.PathPrefix, result.Transforms[0].Type);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithPathPattern_MapsCorrectly()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPattern", "/users/{**catch-all}" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var gatewayPath = analyzer.MapBackendToGatewayPath(route, "/users/123");

        // Assert
        Assert.Equal("/api/users/123", gatewayPath);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithPathPrefix_MapsCorrectly()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPrefix", "/v1" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var gatewayPath = analyzer.MapBackendToGatewayPath(route, "/v1/users");

        // Assert
        Assert.Equal("/api/users", gatewayPath);
    }

    [Fact]
    public void IsPathReachable_WithMatchingPath_ReturnsTrue()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPattern", "/users/{**catch-all}" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var isReachable = analyzer.IsPathReachable(route, "/users/123");

        // Assert
        Assert.True(isReachable);
    }

    [Fact]
    public void IsPathReachable_WithNonMatchingPath_ReturnsFalse()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPattern", "/users/{**catch-all}" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var isReachable = analyzer.IsPathReachable(route, "/admin/settings");

        // Assert
        Assert.False(isReachable);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithNullBackendPath_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms = null
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithEmptyBackendPath_ReturnsEmpty()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms = null
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, String.Empty);

        // Assert
        Assert.Equal(String.Empty, result);
    }

    [Fact]
    public void ApplyReverseTransform_WithUnknownTransformType_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "unknown-route",
            Match = new RouteMatch { Path = "/api/test" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "UnknownTransform", "value" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, "/test/path");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReversePathPattern_WithMissingPathPatternConfig_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "OtherKey", "value" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, "/test/path");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReversePathPrefix_WithNonMatchingPrefix_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPrefix", "/v1" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, "/v2/users");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReversePathSet_WithNonMatchingPath_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            Match = new RouteMatch { Path = "/api/set" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathSet", "/fixed/path" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, "/different/path");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithMultipleTransforms_AppliesInReverse()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "multi-transform",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms =
            [
                new Dictionary<string, string>
                {
                    { "PathPrefix", "/v1" }
                },
                new Dictionary<string, string>
                {
                    { "PathRemovePrefix", "/api" }
                }
            ]
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, "/v1/users");

        // Assert
        Assert.Equal("/api/api/users", result);
    }

    [Fact]
    public void MapBackendToGatewayPath_WithDirectMapping_ReturnsBackendPath()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "direct-route",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Transforms = null
        };

        var analyzer = new RouteTransformAnalyzer();

        // Act
        var result = analyzer.MapBackendToGatewayPath(route, "/users/123");

        // Assert
        Assert.Equal("/users/123", result);
    }
}
