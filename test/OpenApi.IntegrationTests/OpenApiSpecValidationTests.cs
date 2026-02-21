using System.Net;
using System.Text;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// Validates that aggregated OpenAPI specs are structurally valid (conform to OpenAPI 3.x)
/// and semantically correct (paths match YARP config, references resolve, no orphaned schemas).
/// </summary>
[Collection(nameof(KestrelServerCollection))]
public class OpenApiSpecValidationTests
{
    private readonly KestrelServerFixture _fixture;

    public OpenApiSpecValidationTests(KestrelServerFixture fixture)
    {
        _fixture = fixture;
    }

    #region Structural Validation

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task ParsedSpec_HasNoErrorsOrWarnings(string path)
    {
        var (document, diagnostic) = await FetchAndParseAsync(_fixture.GatewayClient, path);

        Assert.NotNull(document);
        Assert.Empty(diagnostic.Errors);
        Assert.Empty(diagnostic.Warnings);
    }

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task AllRefReferences_ResolveToExistingSchemas(string path)
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, path);
        Assert.NotNull(document);

        var schemaNames = document.Components?.Schemas?.Keys.ToHashSet() ?? [];

        foreach (var (pathKey, pathItem) in document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations)
            {
                if (operation.RequestBody?.Content != null)
                {
                    foreach (var (_, mediaType) in operation.RequestBody.Content)
                    {
                        AssertSchemaRefResolvable(mediaType.Schema, schemaNames, $"{method} {pathKey} requestBody");
                    }
                }

                foreach (var (statusCode, response) in operation.Responses)
                {
                    if (response.Content == null) continue;
                    foreach (var (_, mediaType) in response.Content)
                    {
                        AssertSchemaRefResolvable(mediaType.Schema, schemaNames, $"{method} {pathKey} response {statusCode}");
                    }
                }

                if (operation.Parameters != null)
                {
                    foreach (var param in operation.Parameters)
                    {
                        AssertSchemaRefResolvable(param.Schema, schemaNames, $"{method} {pathKey} parameter {param.Name}");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task NoDuplicateOperationIds(string path)
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, path);
        Assert.NotNull(document);

        var operationIds = new List<string>();

        foreach (var (_, pathItem) in document.Paths)
        {
            foreach (var (_, operation) in pathItem.Operations)
            {
                if (!String.IsNullOrEmpty(operation.OperationId))
                {
                    operationIds.Add(operation.OperationId);
                }
            }
        }

        var duplicates = operationIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task SpecHasValidOpenApiVersion(string path)
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, path);
        Assert.NotNull(document);

        Assert.NotNull(document.Info);
        Assert.NotEmpty(document.Info.Title);
        Assert.NotEmpty(document.Info.Version);
    }

    #endregion

    #region Semantic Validation (Aggregation-Specific)

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task AllPaths_StartWithApiPrefix(string path)
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, path);
        Assert.NotNull(document);

        Assert.All(document.Paths.Keys, p =>
            Assert.StartsWith("/api/", p, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UserManagement_HasExactExpectedPaths()
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, "/api-docs/user-management");
        Assert.NotNull(document);

        var paths = document.Paths.Keys.OrderBy(p => p).ToList();
        Assert.Equal(2, paths.Count);
        Assert.Contains("/api/Users", paths);
        Assert.Contains("/api/Users/{id}", paths);
    }

    [Fact]
    public async Task ProductCatalog_HasExactExpectedPaths()
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, "/api-docs/product-catalog");
        Assert.NotNull(document);

        var paths = document.Paths.Keys.OrderBy(p => p).ToList();
        Assert.Equal(2, paths.Count);
        Assert.Contains("/api/Products", paths);
        Assert.Contains("/api/Products/{id}", paths);
    }

    [Fact]
    public async Task UserManagement_SchemasArePrefixed()
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, "/api-docs/user-management");
        Assert.NotNull(document);

        var schemaNames = document.Components?.Schemas?.Keys.ToList() ?? [];
        Assert.NotEmpty(schemaNames);

        Assert.All(schemaNames, name =>
            Assert.StartsWith("UserService", name));
    }

    [Fact]
    public async Task ProductCatalog_SchemasArePrefixed()
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, "/api-docs/product-catalog");
        Assert.NotNull(document);

        var schemaNames = document.Components?.Schemas?.Keys.ToList() ?? [];
        Assert.NotEmpty(schemaNames);

        Assert.All(schemaNames, name =>
            Assert.StartsWith("ProductService", name));
    }

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task AllPrefixedRefPointers_ResolveToExistingSchemas(string path)
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, path);
        Assert.NotNull(document);

        var schemaNames = document.Components?.Schemas?.Keys.ToHashSet() ?? [];
        var referencedSchemas = CollectAllRefSchemaNames(document);

        foreach (var refName in referencedSchemas)
        {
            Assert.True(schemaNames.Contains(refName),
                $"$ref '#/components/schemas/{refName}' does not resolve to an existing schema");
        }
    }

    [Theory]
    [InlineData("/api-docs/user-management")]
    [InlineData("/api-docs/product-catalog")]
    public async Task NoOrphanedSchemas(string path)
    {
        var (document, _) = await FetchAndParseAsync(_fixture.GatewayClient, path);
        Assert.NotNull(document);

        var declaredSchemas = document.Components?.Schemas?.Keys.ToHashSet() ?? [];
        var referencedSchemas = CollectAllRefSchemaNames(document);

        // Also include schemas referenced from within other schemas (nested refs)
        if (document.Components?.Schemas != null)
        {
            foreach (var (_, schema) in document.Components.Schemas)
            {
                CollectSchemaRefs(schema, referencedSchemas);
            }
        }

        var orphaned = declaredSchemas.Except(referencedSchemas).ToList();
        Assert.Empty(orphaned);
    }

    #endregion

    #region Helpers

    private static async Task<(OpenApiDocument, OpenApiDiagnostic)> FetchAndParseAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var (document, diagnostic) = await OpenApiDocument.LoadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        return (document, diagnostic);
    }

    private static void AssertSchemaRefResolvable(IOpenApiSchema schema, HashSet<string> schemaNames, string context)
    {
        if (schema == null) return;

        if (schema is OpenApiSchemaReference schemaRef && schemaRef.Reference != null)
        {
            var refId = schemaRef.Reference.Id;
            if (!String.IsNullOrEmpty(refId))
            {
                Assert.True(schemaNames.Contains(refId),
                    $"Schema ref '{refId}' at {context} not found in components/schemas");
            }
        }

        if (schema is OpenApiSchema concreteSchema)
        {
            // Recurse into items (arrays)
            if (concreteSchema.Items != null)
                AssertSchemaRefResolvable(concreteSchema.Items, schemaNames, context);

            // Recurse into composed schemas
            if (concreteSchema.AllOf != null)
                foreach (var s in concreteSchema.AllOf) AssertSchemaRefResolvable(s, schemaNames, context);
            if (concreteSchema.OneOf != null)
                foreach (var s in concreteSchema.OneOf) AssertSchemaRefResolvable(s, schemaNames, context);
            if (concreteSchema.AnyOf != null)
                foreach (var s in concreteSchema.AnyOf) AssertSchemaRefResolvable(s, schemaNames, context);
        }
    }

    private static HashSet<string> CollectAllRefSchemaNames(OpenApiDocument document)
    {
        var refs = new HashSet<string>();

        foreach (var (_, pathItem) in document.Paths)
        {
            foreach (var (_, operation) in pathItem.Operations)
            {
                if (operation.RequestBody?.Content != null)
                {
                    foreach (var (_, mediaType) in operation.RequestBody.Content)
                    {
                        CollectSchemaRefs(mediaType.Schema, refs);
                    }
                }

                foreach (var (_, response) in operation.Responses)
                {
                    if (response.Content == null) continue;
                    foreach (var (_, mediaType) in response.Content)
                    {
                        CollectSchemaRefs(mediaType.Schema, refs);
                    }
                }

                if (operation.Parameters != null)
                {
                    foreach (var param in operation.Parameters)
                    {
                        CollectSchemaRefs(param.Schema, refs);
                    }
                }
            }
        }

        return refs;
    }

    private static void CollectSchemaRefs(IOpenApiSchema schema, HashSet<string> refs)
    {
        if (schema == null) return;

        if (schema is OpenApiSchemaReference schemaRef && schemaRef.Reference != null)
        {
            var refId = schemaRef.Reference.Id;
            if (!String.IsNullOrEmpty(refId))
            {
                refs.Add(refId);
            }
        }

        if (schema is OpenApiSchema concreteSchema)
        {
            if (concreteSchema.Items != null)
                CollectSchemaRefs(concreteSchema.Items, refs);

            if (concreteSchema.AllOf != null)
                foreach (var s in concreteSchema.AllOf) CollectSchemaRefs(s, refs);
            if (concreteSchema.OneOf != null)
                foreach (var s in concreteSchema.OneOf) CollectSchemaRefs(s, refs);
            if (concreteSchema.AnyOf != null)
                foreach (var s in concreteSchema.AnyOf) CollectSchemaRefs(s, refs);
            if (concreteSchema.Properties != null)
                foreach (var (_, s) in concreteSchema.Properties) CollectSchemaRefs(s, refs);
            if (concreteSchema.AdditionalProperties != null)
                CollectSchemaRefs(concreteSchema.AdditionalProperties, refs);
        }
    }

    #endregion
}
