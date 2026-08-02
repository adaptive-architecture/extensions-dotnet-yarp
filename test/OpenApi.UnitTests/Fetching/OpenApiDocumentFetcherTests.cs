using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Fetching;

public class OpenApiDocumentFetcherTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HybridCache _cache;
    private readonly IOptionsMonitor<OpenApiAggregationOptions> _optionsMonitor;
    private readonly ILogger<OpenApiDocumentFetcher> _logger;

    public OpenApiDocumentFetcherTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _cache = Substitute.For<HybridCache>();
        _optionsMonitor = Substitute.For<IOptionsMonitor<OpenApiAggregationOptions>>();
        _logger = NullLogger<OpenApiDocumentFetcher>.Instance;

        var options = new OpenApiAggregationOptions
        {
            CacheDuration = TimeSpan.FromMinutes(5),
            DefaultFetchTimeoutMs = 5000,
            FallbackPaths = Array.Empty<string>()
        };
        _optionsMonitor.CurrentValue.Returns(options);
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiDocumentFetcher(null, _cache, _optionsMonitor, _logger));
        Assert.Equal("httpClientFactory", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiDocumentFetcher(_httpClientFactory, null, _optionsMonitor, _logger));
        Assert.Equal("cache", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullOptionsMonitor_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiDocumentFetcher(_httpClientFactory, _cache, null, _logger));
        Assert.Equal("optionsMonitor", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, null));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        Assert.NotNull(fetcher);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithNullBaseUrl_ThrowsArgumentException()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fetcher.FetchDocumentAsync(null, "/swagger.json", TestContext.Current.CancellationToken));
        Assert.Equal("baseUrl", exception.ParamName);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithEmptyBaseUrl_ThrowsArgumentException()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fetcher.FetchDocumentAsync("", "/swagger.json", TestContext.Current.CancellationToken));
        Assert.Equal("baseUrl", exception.ParamName);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithWhitespaceBaseUrl_ThrowsArgumentException()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fetcher.FetchDocumentAsync("   ", "/swagger.json", TestContext.Current.CancellationToken));
        Assert.Equal("baseUrl", exception.ParamName);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithNullOpenApiPath_ThrowsArgumentException()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fetcher.FetchDocumentAsync("http://localhost", null, TestContext.Current.CancellationToken));
        Assert.Equal("openApiPath", exception.ParamName);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithEmptyOpenApiPath_ThrowsArgumentException()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fetcher.FetchDocumentAsync("http://localhost", "", TestContext.Current.CancellationToken));
        Assert.Equal("openApiPath", exception.ParamName);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithWhitespaceOpenApiPath_ThrowsArgumentException()
    {
        var fetcher = new OpenApiDocumentFetcher(_httpClientFactory, _cache, _optionsMonitor, _logger);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fetcher.FetchDocumentAsync("http://localhost", "   ", TestContext.Current.CancellationToken));
        Assert.Equal("openApiPath", exception.ParamName);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithSmallValidDocument_ReturnsDocument()
    {
        // Arrange
        const string ValidDocument = /*lang=json,strict*/ """{"openapi":"3.0.1","info":{"title":"Test","version":"1.0"},"paths":{}}""";
        var fetcher = CreateFetcherWithResponse(ValidDocument, maxDocumentSizeBytes: 1024);

        // Act
        var document = await fetcher.FetchDocumentAsync("http://localhost:5001", "/openapi.json", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(document);
        Assert.Equal("Test", document.Info.Title);
    }

    [Fact]
    public async Task FetchDocumentAsync_WithDocumentLargerThanLimit_ReturnsNull()
    {
        // Arrange: a downstream response bigger than MaxDocumentSizeBytes must be rejected
        // instead of being buffered and parsed.
        var oversizedBody = "{\"openapi\":\"3.0.1\",\"info\":{\"title\":\"" + new string('a', 64 * 1024) + "\",\"version\":\"1.0\"},\"paths\":{}}";
        var fetcher = CreateFetcherWithResponse(oversizedBody, maxDocumentSizeBytes: 1024);

        // Act
        var document = await fetcher.FetchDocumentAsync("http://localhost:5001", "/openapi.json", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(document);
    }

    private static OpenApiDocumentFetcher CreateFetcherWithResponse(string responseBody, long maxDocumentSizeBytes)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        });

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));

        var optionsMonitor = Substitute.For<IOptionsMonitor<OpenApiAggregationOptions>>();
        optionsMonitor.CurrentValue.Returns(new OpenApiAggregationOptions
        {
            DefaultFetchTimeoutMs = 5000,
            FallbackPaths = [],
            MaxDocumentSizeBytes = maxDocumentSizeBytes
        });

        // A real cache so the fetch factory actually executes.
        var cache = new ServiceCollection()
            .AddHybridCache().Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        return new OpenApiDocumentFetcher(httpClientFactory, cache, optionsMonitor, NullLogger<OpenApiDocumentFetcher>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
