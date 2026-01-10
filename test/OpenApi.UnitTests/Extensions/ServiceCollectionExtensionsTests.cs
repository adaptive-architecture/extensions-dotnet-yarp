#nullable enable

using AdaptArch.Extensions.Yarp.OpenApi.Analysis;
using AdaptArch.Extensions.Yarp.OpenApi.Caching;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Extensions;
using AdaptArch.Extensions.Yarp.OpenApi.Fetching;
using AdaptArch.Extensions.Yarp.OpenApi.Merging;
using AdaptArch.Extensions.Yarp.OpenApi.Pruning;
using AdaptArch.Extensions.Yarp.OpenApi.Renaming;
using AdaptArch.Extensions.Yarp.OpenApi.Transforms;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddYarpOpenApiAggregation_WithNullServices_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddYarpOpenApiAggregation(null!));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddYarpOpenApiAggregation_RegistersExpectedSingletonServices()
    {
        var services = new ServiceCollection();

        services.AddYarpOpenApiAggregation();

        AssertContainsSingleton<IYarpOpenApiConfigurationReader, YarpOpenApiConfigurationReader>(services);
        AssertContainsSingleton<IRouteTransformAnalyzer, RouteTransformAnalyzer>(services);
        AssertContainsSingleton<IServiceSpecificationAnalyzer, ServiceSpecificationAnalyzer>(services);

        AssertContainsSingleton<IOpenApiDocumentFetcher, OpenApiDocumentFetcher>(services);
        AssertContainsSingleton<IPathReachabilityAnalyzer, PathReachabilityAnalyzer>(services);
        AssertContainsSingleton<IOpenApiDocumentPruner, OpenApiDocumentPruner>(services);
        AssertContainsSingleton<ISchemaRenamer, SchemaRenamer>(services);
        AssertContainsSingleton<IOpenApiMerger, OpenApiMerger>(services);

        AssertContainsSingleton<IOpenApiCacheInvalidator, OpenApiCacheInvalidator>(services);
    }

    [Fact]
    public void AddYarpOpenApiAggregation_DoesNotOverrideExistingRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOpenApiDocumentFetcher, CustomOpenApiDocumentFetcher>();

        services.AddYarpOpenApiAggregation();

        var descriptors = services
            .Where(d => d.ServiceType == typeof(IOpenApiDocumentFetcher))
            .ToArray();

        Assert.Single(descriptors);
        Assert.Equal(typeof(CustomOpenApiDocumentFetcher), descriptors[0].ImplementationType);
    }

    [Fact]
    public void AddYarpOpenApiAggregation_MapsOptionsIntoHybridCacheConfiguration()
    {
        var services = new ServiceCollection();

        services.AddYarpOpenApiAggregation(options =>
        {
            options.CacheDuration = TimeSpan.FromSeconds(7);
            options.MaximumCachePayloadBytes = 123;
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OpenApiAggregationOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(7), options.CacheDuration);
        Assert.Equal(123, options.MaximumCachePayloadBytes);

        var hybridCacheOptions = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;
        Assert.Equal(123, hybridCacheOptions.MaximumPayloadBytes);

        Assert.NotNull(hybridCacheOptions.DefaultEntryOptions);
        var entryOptions = hybridCacheOptions.DefaultEntryOptions!;

        Assert.Equal(TimeSpan.FromSeconds(7), entryOptions.Expiration);
        Assert.Equal(TimeSpan.FromSeconds(7), entryOptions.LocalCacheExpiration);

        Assert.NotNull(provider.GetRequiredService<HybridCache>());
        Assert.NotNull(provider.GetRequiredService<IHttpClientFactory>());
    }

    private static void AssertContainsSingleton<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(TService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
    }

    private sealed class CustomOpenApiDocumentFetcher : IOpenApiDocumentFetcher
    {
        public Task<Microsoft.OpenApi.OpenApiDocument?> FetchDocumentAsync(
            string baseUrl,
            string openApiPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Microsoft.OpenApi.OpenApiDocument?>(null);
        }
    }
}
