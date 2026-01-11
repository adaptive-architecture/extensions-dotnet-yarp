using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Middleware;

/// <summary>
/// <para>Unit tests for OpenApiAggregationMiddleware.</para>
/// <para>
/// Test Coverage Summary:
/// - Constructor validation (null checks)
/// - Path routing and matching logic
/// - Service name validation and security (path traversal prevention)
/// - Service list endpoint functionality
/// - Error handling for missing dependencies
/// </para>
/// <para>
/// Note: Deep integration testing of HandleServiceSpecRequest (cache, fetching, merging, etc.)
/// is covered by integration tests since it requires complex dependency setup including
/// HybridCache, IOpenApiDocumentFetcher, IPathReachabilityAnalyzer, IOpenApiDocumentPruner,
/// ISchemaRenamer, and IOpenApiMerger. The middleware's dependency on GetRequiredService
/// makes it impractical to unit test the full aggregation flow without mocking 8+ dependencies.
/// See OpenApi.IntegrationTests for end-to-end middleware testing.
/// </para>
/// </summary>
public class OpenApiAggregationMiddlewareTests
{
    private readonly RequestDelegate _next;
    private readonly TestLogger<OpenApiAggregationMiddleware> _testLogger;
    private readonly OpenApiAggregationMiddleware _middleware;
    private readonly OpenApiAggregationMiddleware _middlewareWithTestLogger;

