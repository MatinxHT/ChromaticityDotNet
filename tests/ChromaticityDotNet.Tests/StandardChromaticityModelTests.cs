using static ChromaticityDotNet.Model.StandardChromaticityModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class StandardChromaticityModelTests
{
    private static readonly double[] ExpectedTwoDegreeX =
    {
        0.0143, 0.0435, 0.1344, 0.2839, 0.3483, 0.3362, 0.2908, 0.1954,
        0.0956, 0.0320, 0.0049, 0.0093, 0.0633, 0.1655, 0.2904, 0.4334,
        0.5945, 0.7621, 0.9163, 1.0263, 1.0622, 1.0026, 0.8544, 0.6424,
        0.4479, 0.2835, 0.1649, 0.0874, 0.0468, 0.0227, 0.0114
    };

    private static readonly double[] ExpectedTwoDegreeY =
    {
        0.0004, 0.0012, 0.0040, 0.0116, 0.0230, 0.0380, 0.0600, 0.0910,
        0.1390, 0.2080, 0.3230, 0.5030, 0.7100, 0.8620, 0.9540, 0.9950,
        0.9950, 0.9520, 0.8700, 0.7570, 0.6310, 0.5030, 0.3810, 0.2650,
        0.1750, 0.1070, 0.0610, 0.0320, 0.0170, 0.0082, 0.0041
    };

    private static readonly double[] ExpectedTwoDegreeZ =
    {
        0.0679, 0.2074, 0.6456, 1.3856, 1.7471, 1.7721, 1.6692, 1.2876,
        0.8130, 0.4652, 0.2720, 0.1582, 0.0782, 0.0422, 0.0203, 0.0087,
        0.0039, 0.0021, 0.0017, 0.0011, 0.0008, 0.0003, 0.0002, 0.0000,
        0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000
    };

    public static IEnumerable<object[]> Illuminants()
    {
        yield return new object[] { new D65(), Standardilluminant.D65 };
        yield return new object[] { new A(), Standardilluminant.A };
        yield return new object[] { new CWF(), Standardilluminant.CWF };
        yield return new object[] { new F7(), Standardilluminant.F7 };
        yield return new object[] { new TL84(), Standardilluminant.TL84 };
        yield return new object[] { new U30(), Standardilluminant.U30 };
    }

    [Fact]
    public void TwoDegreeFunctionsMatchVerifiedCieTable()
    {
        Assert.Equal(ExpectedTwoDegreeX, CIEConstant.XX_2.Spectrums);
        Assert.Equal(ExpectedTwoDegreeY, CIEConstant.YY_2.Spectrums);
        Assert.Equal(ExpectedTwoDegreeZ, CIEConstant.ZZ_2.Spectrums);
    }

    [Theory]
    [MemberData(nameof(Illuminants))]
    public void EveryIlluminantHasConsistentWhitePointsAndSpectrum(
        IStandardilluminant illuminant,
        Standardilluminant expectedName)
    {
        Assert.Equal(expectedName, illuminant.IlluminantName);
        Assert.Equal(StandardObserver.Degree2, illuminant.WhitePoint_Degree2.Observer);
        Assert.Equal(StandardObserver.Degree10, illuminant.WhitePoint_Degree10.Observer);
        Assert.NotNull(illuminant.WhitePoint_Degree2.WhitePointXnYnZn);
        Assert.NotNull(illuminant.WhitePoint_Degree10.WhitePointXnYnZn);

        Assert.Equal(400, illuminant.Spectrum.StartingWavelength);
        Assert.Equal(700, illuminant.Spectrum.EndingWavelength);
        Assert.Equal(10, illuminant.Spectrum.WavelengthInterval);
        Assert.Equal(31, illuminant.Spectrum.Spectrums!.Length);
        Assert.All(illuminant.Spectrum.Spectrums, value => Assert.True(value >= 0));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void ObserverFunctionsHaveExpectedShape(int observer)
    {
        var x = observer == 2 ? CIEConstant.XX_2 : CIEConstant.XX_10;
        var y = observer == 2 ? CIEConstant.YY_2 : CIEConstant.YY_10;
        var z = observer == 2 ? CIEConstant.ZZ_2 : CIEConstant.ZZ_10;

        foreach (var function in new[] { x, y, z })
        {
            Assert.Equal(400, function.StartingWavelength);
            Assert.Equal(700, function.EndingWavelength);
            Assert.Equal(10, function.WavelengthInterval);
            Assert.Equal(31, function.Spectrums!.Length);
        }
    }

    [Fact]
    public void DiagramAndCctLookupArraysRemainAligned()
    {
        Assert.Equal(CIEConstant.Wavelength_CIE.Length, CIEConstant.x_CIE.Length);
        Assert.Equal(CIEConstant.Wavelength_CIE.Length, CIEConstant.y_CIE.Length);
        Assert.Equal(CIEConstant.CCT_CCT.Length, CIEConstant.x_CCT.Length);
        Assert.Equal(CIEConstant.CCT_CCT.Length, CIEConstant.y_CCT.Length);
        Assert.Equal(1000, CIEConstant.CCT_CCT[0]);
        Assert.Equal(20000, CIEConstant.CCT_CCT[^1]);
    }
}
