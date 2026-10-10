using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ChromaticityMatchTests
{
    [Theory]
    [InlineData(Standardilluminant.D65, typeof(D65))]
    [InlineData(Standardilluminant.A, typeof(A))]
    [InlineData(Standardilluminant.CWF, typeof(CWF))]
    [InlineData(Standardilluminant.F7, typeof(F7))]
    [InlineData(Standardilluminant.TL84, typeof(TL84))]
    [InlineData(Standardilluminant.U30, typeof(U30))]
    public void RegistryReturnsRequestedIlluminant(Standardilluminant name, Type expectedType)
    {
        IStandardilluminant result = ChromaticityMatch.GetStandardilluminantdata(name);

        Assert.IsType(expectedType, result);
        Assert.Equal(name, result.IlluminantName);
    }

    [Fact]
    public void UnknownIlluminantFallsBackToD65()
    {
        IStandardilluminant result =
            ChromaticityMatch.GetStandardilluminantdata((Standardilluminant)999);

        Assert.IsType<D65>(result);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, 95.0471, 100.0, 108.8829)]
    [InlineData(StandardObserver.Degree10, 94.8111, 100.0, 107.3047)]
    public void D65WhitePointMatchesObserver(
        StandardObserver observer,
        double expectedX,
        double expectedY,
        double expectedZ)
    {
        var result = ChromaticityMatch.GetStandardWhitePoint(Standardilluminant.D65, observer);

        Assert.InRange(Math.Abs(expectedX - result.CIEX), 0, 0.00005);
        Assert.Equal(expectedY, result.CIEY);
        Assert.InRange(Math.Abs(expectedZ - result.CIEZ), 0, 0.00005);
    }
}
