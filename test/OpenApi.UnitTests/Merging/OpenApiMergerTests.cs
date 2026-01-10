using AdaptArch.Extensions.Yarp.OpenApi.Merging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Merging;

public class OpenApiMergerTests
{
    private readonly ILogger<OpenApiMerger> _logger;
    private readonly OpenApiMerger _merger;

    public OpenApiMergerTests()
    {
        _logger = NullLogger<OpenApiMerger>.Instance;
        _merger = new OpenApiMerger(_logger);
    }

    [Fact]
    public void MergeDocuments_NullDocuments_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _merger.MergeDocuments(null, "Service"));
    }

    [Fact]
    public void MergeDocuments_EmptyDocuments_ThrowsArgumentException()
    {
        // Arrange
        var documents = new List<OpenApiDocument>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _merger.MergeDocuments(documents, "Service"));
        Assert.Contains("At least one document is required", exception.Message);
    }

    [Fact]
    public void MergeDocuments_NullServiceName_ThrowsArgumentException()
    {
        // Arrange
        var documents = new List<OpenApiDocument>
        {
            new() {
                Info = new OpenApiInfo { Title = "Test", Version = "1.0" }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _merger.MergeDocuments(documents, null));
        Assert.Contains("Service name cannot be null", exception.Message);
    }

    [Fact]
    public void MergeDocuments_EmptyServiceName_ThrowsArgumentException()
    {
        // Arrange
        var documents = new List<OpenApiDocument>
        {
            new() {
                Info = new OpenApiInfo { Title = "Test", Version = "1.0" }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _merger.MergeDocuments(documents, ""));
        Assert.Contains("Service name cannot be null", exception.Message);
    }

    [Fact]
    public void MergeDocuments_SingleDocument_ReturnsDocumentWithUpdatedInfo()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Original API",
                Version = "1.0",
                Description = "Original description"
            },
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "getUsers" }
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([document], "User Management");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("User Management", result.Info.Title);
        Assert.NotNull(result.Paths);
        Assert.True(result.Paths.ContainsKey("/users"));
    }

    [Fact]
    public void MergeDocuments_TwoDocuments_MergesPaths()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "getUsers" }
                    }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/products"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "getProducts" }
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Paths);
        Assert.Equal(2, result.Paths.Count);
        Assert.True(result.Paths.ContainsKey("/users"));
        Assert.True(result.Paths.ContainsKey("/products"));
    }

    [Fact]
    public void MergeDocuments_PathConflicts_MergesOperations()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/items"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "getItems" }
                    }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/items"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new OpenApiOperation { OperationId = "createItem" }
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Paths);
        Assert.Single(result.Paths);
        Assert.True(result.Paths.ContainsKey("/items"));

        var pathItem = result.Paths["/items"];
        Assert.Equal(2, pathItem.Operations.Count);
        Assert.True(pathItem.Operations.ContainsKey(HttpMethod.Get));
        Assert.True(pathItem.Operations.ContainsKey(HttpMethod.Post));
    }

    [Fact]
    public void MergeDocuments_OperationConflicts_KeepsFirstOperation()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/items"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            OperationId = "getItems_v1",
                            Summary = "First version"
                        }
                    }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/items"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            OperationId = "getItems_v2",
                            Summary = "Second version"
                        }
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        var operation = result.Paths["/items"].Operations[HttpMethod.Get];
        Assert.Equal("getItems_v1", operation.OperationId);
        Assert.Equal("First version", operation.Summary);
    }

    [Fact]
    public void MergeDocuments_ComponentSchemas_MergesSchemas()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Product"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Components);
        Assert.NotNull(result.Components.Schemas);
        Assert.Equal(2, result.Components.Schemas.Count);
        Assert.True(result.Components.Schemas.ContainsKey("User"));
        Assert.True(result.Components.Schemas.ContainsKey("Product"));
    }

    [Fact]
    public void MergeDocuments_SchemaConflicts_KeepsFirstSchema()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Item"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Description = "First definition"
                    }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Item"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Description = "Second definition"
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        var schema = result.Components.Schemas["Item"];
        Assert.Equal("First definition", schema.Description);
    }

    [Fact]
    public void MergeDocuments_Servers_DeduplicatesByUrl()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Servers =
            [
                new OpenApiServer { Url = "http://server1.com" },
                new OpenApiServer { Url = "http://server2.com" }
            ]
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Servers =
            [
                new OpenApiServer { Url = "http://server2.com" }, // Duplicate
                new OpenApiServer { Url = "http://server3.com" }
            ]
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Servers);
        Assert.Equal(3, result.Servers.Count);
        Assert.Contains(result.Servers, s => s.Url == "http://server1.com");
        Assert.Contains(result.Servers, s => s.Url == "http://server2.com");
        Assert.Contains(result.Servers, s => s.Url == "http://server3.com");
    }

    [Fact]
    public void MergeDocuments_Tags_DeduplicatesByName()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Tags = new HashSet<OpenApiTag>
            {
                new() { Name = "Users", Description = "User operations" },
                new() { Name = "Products" }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Tags = new HashSet<OpenApiTag>
            {
                new() { Name = "Products" }, // Duplicate
                new() { Name = "Orders" }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Tags);
        Assert.Equal(3, result.Tags.Count);
        Assert.Contains(result.Tags, t => t.Name == "Users");
        Assert.Contains(result.Tags, t => t.Name == "Products");
        Assert.Contains(result.Tags, t => t.Name == "Orders");
    }

    [Fact]
    public void Merge_SecurityRequirements_Merges()
    {
        // Arrange
        var secSchemeRef1 = new OpenApiSecuritySchemeReference("scheme1", null, null);
        var secSchemeRef2 = new OpenApiSecuritySchemeReference("scheme2", null, null);

        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Security =
            [
                new OpenApiSecurityRequirement
                {
                    [secSchemeRef1] = ["read"]
                }
            ]
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Security =
            [
                new OpenApiSecurityRequirement
                {
                    [secSchemeRef2] = ["write"]
                }
            ]
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Security);
        Assert.Equal(2, result.Security.Count);
    }

    [Fact]
    public void MergeDocuments_ComponentResponses_Merges()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, IOpenApiResponse>
                {
                    ["NotFound"] = new OpenApiResponse { Description = "Not found" }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Responses = new Dictionary<string, IOpenApiResponse>
                {
                    ["ServerError"] = new OpenApiResponse { Description = "Server error" }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Components.Responses);
        Assert.Equal(2, result.Components.Responses.Count);
        Assert.True(result.Components.Responses.ContainsKey("NotFound"));
        Assert.True(result.Components.Responses.ContainsKey("ServerError"));
    }

    [Fact]
    public void MergeDocuments_ComponentParameters_Merges()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Parameters = new Dictionary<string, IOpenApiParameter>
                {
                    ["PageNumber"] = new OpenApiParameter
                    {
                        Name = "page",
                        In = ParameterLocation.Query
                    }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Parameters = new Dictionary<string, IOpenApiParameter>
                {
                    ["PageSize"] = new OpenApiParameter
                    {
                        Name = "size",
                        In = ParameterLocation.Query
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.Components.Parameters);
        Assert.Equal(2, result.Components.Parameters.Count);
        Assert.True(result.Components.Parameters.ContainsKey("PageNumber"));
        Assert.True(result.Components.Parameters.ContainsKey("PageSize"));
    }

    [Fact]
    public void MergeDocuments_ExternalDocs_UsesFirstNonNull()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = [],
            ExternalDocs = new OpenApiExternalDocs
            {
                Url = new Uri("http://docs1.com"),
                Description = "First docs"
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = [],
            ExternalDocs = new OpenApiExternalDocs
            {
                Url = new Uri("http://docs2.com"),
                Description = "Second docs"
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.NotNull(result.ExternalDocs);
        Assert.Equal("http://docs1.com/", result.ExternalDocs.Url.ToString());
        Assert.Equal("First docs", result.ExternalDocs.Description);
    }

    [Fact]
    public void MergeDocuments_Info_CreatesAggregatedDescription()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "User API",
                Version = "1.0",
                Description = "Handles user management"
            },
            Paths = []
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Product API",
                Version = "2.0",
                Description = "Handles product catalog"
            },
            Paths = []
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2], "Combined Service");

        // Assert
        Assert.Equal("Combined Service", result.Info.Title);
        Assert.NotNull(result.Info.Description);
        Assert.Contains("Aggregated API for Combined Service", result.Info.Description);
        Assert.Contains("Combined from 2 service(s)", result.Info.Description);
    }

    [Fact]
    public void MergeDocuments_MultipleDocuments_MergesAll()
    {
        // Arrange
        var doc1 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 1", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/path1"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "op1" }
                    }
                }
            }
        };

        var doc2 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 2", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/path2"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "op2" }
                    }
                }
            }
        };

        var doc3 = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API 3", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/path3"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation { OperationId = "op3" }
                    }
                }
            }
        };

        // Act
        var result = _merger.MergeDocuments([doc1, doc2, doc3], "Combined Service");

        // Assert
        Assert.NotNull(result.Paths);
        Assert.Equal(3, result.Paths.Count);
        Assert.True(result.Paths.ContainsKey("/path1"));
        Assert.True(result.Paths.ContainsKey("/path2"));
        Assert.True(result.Paths.ContainsKey("/path3"));
    }
}
