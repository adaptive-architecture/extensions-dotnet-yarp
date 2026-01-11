using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// Integration tests for complex scenarios that can be tested with TestServer.
/// Tests backend service OpenAPI generation, gateway service discovery, and middleware behavior.
/// NOTE: Full aggregation with backend fetching requires running services on actual ports.
/// </summary>
public class ComplexScenarioIntegrationTests
{
    [Fact]
    public async Task Gateway_ServiceDiscovery_ReturnsConfiguredServices()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var serviceList = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(serviceList.TryGetProperty("services", out var services));
        var serviceArray = services.EnumerateArray().ToList();
        Assert.Equal(2, serviceArray.Count);

        var serviceNames = serviceArray.Select(s => s.GetProperty("name").GetString());
        Assert.Contains("User Management", serviceNames);
        Assert.Contains("Product Catalog", serviceNames);

        // Verify kebab-case URL formatting
        var userMgmt = serviceArray.First(s => s.GetProperty("name").GetString() == "User Management");
        Assert.Equal("/api-docs/user-management", userMgmt.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Gateway_NonExistentService_Returns404()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/nonexistent", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_PathTraversal_Rejected()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/../etc/passwd", TestContext.Current.CancellationToken);

        // Assert - Should be 400 or 404
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Gateway_EncodedPathTraversal_Returns400()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/..%2F..%2Fetc", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_SlashInServiceName_Returns400()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/user/admin", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CacheInvalidation_ServiceSpecific_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/admin/cache/invalidate/user-management", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CacheInvalidation_GlobalInvalidation_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/admin/cache/invalidate-all", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("All cache entries invalidated", content);
    }

    [Fact]
    public async Task ServiceList_CaseInsensitive_AllVariationsWork()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        foreach (var url in new[] { "/api-docs", "/API-DOCS", "/Api-Docs" })
        {
            var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
