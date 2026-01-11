using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// Integration tests for OpenAPI aggregation infrastructure.
/// Note: Full end-to-end YARP proxying tests require Kestrel servers running on actual ports.
/// These tests validate that the sample applications can start and API endpoints work correctly.
/// </summary>
public class OpenApiAggregationIntegrationTests
{
    [Fact]
    public async Task Gateway_Application_Starts()
    {
        // Arrange & Act
        await using var factory = new WebApplicationFactory<Samples.Gateway.Program>();
        using var client = factory.CreateClient();

        // Assert - Gateway should start successfully
        // Note: We don't test proxying here as TestServer doesn't support actual HTTP calls to backends
        Assert.NotNull(factory);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task UserService_Application_Starts()
    {
        // Arrange & Act
        await using var factory = new WebApplicationFactory<Samples.UserService.Program>();
        using var client = factory.CreateClient();

        // Assert - UserService should start successfully
        Assert.NotNull(factory);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task ProductService_Application_Starts()
    {
        // Arrange & Act
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        // Assert - ProductService should start successfully
        Assert.NotNull(factory);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task UserService_GetAllUsers_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.UserService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(content);

        var users = JsonSerializer.Deserialize<JsonElement[]>(content);
        Assert.NotNull(users);
        Assert.NotEmpty(users);
    }

    [Fact]
    public async Task UserService_GetUserById_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.UserService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var user = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal(1, user.GetProperty("id").GetInt32());
        Assert.True(user.TryGetProperty("username", out _));
        Assert.True(user.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task UserService_GetUserById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.UserService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/999", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProductService_GetAllProducts_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(content);

        var products = JsonSerializer.Deserialize<JsonElement[]>(content);
        Assert.NotNull(products);
        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task ProductService_GetProductById_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products/1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var product = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal(1, product.GetProperty("id").GetInt32());
        Assert.True(product.TryGetProperty("name", out _));
        Assert.True(product.TryGetProperty("price", out _));
    }

    [Fact]
    public async Task ProductService_GetProductById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products/999", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProductService_GetProductsByCategory_ReturnsFilteredResults()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products?category=Electronics", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var products = JsonSerializer.Deserialize<JsonElement[]>(content);

        Assert.NotNull(products);
        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.Equal("Electronics", p.GetProperty("category").GetString()));
    }

    [Fact]
    public async Task UserService_CreateUser_ReturnsCreated()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.UserService.Program>();
        using var client = factory.CreateClient();

        var newUser = new
        {
            username = "testuser",
            email = "test@example.com",
            fullName = "Test User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", newUser, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var user = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("testuser", user.GetProperty("username").GetString());
        Assert.Equal("test@example.com", user.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ProductService_CreateProduct_ReturnsCreated()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        var newProduct = new
        {
            name = "Test Product",
            description = "A test product",
            price = 99.99m,
            category = "Test",
            stockQuantity = 10
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/products", newProduct, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var product = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("Test Product", product.GetProperty("name").GetString());
        Assert.Equal(99.99m, product.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task UserService_DeleteUser_ReturnsNoContent()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.UserService.Program>();
        using var client = factory.CreateClient();

        // First, create a user to delete
        var newUser = new { username = "deleteuser", email = "delete@example.com", fullName = "Delete User" };
        var createResponse = await client.PostAsJsonAsync("/api/users", newUser, TestContext.Current.CancellationToken);
        var content = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var user = JsonSerializer.Deserialize<JsonElement>(content);
        var userId = user.GetProperty("id").GetInt32();

        // Act
        var deleteResponse = await client.DeleteAsync($"/api/users/{userId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify user is deleted
        var getResponse = await client.GetAsync($"/api/users/{userId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task ProductService_UpdateProduct_ReturnsOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        var updateRequest = new
        {
            name = "Updated Product",
            description = "Updated description",
            price = 149.99m,
            category = "Updated",
            stockQuantity = 20
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/products/1", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var product = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("Updated Product", product.GetProperty("name").GetString());
        Assert.Equal(149.99m, product.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task ProductService_UpdateProduct_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Samples.ProductService.Program>();
        using var client = factory.CreateClient();

        var updateRequest = new
        {
            name = "Updated Product",
            description = "Updated description",
            price = 149.99m,
            category = "Updated",
            stockQuantity = 20
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/products/999", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
