using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Renaming;

public class SchemaRenamerTests
{
    private readonly TestLogger<SchemaRenamer> _testLogger;
    private readonly SchemaRenamer _renamer;

    public SchemaRenamerTests()
    {
        _testLogger = new TestLogger<SchemaRenamer>();
        _renamer = new SchemaRenamer(_testLogger);
    }

    private class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> LogEntries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            LogEntries.Add(new LogEntry
            {
                LogLevel = logLevel,
                Message = formatter(state, exception),
                EventId = eventId
            });
        }
    }

    private class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public string Message { get; set; } = String.Empty;
        public EventId EventId { get; set; }
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
        Assert.False(result.Components.Schemas.ContainsKey("User"));
        Assert.False(result.Components.Schemas.ContainsKey("Address"));

        var userSchema = (OpenApiSchema)result.Components.Schemas["UserServiceUser"];
        Assert.NotNull(userSchema.Properties);
        var addressProp = userSchema.Properties["address"];
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
                    ["BaseUser"] = new OpenApiSchema { Type = JsonSchemaType.Object },
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
        var baseUserRef = (OpenApiSchemaReference)extendedUserSchema.AllOf[0];
        Assert.NotNull(baseUserRef.Reference);
        Assert.Equal("UserServiceBaseUser", baseUserRef.Reference.Id);
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
        Assert.NotNull(valueRef.Reference);
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
        Assert.NotNull(operation.RequestBody);
        Assert.NotNull(operation.RequestBody.Content);
        var mediaType = operation.RequestBody.Content["application/json"];
        Assert.NotNull(mediaType.Schema);
        Assert.True(mediaType.Schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)mediaType.Schema;
        Assert.NotNull(schemaRef.Reference);
        Assert.Equal("UserServiceUser", schemaRef.Reference.Id);
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
        var response = operation.Responses["200"];
        Assert.NotNull(response.Content);
        var mediaType = response.Content["application/json"];
        Assert.NotNull(mediaType.Schema);
        Assert.True(mediaType.Schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)mediaType.Schema;
        Assert.NotNull(schemaRef.Reference);
        Assert.Equal("UserServiceUserList", schemaRef.Reference.Id);
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
        var parameter = operation.Parameters[0];
        Assert.NotNull(parameter.Schema);
        Assert.True(parameter.Schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)parameter.Schema;
        Assert.NotNull(schemaRef.Reference);
        Assert.Equal("UserServiceUserFilter", schemaRef.Reference.Id);
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
        var response = result.Components.Responses["NotFound"];
        Assert.NotNull(response.Content);
        var mediaType = response.Content["application/json"];
        Assert.NotNull(mediaType.Schema);
        Assert.True(mediaType.Schema is OpenApiSchemaReference);
        var schemaRef = (OpenApiSchemaReference)mediaType.Schema;
        Assert.NotNull(schemaRef.Reference);
        Assert.Equal("ErrorSvcError", schemaRef.Reference.Id);
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
        var container2Schema = (OpenApiSchema)result.Components.Schemas["UserServiceContainer2"];
        var container1User = container1Schema.Properties["user"];
        var container2User = container2Schema.Properties["user"];

        Assert.True(container1User is OpenApiSchemaReference);
        Assert.True(container2User is OpenApiSchemaReference);
        var container1Ref = (OpenApiSchemaReference)container1User;
        var container2Ref = (OpenApiSchemaReference)container2User;
        Assert.Equal("UserServiceUser", container1Ref.Reference?.Id);
        Assert.Equal("UserServiceUser", container2Ref.Reference?.Id);
    }

    [Fact]
    public void ApplyPrefix_WithNullPrefix_LogsNoPrefixSpecified()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = []
        };

        // Act
        var result = _renamer.ApplyPrefix(document, null!);

        // Assert
        Assert.Same(document, result);
        var debugLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Debug).ToList();
        Assert.Single(debugLogs);
        Assert.Contains("No prefix specified", debugLogs[0].Message);
    }

    [Fact]
    public void ApplyPrefix_WithEmptyPrefix_LogsNoPrefixSpecified()
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
        var debugLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Debug).ToList();
        Assert.Single(debugLogs);
        Assert.Contains("No prefix specified", debugLogs[0].Message);
    }

    [Fact]
    public void ApplyPrefix_WithValidPrefix_LogsApplyingPrefix()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents()
        };

        // Act
        _ = _renamer.ApplyPrefix(document, "TestPrefix");

        // Assert
        var infoLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Information).ToList();
        Assert.Single(infoLogs);
        Assert.Contains("Applying prefix 'TestPrefix'", infoLogs[0].Message);
    }

    [Fact]
    public void ApplyPrefix_WithNoSchemas_LogsNoSchemasToRename()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0" },
            Paths = [],
            Components = new OpenApiComponents()
        };

        // Act
        var result = _renamer.ApplyPrefix(document, "TestPrefix");

        // Assert
        Assert.Same(document, result);
        var infoLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Information).ToList();
        var debugLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Debug).ToList();

        Assert.Single(infoLogs);
        Assert.Contains("Applying prefix 'TestPrefix'", infoLogs[0].Message);

        Assert.Single(debugLogs);
        Assert.Contains("No schemas to rename", debugLogs[0].Message);
    }

    [Fact]
    public void ApplyPrefix_WithSingleSchema_LogsIndividualSchemaRename()
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
                    ["SingleSchema"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        // Act
        _ = _renamer.ApplyPrefix(document, "MyService");

        // Assert
        var traceLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Trace).ToList();
        Assert.Single(traceLogs);
        Assert.Contains("Renamed schema: SingleSchema -> MyServiceSingleSchema", traceLogs[0].Message);
    }
}
