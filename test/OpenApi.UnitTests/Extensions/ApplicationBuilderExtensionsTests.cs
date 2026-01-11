#nullable enable

using AdaptArch.Extensions.Yarp.OpenApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AppBuilderExtensions = AdaptArch.Extensions.Yarp.OpenApi.Extensions.ApplicationBuilderExtensions;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Extensions;

public class ApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseYarpOpenApiAggregation_WithNullApp_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            AppBuilderExtensions.UseYarpOpenApiAggregation(null!));

        Assert.Equal("app", exception.ParamName);
    }

    [Fact]
    public void UseYarpOpenApiAggregation_WithNullBasePath_ThrowsArgumentNullException()
    {
        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.UseYarpOpenApiAggregation(null!));

        Assert.Equal("basePath", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void UseYarpOpenApiAggregation_WithWhiteSpaceBasePath_ThrowsArgumentException(string basePath)
    {
        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.UseYarpOpenApiAggregation(basePath));

        Assert.Equal("basePath", exception.ParamName);
    }

    [Fact]
    public void UseYarpOpenApiAggregation_WithDefaultBasePath_RegistersMiddleware()
    {
        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        var result = builder.UseYarpOpenApiAggregation();

        Assert.Same(builder, result);
    }

    [Fact]
    public void UseYarpOpenApiAggregation_WithCustomBasePath_RegistersMiddleware()
    {
        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        var result = builder.UseYarpOpenApiAggregation("/custom-docs");

        Assert.Same(builder, result);
    }

    [Theory]
    [InlineData("api-docs")]
    [InlineData("/api-docs")]
    [InlineData("custom/path")]
    [InlineData("/custom/path")]
    public void UseYarpOpenApiAggregation_WithVariousBasePaths_DoesNotThrow(string input)
    {
        var services = new ServiceCollection();
        services.AddYarpOpenApiAggregation();

        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        var exception = Record.Exception(() => builder.UseYarpOpenApiAggregation(input));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("/api-docs/")]
    [InlineData("/custom/path/")]
    [InlineData("api-docs/")]
    public void UseYarpOpenApiAggregation_WithTrailingSlash_DoesNotThrow(string input)
    {
        var services = new ServiceCollection();
        services.AddYarpOpenApiAggregation();

        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        var exception = Record.Exception(() => builder.UseYarpOpenApiAggregation(input));

        Assert.Null(exception);
    }

    [Fact]
    public void UseYarpOpenApiAggregation_ReturnsApplicationBuilder_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddYarpOpenApiAggregation();

        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        var result = builder
            .UseYarpOpenApiAggregation()
            .UseYarpOpenApiAggregation("/another-path");

        Assert.Same(builder, result);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/a")]
    [InlineData("/api")]
    [InlineData("/very/long/path/with/multiple/segments")]
    public void UseYarpOpenApiAggregation_AcceptsValidPaths(string basePath)
    {
        var services = new ServiceCollection();
        services.AddYarpOpenApiAggregation();

        var builder = new ApplicationBuilder(services.BuildServiceProvider());

        var exception = Record.Exception(() => builder.UseYarpOpenApiAggregation(basePath));

        Assert.Null(exception);
    }
}
