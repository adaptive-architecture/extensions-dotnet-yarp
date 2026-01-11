#nullable enable

using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Configuration;

public class OpenApiAggregationOptionsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var options = new OpenApiAggregationOptions();

        Assert.Equal("/swagger/v1/swagger.json", options.DefaultOpenApiPath);
        Assert.True(options.EnableAutoDiscovery);
        Assert.Equal(TimeSpan.FromMinutes(5), options.CacheDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), options.AggregatedSpecCacheDuration);
        Assert.Equal(TimeSpan.FromMinutes(1), options.FailureCacheDuration);
        Assert.Equal(1024 * 1024, options.MaximumCachePayloadBytes);
        Assert.Equal(10, options.MaxConcurrentFetches);
        Assert.Equal(10_000, options.DefaultFetchTimeoutMs);
        Assert.Equal(NonAnalyzableTransformStrategy.IncludeWithWarning, options.NonAnalyzableStrategy);
        Assert.True(options.LogTransformWarnings);
    }

    [Fact]
    public void Constructor_InitializesFallbackPathsWithExpectedValues()
    {
        var options = new OpenApiAggregationOptions();

        Assert.NotNull(options.FallbackPaths);
        Assert.Equal(4, options.FallbackPaths.Length);
        Assert.Contains("/api/v1/openapi.json", options.FallbackPaths);
        Assert.Contains("/openapi.json", options.FallbackPaths);
        Assert.Contains("/docs/openapi.json", options.FallbackPaths);
        Assert.Contains("/swagger/openapi.json", options.FallbackPaths);
    }

    [Fact]
    public void DefaultOpenApiPath_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            DefaultOpenApiPath = "/custom/openapi.json"
        };

        Assert.Equal("/custom/openapi.json", options.DefaultOpenApiPath);
    }

    [Fact]
    public void EnableAutoDiscovery_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            EnableAutoDiscovery = false
        };

        Assert.False(options.EnableAutoDiscovery);
    }

    [Fact]
    public void CacheDuration_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            CacheDuration = TimeSpan.FromMinutes(10)
        };

        Assert.Equal(TimeSpan.FromMinutes(10), options.CacheDuration);
    }

    [Fact]
    public void AggregatedSpecCacheDuration_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            AggregatedSpecCacheDuration = TimeSpan.FromMinutes(15)
        };

        Assert.Equal(TimeSpan.FromMinutes(15), options.AggregatedSpecCacheDuration);
    }

    [Fact]
    public void FailureCacheDuration_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            FailureCacheDuration = TimeSpan.FromSeconds(30)
        };

        Assert.Equal(TimeSpan.FromSeconds(30), options.FailureCacheDuration);
    }

    [Fact]
    public void MaximumCachePayloadBytes_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            MaximumCachePayloadBytes = 2048
        };

        Assert.Equal(2048, options.MaximumCachePayloadBytes);
    }

    [Fact]
    public void MaxConcurrentFetches_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            MaxConcurrentFetches = 20
        };

        Assert.Equal(20, options.MaxConcurrentFetches);
    }

    [Fact]
    public void DefaultFetchTimeoutMs_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            DefaultFetchTimeoutMs = 5000
        };

        Assert.Equal(5000, options.DefaultFetchTimeoutMs);
    }

    [Fact]
    public void FallbackPaths_CanBeSet()
    {
        var customPaths = new[] { "/custom1.json", "/custom2.json" };
        var options = new OpenApiAggregationOptions
        {
            FallbackPaths = customPaths
        };

        Assert.Equal(customPaths, options.FallbackPaths);
    }

    [Fact]
    public void NonAnalyzableStrategy_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            NonAnalyzableStrategy = NonAnalyzableTransformStrategy.ExcludeWithWarning
        };

        Assert.Equal(NonAnalyzableTransformStrategy.ExcludeWithWarning, options.NonAnalyzableStrategy);
    }

    [Fact]
    public void LogTransformWarnings_CanBeSet()
    {
        var options = new OpenApiAggregationOptions
        {
            LogTransformWarnings = false
        };

        Assert.False(options.LogTransformWarnings);
    }

    [Theory]
    [InlineData(NonAnalyzableTransformStrategy.IncludeWithWarning)]
    [InlineData(NonAnalyzableTransformStrategy.ExcludeWithWarning)]
    [InlineData(NonAnalyzableTransformStrategy.SkipService)]
    public void NonAnalyzableStrategy_AcceptsAllEnumValues(NonAnalyzableTransformStrategy strategy)
    {
        var options = new OpenApiAggregationOptions
        {
            NonAnalyzableStrategy = strategy
        };

        Assert.Equal(strategy, options.NonAnalyzableStrategy);
    }
}

public class NonAnalyzableTransformStrategyTests
{
    [Fact]
    public void NonAnalyzableTransformStrategy_HasExpectedValues()
    {
        Assert.Equal(0, (int)NonAnalyzableTransformStrategy.IncludeWithWarning);
        Assert.Equal(1, (int)NonAnalyzableTransformStrategy.ExcludeWithWarning);
        Assert.Equal(2, (int)NonAnalyzableTransformStrategy.SkipService);
    }

    [Fact]
    public void NonAnalyzableTransformStrategy_AllValuesAreDefined()
    {
        var values = Enum.GetValues<NonAnalyzableTransformStrategy>();

        Assert.Equal(3, values.Length);
        Assert.Contains(NonAnalyzableTransformStrategy.IncludeWithWarning, values);
        Assert.Contains(NonAnalyzableTransformStrategy.ExcludeWithWarning, values);
        Assert.Contains(NonAnalyzableTransformStrategy.SkipService, values);
    }

    [Theory]
    [InlineData(NonAnalyzableTransformStrategy.IncludeWithWarning, "IncludeWithWarning")]
    [InlineData(NonAnalyzableTransformStrategy.ExcludeWithWarning, "ExcludeWithWarning")]
    [InlineData(NonAnalyzableTransformStrategy.SkipService, "SkipService")]
    public void NonAnalyzableTransformStrategy_HasExpectedNames(NonAnalyzableTransformStrategy value, string expectedName)
    {
        Assert.Equal(expectedName, value.ToString());
    }
}
