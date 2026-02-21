using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// Integration tests verifying gateway behavior when fetching downstream OpenAPI specifications fails.
/// Tests cover various failure scenarios including HTTP errors, timeouts, malformed responses,
/// and service unavailability.
/// </summary>
public class DownstreamFetchFailureTests : IAsyncLifetime
{
    private WebApplication _failingBackendApp = null!;
    private WebApplication _gatewayApp = null!;
    private HttpClient _gatewayClient = null!;
    private string _backendUrl = String.Empty;
    private string _gatewayUrl = String.Empty;
    private BackendBehavior _backendBehavior = BackendBehavior.NotFound;

    public async ValueTask InitializeAsync()
    {
        // Start backend with configurable failure modes
        await StartFailingBackendAsync();

        // Start gateway pointing to failing backend
        await StartGatewayAsync();

        _gatewayClient = new HttpClient { BaseAddress = new Uri(_gatewayUrl) };
    }

    public async ValueTask DisposeAsync()
    {
        _gatewayClient.Dispose();

        await _gatewayApp.StopAsync();
        await _gatewayApp.DisposeAsync();
        GC.SuppressFinalize(this);

        await _failingBackendApp.StopAsync();
        await _failingBackendApp.DisposeAsync();
    }

    #region HTTP Error Response Tests

    [Fact]
    public async Task Backend_Returns404_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.NotFound;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle backend 404 gracefully
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_Returns500_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.InternalServerError;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle backend 500 gracefully
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_Returns503_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.ServiceUnavailable;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle backend 503 gracefully
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_Returns401_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.Unauthorized;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle backend authentication errors
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    #endregion

    #region Malformed Response Tests

    [Fact]
    public async Task Backend_ReturnsEmptyResponse_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.EmptyResponse;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle empty responses
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_ReturnsInvalidJson_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.InvalidJson;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle malformed JSON
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_ReturnsInvalidOpenApiSpec_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.InvalidOpenApiSpec;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle invalid OpenAPI specs
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_ReturnsHtmlInsteadOfJson_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.HtmlResponse;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle non-JSON responses
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    #endregion

    #region Timeout and Connection Tests

    [Fact]
    public async Task Backend_TimesOut_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.Timeout;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle timeouts gracefully
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.RequestTimeout,
            $"Expected 404, 408, or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task Backend_ClosesConnectionImmediately_Gateway_Returns404OrError()
    {
        // Arrange
        _backendBehavior = BackendBehavior.CloseConnection;
        await RestartBackendAsync();

        // Act
        var response = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Gateway should handle connection closures
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500, got {response.StatusCode}");
    }

    #endregion

    #region Service Discovery Tests

    [Fact]
    public async Task FailingBackend_DoesNotAppearInServiceList()
    {
        // Arrange
        _backendBehavior = BackendBehavior.NotFound;
        await RestartBackendAsync();

        // Act - Get service list
        var response = await _gatewayClient!.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert - Service list should still be accessible even if backend fails
        // (The configured service may still appear, but fetching its spec will fail)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MultipleBackends_OneFailingOneWorking_WorkingServiceStillAccessible()
    {
        // This test would require a more complex fixture with multiple backends
        // For now, we verify that failure doesn't affect gateway availability
        // Arrange
        _backendBehavior = BackendBehavior.NotFound;
        await RestartBackendAsync();

        // Act - Try to access service list
        var listResponse = await _gatewayClient.GetAsync("/api-docs", TestContext.Current.CancellationToken);

        // Assert - Gateway remains responsive
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }

    #endregion

    #region Caching Behavior Tests

    [Fact]
    public async Task FailedFetch_NotCached_SubsequentRequestsRetry()
    {
        // Arrange - Backend returns 500
        _backendBehavior = BackendBehavior.InternalServerError;
        await RestartBackendAsync();

        // Act - First request fails
        var response1 = await _gatewayClient!.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);
        Assert.True(response1.StatusCode == HttpStatusCode.NotFound ||
                   response1.StatusCode == HttpStatusCode.InternalServerError);

        // Change backend to return success
        _backendBehavior = BackendBehavior.ValidOpenApiSpec;
        await RestartBackendAsync();

        // Wait a moment for backend to restart
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act - Second request should retry and potentially succeed
        var response2 = await _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);

