using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// <para>Integration tests for OpenApiAggregationMiddleware that can be tested with TestServer.</para>
/// <para>
/// Note: Full end-to-end tests with actual backend proxying require running services
/// and cannot be tested with TestServer (YARP limitation). These tests focus on
/// middleware behavior that doesn't require backend connectivity:
/// - Service list endpoint
/// - Path validation and security
/// - Error handling
/// - Response format verification
/// </para>
/// </summary>
public class OpenApiMiddlewareIntegrationTests
{
    #region Service List Endpoint Tests

    [Fact]
    public async Task GetServiceList_ReturnsConfiguredServices()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var serviceList = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(serviceList.TryGetProperty("services", out var services));
        Assert.True(serviceList.TryGetProperty("count", out var count));

        var servicesArray = services.EnumerateArray().ToList();
        Assert.NotEmpty(servicesArray);
        Assert.Equal(2, count.GetInt32()); // User Management and Product Catalog

        // Each service should have name and url
        foreach (var service in servicesArray)
        {
            Assert.True(service.TryGetProperty("name", out _));
            Assert.True(service.TryGetProperty("url", out _));
        }
    }

    [Fact]
    public async Task GetServiceList_ContainsExpectedServiceNames()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var serviceList = JsonSerializer.Deserialize<JsonElement>(content);
        var services = serviceList.GetProperty("services").EnumerateArray().ToList();

        var serviceNames = services.Select(s => s.GetProperty("name").GetString()).ToList();

        Assert.Contains("User Management", serviceNames);
        Assert.Contains("Product Catalog", serviceNames);
    }

    [Fact]
    public async Task GetServiceList_ServicesHaveKebabCaseUrls()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var serviceList = JsonSerializer.Deserialize<JsonElement>(content);
        var services = serviceList.GetProperty("services").EnumerateArray().ToList();

        foreach (var service in services)
        {
            var url = service.GetProperty("url").GetString();
            Assert.NotNull(url);

            // URLs should be lowercase with hyphens (kebab-case)
            Assert.Equal(url, url.ToLowerInvariant());
            Assert.DoesNotContain(" ", url);
            Assert.DoesNotContain("_", url);
            Assert.Contains("/api-docs/", url);
        }
    }

    [Fact]
    public async Task GetServiceList_UrlsMatchServiceNames()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var serviceList = JsonSerializer.Deserialize<JsonElement>(content);
        var services = serviceList.GetProperty("services").EnumerateArray().ToList();

        // "User Management" should map to "/api-docs/user-management"
        var userMgmt = services.First(s => s.GetProperty("name").GetString() == "User Management");
        Assert.Equal("/api-docs/user-management", userMgmt.GetProperty("url").GetString());

        // "Product Catalog" should map to "/api-docs/product-catalog"
        var productCatalog = services.First(s => s.GetProperty("name").GetString() == "Product Catalog");
        Assert.Equal("/api-docs/product-catalog", productCatalog.GetProperty("url").GetString());
    }

    #endregion

    #region Error Scenarios and Security

    [Fact]
    public async Task GetServiceOpenApi_WithPathTraversal_Returns400Or404()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/../etc/passwd", TestContext.Current.CancellationToken);

        // Assert
        // ASP.NET Core may normalize the path before middleware sees it,
        // resulting in either 400 (if path still contains ..) or 404 (if normalized away from /api-docs)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected BadRequest or NotFound, but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetServiceOpenApi_WithEncodedPathTraversal_Returns400()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/..%2F..%2Fetc%2Fpasswd", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceOpenApi_WithSlashInName_Returns400()
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
    public async Task GetServiceOpenApi_WithBackslashInName_Returns400()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/user\\admin", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceOpenApi_NonExistentService_Returns404()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/nonexistent-service", TestContext.Current.CancellationToken);

        // Assert
        // Returns 404 because service not found in configuration
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("not found", content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Middleware Configuration and Routing

    [Fact]
    public async Task GetServiceList_WithTrailingSlash_StillWorks()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api-docs/", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetServiceList_IsCaseInsensitive()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response1 = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);
        var response2 = await client.GetAsync("/API-DOCS", TestContext.Current.CancellationToken);
        var response3 = await client.GetAsync("/Api-Docs", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
    }

    [Fact]
    public async Task NonApiDocsPath_PassesThroughMiddleware()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/other-path", TestContext.Current.CancellationToken);

        // Assert
        // Should get 404 from YARP (not from OpenAPI middleware)
        // OpenAPI middleware should pass through to next middleware
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Cache Invalidation Endpoints

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

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("user-management", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CacheInvalidation_All_ReturnsOk()
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

    #endregion
}
