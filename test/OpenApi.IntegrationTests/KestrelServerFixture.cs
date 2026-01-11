using System.Net;
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.IntegrationTests;

/// <summary>
/// Test fixture that starts actual Kestrel servers for UserService, ProductService, and Gateway.
/// Servers run on dynamic ports and are shared across all tests in the collection.
/// This enables true end-to-end integration testing with real HTTP communication.
/// </summary>
public sealed class KestrelServerFixture : IAsyncLifetime
{
    private WebApplication _userServiceApp = null!;
    private WebApplication _productServiceApp = null!;
    private WebApplication _gatewayApp = null!;

    public string UserServiceUrl { get; private set; } = String.Empty;
    public string ProductServiceUrl { get; private set; } = String.Empty;
    public string GatewayUrl { get; private set; } = String.Empty;

    public HttpClient GatewayClient { get; private set; } = null!;
    public HttpClient UserServiceClient { get; private set; } = null!;
    public HttpClient ProductServiceClient { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // Start backend services
        await StartUserServiceAsync();
        await StartProductServiceAsync();

        // Start gateway with backend URLs
        await StartGatewayAsync();

        // Create clients
        GatewayClient = new HttpClient { BaseAddress = new Uri(GatewayUrl) };
        UserServiceClient = new HttpClient { BaseAddress = new Uri(UserServiceUrl) };
        ProductServiceClient = new HttpClient { BaseAddress = new Uri(ProductServiceUrl) };
    }

    public async ValueTask DisposeAsync()
    {
        GatewayClient?.Dispose();
        UserServiceClient?.Dispose();
        ProductServiceClient?.Dispose();

        if (_gatewayApp != null)
        {
            await _gatewayApp.StopAsync();
            await _gatewayApp.DisposeAsync();
        }

        if (_userServiceApp != null)
        {
            await _userServiceApp.StopAsync();
            await _userServiceApp.DisposeAsync();
        }

        if (_productServiceApp != null)
        {
            await _productServiceApp.StopAsync();
            await _productServiceApp.DisposeAsync();
        }
    }

    private async Task StartUserServiceAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // Configure Kestrel to listen on dynamic port
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        // Add UserService configuration
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(Samples.UserService.Program).Assembly);

        _userServiceApp = builder.Build();

        // Disable caching
        _userServiceApp.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
                return Task.CompletedTask;
            });
            await next();
        });

        // Serve pre-generated OpenAPI spec as static file
        var openApiPath = Path.Combine(AppContext.BaseDirectory, "TestData", "UserService.openapi.json");
        _userServiceApp.MapGet("/swagger/v1/swagger.json", async () =>
        {
            var json = await File.ReadAllTextAsync(openApiPath);
            return Results.Content(json, "application/json");
        });

        _userServiceApp.MapControllers();

        await _userServiceApp.StartAsync();
        UserServiceUrl = _userServiceApp.Urls.First();
    }

    private async Task StartProductServiceAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(Samples.ProductService.Program).Assembly);

        _productServiceApp = builder.Build();

        _productServiceApp.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
                return Task.CompletedTask;
            });
            await next();
        });

        // Serve pre-generated OpenAPI spec as static file
        var openApiPath = Path.Combine(AppContext.BaseDirectory, "TestData", "ProductService.openapi.json");
        _productServiceApp.MapGet("/swagger/v1/swagger.json", async () =>
        {
            var json = await File.ReadAllTextAsync(openApiPath);
            return Results.Content(json, "application/json");
        });

        _productServiceApp.MapControllers();

        await _productServiceApp.StartAsync();
        ProductServiceUrl = _productServiceApp.Urls.First();
    }

    private async Task StartGatewayAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        // Configure YARP with backend URLs
        var config = new Dictionary<string, string>
        {
            ["ReverseProxy:Clusters:user-service:Destinations:destination1:Address"] = UserServiceUrl,
            ["ReverseProxy:Clusters:product-service:Destinations:destination1:Address"] = ProductServiceUrl,
            ["ReverseProxy:Routes:users-get-all:ClusterId"] = "user-service",
            ["ReverseProxy:Routes:users-get-all:Match:Path"] = "/api/users",
            ["ReverseProxy:Routes:users-get-all:Metadata:Ada.OpenApi"] = "{\"serviceName\":\"User Management\",\"enabled\":true}",
            ["ReverseProxy:Routes:users-with-id:ClusterId"] = "user-service",
            ["ReverseProxy:Routes:users-with-id:Match:Path"] = "/api/users/{id}",
            ["ReverseProxy:Routes:users-with-id:Metadata:Ada.OpenApi"] = "{\"serviceName\":\"User Management\",\"enabled\":true}",
            ["ReverseProxy:Routes:products-get-all:ClusterId"] = "product-service",
            ["ReverseProxy:Routes:products-get-all:Match:Path"] = "/api/products",
            ["ReverseProxy:Routes:products-get-all:Metadata:Ada.OpenApi"] = "{\"serviceName\":\"Product Catalog\",\"enabled\":true}",
            ["ReverseProxy:Routes:products-with-id:ClusterId"] = "product-service",
            ["ReverseProxy:Routes:products-with-id:Match:Path"] = "/api/products/{id}",
            ["ReverseProxy:Routes:products-with-id:Metadata:Ada.OpenApi"] = "{\"serviceName\":\"Product Catalog\",\"enabled\":true}",
            ["ReverseProxy:Clusters:user-service:Metadata:Ada.OpenApi"] = "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"UserService\"}",
            ["ReverseProxy:Clusters:product-service:Metadata:Ada.OpenApi"] = "{\"openApiPath\":\"/swagger/v1/swagger.json\",\"prefix\":\"ProductService\"}"
        };

        builder.Configuration.AddInMemoryCollection(config);

        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddYarpOpenApiAggregation();

        _gatewayApp = builder.Build();

        _gatewayApp.UseYarpOpenApiAggregation("/api-docs");

        // Add cache invalidation endpoints (from Gateway Program.cs)
        _gatewayApp.MapPost("/admin/cache/invalidate/{serviceName}", async (
            string serviceName,
            Caching.IOpenApiCacheInvalidator invalidator) =>
        {
            await invalidator.InvalidateServiceAsync(serviceName);
            return Results.Ok(new { message = $"Cache invalidated for service: {serviceName}" });
        });

        _gatewayApp.MapPost("/admin/cache/invalidate-all", async (Caching.IOpenApiCacheInvalidator invalidator) =>
        {
            await invalidator.InvalidateAllAsync();
            return Results.Ok(new { message = "All cache entries invalidated" });
        });

        _gatewayApp.MapReverseProxy();

        await _gatewayApp.StartAsync();
        GatewayUrl = _gatewayApp.Urls.First();
    }
}

/// <summary>
/// Xunit collection definition for tests that use the KestrelServerFixture.
/// All tests in this collection will share the same server instances.
/// </summary>
[CollectionDefinition(nameof(KestrelServerCollection))]
public class KestrelServerCollection : ICollectionFixture<KestrelServerFixture>;
