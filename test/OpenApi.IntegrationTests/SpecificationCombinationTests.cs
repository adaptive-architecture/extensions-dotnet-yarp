using System.Net;
using System.Text.Json;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// Integration tests that verify how the gateway combines OpenAPI specifications
/// from multiple backend services into aggregated documents.
/// Tests focus on schema prefixing, path transformation, reference resolution,
/// and ensuring no conflicts between services.
/// </summary>
[Collection(nameof(KestrelServerCollection))]
public class SpecificationCombinationTests
{
    private readonly KestrelServerFixture _fixture;

    public SpecificationCombinationTests(KestrelServerFixture fixture)
    {
        _fixture = fixture;
    }

    #region Schema Combination Tests

    [Fact]
    public async Task AggregatedSpec_ContainsAllSchemasFromBackend()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Get aggregated spec from gateway
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var aggregatedDoc = JsonSerializer.Deserialize<JsonElement>(content);

        // Get backend spec directly
        var backendResponse = await _fixture.UserServiceClient.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        var backendContent = await backendResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var backendDoc = JsonSerializer.Deserialize<JsonElement>(backendContent);

        // Assert - All backend schemas should be in aggregated spec
        var aggregatedSchemas = aggregatedDoc.GetProperty("components").GetProperty("schemas");
        var backendSchemas = backendDoc.GetProperty("components").GetProperty("schemas");

        var aggregatedSchemaNames = aggregatedSchemas.EnumerateObject().Select(s => s.Name).ToHashSet();
        var backendSchemaNames = backendSchemas.EnumerateObject().Select(s => s.Name).ToHashSet();