    public OpenApiAggregationMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
        _testLogger = new TestLogger<OpenApiAggregationMiddleware>();
        _middleware = new OpenApiAggregationMiddleware(_next, "/api-docs", NullLogger<OpenApiAggregationMiddleware>.Instance);
        _middlewareWithTestLogger = new OpenApiAggregationMiddleware(_next, "/api-docs", _testLogger);
    }

    private class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> LogEntries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            LogEntries.Add(new LogEntry
            {
                LogLevel = logLevel,
                Message = formatter(state, exception),
                EventId = eventId
            });
        }
    }

    private class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public string Message { get; set; } = String.Empty;
        public EventId EventId { get; set; }
    }

    #region Existing Tests

    [Fact]
    public async Task InvokeAsync_ServiceListRequest_LogsHandlingServiceListRequest()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs");

        // Mock service analyzer
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(new List<ServiceSpecification>
        {
            new ServiceSpecification { ServiceName = "UserService", Routes = [] }
        });

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middlewareWithTestLogger.InvokeAsync(context);

        // Assert
        var debugLogs = _testLogger.LogEntries.Where(le => le.LogLevel == LogLevel.Debug).ToList();
        Assert.Single(debugLogs);
        Assert.Contains("Handling service list request", debugLogs[0].Message);
    }

    [Fact]
    public async Task InvokeAsync_ServiceNotFound_Returns500()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs/nonexistent-service");

        // Mock service analyzer to return empty list (missing required dependencies causes exception)
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middlewareWithTestLogger.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullNext_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiAggregationMiddleware(null!, "/api-docs", NullLogger<OpenApiAggregationMiddleware>.Instance));

        Assert.Equal("next", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullBasePath_ThrowsArgumentNullException()
    {
        // Arrange
        var next = Substitute.For<RequestDelegate>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiAggregationMiddleware(next, null!, NullLogger<OpenApiAggregationMiddleware>.Instance));

        Assert.Equal("basePath", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var next = Substitute.For<RequestDelegate>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OpenApiAggregationMiddleware(next, "/api-docs", null!));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidArguments_CreatesInstance()
    {
        // Arrange
        var next = Substitute.For<RequestDelegate>();
        var logger = NullLogger<OpenApiAggregationMiddleware>.Instance;

        // Act
        var middleware = new OpenApiAggregationMiddleware(next, "/api-docs", logger);

        // Assert
        Assert.NotNull(middleware);
    }

    #endregion

    #region Path Matching and Routing Tests

    [Theory]
    [InlineData("/other-path")]
    [InlineData("/api")]
    [InlineData("/docs")]
    [InlineData("/")]
    public async Task InvokeAsync_WithNonMatchingPath_CallsNextMiddleware(string path)
    {
        // Arrange
        var context = CreateHttpContext(path);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        await _next.Received(1).Invoke(context);
    }

    [Theory]
    [InlineData("/api-docs")]
    [InlineData("/api-docs/")]
    [InlineData("/API-DOCS")]
    [InlineData("/Api-Docs")]
    public async Task InvokeAsync_WithBasePathOnly_HandlesServiceListRequest(string path)
    {
        // Arrange
        var context = CreateHttpContext(path);
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        await _next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api-docs/my-service")]
    [InlineData("/api-docs/my-service/openapi.json")]
    [InlineData("/api-docs/my-service/openapi.yaml")]
    [InlineData("/api-docs/my-service/openapi.yml")]
    public async Task InvokeAsync_WithServicePath_HandlesServiceSpecRequest(string path)
    {
        // Arrange
        var context = CreateHttpContext(path);
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        await _next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
    }

    #endregion

    #region Service Name Validation Tests

    [Theory]
    [InlineData("/api-docs/../etc/passwd")]
    [InlineData("/api-docs/service/../admin")]
    [InlineData("/api-docs/..%2F..%2Fetc%2Fpasswd")]
    public async Task InvokeAsync_WithPathTraversalAttempt_ReturnsBadRequest(string path)
    {
        // Arrange
        var context = CreateHttpContext(path);
        var services = new ServiceCollection();
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(400, context.Response.StatusCode);
    }

    #endregion

    #region Service List Tests

    [Fact]
    public async Task InvokeAsync_ServiceListRequest_ReturnsServicesWithUrls()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(new List<ServiceSpecification>
        {
            new ServiceSpecification { ServiceName = "UserService", Routes = [] },
            new ServiceSpecification { ServiceName = "OrderService", Routes = [] }
        });

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("UserService", responseBody);
        Assert.Contains("OrderService", responseBody);
        Assert.Contains("/api-docs/userservice", responseBody.ToLower());
        Assert.Contains("/api-docs/orderservice", responseBody.ToLower());
    }

    [Fact]
    public async Task InvokeAsync_ServiceListRequest_WithDuplicateServices_ReturnsDistinctServices()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(new List<ServiceSpecification>
        {
            new ServiceSpecification { ServiceName = "UserService", Routes = [] },
            new ServiceSpecification { ServiceName = "UserService", Routes = [] },
            new ServiceSpecification { ServiceName = "OrderService", Routes = [] }
        });

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        // Should have count of 2, not 3
        Assert.Contains("\"count\":2", responseBody.ToLower());
    }

    [Fact]
    public async Task InvokeAsync_ServiceListRequest_WithException_ReturnsInternalServerError()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(_ => throw new InvalidOperationException("Test exception"));

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Internal server error", responseBody);
    }

    [Fact]
    public async Task InvokeAsync_ServiceListRequest_WithEmptyServices_ReturnsEmptyList()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"count\":0", responseBody.ToLower());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InvokeAsync_ServiceSpecRequest_WithException_ReturnsInternalServerError()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs/test-service");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(_ => throw new InvalidOperationException("Test exception"));

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
    }



    #endregion

    #region Path Processing Tests

    [Theory]
    [InlineData("/api-docs/User Management")]
    [InlineData("/api-docs/user_service")]
    [InlineData("/api-docs/UserService")]
    public async Task InvokeAsync_WithServiceName_HandlesRequest(string requestPath)
    {
        // Arrange
        var context = CreateHttpContext(requestPath);
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        var services = new List<ServiceSpecification>();
        mockServiceAnalyzer.AnalyzeServices().Returns(services);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = serviceCollection.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        // Middleware should not call next middleware - it handles the request
        await _next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
    }

    #endregion

    #region Helper Methods

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        return context;
    }

    #endregion
}
