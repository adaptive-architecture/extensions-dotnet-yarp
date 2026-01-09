using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Merging;

/// <summary>
/// Service for merging multiple OpenAPI documents into a single unified specification.
/// </summary>
public interface IOpenApiMerger
{
    /// <summary>
    /// Merges multiple OpenAPI documents into a single unified specification.
    /// </summary>
    /// <param name="documents">The OpenAPI documents to merge.</param>
    /// <param name="serviceName">The name of the unified service.</param>
    /// <returns>A merged OpenAPI document.</returns>
    OpenApiDocument MergeDocuments(IEnumerable<OpenApiDocument> documents, string serviceName);
}

/// <summary>
/// Default implementation of <see cref="IOpenApiMerger"/>.
/// Merges multiple OpenAPI documents by combining paths, components, and metadata.
/// </summary>
public sealed partial class OpenApiMerger : IOpenApiMerger
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiMerger"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OpenApiMerger(ILogger<OpenApiMerger> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Merging {count} OpenAPI document(s) for service '{serviceName}'")]
    private partial void LogMergingDocuments(int count, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Path conflict detected: {path} exists in multiple documents, merging operations")]
    private partial void LogPathConflict(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Merge complete: {pathCount} paths, {schemaCount} schemas, {pathConflicts} path conflicts resolved")]
    private partial void LogMergeComplete(int pathCount, int schemaCount, int pathConflicts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Operation conflict: {httpMethod} already exists, keeping first occurrence")]
    private partial void LogOperationConflict(HttpMethod httpMethod);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Schema name conflict: '{schemaName}' exists in multiple documents. Consider using prefix collision avoidance. Keeping first occurrence.")]
    private partial void LogSchemaConflict(string schemaName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Components merged: {schemas} schemas, {responses} responses, {parameters} parameters, {securitySchemes} security schemes")]
    private partial void LogComponentsMerged(int schemas, int responses, int parameters, int securitySchemes);

    /// <inheritdoc/>
    public OpenApiDocument MergeDocuments(IEnumerable<OpenApiDocument> documents, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var documentList = documents.ToList();

        if (documentList.Count == 0)
        {
            throw new ArgumentException("At least one document is required for merging", nameof(documents));
        }

        if (String.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or whitespace", nameof(serviceName));
        }

        LogMergingDocuments(documentList.Count, serviceName);

        // If only one document, return a copy with updated info
        if (documentList.Count == 1)
        {
            return CreateMergedDocument(documentList[0], serviceName);
        }

        // Create merged document
        var mergedDocument = new OpenApiDocument
        {
            Info = CreateMergedInfo(documentList, serviceName),
            Servers = MergeServers(documentList),
            Paths = [],
            Components = new OpenApiComponents(),
            Security = MergeSecurity(documentList),
            Tags = MergeTags(documentList),
            ExternalDocs = documentList.FirstOrDefault(d => d.ExternalDocs != null)?.ExternalDocs
        };

        // Merge paths
        var pathConflicts = 0;
        foreach (var document in documentList)
        {
            if (document.Paths != null)
            {
                foreach (var (path, pathItem) in document.Paths)
                {
                    if (mergedDocument.Paths.ContainsKey(path))
                    {
                        LogPathConflict(path);
                        mergedDocument.Paths[path] = MergePathItems((OpenApiPathItem)mergedDocument.Paths[path], (OpenApiPathItem)pathItem);
                        pathConflicts++;
                    }
                    else
                    {
                        mergedDocument.Paths[path] = pathItem;
                    }
                }
            }
        }

        // Merge components
        MergeComponents(documentList, mergedDocument.Components);

        LogMergeComplete(
            mergedDocument.Paths.Count,
            mergedDocument.Components?.Schemas?.Count ?? 0,
            pathConflicts);

        return mergedDocument;
    }

    private static OpenApiDocument CreateMergedDocument(OpenApiDocument source, string serviceName)
    {
        return new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = serviceName,
                Version = source.Info?.Version ?? "1.0.0",
                Description = source.Info?.Description
            },
            Servers = source.Servers != null ? new List<OpenApiServer>(source.Servers) : [],
            Paths = source.Paths,
            Components = source.Components,
            Security = source.Security != null ? new List<OpenApiSecurityRequirement>(source.Security) : [],
            Tags = source.Tags != null ? new HashSet<OpenApiTag>(source.Tags) : [],
            ExternalDocs = source.ExternalDocs
        };
    }

    private static OpenApiInfo CreateMergedInfo(List<OpenApiDocument> documents, string serviceName)
    {
        var firstDoc = documents.First();
        var descriptions = documents
            .Where(d => d.Info?.Description != null)
            .Select(d => d.Info.Description)
            .Distinct()
            .ToList();

        return new OpenApiInfo
        {
            Title = serviceName,
            Version = firstDoc.Info?.Version ?? "1.0.0",
            Description = descriptions.Count > 0
                ? $"Aggregated API for {serviceName}. Combined from {documents.Count} service(s)."
                : null
        };
    }

    private static List<OpenApiServer> MergeServers(List<OpenApiDocument> documents)
    {
        var servers = new List<OpenApiServer>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            if (document.Servers != null)
            {
                foreach (var server in document.Servers)
                {
                    if (!String.IsNullOrWhiteSpace(server.Url) && seenUrls.Add(server.Url))
                    {
                        servers.Add(server);
                    }
                }
            }
        }

        return servers;
    }

    private static List<OpenApiSecurityRequirement> MergeSecurity(List<OpenApiDocument> documents)
    {
        var requirements = new List<OpenApiSecurityRequirement>();

        foreach (var document in documents)
        {
            if (document.Security != null)
            {
                requirements.AddRange(document.Security);
            }
        }

        return requirements;
    }

    private static HashSet<OpenApiTag> MergeTags(List<OpenApiDocument> documents)
    {
        var tags = new Dictionary<string, OpenApiTag>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            if (document.Tags != null)
            {
                foreach (var tag in document.Tags)
                {
                    if (!String.IsNullOrWhiteSpace(tag.Name) && !tags.ContainsKey(tag.Name))
                    {
                        tags[tag.Name] = tag;
                    }
                }
            }
        }

        return [.. tags.Values];
    }

    private OpenApiPathItem MergePathItems(OpenApiPathItem existing, OpenApiPathItem incoming)
    {
        // Create a new path item with operations from both
        var merged = new OpenApiPathItem
        {
            Operations = []
        };

        // Copy existing operations
        if (existing.Operations != null)
        {
            foreach (var (operationType, operation) in existing.Operations)
            {
                merged.Operations[operationType] = operation;
            }
        }

        // Add incoming operations (may overwrite if same HTTP method)
        if (incoming.Operations != null)
        {
            foreach (var (operationType, operation) in incoming.Operations)
            {
                if (merged.Operations.ContainsKey(operationType))
                {
                    LogOperationConflict(operationType);
                }
                else
                {
                    merged.Operations[operationType] = operation;
                }
            }
        }

        return merged;
    }

    private void MergeComponents(List<OpenApiDocument> documents, OpenApiComponents target)
    {
        // Merge schemas
        target.Schemas = new Dictionary<string, IOpenApiSchema>();
        foreach (var document in documents)
        {
            if (document.Components?.Schemas != null)
            {
                foreach (var (name, schema) in document.Components.Schemas)
                {
                    if (target.Schemas.ContainsKey(name))
                    {
                        LogSchemaConflict(name);
                    }
                    else
                    {
                        target.Schemas[name] = schema;
                    }
                }
            }
        }

        // Merge responses
        target.Responses = new Dictionary<string, IOpenApiResponse>();
        foreach (var document in documents)
        {
            if (document.Components?.Responses != null)
            {
                foreach (var (name, response) in document.Components.Responses)
                {
                    if (!target.Responses.ContainsKey(name))
                    {
                        target.Responses[name] = response;
                    }
                }
            }
        }

        // Merge parameters
        target.Parameters = new Dictionary<string, IOpenApiParameter>();
        foreach (var document in documents)
        {
            if (document.Components?.Parameters != null)
            {
                foreach (var (name, parameter) in document.Components.Parameters)
                {
                    if (!target.Parameters.ContainsKey(name))
                    {
                        target.Parameters[name] = parameter;
                    }
                }
            }
        }

        // Merge request bodies
        target.RequestBodies = new Dictionary<string, IOpenApiRequestBody>();
        foreach (var document in documents)
        {
            if (document.Components?.RequestBodies != null)
            {
                foreach (var (name, requestBody) in document.Components.RequestBodies)
                {
                    if (!target.RequestBodies.ContainsKey(name))
                    {
                        target.RequestBodies[name] = requestBody;
                    }
                }
            }
        }

        // Merge security schemes
        target.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
        foreach (var document in documents)
        {
            if (document.Components?.SecuritySchemes != null)
            {
                foreach (var (name, scheme) in document.Components.SecuritySchemes)
                {
                    if (!target.SecuritySchemes.ContainsKey(name))
                    {
                        target.SecuritySchemes[name] = scheme;
                    }
                }
            }
        }

        // Merge other components (headers, examples, links, callbacks)
        target.Headers = new Dictionary<string, IOpenApiHeader>();
        target.Examples = new Dictionary<string, IOpenApiExample>();
        target.Links = new Dictionary<string, IOpenApiLink>();
        target.Callbacks = new Dictionary<string, IOpenApiCallback>();

        foreach (var document in documents)
        {
            if (document.Components?.Headers != null)
            {
                foreach (var (name, header) in document.Components.Headers)
                {
                    if (!target.Headers.ContainsKey(name))
                    {
                        target.Headers[name] = header;
                    }
                }
            }

            if (document.Components?.Examples != null)
            {
                foreach (var (name, example) in document.Components.Examples)
                {
                    if (!target.Examples.ContainsKey(name))
                    {
                        target.Examples[name] = example;
                    }
                }
            }

            if (document.Components?.Links != null)
            {
                foreach (var (name, link) in document.Components.Links)
                {
                    if (!target.Links.ContainsKey(name))
                    {
                        target.Links[name] = link;
                    }
                }
            }

            if (document.Components?.Callbacks != null)
            {
                foreach (var (name, callback) in document.Components.Callbacks)
                {
                    if (!target.Callbacks.ContainsKey(name))
                    {
                        target.Callbacks[name] = callback;
                    }
                }
            }
        }

        LogComponentsMerged(target.Schemas.Count, target.Responses.Count, target.Parameters.Count, target.SecuritySchemes.Count);
    }
}
