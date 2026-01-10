using AdaptArch.Extensions.Yarp.OpenApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Middleware;

public class OpenApiAggregationMiddlewareTests
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OpenApiAggregationMiddleware> _logger;
    private readonly OpenApiAggregationMiddleware _middleware;

    public OpenApiAggregationMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
        _logger = NullLogger<OpenApiAggregationMiddleware>.Instance;
        _middleware = new OpenApiAggregationMiddleware(_next, "/api-docs", _logger);
    }

    [Fact]
    public void Constructor_WithNullNext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiAggregationMiddleware(null, "/api-docs", _logger));
    }

    [Fact]
    public void Constructor_WithNullBasePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiAggregationMiddleware(_next, null, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenApiAggregationMiddleware(_next, "/api-docs", null));
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        var middleware = new OpenApiAggregationMiddleware(_next, "/api-docs", _logger);
        Assert.NotNull(middleware);
    }

    [Fact]
    public async Task InvokeAsync_WithNonMatchingPath_CallsNext()
    {
        var context = CreateHttpContext("/other-path");

        await _middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithMatchingBasePath_DoesNotCallNext()
    {
        var context = CreateHttpContext("/api-docs");

        await _middleware.InvokeAsync(context);

        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithMatchingBasePathAndService_DoesNotCallNext()
    {
        var context = CreateHttpContext("/api-docs/user-service");

        await _middleware.InvokeAsync(context);

        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithNullPath_CallsNext()
    {
        var context = CreateHttpContext(null);

        await _middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyPath_CallsNext()
    {
        var context = CreateHttpContext("");

        await _middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithCaseInsensitiveBasePath_Matches()
    {
        var context = CreateHttpContext("/API-DOCS");

        await _middleware.InvokeAsync(context);

        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithTrailingSlash_Matches()
    {
        var context = CreateHttpContext("/api-docs/");

        await _middleware.InvokeAsync(context);

        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithDifferentBasePath_CallsNext()
    {
        var middleware = new OpenApiAggregationMiddleware(_next, "/swagger", _logger);
        var context = CreateHttpContext("/api-docs");

        await middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    [Theory]
    [InlineData("/api-docs/user-service/openapi.json")]
    [InlineData("/api-docs/user-service/openapi.yaml")]
    [InlineData("/api-docs/user-service/openapi.yml")]
    public async Task InvokeAsync_WithExplicitFormatPath_DoesNotCallNext(string path)
    {
        var context = CreateHttpContext(path);

        await _middleware.InvokeAsync(context);

        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_WithPartialMatch_CallsNext()
    {
        var context = CreateHttpContext("/api-documentation");

        await _middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        return context;
    }
}
