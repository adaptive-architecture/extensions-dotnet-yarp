using System.Text;
using Microsoft.OpenApi;

namespace AdaptArch.Extensions.Yarp.OpenApi.Caching;

/// <summary>
/// A wrapper class for caching OpenApiDocument objects in HybridCache.
/// Serializes/deserializes using OpenAPI JSON format instead of System.Text.Json.
/// </summary>
internal sealed class OpenApiDocumentCacheWrapper
{
    /// <summary>
    /// Gets or sets the OpenAPI document JSON string.
    /// </summary>
    public string? Json { get; set; }

    /// <summary>
    /// Converts an OpenApiDocument to a cache wrapper.
    /// </summary>
    public static async Task<OpenApiDocumentCacheWrapper> FromDocumentAsync(OpenApiDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var memoryStream = new MemoryStream();
        await using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
        var jsonWriter = new OpenApiJsonWriter(streamWriter);
        document.SerializeAsV3(jsonWriter);
        await streamWriter.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(cancellationToken);

        return new OpenApiDocumentCacheWrapper { Json = json };
    }

    /// <summary>
    /// Converts the cache wrapper back to an OpenApiDocument.
    /// </summary>
    public Task<OpenApiDocument?> ToDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (String.IsNullOrEmpty(Json))
        {
            return Task.FromResult<OpenApiDocument?>(null);
        }

        var (document, diagnostic) = OpenApiDocument.Parse(Json, format: "json");
        if (document == null)
        {
            throw new InvalidOperationException($"Cached OpenAPI document could not be parsed: {diagnostic?.Errors?.FirstOrDefault()?.Message ?? "unknown error"}");
        }

        return Task.FromResult<OpenApiDocument?>(document);
    }
}
