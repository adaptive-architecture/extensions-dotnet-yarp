using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Configuration;

public class OpenApiMetadataNormalizationFilterTests
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static OpenApiMetadataNormalizationFilter CreateFilter(
        IConfiguration configuration,
        string sectionName = "ReverseProxy")
    {
        var options = Options.Create(new OpenApiAggregationOptions { ReverseProxyConfigSectionName = sectionName });
        return new OpenApiMetadataNormalizationFilter(configuration, options);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string> data) =>
        new ConfigurationBuilder().AddInMemoryCollection(data).Build();

    // --- Route tests ---

    [Fact]
    public async Task ConfigureRouteAsync_WithNestedObjectInConfig_NormalizesMetadata()
    {
        // Arrange: metadata key absent from route (YARP didn't bind the nested object)
        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["ReverseProxy:Routes:route1:Metadata:Ada.OpenApi:serviceName"] = "User Management",
            ["ReverseProxy:Routes:route1:Metadata:Ada.OpenApi:enabled"] = "true"
        });

        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
            // No Metadata — YARP would omit the key because the value was a nested object
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureRouteAsync(route, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("Ada.OpenApi"));
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiRouteConfig>(result.Metadata["Ada.OpenApi"], SerializeOptions);
        Assert.NotNull(deserialized);
        Assert.Equal("User Management", deserialized.ServiceName);
        Assert.True(deserialized.Enabled);
    }

    [Fact]
    public async Task ConfigureRouteAsync_WithBooleanValue_PreservesBoolNotString()
    {
        // Arrange: ensure "enabled" round-trips as a JSON boolean, not the string "true"
        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["ReverseProxy:Routes:route1:Metadata:Ada.OpenApi:serviceName"] = "Svc",
            ["ReverseProxy:Routes:route1:Metadata:Ada.OpenApi:enabled"] = "true"
        });

        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureRouteAsync(route, null, CancellationToken.None);

        // Assert: the JSON must contain a boolean, not a string
        Assert.NotNull(result.Metadata);
        var json = result.Metadata["Ada.OpenApi"];
        Assert.Contains("\"enabled\":true", json);
        Assert.DoesNotContain("\"enabled\":\"true\"", json);
    }

    [Fact]
    public async Task ConfigureRouteAsync_WithExistingStringMetadata_LeavesOldFormatUnchanged()
    {
        // Arrange: old escaped-string format already present — must not be touched
        var expectedJson = JsonSerializer.Serialize(
            new AdaOpenApiRouteConfig { ServiceName = "Old Format", Enabled = false },
            SerializeOptions);

        var config = BuildConfiguration(new Dictionary<string, string>
        {
            // IConfiguration also has nested keys — should NOT win over the existing string value
            ["ReverseProxy:Routes:route1:Metadata:Ada.OpenApi:serviceName"] = "Overridden",
            ["ReverseProxy:Routes:route1:Metadata:Ada.OpenApi:enabled"] = "true"
        });

        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Metadata = new Dictionary<string, string> { ["Ada.OpenApi"] = expectedJson }
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureRouteAsync(route, null, CancellationToken.None);

        // Assert: original value preserved
        Assert.Equal(expectedJson, result.Metadata!["Ada.OpenApi"]);
    }

    [Fact]
    public async Task ConfigureRouteAsync_RouteNotInConfiguration_MetadataUnchanged()
    {
        // Arrange: IConfiguration has no entry for this route
        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["ReverseProxy:Routes:other-route:Metadata:Ada.OpenApi:serviceName"] = "Other"
        });

        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureRouteAsync(route, null, CancellationToken.None);

        // Assert
        Assert.Same(route, result);
        Assert.Null(result.Metadata);
    }

    [Fact]
    public async Task ConfigureRouteAsync_MixedMetadata_OnlyNormalizesNullKeys()
    {
        // Arrange: one key in old string format, one in new nested format
        var oldFormatJson = JsonSerializer.Serialize(
            new AdaOpenApiRouteConfig { ServiceName = "Preserved", Enabled = true },
            SerializeOptions);

        var config = BuildConfiguration(new Dictionary<string, string>
        {
            // Nested key for "Other.Key" only
            ["ReverseProxy:Routes:route1:Metadata:Other.Key:foo"] = "bar"
        });

        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                ["Ada.OpenApi"] = oldFormatJson  // existing string — must not be overwritten
            }
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureRouteAsync(route, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Equal(oldFormatJson, result.Metadata["Ada.OpenApi"]);
        Assert.True(result.Metadata.ContainsKey("Other.Key"));
        var otherJson = JsonSerializer.Deserialize<JsonElement>(result.Metadata["Other.Key"]);
        Assert.Equal("bar", otherJson.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task ConfigureRouteAsync_CustomSectionName_LooksUnderCorrectSection()
    {
        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["Gateway:Routes:route1:Metadata:Ada.OpenApi:serviceName"] = "Custom Section",
            ["Gateway:Routes:route1:Metadata:Ada.OpenApi:enabled"] = "true"
        });

        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var filter = CreateFilter(config, sectionName: "Gateway");

        // Act
        var result = await filter.ConfigureRouteAsync(route, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Metadata);
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiRouteConfig>(result.Metadata["Ada.OpenApi"], SerializeOptions);
        Assert.Equal("Custom Section", deserialized!.ServiceName);
    }

    // --- Cluster tests ---

    [Fact]
    public async Task ConfigureClusterAsync_WithNestedObjectInConfig_NormalizesMetadata()
    {
        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["ReverseProxy:Clusters:cluster1:Metadata:Ada.OpenApi:openApiPath"] = "/openapi/v1.json",
            ["ReverseProxy:Clusters:cluster1:Metadata:Ada.OpenApi:prefix"] = "UserService"
        });

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1"
            // No Metadata
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("Ada.OpenApi"));
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiClusterConfig>(result.Metadata["Ada.OpenApi"], SerializeOptions);
        Assert.NotNull(deserialized);
        Assert.Equal("/openapi/v1.json", deserialized.OpenApiPath);
        Assert.Equal("UserService", deserialized.Prefix);
    }

    [Fact]
    public async Task ConfigureClusterAsync_WithExistingStringMetadata_LeavesOldFormatUnchanged()
    {
        // Arrange: old escaped-string format already present
        var expectedJson = JsonSerializer.Serialize(
            new AdaOpenApiClusterConfig { OpenApiPath = "/old/path", Prefix = "Old" },
            SerializeOptions);

        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["ReverseProxy:Clusters:cluster1:Metadata:Ada.OpenApi:openApiPath"] = "/new/path",
            ["ReverseProxy:Clusters:cluster1:Metadata:Ada.OpenApi:prefix"] = "New"
        });

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Metadata = new Dictionary<string, string> { ["Ada.OpenApi"] = expectedJson }
        };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);

        // Assert: original value preserved
        Assert.Equal(expectedJson, result.Metadata!["Ada.OpenApi"]);
    }

    [Fact]
    public async Task ConfigureClusterAsync_ClusterNotInConfiguration_MetadataUnchanged()
    {
        var config = BuildConfiguration(new Dictionary<string, string>
        {
            ["ReverseProxy:Clusters:other-cluster:Metadata:Ada.OpenApi:openApiPath"] = "/api/docs"
        });

        var cluster = new ClusterConfig { ClusterId = "cluster1" };

        var filter = CreateFilter(config);

        // Act
        var result = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);

        // Assert
        Assert.Same(cluster, result);
        Assert.Null(result.Metadata);
    }
}
