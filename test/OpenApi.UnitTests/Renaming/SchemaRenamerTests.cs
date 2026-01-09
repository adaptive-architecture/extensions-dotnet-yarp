using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using NSubstitute;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Renaming;

public class SchemaRenamerTests
{
    private readonly ILogger<SchemaRenamer> _logger;
    private readonly SchemaRenamer _renamer;

    public SchemaRenamerTests()
    {
        _logger = Substitute.For<ILogger<SchemaRenamer>>();
        _renamer = new SchemaRenamer(_logger);
    }

    [Fact]
    public void ApplyPrefix_NullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _renamer.ApplyPrefix(null, "Prefix"));
    }

    [Fact]
    public void ApplyPrefix_EmptyPrefix_ReturnsDocumentUnchanged()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = []
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "");

        // Assert
        Assert.Same(document, result);
    }

    [Fact]
    public void ApplyPrefix_WhitespacePrefix_ReturnsDocumentUnchanged()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = []
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "   ");

        // Assert
        Assert.Same(document, result);
    }

    [Fact]
    public void ApplyPrefix_NoSchemas_ReturnsDocumentUnchanged()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents()
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "Prefix");

        // Assert
        Assert.Same(document, result);
    }

    [Fact]
    public void ApplyPrefix_SimpleSchema_RenamesSchemaCorrectly()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["id"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                            ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        Assert.NotNull(result.Components);
        Assert.NotNull(result.Components.Schemas);
        Assert.True(result.Components.Schemas.ContainsKey("UserServiceUser"));
        Assert.False(result.Components.Schemas.ContainsKey("User"));
        Assert.Equal(JsonSchemaType.Object, result.Components.Schemas["UserServiceUser"].Type);
    }

    [Fact]
    public void ApplyPrefix_SchemaWithReference_UpdatesReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Address"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["street"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    },
                    ["User"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            // In v3, use OpenApiSchemaReference instead of schema with Reference property
                            ["address"] = new OpenApiSchemaReference("Address", null, null)
                        }
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        Assert.NotNull(result.Components);
        Assert.NotNull(result.Components.Schemas);
        Assert.True(result.Components.Schemas.ContainsKey("UserServiceUser"));
        Assert.True(result.Components.Schemas.ContainsKey("UserServiceAddress"));

        var userSchema = (OpenApiSchema)result.Components.Schemas["UserServiceUser"];
        Assert.NotNull(userSchema.Properties);
        var addressProp = userSchema.Properties["address"];

        // In v3, check if it's an OpenApiSchemaReference and verify the reference ID
        Assert.True(addressProp is OpenApiSchemaReference);
        var addressRef = (OpenApiSchemaReference)addressProp;
        Assert.NotNull(addressRef.Reference);
        Assert.Equal("UserServiceAddress", addressRef.Reference.Id);
    }

    [Fact]
    public void ApplyPrefix_ArraySchema_UpdatesItemReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    },
                    ["UserArray"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchemaReference("User", null, null)
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "Api");

        // Assert
        var userArray = (OpenApiSchema)result.Components.Schemas["ApiUserArray"];
        Assert.NotNull(userArray.Items);
        var itemsRef = userArray.Items;
        Assert.True(itemsRef is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)itemsRef;
        Assert.NotNull(schemaRef.Reference);
        Assert.Equal("ApiUser", schemaRef.Reference.Id);
    }

    [Fact]
    public void ApplyPrefix_AllOfSchema_UpdatesReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["BaseUser"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["id"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
                        }
                    },
                    ["ExtendedUser"] = new OpenApiSchema
                    {
                        AllOf =
                        [
                            new OpenApiSchemaReference("BaseUser", null, null),
                            new OpenApiSchema
                            {
                                Type = JsonSchemaType.Object,
                                Properties = new Dictionary<string, IOpenApiSchema>
                                {
                                    ["email"] = new OpenApiSchema { Type = JsonSchemaType.String }
                                }
                            }
                        ]
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        var extendedUserSchema = (OpenApiSchema)result.Components.Schemas["UserServiceExtendedUser"];
        Assert.NotNull(extendedUserSchema.AllOf);
        Assert.Equal(2, extendedUserSchema.AllOf.Count);
        Assert.True(extendedUserSchema.AllOf[0] is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)extendedUserSchema.AllOf[0];
        Assert.NotNull(schemaRef.Reference);
        Assert.Equal("UserServiceBaseUser", schemaRef.Reference.Id);
    }

    [Fact]
    public void ApplyPrefix_OneOfSchema_UpdatesReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Cat"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["Dog"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["Pet"] = new OpenApiSchema
                    {
                        OneOf =
                        [
                            new OpenApiSchemaReference("Cat", null, null),
                            new OpenApiSchemaReference("Dog", null, null)
                        ]
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "PetSvc");

        // Assert
        var petSchema = (OpenApiSchema)result.Components.Schemas["PetSvcPet"];
        Assert.NotNull(petSchema.OneOf);
        Assert.Equal(2, petSchema.OneOf.Count);
        Assert.True(petSchema.OneOf[0] is OpenApiSchemaReference);
        Assert.True(petSchema.OneOf[1] is OpenApiSchemaReference);
        var catRef = (OpenApiSchemaReference)petSchema.OneOf[0];
        var dogRef = (OpenApiSchemaReference)petSchema.OneOf[1];
        Assert.Equal("PetSvcCat", catRef.Reference?.Id);
        Assert.Equal("PetSvcDog", dogRef.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_AnyOfSchema_UpdatesReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Type1"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["Type2"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["Combined"] = new OpenApiSchema
                    {
                        AnyOf =
                        [
                            new OpenApiSchemaReference("Type1", null, null),
                            new OpenApiSchemaReference("Type2", null, null)
                        ]
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "Svc");

        // Assert
        var combinedSchema = (OpenApiSchema)result.Components.Schemas["SvcCombined"];
        Assert.NotNull(combinedSchema.AnyOf);
        Assert.Equal(2, combinedSchema.AnyOf.Count);
        Assert.True(combinedSchema.AnyOf[0] is OpenApiSchemaReference);
        Assert.True(combinedSchema.AnyOf[1] is OpenApiSchemaReference);
        var type1Ref = (OpenApiSchemaReference)combinedSchema.AnyOf[0];
        var type2Ref = (OpenApiSchemaReference)combinedSchema.AnyOf[1];
        Assert.Equal("SvcType1", type1Ref.Reference?.Id);
        Assert.Equal("SvcType2", type2Ref.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_SchemaWithAdditionalProperties_UpdatesReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Value"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["Dictionary"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        AdditionalProperties = new OpenApiSchemaReference("Value", null, null)
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "DataSvc");

        // Assert
        var dictionarySchema = (OpenApiSchema)result.Components.Schemas["DataSvcDictionary"];
        Assert.NotNull(dictionarySchema.AdditionalProperties);
        Assert.True(dictionarySchema.AdditionalProperties is OpenApiSchemaReference);
        var valueRef = (OpenApiSchemaReference)dictionarySchema.AdditionalProperties;
        Assert.Equal("DataSvcValue", valueRef.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_OperationRequestBody_UpdatesReferences()
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
                            OperationId = "createUser",
                            RequestBody = new OpenApiRequestBody
                            {
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
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        var operation = result.Paths["/users"].Operations[HttpMethod.Post];
        var schema = operation.RequestBody.Content["application/json"].Schema;
        Assert.True(schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)schema;
        Assert.Equal("UserServiceUser", schemaRef.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_OperationResponse_UpdatesReferences()
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
                                            Schema = new OpenApiSchemaReference("UserList", null, null)
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
                    ["UserList"] = new OpenApiSchema { Type = JsonSchemaType.Array }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        var operation = result.Paths["/users"].Operations[HttpMethod.Get];
        var schema = operation.Responses["200"].Content["application/json"].Schema;
        Assert.True(schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)schema;
        Assert.Equal("UserServiceUserList", schemaRef.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_OperationParameter_UpdatesReferences()
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
                            OperationId = "getUsers",
                            Parameters =
                            [
                                new OpenApiParameter
                                {
                                    Name = "filter",
                                    In = ParameterLocation.Query,
                                    Schema = new OpenApiSchemaReference("UserFilter", null, null)
                                }
                            ]
                        }
                    }
                }
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["UserFilter"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        var operation = result.Paths["/users"].Operations[HttpMethod.Get];
        var paramSchema = operation.Parameters[0].Schema;
        Assert.True(paramSchema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)paramSchema;
        Assert.Equal("UserServiceUserFilter", schemaRef.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_ComponentResponses_UpdatesReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Error"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                },
                Responses = new Dictionary<string, IOpenApiResponse>
                {
                    ["NotFound"] = new OpenApiResponse
                    {
                        Description = "Not found",
                        Content = new Dictionary<string, IOpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchemaReference("Error", null, null)
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "ErrorSvc");

        // Assert
        Assert.NotNull(result.Components.Responses);
        var response = result.Components.Responses["NotFound"];
        var schema = response.Content["application/json"].Schema;
        Assert.True(schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)schema;
        Assert.Equal("ErrorSvcError", schemaRef.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_PreservesDocumentMetadata()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Test API",
                Version = "1.0",
                Description = "Test description"
            },
            Servers =
            [
                new OpenApiServer { Url = "http://localhost:8080" }
            ],
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        Assert.Equal("Test API", result.Info.Title);
        Assert.Equal("1.0", result.Info.Version);
        Assert.Equal("Test description", result.Info.Description);
        Assert.Single(result.Servers);
        Assert.Equal("http://localhost:8080", result.Servers[0].Url);
    }

    [Fact]
    public void ApplyPrefix_MultipleReferencesToSameSchema_UpdatesAllReferences()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["User"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["Container1"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["user"] = new OpenApiSchemaReference("User", null, null)
                        }
                    },
                    ["Container2"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["user"] = new OpenApiSchemaReference("User", null, null)
                        }
                    }
                }
            }
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "UserService");

        // Assert
        var container1Schema = (OpenApiSchema)result.Components.Schemas["UserServiceContainer1"];
        var container1User = container1Schema.Properties["user"];
        var container2Schema = (OpenApiSchema)result.Components.Schemas["UserServiceContainer2"];
        var container2User = container2Schema.Properties["user"];

        Assert.True(container1User is OpenApiSchemaReference);
        Assert.True(container2User is OpenApiSchemaReference);
        var container1Ref = (OpenApiSchemaReference)container1User;
        var container2Ref = (OpenApiSchemaReference)container2User;
        Assert.Equal("UserServiceUser", container1Ref.Reference?.Id);
        Assert.Equal("UserServiceUser", container2Ref.Reference?.Id);
    }
}
