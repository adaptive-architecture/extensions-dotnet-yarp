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
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        var metadataJson = JsonSerializer.Serialize(clusterConfig, SerializeOptions);

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
        var metadataJson = JsonSerializer.Serialize(routeConfig, SerializeOptions);

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

    [Fact]
    public void GetRouteOpenApiConfig_WithNoMetadata_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "user-route",
            ClusterId = "user-service",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Metadata = new Dictionary<string, string>()
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
        Assert.Null(result);
    }

    [Fact]
    public void GetRouteOpenApiConfig_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "user-route",
            ClusterId = "user-service",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", "{ not valid json" }
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
        Assert.Null(result);
    }

    [Fact]
    public void GetRouteOpenApiConfig_WithNonExistentRoute_ReturnsNull()
    {
        // Arrange
        var proxyConfig = new TestProxyConfig
        {
            Routes = []
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetRouteOpenApiConfig("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetClusterOpenApiConfig_WithNonExistentCluster_ReturnsNull()
    {
        // Arrange
        var proxyConfig = new TestProxyConfig
        {
            Clusters = []
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetClusterOpenApiConfig("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetClusterOpenApiConfig_WithNullMetadata_ReturnsNull()
    {
        // Arrange
        var cluster = new ClusterConfig
        {
            ClusterId = "user-service",
            Metadata = null
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
    public void GetRouteOpenApiConfig_WithNullMetadata_ReturnsNull()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "user-route",
            ClusterId = "user-service",
            Match = new RouteMatch { Path = "/api/users/{**catch-all}" },
            Metadata = null
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
        Assert.Null(result);
    }

    [Fact]
    public void GetAllClusters_WithNoClusters_ReturnsEmpty()
    {
        // Arrange
        var proxyConfig = new TestProxyConfig
        {
            Clusters = []
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllClusters();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllClusters_WithMultipleClusters_ReturnsAll()
    {
        // Arrange
        var cluster1 = new ClusterConfig { ClusterId = "cluster1" };
        var cluster2 = new ClusterConfig { ClusterId = "cluster2" };
        var cluster3 = new ClusterConfig { ClusterId = "cluster3" };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster1, cluster2, cluster3]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllClusters().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.ClusterId == "cluster1");
        Assert.Contains(result, c => c.ClusterId == "cluster2");
        Assert.Contains(result, c => c.ClusterId == "cluster3");
    }

    [Fact]
    public void GetAllRoutes_WithNoRoutes_ReturnsEmpty()
    {
        // Arrange
        var proxyConfig = new TestProxyConfig
        {
            Routes = []
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllRoutes();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllRoutes_WithMultipleRoutes_ReturnsAll()
    {
        // Arrange
        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/v1/{**catch-all}" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/api/v2/{**catch-all}" }
        };

        var proxyConfig = new TestProxyConfig
        {
            Routes = [route1, route2]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllRoutes().ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.RouteId == "route1");
        Assert.Contains(result, r => r.RouteId == "route2");
    }

    [Fact]
    public void GetAllRouteOpenApiConfigs_WithNoRoutes_ReturnsEmpty()
    {
        // Arrange
        var proxyConfig = new TestProxyConfig
        {
            Routes = []
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllRouteOpenApiConfigs();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllRouteOpenApiConfigs_WithRoutesButNoMetadata_ReturnsEmpty()
    {
        // Arrange
        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/v1/{**catch-all}" },
            Metadata = null
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/api/v2/{**catch-all}" },
            Metadata = new Dictionary<string, string>()
        };

        var proxyConfig = new TestProxyConfig
        {
            Routes = [route1, route2]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllRouteOpenApiConfigs();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllRouteOpenApiConfigs_WithValidMetadata_ReturnsConfigs()
    {
        // Arrange
        var routeConfig1 = new AdaOpenApiRouteConfig { ServiceName = "Service1", Enabled = true };
        var routeConfig2 = new AdaOpenApiRouteConfig { ServiceName = "Service2", Enabled = false };

        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/v1/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", JsonSerializer.Serialize(routeConfig1, SerializeOptions) }
            }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/api/v2/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", JsonSerializer.Serialize(routeConfig2, SerializeOptions) }
            }
        };

        var proxyConfig = new TestProxyConfig
        {
            Routes = [route1, route2]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllRouteOpenApiConfigs().ToList();

        // Assert
        Assert.Equal(2, result.Count);

        var first = result.First(r => r.Route.RouteId == "route1");
        Assert.Equal("Service1", first.AdaConfig.ServiceName);
        Assert.True(first.AdaConfig.Enabled);

        var second = result.First(r => r.Route.RouteId == "route2");
        Assert.Equal("Service2", second.AdaConfig.ServiceName);
        Assert.False(second.AdaConfig.Enabled);
    }

    [Fact]
    public void GetAllRouteOpenApiConfigs_WithMixedValidAndInvalid_ReturnsOnlyValid()
    {
        // Arrange
        var routeConfig = new AdaOpenApiRouteConfig { ServiceName = "ValidService", Enabled = true };

        var route1 = new RouteConfig
        {
            RouteId = "valid-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/v1/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", JsonSerializer.Serialize(routeConfig, SerializeOptions) }
            }
        };
        var route2 = new RouteConfig
        {
            RouteId = "invalid-route",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/api/v2/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", "{ invalid json" }
            }
        };
        var route3 = new RouteConfig
        {
            RouteId = "no-metadata-route",
            ClusterId = "cluster3",
            Match = new RouteMatch { Path = "/api/v3/{**catch-all}" },
            Metadata = null
        };

        var proxyConfig = new TestProxyConfig
        {
            Routes = [route1, route2, route3]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllRouteOpenApiConfigs().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("valid-route", result[0].Route.RouteId);
        Assert.Equal("ValidService", result[0].AdaConfig.ServiceName);
    }

    [Fact]
    public void GetAllClusterOpenApiConfigs_WithNoClusters_ReturnsEmpty()
    {
        // Arrange
        var proxyConfig = new TestProxyConfig
        {
            Clusters = []
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllClusterOpenApiConfigs();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllClusterOpenApiConfigs_WithClustersButNoMetadata_ReturnsEmpty()
    {
        // Arrange
        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Metadata = null
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Metadata = new Dictionary<string, string>()
        };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster1, cluster2]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllClusterOpenApiConfigs();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllClusterOpenApiConfigs_WithValidMetadata_ReturnsConfigs()
    {
        // Arrange
        var clusterConfig1 = new AdaOpenApiClusterConfig { OpenApiPath = "/api/openapi.json", Prefix = "Service1" };
        var clusterConfig2 = new AdaOpenApiClusterConfig { OpenApiPath = "/swagger/v1/swagger.json", Prefix = "Service2" };

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", JsonSerializer.Serialize(clusterConfig1, SerializeOptions) }
            }
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", JsonSerializer.Serialize(clusterConfig2, SerializeOptions) }
            }
        };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster1, cluster2]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllClusterOpenApiConfigs().ToList();

        // Assert
        Assert.Equal(2, result.Count);

        var first = result.First(c => c.Cluster.ClusterId == "cluster1");
        Assert.Equal("/api/openapi.json", first.AdaConfig.OpenApiPath);
        Assert.Equal("Service1", first.AdaConfig.Prefix);

        var second = result.First(c => c.Cluster.ClusterId == "cluster2");
        Assert.Equal("/swagger/v1/swagger.json", second.AdaConfig.OpenApiPath);
        Assert.Equal("Service2", second.AdaConfig.Prefix);
    }

    [Fact]
    public void GetAllClusterOpenApiConfigs_WithMixedValidAndInvalid_ReturnsOnlyValid()
    {
        // Arrange
        var clusterConfig = new AdaOpenApiClusterConfig { OpenApiPath = "/api/spec.json", Prefix = "Valid" };

        var cluster1 = new ClusterConfig
        {
            ClusterId = "valid-cluster",
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", JsonSerializer.Serialize(clusterConfig, SerializeOptions) }
            }
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "invalid-cluster",
            Metadata = new Dictionary<string, string>
            {
                { "Ada.OpenApi", "{ not valid json" }
            }
        };
        var cluster3 = new ClusterConfig
        {
            ClusterId = "no-metadata-cluster",
            Metadata = null
        };

        var proxyConfig = new TestProxyConfig
        {
            Clusters = [cluster1, cluster2, cluster3]
        };

        _proxyConfigProvider.GetConfig().Returns(proxyConfig);

        var reader = new YarpOpenApiConfigurationReader(_proxyConfigProvider, _logger);

        // Act
        var result = reader.GetAllClusterOpenApiConfigs().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("valid-cluster", result[0].Cluster.ClusterId);
        Assert.Equal("/api/spec.json", result[0].AdaConfig.OpenApiPath);
        Assert.Equal("Valid", result[0].AdaConfig.Prefix);
    }

    [Fact]
    public void GetClusterOpenApiConfig_WithDefaultOpenApiPath_UsesDefault()
    {
        // Arrange
        var clusterConfig = new AdaOpenApiClusterConfig
        {
            Prefix = "UserService"
            // OpenApiPath not specified - should default
        };
        var metadataJson = JsonSerializer.Serialize(clusterConfig, SerializeOptions);

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
        Assert.Equal("/swagger/v1/swagger.json", result.OpenApiPath);
        Assert.Equal("UserService", result.Prefix);
    }

    [Fact]
    public void GetRouteOpenApiConfig_WithDefaultEnabled_UsesDefault()
    {
        // Arrange
        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "User Management"
            // Enabled not specified - should default to true
        };
        var metadataJson = JsonSerializer.Serialize(routeConfig, SerializeOptions);

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
