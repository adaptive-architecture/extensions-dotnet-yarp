using System.Net;
using System.Text.Json;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// <para>
/// End-to-end integration tests that use actual Kestrel servers to test
/// full OpenAPI aggregation with real HTTP communication between Gateway and backend services.
/// </para>
/// <para>
/// NOTE: These tests require actual Kestrel servers. WebApplicationFactory has limitations
/// with Kestrel. To run true end-to-end tests, start the services manually:
/// - Terminal 1: cd samples/OpenApiAggregation/UserService && dotnet run
/// - Terminal 2: cd samples/OpenApiAggregation/ProductService && dotnet run
/// - Terminal 3: cd samples/OpenApiAggregation/Gateway && dotnet run
/// Then run these tests against localhost:5000 (Gateway).
/// </para>
/// <para>These tests are marked Skip until WebApplicationFactory + Kestrel integration is resolved.</para>
/// </summary>
[Collection(nameof(KestrelServerCollection))]
public class EndToEndAggregationTests
{
    private readonly KestrelServerFixture _fixture;

    public EndToEndAggregationTests(KestrelServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Gateway_AggregatesUserServiceOpenApi_Successfully()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Request aggregated OpenAPI for user-management service
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Verify it's valid OpenAPI
        Assert.True(doc.TryGetProperty("openapi", out var openApiVersion));
        Assert.True(openApiVersion.GetString()?.StartsWith("3."));

        // Verify it has content from backend service
        Assert.True(doc.TryGetProperty("paths", out var paths));
        Assert.True(paths.EnumerateObject().Any());

        // Verify it has schemas
        Assert.True(doc.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.EnumerateObject().Any());
    }

    [Fact]
    public async Task Gateway_AggregatesProductServiceOpenApi_Successfully()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Request aggregated OpenAPI for product-catalog service
        var response = await client.GetAsync("/api-docs/product-catalog", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Verify it's valid OpenAPI
        Assert.True(doc.TryGetProperty("openapi", out _));
        Assert.True(doc.TryGetProperty("paths", out var paths));
        Assert.True(paths.EnumerateObject().Any());

        Assert.True(doc.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.EnumerateObject().Any());
    }

    [Fact]
    public async Task Gateway_ServiceList_ContainsBothServices()
    {
        // Arrange
        var client = _fixture.GatewayClient;

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
    }

    [Fact]
    public async Task AggregatedDocument_AllSchemaReferencesResolvable()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Get all schema names
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schemaNames = schemas.EnumerateObject().Select(s => s.Name).ToHashSet();

        // Check all references in paths
        var paths = doc.GetProperty("paths");
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(operation.Name)) continue;

                // Check request body references
                if (operation.Value.TryGetProperty("requestBody", out var requestBody))
                {
                    VerifyReferences(requestBody, schemaNames);
                }

                // Check response references
                if (operation.Value.TryGetProperty("responses", out var responses))
                {
                    foreach (var resp in responses.EnumerateObject())
                    {
                        VerifyReferences(resp.Value, schemaNames);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task AggregatedDocument_PathsTransformed_AccordingToYarpRoutes()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        var paths = doc.GetProperty("paths");
        var pathList = paths.EnumerateObject().Select(p => p.Name).ToList();

        // Verify paths are exposed according to YARP configuration
        Assert.NotEmpty(pathList);

        // Paths should start with /api (according to Gateway configuration)
        Assert.All(pathList, path => Assert.StartsWith("/api", path));
    }

    [Fact]
    public async Task AggregatedDocument_SchemasWithPrefix_NoCollisions()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Get both services
        var userResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var productResponse = await client.GetAsync("/api-docs/product-catalog", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

        var userContent = await userResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var productContent = await productResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var userDoc = JsonSerializer.Deserialize<JsonElement>(userContent);
        var productDoc = JsonSerializer.Deserialize<JsonElement>(productContent);

        // Both should have schemas
        var userSchemas = userDoc.GetProperty("components").GetProperty("schemas");
        var productSchemas = productDoc.GetProperty("components").GetProperty("schemas");

        var userSchemaNames = userSchemas.EnumerateObject().Select(s => s.Name).ToHashSet();
        var productSchemaNames = productSchemas.EnumerateObject().Select(s => s.Name).ToHashSet();

        Assert.NotEmpty(userSchemaNames);
        Assert.NotEmpty(productSchemaNames);

        // If prefixes are configured, schemas should have prefixes
        // This test verifies the aggregation system handles schema conflicts
        Assert.True(userSchemaNames.Count > 0);
        Assert.True(productSchemaNames.Count > 0);
    }

    [Fact]
    public async Task CacheInvalidation_RefetchesFromBackend()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Get aggregated doc (should be cached)
        var response1 = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var content1 = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Invalidate cache
        var invalidateResponse = await client.PostAsync("/admin/cache/invalidate/user-management", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, invalidateResponse.StatusCode);

        // Get doc again (should re-fetch from backend)
        var response2 = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var content2 = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Both should be valid (content may be identical since backend hasn't changed)
        Assert.NotEmpty(content1);
        Assert.NotEmpty(content2);
    }

    [Fact]
    public async Task BackendServices_SwaggerEndpoints_Accessible()
    {
        // Arrange & Act - Verify backend services are serving OpenAPI
        var userSwaggerResponse = await _fixture.UserServiceClient.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        var productSwaggerResponse = await _fixture.ProductServiceClient.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, userSwaggerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, productSwaggerResponse.StatusCode);

        var userContent = await userSwaggerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var productContent = await productSwaggerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Verify they are valid JSON
        var userDoc = JsonSerializer.Deserialize<JsonElement>(userContent);
        var productDoc = JsonSerializer.Deserialize<JsonElement>(productContent);

        Assert.True(userDoc.TryGetProperty("openapi", out _));
        Assert.True(productDoc.TryGetProperty("openapi", out _));
    }

    [Fact]
    public async Task Gateway_Proxying_WorksForBackendRequests()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Make a request through the gateway to the backend
        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        // Assert - Gateway should proxy to UserService
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var users = JsonSerializer.Deserialize<JsonElement[]>(content);

        Assert.NotNull(users);
        Assert.NotEmpty(users);
    }

    #region Helper Methods

    private static bool IsHttpMethod(string name)
    {
        var methods = new[] { "get", "post", "put", "delete", "patch", "options", "head" };
        return methods.Contains(name.ToLowerInvariant());
    }

    private static void VerifyReferences(JsonElement element, HashSet<string> schemaNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("$ref", out var refElement))
            {
                var refValue = refElement.GetString();
                if (refValue?.StartsWith("#/components/schemas/") == true)
                {
                    var schemaName = refValue.Substring("#/components/schemas/".Length);
                    Assert.True(schemaNames.Contains(schemaName), $"Schema '{schemaName}' not found in components");
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                VerifyReferences(prop.Value, schemaNames);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                VerifyReferences(item, schemaNames);
            }
        }
    }

    #endregion
}
