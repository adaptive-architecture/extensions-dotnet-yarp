namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Pruning;

using System.Collections.Generic;
using System.Net.Http;
using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Pruning;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using NSubstitute;
using Xunit;
using RouteTransformAnalysis = OpenApi.Transforms.RouteTransformAnalysis;
using TransformType = OpenApi.Transforms.TransformType;

public class OpenApiDocumentPrunerTests
{
    private readonly ILogger<OpenApiDocumentPruner> _logger;
    private readonly OpenApiDocumentPruner _pruner;

    public OpenApiDocumentPrunerTests()
    {
        _logger = Substitute.For<ILogger<OpenApiDocumentPruner>>();
        _pruner = new OpenApiDocumentPruner(_logger);
    }

    [Fact]
    public void PruneDocument_WithNullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var reachabilityResult = CreateEmptyReachabilityResult();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _pruner.PruneDocument(null, reachabilityResult));
    }

    [Fact]
    public void PruneDocument_WithNullReachabilityResult_ThrowsArgumentNullException()
    {
        // Arrange
        var document = new OpenApiDocument();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _pruner.PruneDocument(document, null));
    }

    [Fact]
    public void PruneDocument_WithNoReachablePaths_ReturnsEmptyPaths()
    {
        // Arrange
        var document = CreateDocumentWithPaths("/users", "/posts");
        var reachabilityResult = CreateEmptyReachabilityResult();

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Empty(pruned.Paths);
    }

    [Fact]
    public void PruneDocument_WithReachablePath_IncludesInPrunedDocument()
    {
        // Arrange
        var document = CreateDocumentWithPaths("/users", "/posts");
        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = CreateReachablePathInfo("/users", "/api/users", HttpMethod.Get)
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Single(pruned.Paths);
        Assert.True(pruned.Paths.ContainsKey("/api/users"));
        Assert.True(pruned.Paths["/api/users"].Operations.ContainsKey(HttpMethod.Get));
    }

    [Fact]
    public void PruneDocument_WithMultipleReachablePaths_IncludesAll()
    {
        // Arrange
        var document = CreateDocumentWithPaths("/users", "/posts", "/admin");
        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = CreateReachablePathInfo("/users", "/api/users", HttpMethod.Get),
                ["/api/posts"] = CreateReachablePathInfo("/posts", "/api/posts", HttpMethod.Get)
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Equal(2, pruned.Paths.Count);
        Assert.True(pruned.Paths.ContainsKey("/api/users"));
        Assert.True(pruned.Paths.ContainsKey("/api/posts"));
        Assert.False(pruned.Paths.ContainsKey("/admin"));
    }

    [Fact]
    public void PruneDocument_WithMultipleOperations_IncludesAll()
    {
        // Arrange
        var document = CreateDocumentWithPath("/users", HttpMethod.Get, HttpMethod.Post, HttpMethod.Put);
        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        var prunedPath = pruned.Paths["/api/users"];
        Assert.Equal(3, prunedPath.Operations.Count);
        Assert.True(prunedPath.Operations.ContainsKey(HttpMethod.Get));
        Assert.True(prunedPath.Operations.ContainsKey(HttpMethod.Post));
        Assert.True(prunedPath.Operations.ContainsKey(HttpMethod.Put));
    }

    [Fact]
    public void PruneDocument_WithSchemaReferences_IncludesReferencedSchemas()
    {
        // Arrange
        var userSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };

        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            OperationId = "getUsers",
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Description = "Success",
                                    Content = new Dictionary<string, IOpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = new OpenApiSchemaReference("User", null, null)
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = userSchema,
                    ["Post"] = new OpenApiSchema { Type = JsonSchemaType.Object } // Unused
                }
            }
        };

        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Single(pruned.Components.Schemas);
        Assert.True(pruned.Components.Schemas.ContainsKey("User"));
        Assert.False(pruned.Components.Schemas.ContainsKey("Post"));
    }

    [Fact]
    public void PruneDocument_WithNestedSchemaReferences_IncludesAllDependencies()
    {
        // Arrange
        var addressSchema = new OpenApiSchema { Type = JsonSchemaType.Object };
        var userSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["address"] = new OpenApiSchemaReference("Address", null, null)
            }
        };

        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/users"] = CreatePathItemWithSchemaResponse("User")
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = userSchema,
                    ["Address"] = addressSchema,
                    ["Post"] = new OpenApiSchema { Type = JsonSchemaType.Object } // Unused
                }
            }
        };

        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Equal(2, pruned.Components.Schemas.Count);
        Assert.True(pruned.Components.Schemas.ContainsKey("User"));
        Assert.True(pruned.Components.Schemas.ContainsKey("Address"));
        Assert.False(pruned.Components.Schemas.ContainsKey("Post"));
    }

    [Fact]
    public void PruneDocument_WithArraySchemas_IncludesItemSchemas()
    {
        // Arrange
        var userSchema = new OpenApiSchema { Type = JsonSchemaType.Object };
        var usersArraySchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchemaReference("User", null, null)
        };

        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Description = "Success",
                                    Content = new Dictionary<string, IOpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = usersArraySchema
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = userSchema
                }
            }
        };

        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Single(pruned.Components.Schemas);
        Assert.True(pruned.Components.Schemas.ContainsKey("User"));
    }

    [Fact]
    public void PruneDocument_WithRequestBodySchema_IncludesSchema()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new OpenApiOperation
                        {
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, IOpenApiMediaType>
                                {
                                    ["application/json"] = new OpenApiMediaType
                                    {
                                        Schema = new OpenApiSchemaReference("CreateUserRequest", null, null)
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["CreateUserRequest"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Single(pruned.Components.Schemas);
        Assert.True(pruned.Components.Schemas.ContainsKey("CreateUserRequest"));
    }

    [Fact]
    public void PruneDocument_WithTags_IncludesUsedTagsOnly()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                ["/users"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            Tags = new HashSet<OpenApiTagReference>
                            {
                                new("Users")
                            }
                        }
                    }
                },
                ["/posts"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            Tags = new HashSet<OpenApiTagReference>
                            {
                                new("Posts")
                            }
                        }
                    }
                }
            },
            Tags = new HashSet<OpenApiTag>
            {
                new() { Name = "Users", Description = "User operations" },
                new() { Name = "Posts", Description = "Post operations" }
            }
        };

        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Single(pruned.Tags);
        Assert.Equal("Users", pruned.Tags.First().Name);
    }

    [Fact]
    public void PruneDocument_PreservesDocumentMetadata()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Test API",
                Version = "1.0.0",
                Description = "Test API Description"
            },
            Servers =
            [
                new OpenApiServer { Url = "https://api.example.com" }
            ],
            ExternalDocs = new OpenApiExternalDocs
            {
                Url = new Uri("https://docs.example.com")
            },
            Paths = new OpenApiPaths
            {
                ["/users"] = CreatePathItemWithSchemaResponse("User")
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        var reachabilityResult = CreateReachabilityResult(
            reachablePaths: new Dictionary<string, ReachablePathInfo>
            {
                ["/api/users"] = new ReachablePathInfo
                {
                    BackendPath = "/users",
                    GatewayPath = "/api/users",
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>(document.Paths["/users"].Operations),
                    RouteId = "test-route",
                    TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
                }
            }
        );

        // Act
        var pruned = _pruner.PruneDocument(document, reachabilityResult);

        // Assert
        Assert.Equal("Test API", pruned.Info.Title);
        Assert.Equal("1.0.0", pruned.Info.Version);
        Assert.Equal("Test API Description", pruned.Info.Description);
        Assert.Single(pruned.Servers);
        Assert.Equal("https://api.example.com", pruned.Servers[0].Url);
        Assert.NotNull(pruned.ExternalDocs);
        Assert.Equal("https://docs.example.com/", pruned.ExternalDocs.Url.ToString());
    }

    // Helper methods
    private static PathReachabilityResult CreateEmptyReachabilityResult()
    {
        return new PathReachabilityResult
        {
            ReachablePaths = [],
            UnreachablePaths = [],
            Warnings = []
        };
    }

    private static PathReachabilityResult CreateReachabilityResult(
        Dictionary<string, ReachablePathInfo> reachablePaths = null)
    {
        return new PathReachabilityResult
        {
            ReachablePaths = reachablePaths ?? [],
            UnreachablePaths = [],
            Warnings = []
        };
    }

    private static ReachablePathInfo CreateReachablePathInfo(string backendPath, string gatewayPath, HttpMethod operationType)
    {
        return new ReachablePathInfo
        {
            BackendPath = backendPath,
            GatewayPath = gatewayPath,
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [operationType] = new OpenApiOperation { OperationId = $"{operationType}Operation" }
            },
            RouteId = "test-route",
            TransformAnalysis = new RouteTransformAnalysis { TransformType = TransformType.Direct }
        };
    }

    private static OpenApiDocument CreateDocumentWithPaths(params string[] paths)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = []
        };

        foreach (var path in paths)
        {
            document.Paths[path] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation { OperationId = $"get{path.Replace("/", "")}" }
                }
            };
        }

        return document;
    }

    private static OpenApiDocument CreateDocumentWithPath(string path, params HttpMethod[] operationTypes)
    {
        var operations = new Dictionary<HttpMethod, OpenApiOperation>();
        foreach (var opType in operationTypes)
        {
            operations[opType] = new OpenApiOperation { OperationId = $"{opType}{path.Replace("/", "")}" };
        }

        return new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = new OpenApiPaths
            {
                [path] = new OpenApiPathItem { Operations = operations }
            }
        };
    }

    private static OpenApiPathItem CreatePathItemWithSchemaResponse(string schemaName)
    {
        return new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse
                        {
                            Description = "Success",
                            Content = new Dictionary<string, IOpenApiMediaType>
                            {
                                ["application/json"] = new OpenApiMediaType
                                {
                                    Schema = new OpenApiSchemaReference(schemaName, null, null)
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
