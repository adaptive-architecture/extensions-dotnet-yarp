using AdaptArch.Extensions.Yarp.OpenApi.Caching;
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add YARP OpenAPI Aggregation with HybridCache
builder.Services.AddYarpOpenApiAggregation(options =>
{
    options.CacheDuration = TimeSpan.FromMinutes(5);
    options.AggregatedSpecCacheDuration = TimeSpan.FromMinutes(5);
    options.FailureCacheDuration = TimeSpan.FromMinutes(1);
    options.MaximumCachePayloadBytes = 2 * 1024 * 1024; // 2 MB
});

var app = builder.Build();

// Disable HTTP caching for all responses (development/sample only)
app.Use(async (context, next) =>
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

// Use YARP OpenAPI Aggregation middleware
app.UseYarpOpenApiAggregation("/api-docs");

// Configure Swagger UI to display aggregated OpenAPI specs
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.ConfigObject.Urls =
    [
        new UrlDescriptor
        {
            Url = "/api-docs/user-management", Name = "User Management"
        },
        new UrlDescriptor
        {
            Url = "/api-docs/product-catalog", Name = "Product Catalog"
        }
    ];
});

// Optional: Add cache invalidation endpoints for testing/admin purposes
app.MapPost("/admin/cache/invalidate/{serviceName}", async (
    string serviceName,
    IOpenApiCacheInvalidator invalidator) =>
{
    await invalidator.InvalidateServiceAsync(serviceName);
    return Results.Ok(new { message = $"Cache invalidated for service: {serviceName}" });
});

app.MapPost("/admin/cache/invalidate-all", async (IOpenApiCacheInvalidator invalidator) =>
{
    await invalidator.InvalidateAllAsync();
    return Results.Ok(new { message = "All cache entries invalidated" });
});

// Map YARP reverse proxy
app.MapReverseProxy();

await app.RunAsync();

// Make Program class accessible for integration tests
namespace AdaptArch.Extensions.Yarp.Samples.Gateway
{
    /// <summary>
    /// Entry point for the Gateway application that aggregates OpenAPI specifications.
    /// </summary>
    public partial class Program;
}
