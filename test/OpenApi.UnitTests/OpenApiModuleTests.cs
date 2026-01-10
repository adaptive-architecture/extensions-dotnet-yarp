
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests;

public class OpenApiModuleTests
{
    [Fact]
    public void Name_ReturnsOpenApi()
    {
        Assert.Equal("OpenApi", OpenApiModule.Name);
    }
}
