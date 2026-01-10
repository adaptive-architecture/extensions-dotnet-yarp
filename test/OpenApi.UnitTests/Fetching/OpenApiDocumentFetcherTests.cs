using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using Microsoft.Extensions.Caching.Hybrid;
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
}
