
using Xunit;

namespace AdaptArch.Extensions.Yarp.Auth.UnitTests;

public class AuthModuleTests
{
    [Fact]
    public void Name_ReturnsAuth()
    {
        Assert.Equal("Auth", AuthModule.Name);
    }
}
