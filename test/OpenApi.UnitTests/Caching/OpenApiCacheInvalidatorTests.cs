using AdaptArch.Extensions.Yarp.OpenApi.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Caching;

public class OpenApiCacheInvalidatorTests
{
    private readonly HybridCache _cache;
    private readonly ILogger<OpenApiCacheInvalidator> _logger;
    private readonly OpenApiCacheInvalidator _invalidator;

    public OpenApiCacheInvalidatorTests()
    {
        _cache = Substitute.For<HybridCache>();
        _logger = NullLogger<OpenApiCacheInvalidator>.Instance;
        _invalidator = new OpenApiCacheInvalidator(_cache, _logger);
    }

    [Fact]
    public async Task InvalidateServiceAsync_WithNullServiceName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _invalidator.InvalidateServiceAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidateServiceAsync_WithEmptyServiceName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _invalidator.InvalidateServiceAsync("", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidateServiceAsync_WithWhitespaceServiceName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _invalidator.InvalidateServiceAsync("   ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidateServiceAsync_WithValidServiceName_RemovesByTag()
    {
        const string serviceName = "user-service";

        await _invalidator.InvalidateServiceAsync(serviceName, TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            $"service:{serviceName}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateServiceAsync_WithValidServiceName_UsesCorrectTag()
    {
        const string serviceName = "product-service";

        await _invalidator.InvalidateServiceAsync(serviceName, TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            "service:product-service",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateServiceAsync_PropagatesCancellationToken()
    {
        const string serviceName = "test-service";

        await _invalidator.InvalidateServiceAsync(serviceName, TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            Arg.Any<string>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidateClusterAsync_WithNullClusterId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _invalidator.InvalidateClusterAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidateClusterAsync_WithEmptyClusterId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _invalidator.InvalidateClusterAsync("", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidateClusterAsync_WithWhitespaceClusterId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _invalidator.InvalidateClusterAsync("   ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidateClusterAsync_WithValidClusterId_RemovesByTag()
    {
        const string clusterId = "user-cluster";

        await _invalidator.InvalidateClusterAsync(clusterId, TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            $"cluster:{clusterId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateClusterAsync_WithValidClusterId_UsesCorrectTag()
    {
        const string clusterId = "product-cluster";

        await _invalidator.InvalidateClusterAsync(clusterId, TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            "cluster:product-cluster",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateClusterAsync_PropagatesCancellationToken()
    {
        const string clusterId = "test-cluster";

        await _invalidator.InvalidateClusterAsync(clusterId, TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            Arg.Any<string>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidateAllAsync_RemovesByWildcardTag()
    {
        await _invalidator.InvalidateAllAsync(TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            "*",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAllAsync_PropagatesCancellationToken()
    {
        await _invalidator.InvalidateAllAsync(TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveByTagAsync(
            "*",
            TestContext.Current.CancellationToken);
    }
}
