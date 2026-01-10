using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Renaming;

/// <summary>
/// Service for renaming schemas in OpenAPI documents to avoid naming collisions.
/// </summary>
public interface ISchemaRenamer
{
    /// <summary>
    /// Applies a prefix to all schema names in the document and updates all $ref references.
    /// </summary>
    /// <param name="document">The OpenAPI document to process.</param>
    /// <param name="prefix">The prefix to apply (should be PascalCase, e.g., "UserService").</param>
    /// <returns>A new document with renamed schemas and updated references.</returns>
    OpenApiDocument ApplyPrefix(OpenApiDocument document, string prefix);
}

/// <summary>
/// Default implementation of <see cref="ISchemaRenamer"/>.
/// Renames schemas by applying a PascalCase prefix and updates all $ref references throughout the document.
/// </summary>
public sealed partial class SchemaRenamer : ISchemaRenamer
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaRenamer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SchemaRenamer(ILogger<SchemaRenamer> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No prefix specified, returning document unchanged")]
    private partial void LogNoPrefixSpecified();

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying prefix '{prefix}' to schema names")]
    private partial void LogApplyingPrefix(string prefix);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No schemas to rename, returning document unchanged")]
    private partial void LogNoSchemasToRename();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Renaming {count} schemas with prefix '{prefix}'")]
    private partial void LogRenamingSchemas(int count, string prefix);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Renamed schema: {oldName} -> {newName}")]
    private partial void LogRenamedSchema(string oldName, string newName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Schema renaming complete: {count} schemas renamed with prefix '{prefix}'")]
    private partial void LogRenamingComplete(int count, string prefix);

    /// <inheritdoc/>
    public OpenApiDocument ApplyPrefix(OpenApiDocument document, string prefix)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (String.IsNullOrWhiteSpace(prefix))
        {
            LogNoPrefixSpecified();
            return document;
        }

        LogApplyingPrefix(prefix);

        // Build schema name mapping (oldName -> newName)
        var schemaNameMap = BuildSchemaNameMap(document, prefix);

        if (schemaNameMap.Count == 0)
        {
            LogNoSchemasToRename();
            return document;
        }

        LogRenamingSchemas(schemaNameMap.Count, prefix);

        // Create new document with renamed schemas and updated references
        var renamedDocument = new OpenApiDocument
        {
            Info = document.Info,
            Servers = document.Servers != null ? new List<OpenApiServer>(document.Servers) : [],
            Paths = [],
            Components = new OpenApiComponents(),
            Security = document.Security != null ? new List<OpenApiSecurityRequirement>(document.Security) : [],
            Tags = document.Tags != null ? new HashSet<OpenApiTag>(document.Tags) : [],
            ExternalDocs = document.ExternalDocs
        };

        // Rename schemas in Components section
        if (document.Components?.Schemas != null)
        {
            renamedDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>();
            foreach (var (oldName, schema) in document.Components.Schemas)
            {
                var newName = schemaNameMap[oldName];
                renamedDocument.Components.Schemas[newName] = (OpenApiSchema)UpdateSchemaReferences(schema, schemaNameMap)!;
                LogRenamedSchema(oldName, newName);
            }
        }

        // Copy and update other components
        CopyComponentsWithUpdatedReferences(document.Components, renamedDocument.Components, schemaNameMap);

        // Update paths and operations
        if (document.Paths != null)
        {
            foreach (var (path, pathItem) in document.Paths)
            {
                renamedDocument.Paths[path] = UpdatePathItemReferences((OpenApiPathItem)pathItem, schemaNameMap)!;
            }
        }

        LogRenamingComplete(schemaNameMap.Count, prefix);

        return renamedDocument;
    }

    private static Dictionary<string, string> BuildSchemaNameMap(OpenApiDocument document, string prefix)
    {
        var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (document.Components?.Schemas == null)
        {
            return nameMap;
        }

        foreach (var schemaName in document.Components.Schemas.Keys)
        {
            nameMap[schemaName] = prefix + schemaName;
        }

        return nameMap;
    }

    private static List<IOpenApiSchema> UpdateSchemaList(IEnumerable<IOpenApiSchema> schemas, Dictionary<string, string> nameMap)
    {
        return schemas.Select(s => UpdateSchemaReferences(s, nameMap)!).ToList();
    }

    private static IOpenApiSchema? UpdateSchemaReferences(IOpenApiSchema? schema, Dictionary<string, string> nameMap)
    {
        if (schema == null)
        {
            return null;
        }

        // Handle schema references - In v3, references are separate types
        if (schema is OpenApiSchemaReference schemaRef && schemaRef.Reference != null)
        {
            var refId = schemaRef.Reference.Id;
            if (String.IsNullOrWhiteSpace(refId))
            {
                return schemaRef; // Return as-is if no valid ID
            }

            if (nameMap.TryGetValue(refId, out var newName))
            {
                // Create a new reference with the updated name
                return new OpenApiSchemaReference(newName, null, null);
            }
            // Return the original reference if no mapping found
            return new OpenApiSchemaReference(refId, null, null);
        }

        // Create a new schema instance to avoid modifying the original
        var updatedSchema = new OpenApiSchema
        {
            Type = schema.Type,
            Format = schema.Format,
            Description = schema.Description,
            ReadOnly = schema.ReadOnly,
            WriteOnly = schema.WriteOnly,
            Required = schema.Required != null ? new HashSet<string>(schema.Required) : null,
            Enum = schema.Enum != null ? new List<System.Text.Json.Nodes.JsonNode>(schema.Enum) : null,
            Default = schema.Default,
            Deprecated = schema.Deprecated,
            Title = schema.Title,
            MultipleOf = schema.MultipleOf,
            Maximum = schema.Maximum,
            ExclusiveMaximum = schema.ExclusiveMaximum,
            Minimum = schema.Minimum,
            ExclusiveMinimum = schema.ExclusiveMinimum,
            MaxLength = schema.MaxLength,
            MinLength = schema.MinLength,
            Pattern = schema.Pattern,
            MaxItems = schema.MaxItems,
            MinItems = schema.MinItems,
            UniqueItems = schema.UniqueItems,
            MaxProperties = schema.MaxProperties,
            MinProperties = schema.MinProperties
        };

        // Update Items
        if (schema.Items != null)
        {
            updatedSchema.Items = UpdateSchemaReferences(schema.Items, nameMap);
        }

        // Update AllOf
        if (schema.AllOf != null)
        {
            updatedSchema.AllOf = UpdateSchemaList(schema.AllOf, nameMap);
        }

        // Update OneOf
        if (schema.OneOf != null)
        {
            updatedSchema.OneOf = UpdateSchemaList(schema.OneOf, nameMap);
        }

        // Update AnyOf
        if (schema.AnyOf != null)
        {
            updatedSchema.AnyOf = UpdateSchemaList(schema.AnyOf, nameMap);
        }

        // Update Not
        if (schema.Not != null)
        {
            updatedSchema.Not = UpdateSchemaReferences(schema.Not, nameMap);
        }

        // Update AdditionalProperties
        if (schema.AdditionalProperties != null)
        {
            updatedSchema.AdditionalProperties = UpdateSchemaReferences(schema.AdditionalProperties, nameMap);
        }

        // Update Properties
        if (schema.Properties != null)
        {
            updatedSchema.Properties = schema.Properties.ToDictionary(p => p.Key, p => UpdateSchemaReferences(p.Value, nameMap)!);
        }

        return updatedSchema;
    }

    private OpenApiPathItem? UpdatePathItemReferences(IOpenApiPathItem? pathItem, Dictionary<string, string> nameMap)
    {
        if (pathItem == null)
        {
            return null;
        }

        var updatedPathItem = new OpenApiPathItem();

        if (pathItem.Operations != null)
        {
            foreach (var (operationType, operation) in pathItem.Operations)
            {
                updatedPathItem.AddOperation(operationType, UpdateOperationReferences(operation, nameMap)!);
            }
        }

        return updatedPathItem;
    }

    private static OpenApiOperation? UpdateOperationReferences(OpenApiOperation? operation, Dictionary<string, string> nameMap)
    {
        if (operation == null)
        {
            return null;
        }

        var updatedOperation = new OpenApiOperation
        {
            OperationId = operation.OperationId,
            Summary = operation.Summary,
            Description = operation.Description,
            Deprecated = operation.Deprecated,
            Tags = operation.Tags != null ? new HashSet<OpenApiTagReference>(operation.Tags) : null
        };

        // Update request body
        if (operation.RequestBody != null)
        {
            updatedOperation.RequestBody = UpdateRequestBodyReferences(operation.RequestBody, nameMap);
        }

        // Update parameters
        if (operation.Parameters != null)
        {
            updatedOperation.Parameters = [];
            foreach (var parameter in operation.Parameters)
            {
                updatedOperation.Parameters.Add(UpdateParameterReferences(parameter, nameMap));
            }
        }

        // Update responses
        if (operation.Responses != null)
        {
            updatedOperation.Responses = [];
            foreach (var (statusCode, response) in operation.Responses)
            {
                updatedOperation.Responses[statusCode] = UpdateResponseReferences(response, nameMap)!;
            }
        }

        return updatedOperation;
    }

    private static OpenApiRequestBody? UpdateRequestBodyReferences(IOpenApiRequestBody? requestBody, Dictionary<string, string> nameMap)
    {
        if (requestBody?.Content == null)
        {
            return null;
        }

        var updatedRequestBody = new OpenApiRequestBody
        {
            Description = requestBody.Description,
            Required = requestBody.Required,
            Content = new Dictionary<string, IOpenApiMediaType>()
        };

        foreach (var (mediaType, mediaTypeObj) in requestBody.Content)
        {
            updatedRequestBody.Content[mediaType] = new OpenApiMediaType
            {
                Schema = UpdateSchemaReferences(mediaTypeObj.Schema, nameMap)
            };
        }

        return updatedRequestBody;
    }

    private static OpenApiParameter UpdateParameterReferences(IOpenApiParameter parameter, Dictionary<string, string> nameMap)
    {
        return new OpenApiParameter
        {
            Name = parameter.Name,
            In = parameter.In,
            Description = parameter.Description,
            Required = parameter.Required,
            Deprecated = parameter.Deprecated,
            AllowEmptyValue = parameter.AllowEmptyValue,
            Schema = UpdateSchemaReferences(parameter.Schema, nameMap)
        };
    }

    private static OpenApiResponse? UpdateResponseReferences(IOpenApiResponse? response, Dictionary<string, string> nameMap)
    {
        if (response?.Content == null)
        {
            return null;
        }

        var updatedResponse = new OpenApiResponse
        {
            Description = response.Description,
            Content = new Dictionary<string, IOpenApiMediaType>()
        };

        foreach (var (mediaType, mediaTypeObj) in response.Content)
        {
            updatedResponse.Content[mediaType] = new OpenApiMediaType
            {
                Schema = UpdateSchemaReferences(mediaTypeObj.Schema, nameMap)
            };
        }

        return updatedResponse;
    }

    private static void CopyComponentsWithUpdatedReferences(OpenApiComponents? source, OpenApiComponents target, Dictionary<string, string> nameMap)
    {
        if (source == null)
        {
            return;
        }

        // Copy security schemes (no schema references)
        if (source.SecuritySchemes != null)
        {
            target.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(source.SecuritySchemes);
        }

        // Copy and update responses
        if (source.Responses != null)
        {
            target.Responses = new Dictionary<string, IOpenApiResponse>();
            foreach (var (name, response) in source.Responses)
            {
                target.Responses[name] = UpdateResponseReferences(response, nameMap)!;
            }
        }

        // Copy and update parameters
        if (source.Parameters != null)
        {
            target.Parameters = new Dictionary<string, IOpenApiParameter>();
            foreach (var (name, parameter) in source.Parameters)
            {
                target.Parameters[name] = UpdateParameterReferences(parameter, nameMap);
            }
        }

        // Copy and update request bodies
        if (source.RequestBodies != null)
        {
            target.RequestBodies = new Dictionary<string, IOpenApiRequestBody>();
            foreach (var (name, requestBody) in source.RequestBodies)
            {
                target.RequestBodies[name] = UpdateRequestBodyReferences(requestBody, nameMap)!;
            }
        }

        // Copy other components (headers, examples, links, callbacks) - these rarely contain schema references
        if (source.Headers != null)
        {
            target.Headers = new Dictionary<string, IOpenApiHeader>(source.Headers);
        }

        if (source.Examples != null)
        {
            target.Examples = new Dictionary<string, IOpenApiExample>(source.Examples);
        }

        if (source.Links != null)
        {
            target.Links = new Dictionary<string, IOpenApiLink>(source.Links);
        }

        if (source.Callbacks != null)
        {
            target.Callbacks = new Dictionary<string, IOpenApiCallback>(source.Callbacks);
        }
    }
}
