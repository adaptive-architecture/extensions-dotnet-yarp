using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Analysis;

public class ServiceSpecificationAnalyzerTests
{
    private readonly IYarpOpenApiConfigurationReader _configReader;
    private readonly ILogger<ServiceSpecificationAnalyzer> _logger;
    private readonly ServiceSpecificationAnalyzer _analyzer;

    public ServiceSpecificationAnalyzerTests()
    {
        _configReader = Substitute.For<IYarpOpenApiConfigurationReader>();
        _logger = NullLogger<ServiceSpecificationAnalyzer>.Instance;
        _analyzer = new ServiceSpecificationAnalyzer(_configReader, _logger);
    }

    [Fact]
    public void AnalyzeServices_WithNoRoutes_ReturnsEmptyList()
    {
        // Arrange
        _configReader.GetAllRoutes().Returns([]);
        _configReader.GetAllClusters().Returns([]);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AnalyzeServices_WithRouteWithoutMetadata_SkipsRoute()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/test" }
        };

        _configReader.GetAllRoutes().Returns([route]);
        _configReader.GetAllClusters().Returns([]);
        _configReader.GetRouteOpenApiConfig("test-route").Returns((AdaOpenApiRouteConfig)null);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AnalyzeServices_WithDisabledRoute_SkipsRoute()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/test" }
        };

        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service",
            Enabled = false
        };

        _configReader.GetAllRoutes().Returns([route]);
        _configReader.GetAllClusters().Returns([]);
        _configReader.GetRouteOpenApiConfig("test-route").Returns(routeConfig);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AnalyzeServices_WithEmptyServiceName_SkipsRoute()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/test" }
        };

        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "",
            Enabled = true
        };

        _configReader.GetAllRoutes().Returns([route]);
        _configReader.GetAllClusters().Returns([]);
        _configReader.GetRouteOpenApiConfig("test-route").Returns(routeConfig);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AnalyzeServices_WithMissingCluster_SkipsRoute()
    {
        // Arrange
        var route = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "missing-cluster",
            Match = new RouteMatch { Path = "/api/test" }
        };

        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service",
            Enabled = true
        };

        _configReader.GetAllRoutes().Returns([route]);
        _configReader.GetAllClusters().Returns([]);
        _configReader.GetRouteOpenApiConfig("test-route").Returns(routeConfig);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AnalyzeServices_WithValidSingleRoute_CreatesServiceSpecification()
    {
        // Arrange
        var cluster = new ClusterConfig
        {
            ClusterId = "test-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                { "dest1", new DestinationConfig { Address = "http://localhost:8080" } }
            }
        };

        var route = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/test" }
        };

        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service",
            Enabled = true
        };

        var clusterConfig = new AdaOpenApiClusterConfig
        {
            OpenApiPath = "/swagger/v1/swagger.json",
            Prefix = "TestSvc"
        };

        _configReader.GetAllRoutes().Returns([route]);
        _configReader.GetAllClusters().Returns([cluster]);
        _configReader.GetRouteOpenApiConfig("test-route").Returns(routeConfig);
        _configReader.GetClusterOpenApiConfig("test-cluster").Returns(clusterConfig);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Single(result);
        var spec = result[0];
        Assert.Equal("Test Service", spec.ServiceName);
        Assert.Single(spec.Routes);
        Assert.Equal("test-route", spec.Routes[0].Route.RouteId);
        Assert.Equal("test-cluster", spec.Routes[0].Cluster.ClusterId);
        Assert.Equal(routeConfig, spec.Routes[0].RouteOpenApiConfig);
        Assert.Equal(clusterConfig, spec.Routes[0].ClusterOpenApiConfig);
    }

    [Fact]
    public void AnalyzeServices_WithMultipleRoutesForSameService_GroupsTogether()
    {
        // Arrange
        var cluster = new ClusterConfig
        {
            ClusterId = "test-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                { "dest1", new DestinationConfig { Address = "http://localhost:8080" } }
            }
        };

        var route1 = new RouteConfig
        {
            RouteId = "test-route-1",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/users" }
        };

        var route2 = new RouteConfig
        {
            RouteId = "test-route-2",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/posts" }
        };

        var routeConfig1 = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service",
            Enabled = true
        };

        var routeConfig2 = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service", // Same service name
            Enabled = true
        };

        _configReader.GetAllRoutes().Returns([route1, route2]);
        _configReader.GetAllClusters().Returns([cluster]);
        _configReader.GetRouteOpenApiConfig("test-route-1").Returns(routeConfig1);
        _configReader.GetRouteOpenApiConfig("test-route-2").Returns(routeConfig2);
        _configReader.GetClusterOpenApiConfig("test-cluster").Returns(new AdaOpenApiClusterConfig());

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Single(result);
        var spec = result[0];
        Assert.Equal("Test Service", spec.ServiceName);
        Assert.Equal(2, spec.Routes.Count);
    }

    [Fact]
    public void AnalyzeServices_WithMultipleServices_CreatesMultipleSpecifications()
    {
        // Arrange
        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster-1",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                { "dest1", new DestinationConfig { Address = "http://localhost:8080" } }
            }
        };

        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster-2",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                { "dest1", new DestinationConfig { Address = "http://localhost:8081" } }
            }
        };

        var route1 = new RouteConfig
        {
            RouteId = "route-1",
            ClusterId = "cluster-1",
            Match = new RouteMatch { Path = "/api/users" }
        };

        var route2 = new RouteConfig
        {
            RouteId = "route-2",
            ClusterId = "cluster-2",
            Match = new RouteMatch { Path = "/api/products" }
        };

        var routeConfig1 = new AdaOpenApiRouteConfig
        {
            ServiceName = "User Service",
            Enabled = true
        };

        var routeConfig2 = new AdaOpenApiRouteConfig
        {
            ServiceName = "Product Service",
            Enabled = true
        };

        _configReader.GetAllRoutes().Returns([route1, route2]);
        _configReader.GetAllClusters().Returns([cluster1, cluster2]);
        _configReader.GetRouteOpenApiConfig("route-1").Returns(routeConfig1);
        _configReader.GetRouteOpenApiConfig("route-2").Returns(routeConfig2);
        _configReader.GetClusterOpenApiConfig("cluster-1").Returns(new AdaOpenApiClusterConfig());
        _configReader.GetClusterOpenApiConfig("cluster-2").Returns(new AdaOpenApiClusterConfig());

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.ServiceName == "User Service");
        Assert.Contains(result, s => s.ServiceName == "Product Service");
    }

    [Fact]
    public void AnalyzeServices_WithNullClusterConfig_UsesDefaults()
    {
        // Arrange
        var cluster = new ClusterConfig
        {
            ClusterId = "test-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                { "dest1", new DestinationConfig { Address = "http://localhost:8080" } }
            }
        };

        var route = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/api/test" }
        };

        var routeConfig = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service",
            Enabled = true
        };

        _configReader.GetAllRoutes().Returns([route]);
        _configReader.GetAllClusters().Returns([cluster]);
        _configReader.GetRouteOpenApiConfig("test-route").Returns(routeConfig);
        _configReader.GetClusterOpenApiConfig("test-cluster").Returns((AdaOpenApiClusterConfig)null);

        // Act
        var result = _analyzer.AnalyzeServices();

        // Assert
        Assert.Single(result);
        var spec = result[0];
        Assert.NotNull(spec.Routes[0].ClusterOpenApiConfig);
        Assert.Equal("/swagger/v1/swagger.json", spec.Routes[0].ClusterOpenApiConfig.OpenApiPath);
    }
}