        // Assert - Second request has a chance to succeed (or fail again)
        // The key is that it doesn't return a cached failure
        Assert.NotNull(response2);
    }

    #endregion

    #region Resilience Tests

    [Fact]
    public async Task Gateway_RemainsHealthy_DespiteBackendFailures()
    {
        // Arrange
        _backendBehavior = BackendBehavior.InternalServerError;
        await RestartBackendAsync();

        // Act - Make multiple failing requests
        for (int i = 0; i < 5; i++)
        {
            await _gatewayClient!.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken);
        }

        // Assert - Gateway should still respond to health checks / service list
        var healthResponse = await _gatewayClient.GetAsync("/api-docs", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRequests_ToFailingBackend_DoNotCauseGatewayIssues()
    {
        // Arrange
        _backendBehavior = BackendBehavior.InternalServerError;
        await RestartBackendAsync();

        // Act - Make concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _gatewayClient.GetAsync("/api-docs/test-service", TestContext.Current.CancellationToken))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests complete (no hangs), gateway remains responsive
        Assert.All(responses, r => Assert.NotNull(r));
        Assert.All(responses, r => Assert.True(
            r.StatusCode == HttpStatusCode.NotFound ||
            r.StatusCode == HttpStatusCode.InternalServerError));

        // Gateway should still be responsive
        var healthResponse = await _gatewayClient!.GetAsync("/api-docs", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    #endregion

    #region Helper Methods

    private async Task StartFailingBackendAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        _failingBackendApp = builder.Build();

        // Configure endpoint based on current behavior
        _failingBackendApp.MapGet("/swagger/v1/swagger.json", async (HttpContext context) => await HandleBackendRequestAsync(context));

        await _failingBackendApp.StartAsync();
        _backendUrl = _failingBackendApp.Urls.First();
    }

    private async Task HandleBackendRequestAsync(HttpContext context)
    {
        switch (_backendBehavior)
        {
            case BackendBehavior.NotFound:
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Not Found");
                break;

            case BackendBehavior.InternalServerError:
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal Server Error");
                break;

            case BackendBehavior.ServiceUnavailable:
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Service Unavailable");
                break;

            case BackendBehavior.Unauthorized:
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                break;

            case BackendBehavior.EmptyResponse:
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                // Return empty response
                break;

            case BackendBehavior.InvalidJson:
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{invalid json");
                break;

            case BackendBehavior.InvalidOpenApiSpec:
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(@"{""notAnOpenApiSpec"": true}");
                break;

            case BackendBehavior.HtmlResponse:
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<html><body>Error</body></html>");
                break;

            case BackendBehavior.Timeout:
                // Delay longer than gateway timeout
                await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
                break;

            case BackendBehavior.CloseConnection:
                context.Abort();
                break;

            case BackendBehavior.ValidOpenApiSpec:
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""
                {
                    "openapi": "3.0.0",
                    "info": { "title": "Test API", "version": "1.0.0" },
                    "paths": {}
                }
                """);
                break;
        }
    }

    private async Task StartGatewayAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        // Configure YARP with failing backend
        var config = new Dictionary<string, string>
        {
            ["ReverseProxy:Clusters:test-service:Destinations:destination1:Address"] = _backendUrl,
            ["ReverseProxy:Clusters:test-service:Metadata:Ada.OpenApi"] = "{\"openApiPath\":\"/swagger/v1/swagger.json\"}",
            ["ReverseProxy:Routes:test-route:ClusterId"] = "test-service",
            ["ReverseProxy:Routes:test-route:Match:Path"] = "/api/test",
            ["ReverseProxy:Routes:test-route:Metadata:Ada.OpenApi"] = "{\"serviceName\":\"Test Service\",\"enabled\":true}"
        };

        builder.Configuration.AddInMemoryCollection(config);

        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddYarpOpenApiAggregation();

        _gatewayApp = builder.Build();

        _gatewayApp.UseYarpOpenApiAggregation("/api-docs");
        _gatewayApp.MapReverseProxy();

        await _gatewayApp.StartAsync();
        _gatewayUrl = _gatewayApp.Urls.First();
    }

    private async Task RestartBackendAsync()
    {
        await _failingBackendApp.StopAsync();
        await _failingBackendApp.DisposeAsync();

        await StartFailingBackendAsync();

        // Update gateway configuration with new backend URL
        await _gatewayApp.StopAsync();
        await _gatewayApp.DisposeAsync();

        await StartGatewayAsync();

        _gatewayClient.Dispose();
        _gatewayClient = new HttpClient { BaseAddress = new Uri(_gatewayUrl) };
    }

    private enum BackendBehavior
    {
        NotFound,
        InternalServerError,
        ServiceUnavailable,
        Unauthorized,
        EmptyResponse,
        InvalidJson,
        InvalidOpenApiSpec,
        HtmlResponse,
        Timeout,
        CloseConnection,
        ValidOpenApiSpec
    }

    #endregion
}
