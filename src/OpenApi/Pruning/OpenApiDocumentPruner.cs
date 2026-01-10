using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Pruning;

/// <summary>
/// Service for pruning OpenAPI documents by removing unreachable paths and unused components.
/// </summary>
public interface IOpenApiDocumentPruner
{
    /// <summary>
    /// Prunes an OpenAPI document to include only reachable paths and their dependencies.
    /// </summary>
    /// <param name="document">The original OpenAPI document.</param>
    /// <param name="reachabilityResult">The path reachability analysis result.</param>
    /// <returns>A new pruned OpenAPI document.</returns>
    OpenApiDocument PruneDocument(OpenApiDocument document, PathReachabilityResult reachabilityResult);
}

/// <summary>
/// Default implementation of <see cref="IOpenApiDocumentPruner"/>.
/// Removes unreachable paths and analyzes schema dependencies to remove unused components.
/// </summary>
public sealed partial class OpenApiDocumentPruner : IOpenApiDocumentPruner
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiDocumentPruner"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OpenApiDocumentPruner(ILogger<OpenApiDocumentPruner> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pruning OpenAPI document: {pathCount} original paths, {reachableCount} reachable")]
    private partial void LogPruningDocument(int pathCount, int reachableCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added {pathCount} reachable paths to pruned document")]
    private partial void LogAddedReachablePaths(int pathCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {schemaCount} schemas in use")]
    private partial void LogSchemasInUse(int schemaCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pruned schemas: {original} → {pruned}")]
    private partial void LogPrunedSchemas(int original, int pruned);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pruned tags: {original} → {pruned}")]
    private partial void LogPrunedTags(int original, int pruned);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document pruning complete: {pathCount} paths, {schemaCount} schemas, {tagCount} tags")]
    private partial void LogPruningComplete(int pathCount, int schemaCount, int tagCount);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Tracking schema dependency: {schemaName}")]
    private partial void LogTrackingSchema(string schemaName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} response components")]
    private partial void LogCopiedResponses(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} parameter components")]
    private partial void LogCopiedParameters(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} request body components")]
    private partial void LogCopiedRequestBodies(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} header components")]
    private partial void LogCopiedHeaders(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} example components")]
    private partial void LogCopiedExamples(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} link components")]
    private partial void LogCopiedLinks(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied {count} callback components")]
    private partial void LogCopiedCallbacks(int count);

    /// <inheritdoc/>
    public OpenApiDocument PruneDocument(OpenApiDocument document, PathReachabilityResult reachabilityResult)
    {
        ArgumentNullException.ThrowIfNull(document);

        ArgumentNullException.ThrowIfNull(reachabilityResult);

        LogPruningDocument(document.Paths?.Count ?? 0, reachabilityResult.ReachablePaths.Count);

        // Create a new document (don't modify original)
        var prunedDocument = new OpenApiDocument
        {
            Info = document.Info,
            Servers = document.Servers != null ? new List<OpenApiServer>(document.Servers) : [],
            Paths = [],
            Components = new OpenApiComponents(),
            Security = document.Security != null ? new List<OpenApiSecurityRequirement>(document.Security) : [],
            Tags = new HashSet<OpenApiTag>(),
            ExternalDocs = document.ExternalDocs
        };

        // Step 1: Add only reachable paths using gateway paths as keys
        var usedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (gatewayPath, reachableInfo) in reachabilityResult.ReachablePaths)
        {
            prunedDocument.Paths[gatewayPath] = CreatePrunedPathItem(reachableInfo.Operations, usedTags);
        }

        LogAddedReachablePaths(prunedDocument.Paths.Count);

        // Step 2: Analyze schema dependencies from remaining operations
        var usedSchemas = AnalyzeSchemaDependencies(prunedDocument.Paths, document.Components?.Schemas);
        LogSchemasInUse(usedSchemas.Count);

        // Step 3: Copy only used schemas to pruned document
        if (document.Components?.Schemas != null)
        {
            prunedDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>();
            foreach (var schemaName in usedSchemas)
            {
                if (document.Components.Schemas.TryGetValue(schemaName, out var schema))
                {
                    prunedDocument.Components.Schemas[schemaName] = schema;
                }
            }
            LogPrunedSchemas(document.Components.Schemas.Count, prunedDocument.Components.Schemas.Count);
        }

        // Step 4: Copy only used tags
        if (document.Tags != null)
        {
            foreach (var tag in document.Tags)
            {
                if (usedTags.Contains(tag.Name!))
                {
                    prunedDocument.Tags.Add(tag);
                }
            }
            LogPrunedTags(document.Tags.Count, prunedDocument.Tags.Count);
        }

        // Step 5: Copy security schemes (keep all for now - they might be referenced globally)
        if (document.Components?.SecuritySchemes != null)
        {
            prunedDocument.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(document.Components.SecuritySchemes);
        }

        // Step 6: Copy other components that might be referenced (responses, parameters, etc.)
        CopyReferencedComponents(document, prunedDocument);

        LogPruningComplete(prunedDocument.Paths.Count, prunedDocument.Components?.Schemas?.Count ?? 0, prunedDocument.Tags?.Count ?? 0);

        return prunedDocument;
    }

    private static OpenApiPathItem CreatePrunedPathItem(Dictionary<HttpMethod, OpenApiOperation> operations, HashSet<string> usedTags)
    {
        var pathItem = new OpenApiPathItem
        {
            Operations = []
        };

        foreach (var (operationType, operation) in operations)
        {
            pathItem.Operations[operationType] = operation;

            // Track used tags
            if (operation.Tags != null)
            {
                foreach (var tag in operation.Tags)
                {
                    if (!String.IsNullOrWhiteSpace(tag.Name))
                    {
                        usedTags.Add(tag.Name);
                    }
                }
            }
        }

        return pathItem;
    }

    private static void AnalyzeOperationSchemas(OpenApiOperation operation, Queue<string> schemasToAnalyze)
    {
        // Check request body
        if (operation.RequestBody?.Content != null)
        {
            foreach (var mediaType in operation.RequestBody.Content.Values)
            {
                AddSchemaReferences(mediaType.Schema, schemasToAnalyze);
            }
        }

        // Check parameters
        if (operation.Parameters != null)
        {
            foreach (var parameter in operation.Parameters)
            {
                AddSchemaReferences(parameter.Schema, schemasToAnalyze);
            }
        }

        // Check responses
        if (operation.Responses != null)
        {
            foreach (var response in operation.Responses.Values)
            {
                if (response.Content != null)
                {
                    foreach (var mediaType in response.Content.Values)
                    {
                        AddSchemaReferences(mediaType.Schema, schemasToAnalyze);
                    }
                }
            }
        }
    }

    private HashSet<string> AnalyzeSchemaDependencies(OpenApiPaths paths, IDictionary<string, IOpenApiSchema>? componentSchemas)
    {
        var usedSchemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schemasToAnalyze = new Queue<string>();

        // Step 1: Find all directly referenced schemas from operations
        foreach (var pathItem in paths.Values)
        {
            if (pathItem.Operations == null) continue;

            foreach (var operation in pathItem.Operations.Values)
            {
                AnalyzeOperationSchemas(operation, schemasToAnalyze);
            }
        }

        // Step 2: Process queue and find nested schema references (recursive)
        while (schemasToAnalyze.Count > 0)
        {
            var schemaName = schemasToAnalyze.Dequeue();

            if (usedSchemas.Add(schemaName))
            {
                LogTrackingSchema(schemaName);

                // Look up the schema in the components and analyze its nested references
                if (componentSchemas != null && componentSchemas.TryGetValue(schemaName, out var schema))
                {
                    AddSchemaReferences(schema, schemasToAnalyze);
                }
            }
        }

        return usedSchemas;
    }

    private static void AddSchemaList(IEnumerable<IOpenApiSchema> schemas, Queue<string> schemasToAnalyze)
    {
        foreach (var s in schemas)
        {
            AddSchemaReferences(s, schemasToAnalyze);
        }
    }

    private static void AddSchemaReferences(IOpenApiSchema? schema, Queue<string> schemasToAnalyze)
    {
        if (schema == null) return;

        // Direct reference - In v3, references are separate types
        if (schema is OpenApiSchemaReference schemaRef && schemaRef.Reference != null)
        {
            var refId = schemaRef.Reference.Id;
            if (!String.IsNullOrWhiteSpace(refId))
            {
                schemasToAnalyze.Enqueue(refId);
            }
        }

        // Array items
        if (schema.Items != null)
        {
            AddSchemaReferences(schema.Items, schemasToAnalyze);
        }

        // Object properties
        if (schema.Properties != null)
        {
            AddSchemaList(schema.Properties.Values, schemasToAnalyze);
        }

        // AllOf, OneOf, AnyOf
        if (schema.AllOf != null)
        {
            AddSchemaList(schema.AllOf, schemasToAnalyze);
        }

        if (schema.OneOf != null)
        {
            AddSchemaList(schema.OneOf, schemasToAnalyze);
        }

        if (schema.AnyOf != null)
        {
            AddSchemaList(schema.AnyOf, schemasToAnalyze);
        }

        // Not schema
        if (schema.Not != null)
        {
            AddSchemaReferences(schema.Not, schemasToAnalyze);
        }

        // Additional properties
        if (schema.AdditionalProperties != null)
        {
            AddSchemaReferences(schema.AdditionalProperties, schemasToAnalyze);
        }
    }

    private void CopyReferencedComponents(OpenApiDocument source, OpenApiDocument target)
    {
        if (source.Components == null) return;

        // Copy responses (might be referenced by operations)
        if (source.Components.Responses?.Count > 0)
        {
            target.Components!.Responses = new Dictionary<string, IOpenApiResponse>(source.Components.Responses);
            LogCopiedResponses(target.Components.Responses.Count);
        }

        // Copy parameters (might be referenced by operations)
        if (source.Components.Parameters?.Count > 0)
        {
            target.Components!.Parameters = new Dictionary<string, IOpenApiParameter>(source.Components.Parameters);
            LogCopiedParameters(target.Components.Parameters.Count);
        }

        // Copy request bodies (might be referenced by operations)
        if (source.Components.RequestBodies?.Count > 0)
        {
            target.Components!.RequestBodies = new Dictionary<string, IOpenApiRequestBody>(source.Components.RequestBodies);
            LogCopiedRequestBodies(target.Components.RequestBodies.Count);
        }

        // Copy headers (might be referenced by responses)
        if (source.Components.Headers?.Count > 0)
        {
            target.Components!.Headers = new Dictionary<string, IOpenApiHeader>(source.Components.Headers);
            LogCopiedHeaders(target.Components.Headers.Count);
        }

        // Copy examples (might be referenced by media types)
        if (source.Components.Examples?.Count > 0)
        {
            target.Components!.Examples = new Dictionary<string, IOpenApiExample>(source.Components.Examples);
            LogCopiedExamples(target.Components.Examples.Count);
        }

        // Copy links (might be referenced by responses)
        if (source.Components.Links?.Count > 0)
        {
            target.Components!.Links = new Dictionary<string, IOpenApiLink>(source.Components.Links);
            LogCopiedLinks(target.Components.Links.Count);
        }

        // Copy callbacks (might be referenced by operations)
        if (source.Components.Callbacks?.Count > 0)
        {
            target.Components!.Callbacks = new Dictionary<string, IOpenApiCallback>(source.Components.Callbacks);
            LogCopiedCallbacks(target.Components.Callbacks.Count);
        }
    }
}
