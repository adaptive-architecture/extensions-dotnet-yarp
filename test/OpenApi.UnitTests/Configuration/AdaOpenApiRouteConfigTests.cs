
using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Configuration;

public class AdaOpenApiRouteConfigTests
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

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
        var json = JsonSerializer.Serialize(config, SerializeOptions);
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiRouteConfig>(json, DeserializeOptions);

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
        var json = JsonSerializer.Serialize(config, SerializeOptions);
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiRouteConfig>(json, DeserializeOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.ServiceName);
        Assert.True(deserialized.Enabled);
    }
}
