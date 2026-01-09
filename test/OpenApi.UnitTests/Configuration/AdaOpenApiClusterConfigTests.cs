namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Configuration;

using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Xunit;

public class AdaOpenApiClusterConfigTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var config = new AdaOpenApiClusterConfig();

        // Assert
        Assert.Equal("/swagger/v1/swagger.json", config.OpenApiPath);
        Assert.Null(config.Prefix);
    }

    [Fact]
    public void JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var config = new AdaOpenApiClusterConfig
        {
            OpenApiPath = "/api/v1/openapi.json",
            Prefix = "UserService"
        };

        // Act
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiClusterConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(config.OpenApiPath, deserialized.OpenApiPath);
        Assert.Equal(config.Prefix, deserialized.Prefix);
    }

    [Fact]
    public void JsonSerialization_WithDefaults_WorksCorrectly()
    {
        // Arrange
        var config = new AdaOpenApiClusterConfig();

        // Act
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var deserialized = JsonSerializer.Deserialize<AdaOpenApiClusterConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("/swagger/v1/swagger.json", deserialized.OpenApiPath);
        Assert.Null(deserialized.Prefix);
    }
}
