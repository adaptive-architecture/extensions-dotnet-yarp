using AdaptArch.Extensions.Yarp.OpenApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add YARP OpenAPI Aggregation
builder.Services.AddYarpOpenApiAggregation(options => options.CacheDuration = TimeSpan.FromMinutes(5));

var app = builder.Build();

// Use YARP OpenAPI Aggregation middleware
app.UseYarpOpenApiAggregation("/api-docs");

// Map YARP reverse proxy
app.MapReverseProxy();

app.Run();
