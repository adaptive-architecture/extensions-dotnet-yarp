using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Configuration;

public class YarpOpenApiConfigurationReaderTests
{
    private readonly IProxyConfigProvider _proxyConfigProvider;
    private readonly ILogger<YarpOpenApiConfigurationReader> _logger;

    public YarpOpenApiConfigurationReaderTests()
    {
        _proxyConfigProvider = Substitute.For<IProxyConfigProvider>();
        _logger = NullLogger<YarpOpenApiConfigurationReader>.Instance;
    }

    [Fact]
    public void GetClusterOpenApiConfig_WithValidMetadata_ReturnsConfig()
    {
        // Arrange
        var clusterConfig = new AdaOpenApiClusterConfig
        {
            OpenApiPath = "/api/v1/openapi.json",
            Prefix = "UserService"
        };
        var metadataJson = JsonSerializer.Serialize(clusterConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var cluster = new ClusterConfig
        {
            ClusterId = "user-service",
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", metadataJson }
            }
        };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetClusterOpenApiConfig("user-service");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/api/v1/openapi.json", result.OpenApiPath);
        Assert.Equal("UserService", result.Prefix);
    }

    [Fact]
    public void GetClusterOpenApiConfig_WithNoMetadata_ReturnsNull()
    {
        // Arrange
        var cluster = new ClusterConfig
        {
            ClusterId = "user-service",
            Metadata = new Dictionary<string, string>()
        };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetClusterOpenApiConfig("user-service");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetClusterOpenApiConfig_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var cluster = new ClusterConfig
        {
            ClusterId = "user-service",
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", "invalid json {" }
            }
        };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetClusterOpenApiConfig("user-service");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRouteOpenApiConfig_WithValidMetadata_ReturnsConfig()
    {
        // Arrange
        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "User Management",
            Enabled = true
        };
        var metadataJson = JsonSerializer.Serialize(routeConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var route = new RouteConfig
        {
            RouteId = "user-route",
            ClusterId = "user-service",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", metadataJson }
            }
        };

        var proxyConfig = new TestProxyConfig
        {
            Routes = [route]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetRouteOpenApiConfig("user-route");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("User Management", result.ServiceName);
        Assert.True(result.Enabled);
    }

    private class TestProxyConfig : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes { get; set; } = [];
        public IReadOnlyList<ClusterConfig> Clusters { get; set; } = [];
        public IChangeToken ChangeToken { get; } = new TestChangeToken();
    }

    private class TestChangeToken : IChangeToken
    {
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object> callback, object state) =>
            new TestDisposable();
    }

    private sealed class TestDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
