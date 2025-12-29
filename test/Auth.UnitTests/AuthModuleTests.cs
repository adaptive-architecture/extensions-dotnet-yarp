namespace AdaptArch.Extensions.Yarp.Auth.UnitTests;

using Xunit;

public class AuthModuleTests
{
    [Fact]
    public void Name_ReturnsAuth()
    {
        Assert.Equal("Auth", AuthModule.Name);
    }
}
