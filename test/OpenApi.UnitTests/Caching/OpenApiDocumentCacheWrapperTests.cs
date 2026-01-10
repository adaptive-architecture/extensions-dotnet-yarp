using AdaptArch.Extensions.Yarp.OpenApi.Caching;
using Microsoft.OpenApi;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Caching;

public class OpenApiDocumentCacheWrapperTests
{
    [Fact]
    public async Task FromDocumentAsync_WithNullDocument_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            OpenApiDocumentCacheWrapper.FromDocumentAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FromDocumentAsync_WithValidDocument_ReturnsWrapper()
    {
        var document = CreateTestDocument();

        var wrapper = await OpenApiDocumentCacheWrapper.FromDocumentAsync(document, TestContext.Current.CancellationToken);

        Assert.NotNull(wrapper);
        Assert.NotNull(wrapper.Json);
        Assert.Contains("Test API", wrapper.Json);
    }

    [Fact]
    public async Task FromDocumentAsync_WithValidDocument_SerializesCorrectly()
    {
        var document = CreateTestDocument();

        var wrapper = await OpenApiDocumentCacheWrapper.FromDocumentAsync(document, TestContext.Current.CancellationToken);

        Assert.NotNull(wrapper.Json);
        Assert.Contains("\"title\": \"Test API\"", wrapper.Json);
        Assert.Contains("\"version\": \"v1\"", wrapper.Json);
        Assert.Contains("\"/api/test\"", wrapper.Json);
    }

    [Fact]
    public async Task ToDocumentAsync_WithNullJson_ReturnsNull()
    {
        var wrapper = new OpenApiDocumentCacheWrapper { Json = null };

        var document = await wrapper.ToDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Null(document);
    }

    [Fact]
    public async Task ToDocumentAsync_WithEmptyJson_ReturnsNull()
    {
        var wrapper = new OpenApiDocumentCacheWrapper { Json = "" };

        var document = await wrapper.ToDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Null(document);
    }

    [Fact]
    public async Task ToDocumentAsync_WithValidJson_ReturnsDocument()
    {
        var original = CreateTestDocument();
        var wrapper = await OpenApiDocumentCacheWrapper.FromDocumentAsync(original, TestContext.Current.CancellationToken);

        var restored = await wrapper.ToDocumentAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Info);
        Assert.Equal("Test API", restored.Info.Title);
        Assert.Equal("v1", restored.Info.Version);
    }

    [Fact]
    public async Task RoundTrip_PreservesDocumentStructure()
    {
        var original = CreateTestDocument();

        var wrapper = await OpenApiDocumentCacheWrapper.FromDocumentAsync(original, TestContext.Current.CancellationToken);
        var restored = await wrapper.ToDocumentAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Info);
        Assert.Equal(original.Info.Title, restored.Info.Title);
        Assert.Equal(original.Info.Version, restored.Info.Version);
        Assert.NotNull(restored.Paths);
        Assert.Equal(original.Paths.Count, restored.Paths.Count);
    }

    [Fact]
    public async Task ToDocumentAsync_WithInvalidJson_ThrowsException()
    {
        var wrapper = new OpenApiDocumentCacheWrapper { Json = "invalid json content" };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            wrapper.ToDocumentAsync(TestContext.Current.CancellationToken));
    }

    private static OpenApiDocument CreateTestDocument()
    {
        return new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Test API",
                Version = "v1"
            },
            Paths = new OpenApiPaths
            {
                ["/api/test"] = new OpenApiPathItem
                {
                    Description = "Test path"
                }
            }
        };
    }
}
