var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "API for managing products in the catalog"
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
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

// Configure the HTTP request pipeline
// Always enable Swagger for this sample/demo project
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Service API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

await app.RunAsync();

// Make Program class accessible for integration tests
namespace AdaptArch.Extensions.Yarp.Samples.ProductService
{
    /// <summary>
    /// Entry point for the Product Service application.
    /// </summary>
    public partial class Program;
}
