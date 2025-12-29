namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests;

using Xunit;

public class OpenApiModuleTests
{
    [Fact]
    public void Name_ReturnsOpenApi()
    {
        Assert.Equal("OpenApi", OpenApiModule.Name);
    }
}
