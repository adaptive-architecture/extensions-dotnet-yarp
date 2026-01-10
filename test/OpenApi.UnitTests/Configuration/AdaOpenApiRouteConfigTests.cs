
using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Configuration;

public class AdaOpenApiRouteConfigTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var config = new AdaOpenApiRouteConfig();

        // Assert
        Assert.Null(config.ServiceName);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var config = new AdaOpenApiRouteConfig
        {
            ServiceName = "User Management",
            Enabled = false
        };

        // Act
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiRouteConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(config.ServiceName, deserialized.ServiceName);
        Assert.Equal(config.Enabled, deserialized.Enabled);
    }

    [Fact]
    public void ServiceName_CanBeNull()
    {
        // Arrange
        var config = new AdaOpenApiRouteConfig
        {
            ServiceName = null,
            Enabled = true
        };

        // Act
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiRouteConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.ServiceName);
        Assert.True(deserialized.Enabled);
    }
}
