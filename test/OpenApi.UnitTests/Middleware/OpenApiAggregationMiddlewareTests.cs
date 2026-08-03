using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using AdaptArch.Extensions.Yarp.OpenApi.Merging;
using AdaptArch.Extensions.Yarp.OpenApi.Middleware;
using AdaptArch.Extensions.Yarp.OpenApi.Pruning;
using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using NSubstitute;
using Xunit;
using Yarp.ReverseProxy.Configuration;

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
    public async Task InvokeAsync_ServiceNotFound_Returns404WithoutTouchingCache()
    {
        // Arrange: only the analyzer is registered — an unknown service must be rejected
        // before the cache (or any other dependency) is resolved.
        var context = CreateHttpContext("/api-docs/nonexistent-service");

        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middlewareWithTestLogger.InvokeAsync(context);

        // Assert
        Assert.Equal(404, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ServiceNotFound_DoesNotEchoServiceNameInResponse()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs/some-unknown-name");

        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middlewareWithTestLogger.InvokeAsync(context);

        // Assert
        Assert.Equal(404, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("some-unknown-name", responseBody);
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

    #region Security Tests

    [Theory]
    [InlineData("/api-docsomething")]
    [InlineData("/api-docs2/my-service")]
    [InlineData("/api-docs-internal")]
    public async Task InvokeAsync_WithPathSharingBasePathPrefix_CallsNextMiddleware(string path)
    {
        // Arrange: paths that share the base path prefix but are not below it must pass through.
        var context = CreateHttpContext(path);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_ServiceListRequest_WithException_DoesNotLeakExceptionDetails()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(_ => throw new InvalidOperationException("sensitive connection string details"));

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("sensitive connection string details", responseBody);
    }

    [Fact]
    public async Task InvokeAsync_ServiceSpecRequest_WithException_DoesNotLeakExceptionDetails()
    {
        // Arrange
        var context = CreateHttpContext("/api-docs/test-service");
        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns(_ => throw new InvalidOperationException("sensitive connection string details"));

        var services = new ServiceCollection();
        services.AddSingleton(mockServiceAnalyzer);
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("sensitive connection string details", responseBody);
    }

    [Fact]
    public async Task InvokeAsync_ServiceSpecRequest_WithOverlongServiceName_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateHttpContext($"/api-docs/{new string('a', 1000)}");
        var services = new ServiceCollection();
        context.RequestServices = services.BuildServiceProvider();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ServiceSpecRequest_CaseVariantsShareOneCacheEntry()
    {
        // Arrange: requests differing only in casing must resolve to the canonical service
        // name so they share a single cache entry (and failed aggregations are cached too).
        var serviceProvider = BuildFullServiceProvider(out var mockFetcher);

        var context1 = CreateHttpContext("/api-docs/TestService");
        context1.RequestServices = serviceProvider;
        var context2 = CreateHttpContext("/api-docs/testservice");
        context2.RequestServices = serviceProvider;

        // Act
        await _middleware.InvokeAsync(context1);
        await _middleware.InvokeAsync(context2);

        // Assert: both requests 404 (fetch fails) but aggregation ran only once.
        Assert.Equal(404, context1.Response.StatusCode);
        Assert.Equal(404, context2.Response.StatusCode);
        await mockFetcher.Received(1).FetchDocumentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static ServiceProvider BuildFullServiceProvider(
        out IOpenApiDocumentFetcher mockFetcher,
        Action<OpenApiAggregationOptions> configureOptions = null,
        int clusterCount = 1,
        Func<Task<OpenApiDocument>> fetchHandler = null)
    {
        var mappings = new List<RouteClusterMapping>();
        for (var i = 0; i < clusterCount; i++)
        {
            var routeConfig = new RouteConfig
            {
                RouteId = $"test-route-{i}",
                ClusterId = $"test-cluster-{i}",
                Match = new RouteMatch { Path = $"/test{i}/{{**catch-all}}" }
            };
            var clusterConfig = new ClusterConfig
            {
                ClusterId = $"test-cluster-{i}",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["primary"] = new DestinationConfig { Address = $"http://localhost:500{i}" }
                }
            };
            mappings.Add(new RouteClusterMapping
            {
                Route = routeConfig,
                Cluster = clusterConfig,
                RouteOpenApiConfig = new AdaOpenApiRouteConfig { ServiceName = "TestService" },
                ClusterOpenApiConfig = new AdaOpenApiClusterConfig()
            });
        }

        var serviceSpec = new ServiceSpecification
        {
            ServiceName = "TestService",
            Routes = mappings
        };

        var mockServiceAnalyzer = Substitute.For<IServiceSpecificationAnalyzer>();
        mockServiceAnalyzer.AnalyzeServices().Returns([serviceSpec]);

        var fetcher = Substitute.For<IOpenApiDocumentFetcher>();
        fetcher.FetchDocumentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => fetchHandler != null ? fetchHandler() : Task.FromResult<OpenApiDocument>(null));

        var services = new ServiceCollection();
        services.AddOptions();
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        services.AddHybridCache();
        services.AddSingleton(mockServiceAnalyzer);
        services.AddSingleton(fetcher);
        services.AddSingleton(Substitute.For<IOpenApiMerger>());
        services.AddSingleton(Substitute.For<IPathReachabilityAnalyzer>());
        services.AddSingleton(Substitute.For<IOpenApiDocumentPruner>());
        services.AddSingleton(Substitute.For<ISchemaRenamer>());

        mockFetcher = fetcher;
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InvokeAsync_ServiceSpecRequest_UsesConfiguredDefaultOpenApiPath()
    {
        // Arrange: a cluster without an explicit OpenApiPath must fall back to the
        // configured OpenApiAggregationOptions.DefaultOpenApiPath.
        var serviceProvider = BuildFullServiceProvider(
            out var mockFetcher,
            configureOptions: o => o.DefaultOpenApiPath = "/custom/openapi.json");

        var context = CreateHttpContext("/api-docs/TestService");
        context.RequestServices = serviceProvider;

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        await mockFetcher.Received(1).FetchDocumentAsync(Arg.Any<string>(), "/custom/openapi.json", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ServiceSpecRequest_LimitsConcurrentClusterFetches()
    {
        // Arrange: 4 clusters with MaxConcurrentFetches = 2 must fetch in parallel,
        // but never more than 2 at a time.
        var concurrent = 0;
        var maxConcurrent = 0;

        var serviceProvider = BuildFullServiceProvider(
            out _,
            configureOptions: o => o.MaxConcurrentFetches = 2,
            clusterCount: 4,
            fetchHandler: async () =>
            {
                var current = Interlocked.Increment(ref concurrent);
                int snapshot;
                do
                {
                    snapshot = Volatile.Read(ref maxConcurrent);
                }
                while (current > snapshot && Interlocked.CompareExchange(ref maxConcurrent, current, snapshot) != snapshot);

                await Task.Delay(250);
                _ = Interlocked.Decrement(ref concurrent);
                return null;
            });

        var context = CreateHttpContext("/api-docs/TestService");
        context.RequestServices = serviceProvider;

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(2, maxConcurrent);
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
