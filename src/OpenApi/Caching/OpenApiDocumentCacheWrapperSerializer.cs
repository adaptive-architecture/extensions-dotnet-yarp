using System.Buffers;
using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace AdaptArch.Extensions.Yarp.OpenApi.Caching;

/// <summary>
/// Source-generated JSON serializer for <see cref="OpenApiDocumentCacheWrapper"/> used by
/// <see cref="HybridCache"/>. Keeps cache serialization reflection-free (AOT compatible),
/// consistent with the rest of the library.
/// </summary>
internal sealed class OpenApiDocumentCacheWrapperSerializer : IHybridCacheSerializer<OpenApiDocumentCacheWrapper>
{
    public OpenApiDocumentCacheWrapper Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize(ref reader, OpenApiJsonContext.Default.OpenApiDocumentCacheWrapper)!;
    }

    public void Serialize(OpenApiDocumentCacheWrapper value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, OpenApiJsonContext.Default.OpenApiDocumentCacheWrapper);
    }
}
