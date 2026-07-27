using Xunit;

namespace ChromaticityDotNet.Tests;

public class ChromaticityDotNetCoreTests
{
    [Fact]
    public void ExposedVersionMatchesAssemblyVersion()
    {
        string expected =
            typeof(ChromaticityDotNetCore).Assembly.GetName().Version!.ToString();

        Assert.Equal(expected, ChromaticityDotNetCore.Version);
        Assert.False(string.IsNullOrWhiteSpace(ChromaticityDotNetCore.Version));
    }
}
