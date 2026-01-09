namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Transforms;

using System.Collections.Generic;
using AdaptArch.Extensions.Yarp.OpenApi.Transforms;
using global::Yarp.ReverseProxy.Configuration;
using Xunit;

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
}
