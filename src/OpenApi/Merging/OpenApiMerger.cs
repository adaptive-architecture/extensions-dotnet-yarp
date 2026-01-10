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
                    if (mergedDocument.Paths.TryGetValue(path, out var existing))
                    {
                        LogPathConflict(path);
                        mergedDocument.Paths[path] = MergePathItems((OpenApiPathItem)existing, (OpenApiPathItem)pathItem);
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
        var firstDoc = documents[0];
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
        MergeDictionary(target.Schemas, documents, c => c.Schemas, name => LogSchemaConflict(name));

        // Merge responses
        target.Responses = new Dictionary<string, IOpenApiResponse>();
        MergeDictionary(target.Responses, documents, c => c.Responses);

        // Merge parameters
        target.Parameters = new Dictionary<string, IOpenApiParameter>();
        MergeDictionary(target.Parameters, documents, c => c.Parameters);

        // Merge request bodies
        target.RequestBodies = new Dictionary<string, IOpenApiRequestBody>();
        MergeDictionary(target.RequestBodies, documents, c => c.RequestBodies);

        // Merge security schemes
        target.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
        MergeDictionary(target.SecuritySchemes, documents, c => c.SecuritySchemes);

        // Merge other components (headers, examples, links, callbacks)
        target.Headers = new Dictionary<string, IOpenApiHeader>();
        MergeDictionary(target.Headers, documents, c => c.Headers);

        target.Examples = new Dictionary<string, IOpenApiExample>();
        MergeDictionary(target.Examples, documents, c => c.Examples);

        target.Links = new Dictionary<string, IOpenApiLink>();
        MergeDictionary(target.Links, documents, c => c.Links);

        target.Callbacks = new Dictionary<string, IOpenApiCallback>();
        MergeDictionary(target.Callbacks, documents, c => c.Callbacks);

        LogComponentsMerged(target.Schemas.Count, target.Responses.Count, target.Parameters.Count, target.SecuritySchemes.Count);
    }

    private static void MergeDictionary<T>(IDictionary<string, T> target, IEnumerable<OpenApiDocument> documents, Func<OpenApiComponents, IDictionary<string, T>?> getComponents, Action<string>? onConflict = null)
    {
        foreach (var document in documents)
        {
            if (document.Components != null)
            {
                var components = getComponents(document.Components);
                if (components != null)
                {
                    foreach (var (name, item) in components)
                    {
                        if (target.ContainsKey(name))
                        {
                            onConflict?.Invoke(name);
                        }
                        else
                        {
                            target[name] = item;
                        }
                    }
                }
            }
        }
    }
}