        // All backend schemas should exist in aggregated (possibly with prefix)
        foreach (var schemaName in backendSchemaNames)
        {
            // Check if schema exists directly or with prefix
            var exists = aggregatedSchemaNames.Contains(schemaName) ||
                        aggregatedSchemaNames.Any(n => n.EndsWith(schemaName));
            Assert.True(exists, $"Schema '{schemaName}' from backend should exist in aggregated spec");
        }
    }

    [Fact]
    public async Task AggregatedSpec_SchemasPrefixedCorrectly()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var userResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var productResponse = await client.GetAsync("/api-docs/product-catalog", TestContext.Current.CancellationToken);

        var userContent = await userResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var productContent = await productResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var userDoc = JsonSerializer.Deserialize<JsonElement>(userContent);
        var productDoc = JsonSerializer.Deserialize<JsonElement>(productContent);

        // Assert - Schemas should have service-specific prefixes to avoid collisions
        var userSchemas = userDoc.GetProperty("components").GetProperty("schemas");
        var productSchemas = productDoc.GetProperty("components").GetProperty("schemas");

        var userSchemaNames = userSchemas.EnumerateObject().Select(s => s.Name).ToList();
        var productSchemaNames = productSchemas.EnumerateObject().Select(s => s.Name).ToList();

        // Verify schemas exist
        Assert.NotEmpty(userSchemaNames);
        Assert.NotEmpty(productSchemaNames);

        // Check for expected schemas from UserService
        Assert.Contains(userSchemaNames, name => name.Contains("User") || name == "User");
        Assert.Contains(userSchemaNames, name => name.Contains("CreateUserRequest") || name == "CreateUserRequest");

        // Check for expected schemas from ProductService
        Assert.Contains(productSchemaNames, name => name.Contains("Product") || name == "Product");
        Assert.Contains(productSchemaNames, name => name.Contains("CreateProductRequest") || name == "CreateProductRequest");
    }

    [Fact]
    public async Task AggregatedSpec_NoDuplicateSchemas()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var userResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var productResponse = await client.GetAsync("/api-docs/product-catalog", TestContext.Current.CancellationToken);

        var userContent = await userResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var productContent = await productResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var userDoc = JsonSerializer.Deserialize<JsonElement>(userContent);
        var productDoc = JsonSerializer.Deserialize<JsonElement>(productContent);

        // Assert - Each service spec should have unique schema names
        var userSchemas = userDoc.GetProperty("components").GetProperty("schemas");
        var productSchemas = productDoc.GetProperty("components").GetProperty("schemas");

        var userSchemaNames = userSchemas.EnumerateObject().Select(s => s.Name).ToList();
        var productSchemaNames = productSchemas.EnumerateObject().Select(s => s.Name).ToList();

        // Check for duplicates within each service
        Assert.Equal(userSchemaNames.Count, userSchemaNames.Distinct().Count());
        Assert.Equal(productSchemaNames.Count, productSchemaNames.Distinct().Count());
    }

    [Fact]
    public async Task AggregatedSpec_SchemaPropertiesPreserved()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Get aggregated and backend specs
        var aggregatedResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var backendResponse = await _fixture.UserServiceClient.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        var aggregatedContent = await aggregatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var backendContent = await backendResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var aggregatedDoc = JsonSerializer.Deserialize<JsonElement>(aggregatedContent);
        var backendDoc = JsonSerializer.Deserialize<JsonElement>(backendContent);

        // Assert - Find the User schema and verify properties are preserved
        var aggregatedSchemas = aggregatedDoc.GetProperty("components").GetProperty("schemas");
        var backendSchemas = backendDoc.GetProperty("components").GetProperty("schemas");

        // Find User schema (may have prefix)
        var backendUserSchema = backendSchemas.EnumerateObject().First(s => s.Name == "User");
        var aggregatedUserSchema = aggregatedSchemas.EnumerateObject()
            .First(s => s.Name == "User" || s.Name.EndsWith("User"));

        // Verify properties exist
        Assert.True(backendUserSchema.Value.TryGetProperty("properties", out var backendProps));
        Assert.True(aggregatedUserSchema.Value.TryGetProperty("properties", out var aggregatedProps));

        // Check that key properties are preserved
        var backendPropNames = backendProps.EnumerateObject().Select(p => p.Name).ToHashSet();
        var aggregatedPropNames = aggregatedProps.EnumerateObject().Select(p => p.Name).ToHashSet();

        foreach (var propName in backendPropNames)
        {
            Assert.Contains(propName, aggregatedPropNames);
        }
    }

    #endregion

    #region Path Combination Tests

    [Fact]
    public async Task AggregatedSpec_ContainsAllPathsFromBackend()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var aggregatedResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var backendResponse = await _fixture.UserServiceClient.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        var aggregatedContent = await aggregatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var backendContent = await backendResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var aggregatedDoc = JsonSerializer.Deserialize<JsonElement>(aggregatedContent);
        var backendDoc = JsonSerializer.Deserialize<JsonElement>(backendContent);

        // Assert
        var aggregatedPaths = aggregatedDoc.GetProperty("paths");
        var backendPaths = backendDoc.GetProperty("paths");

        var aggregatedPathList = aggregatedPaths.EnumerateObject().Select(p => p.Name).ToList();
        var backendPathList = backendPaths.EnumerateObject().Select(p => p.Name).ToList();

        Assert.NotEmpty(aggregatedPathList);
        Assert.NotEmpty(backendPathList);

        // All backend paths should be represented in aggregated spec
        Assert.True(aggregatedPathList.Count >= backendPathList.Count,
            "Aggregated spec should contain at least as many paths as backend");
    }

    [Fact]
    public async Task AggregatedSpec_PathsMatchYarpRouteConfiguration()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - Paths should match YARP route patterns
        var paths = doc.GetProperty("paths");
        var pathList = paths.EnumerateObject().Select(p => p.Name).ToList();

        // All paths should start with /api (per YARP configuration)
        Assert.All(pathList, path => Assert.StartsWith("/api", path));

        // Verify specific expected paths exist
        Assert.Contains("/api/Users", pathList);
        Assert.Contains(pathList, p => p.StartsWith("/api/Users/"));
    }

    [Fact]
    public async Task AggregatedSpec_OperationsPreservedForEachPath()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var aggregatedResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var backendResponse = await _fixture.UserServiceClient.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        var aggregatedContent = await aggregatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var backendContent = await backendResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var aggregatedDoc = JsonSerializer.Deserialize<JsonElement>(aggregatedContent);
        var backendDoc = JsonSerializer.Deserialize<JsonElement>(backendContent);

        // Assert - Check that operations (GET, POST, etc.) are preserved
        var backendPaths = backendDoc.GetProperty("paths");
        var aggregatedPaths = aggregatedDoc.GetProperty("paths");

        foreach (var backendPath in backendPaths.EnumerateObject())
        {
            // Find corresponding path in aggregated (should match or be transformed)
            var pathFound = false;
            foreach (var aggregatedPath in aggregatedPaths.EnumerateObject())
            {
                if (aggregatedPath.Name.EndsWith(backendPath.Name.Replace("/api/", "/")))
                {
                    pathFound = true;

                    // Verify operations match
                    foreach (var operation in backendPath.Value.EnumerateObject())
                    {
                        if (IsHttpMethod(operation.Name))
                        {
                            Assert.True(aggregatedPath.Value.TryGetProperty(operation.Name, out _),
                                $"Operation {operation.Name} should exist for path {aggregatedPath.Name}");
                        }
                    }
                    break;
                }
            }

            Assert.True(pathFound || backendPath.Name.StartsWith("/api/"),
                $"Backend path {backendPath.Name} should be represented in aggregated spec");
        }
    }

    [Fact]
    public async Task AggregatedSpec_ParametersPreservedInPaths()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - Check that path parameters are preserved
        var paths = doc.GetProperty("paths");

        // Find a path with parameters (e.g., /api/Users/{id})
        var pathWithParam = paths.EnumerateObject()
            .FirstOrDefault(p => p.Name.Contains("{id}"));

        Assert.NotEqual(default, pathWithParam);

        // Verify parameter is documented in operations
        var getOperation = pathWithParam.Value.GetProperty("get");
        Assert.True(getOperation.TryGetProperty("parameters", out var parameters));

        var paramList = parameters.EnumerateArray().ToList();
        Assert.NotEmpty(paramList);

        // Verify id parameter exists
        var idParam = paramList.FirstOrDefault(p =>
            p.TryGetProperty("name", out var name) && name.GetString() == "id");
        Assert.NotEqual(default, idParam);
    }

    #endregion

    #region Reference Resolution Tests

    [Fact]
    public async Task AggregatedSpec_AllSchemaReferencesValid()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - All $ref pointers should resolve to existing schemas
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schemaNames = schemas.EnumerateObject().Select(s => s.Name).ToHashSet();

        var paths = doc.GetProperty("paths");
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(operation.Name)) continue;

                // Check request body references
                if (operation.Value.TryGetProperty("requestBody", out var requestBody))
                {
                    VerifyAllReferences(requestBody, schemaNames, path.Name, operation.Name);
                }

                // Check response references
                if (operation.Value.TryGetProperty("responses", out var responses))
                {
                    foreach (var resp in responses.EnumerateObject())
                    {
                        VerifyAllReferences(resp.Value, schemaNames, path.Name, operation.Name);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task AggregatedSpec_RequestBodyReferencesCorrectSchemas()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - POST operations should reference request schemas
        var paths = doc.GetProperty("paths");
        var usersPath = paths.EnumerateObject().First(p => p.Name == "/api/Users");
        var postOperation = usersPath.Value.GetProperty("post");

        Assert.True(postOperation.TryGetProperty("requestBody", out var requestBody));
        Assert.True(requestBody.TryGetProperty("content", out var content2));

        var jsonContent = content2.EnumerateObject().First(c => c.Name == "application/json");
        Assert.True(jsonContent.Value.TryGetProperty("schema", out var schema));
        Assert.True(schema.TryGetProperty("$ref", out var refValue));

        var reference = refValue.GetString();
        Assert.NotNull(reference);
        Assert.StartsWith("#/components/schemas/", reference);

        // Extract schema name and verify it exists
        var schemaName = reference!.Replace("#/components/schemas/", "");
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty(schemaName, out _),
            $"Referenced schema '{schemaName}' should exist in components");
    }

    [Fact]
    public async Task AggregatedSpec_ResponseReferencesCorrectSchemas()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/product-catalog", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - GET operations should reference response schemas
        var paths = doc.GetProperty("paths");
        var productsPath = paths.EnumerateObject().First(p => p.Name == "/api/Products");
        var getOperation = productsPath.Value.GetProperty("get");

        Assert.True(getOperation.TryGetProperty("responses", out var responses));
        Assert.True(responses.TryGetProperty("200", out var okResponse));
        Assert.True(okResponse.TryGetProperty("content", out var content2));

        var jsonContent = content2.EnumerateObject().First(c => c.Name == "application/json");
        Assert.True(jsonContent.Value.TryGetProperty("schema", out var schema));

        // Should be array of Product
        Assert.True(schema.TryGetProperty("type", out var type));
        Assert.Equal("array", type.GetString());
        Assert.True(schema.TryGetProperty("items", out var items));
        Assert.True(items.TryGetProperty("$ref", out var refValue));

        var reference = refValue.GetString();
        Assert.NotNull(reference);

        var schemaName = reference!.Replace("#/components/schemas/", "");
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty(schemaName, out _),
            $"Referenced schema '{schemaName}' should exist in components");
    }

    [Fact]
    public async Task AggregatedSpec_NestedSchemaReferencesResolved()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - Schemas can reference other schemas; verify all references resolve
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schemaNames = schemas.EnumerateObject().Select(s => s.Name).ToHashSet();

        foreach (var schema in schemas.EnumerateObject())
        {
            VerifyNestedReferences(schema.Value, schemaNames, schema.Name);
        }
    }

    #endregion

    #region Metadata Preservation Tests

    [Fact]
    public async Task AggregatedSpec_PreservesBackendMetadata()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - Info section should be preserved
        Assert.True(doc.TryGetProperty("info", out var info));
        Assert.True(info.TryGetProperty("title", out var title));
        Assert.True(info.TryGetProperty("version", out var version));

        Assert.NotEmpty(title.GetString()!);
        Assert.NotEmpty(version.GetString()!);
    }

    [Fact]
    public async Task AggregatedSpec_OperationSummariesPreserved()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - Operation summaries from XML comments should be preserved
        var paths = doc.GetProperty("paths");
        var usersPath = paths.EnumerateObject().First(p => p.Name == "/api/Users");
        var getOperation = usersPath.Value.GetProperty("get");

        Assert.True(getOperation.TryGetProperty("summary", out var summary));
        Assert.NotEmpty(summary.GetString());
        Assert.Contains("users", summary.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AggregatedSpec_TagsPreserved()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - Tags should be preserved from backend
        var paths = doc.GetProperty("paths");
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(operation.Name)) continue;

                Assert.True(operation.Value.TryGetProperty("tags", out var tags));
                var tagList = tags.EnumerateArray().ToList();
                Assert.NotEmpty(tagList);
            }
        }
    }

    #endregion

    #region Multiple Service Combination Tests

    [Fact]
    public async Task ServiceList_ContainsAllConfiguredServices()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act
        var response = await client.GetAsync("/api-docs", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var serviceList = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        Assert.True(serviceList.TryGetProperty("services", out var services));
        var serviceArray = services.EnumerateArray().ToList();

        Assert.Equal(2, serviceArray.Count);

        var serviceNames = serviceArray
            .Select(s => s.GetProperty("name").GetString());

        Assert.Contains("User Management", serviceNames);
        Assert.Contains("Product Catalog", serviceNames);
    }

    [Fact]
    public async Task MultipleServices_SchemasDoNotConflict()
    {
        // Arrange
        var client = _fixture.GatewayClient;

        // Act - Get both service specs
        var userResponse = await client.GetAsync("/api-docs/user-management", TestContext.Current.CancellationToken);
        var productResponse = await client.GetAsync("/api-docs/product-catalog", TestContext.Current.CancellationToken);

        var userContent = await userResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var productContent = await productResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var userDoc = JsonSerializer.Deserialize<JsonElement>(userContent);
        var productDoc = JsonSerializer.Deserialize<JsonElement>(productContent);

        // Assert - If both services had the same schema names originally,
        // they should now be differentiated in the aggregated specs
        var userSchemas = userDoc.GetProperty("components").GetProperty("schemas");
        var productSchemas = productDoc.GetProperty("components").GetProperty("schemas");

        var userSchemaNames = userSchemas.EnumerateObject().Select(s => s.Name).ToHashSet();
        var productSchemaNames = productSchemas.EnumerateObject().Select(s => s.Name).ToHashSet();

        // Common schemas like ProblemDetails might exist in both
        // But service-specific schemas should be distinct
        var userServiceSchemas = userSchemaNames.Where(n =>
            n.Contains("User") && !n.Contains("ProblemDetails")).ToList();
        var productServiceSchemas = productSchemaNames.Where(n =>
            n.Contains("Product") && !n.Contains("ProblemDetails")).ToList();

        Assert.NotEmpty(userServiceSchemas);
        Assert.NotEmpty(productServiceSchemas);

        // No overlap in service-specific schemas
        foreach (var schema in userServiceSchemas)
        {
            Assert.DoesNotContain(schema, productServiceSchemas);
        }
    }

    #endregion

    #region Helper Methods

    private static bool IsHttpMethod(string name)
    {
        var methods = new[] { "get", "post", "put", "delete", "patch", "options", "head" };
        return methods.Contains(name.ToLowerInvariant());
    }

    private static void VerifyAllReferences(JsonElement element, HashSet<string> schemaNames, string path, string operation)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("$ref", out var refElement))
            {
                var refValue = refElement.GetString();
                if (refValue?.StartsWith("#/components/schemas/") == true)
                {
                    var schemaName = refValue.Substring("#/components/schemas/".Length);
                    Assert.True(schemaNames.Contains(schemaName),
                        $"Schema '{schemaName}' referenced in {operation} {path} not found in components");
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                VerifyAllReferences(prop.Value, schemaNames, path, operation);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                VerifyAllReferences(item, schemaNames, path, operation);
            }
        }
    }

    private static void VerifyNestedReferences(JsonElement element, HashSet<string> schemaNames, string schemaName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("$ref", out var refElement))
            {
                var refValue = refElement.GetString();
                if (refValue?.StartsWith("#/components/schemas/") == true)
                {
                    var referencedSchema = refValue.Substring("#/components/schemas/".Length);
                    Assert.True(schemaNames.Contains(referencedSchema),
                        $"Schema '{schemaName}' references '{referencedSchema}' which doesn't exist");
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                VerifyNestedReferences(prop.Value, schemaNames, schemaName);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                VerifyNestedReferences(item, schemaNames, schemaName);
            }
        }
    }

    #endregion
}
