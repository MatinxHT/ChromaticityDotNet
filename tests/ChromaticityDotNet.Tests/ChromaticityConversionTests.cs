using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ChromaticityConversionTests
{
    public static IEnumerable<object[]> MeasurementConditions()
    {
        foreach (Standardilluminant illuminant in Enum.GetValues<Standardilluminant>())
        {
            yield return new object[] { illuminant, StandardObserver.Degree2 };
            yield return new object[] { illuminant, StandardObserver.Degree10 };
        }
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, 0.0143, 0.0004, 0.0679)]
    [InlineData(StandardObserver.Degree10, 0.0191, 0.0020, 0.0860)]
    public void SpdToXyzUsesSelectedObserverFunctions(
        StandardObserver observer,
        double expectedX,
        double expectedY,
        double expectedZ)
    {
        double[] spd = new double[31];
        spd[0] = 1.0;

        CIEXYZ result = ChromaticityConversion.SPDtoXYZ(spd, observer);

        Assert.Equal(expectedX, result.CIEX);
        Assert.Equal(expectedY, result.CIEY);
        Assert.Equal(expectedZ, result.CIEZ);
    }

    [Fact]
    public void SpdToXyzReturnsZeroForZeroSpectrum()
    {
        CIEXYZ result = ChromaticityConversion.SPDtoXYZ(
            new double[31],
            StandardObserver.Degree2);

        Assert.Equal(0.0, result.CIEX);
        Assert.Equal(0.0, result.CIEY);
        Assert.Equal(0.0, result.CIEZ);
    }

    [Theory]
    [MemberData(nameof(MeasurementConditions))]
    public void PerfectReflectorIsNormalizedToY100(
        Standardilluminant illuminant,
        StandardObserver observer)
    {
        double[] perfectReflector = Enumerable.Repeat(100.0, 31).ToArray();

        CIEXYZ result = ChromaticityConversion.REFtoXYZ(
            perfectReflector,
            illuminant,
            observer);

        Assert.Equal(100.0, result.CIEY);
        Assert.True(result.CIEX > 0);
        Assert.True(result.CIEZ >= 0);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEX);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEY);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEZ);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void ZeroReflectanceProducesZeroXyz(StandardObserver observer)
    {
        CIEXYZ result = ChromaticityConversion.REFtoXYZ(
            new double[31],
            Standardilluminant.D65,
            observer);

        Assert.Equal(0.0, result.CIEX);
        Assert.Equal(0.0, result.CIEY);
        Assert.Equal(0.0, result.CIEZ);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void ReferenceWhiteConvertsToNeutralLab(StandardObserver observer)
    {
        CIEXYZ white = ChromaticityMatch.GetStandardWhitePoint(
            Standardilluminant.D65,
            observer);

        CIELABCH result = ChromaticityConversion.XYZ2Labch(
            white,
            Standardilluminant.D65,
            observer);

        Assert.Equal(100.0, result.CIEL);
        Assert.Equal(0.0, result.CIEA);
        Assert.Equal(0.0, result.CIEB);
        Assert.Equal(0.0, result.CIEC);
    }

    [Fact]
    public void XyzToLabMatchesKnownSrgbRed()
    {
        CIELABCH result = ChromaticityConversion.XYZ2Labch(
            new CIEXYZ { CIEX = 41.24, CIEY = 21.26, CIEZ = 1.93 },
            Standardilluminant.D65,
            StandardObserver.Degree2);

        Assert.InRange(result.CIEL, 53.22, 53.25);
        Assert.InRange(result.CIEA, 80.08, 80.13);
        Assert.InRange(result.CIEB, 67.19, 67.24);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEL);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEA);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEB);
    }

    [Fact]
    public void XyzToXyYConvertsD65White()
    {
        CIExyY result = ChromaticityConversion.XYZ2xyY(new CIEXYZ
        {
            CIEX = 95.047,
            CIEY = 100.0,
            CIEZ = 108.883
        });

        Assert.Equal(0.3127, result.CIEx);
        Assert.Equal(0.3290, result.CIEy);
        Assert.Equal(100.0, result.CIEY);
    }

    [Fact]
    public void XyYToXyzUsesLuminanceScale()
    {
        CIEXYZ result = ChromaticityConversion.xy2XYZ(new CIExyY
        {
            CIEx = 0.25,
            CIEy = 0.40,
            CIEY = 80.0
        });

        Assert.Equal(50.0, result.CIEX);
        Assert.Equal(80.0, result.CIEY);
        Assert.Equal(70.0, result.CIEZ);
    }

    [Fact]
    public void XyToUvConvertsD65Chromaticity()
    {
        CIEuv result = ChromaticityConversion.xy2uv(new CIExyY
        {
            CIEx = 0.3127,
            CIEy = 0.3290,
            CIEY = 100.0
        });

        Assert.Equal(0.1978, result.CIEu);
        Assert.Equal(0.4683, result.CIEv);
    }

    [Fact]
    public void XyToUvReturnsSentinelWhenDenominatorIsZero()
    {
        CIEuv result = ChromaticityConversion.xy2uv(new CIExyY
        {
            CIEx = 1.5,
            CIEy = 0.0
        });

        Assert.Equal(-1.0, result.CIEu);
        Assert.Equal(-1.0, result.CIEv);
    }

    [Fact]
    public void D65ReferenceWhiteMapsToNeutralLuv()
    {
        CIELuv result = ChromaticityConversion.XYZ2Luv(
            new CIEXYZ { CIEX = 95.047, CIEY = 100.0, CIEZ = 108.883 },
            Standardilluminant.D65,
            StandardObserver.Degree2);

        Assert.Equal(100.0, result.CIEL);
        Assert.Equal(0.0, result.CIEu);
        Assert.Equal(0.0, result.CIEv);
    }

    [Fact]
    public void XyzToLuvMatchesKnownSrgbRedRange()
    {
        CIELuv result = ChromaticityConversion.XYZ2Luv(
            new CIEXYZ { CIEX = 41.24, CIEY = 21.26, CIEZ = 1.93 },
            Standardilluminant.D65,
            StandardObserver.Degree2);

        Assert.InRange(result.CIEL, 53.22, 53.25);
        Assert.InRange(result.CIEu, 174.9, 175.2);
        Assert.InRange(result.CIEv, 37.6, 37.9);
    }

    [Theory]
    [InlineData(95.047, 100.0, 108.883, 255, 255, 255)]
    [InlineData(0.0, 0.0, 0.0, 0, 0, 0)]
    [InlineData(41.24, 21.26, 1.93, 255, 0, 0)]
    [InlineData(35.76, 71.52, 11.92, 0, 255, 0)]
    [InlineData(18.05, 7.22, 95.05, 0, 0, 255)]
    [InlineData(190.094, 200.0, 217.766, 255, 255, 255)]
    public void XyzToRgbProducesExpectedSrgbBytes(
        double x,
        double y,
        double z,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        CIERGB result = ChromaticityConversion.XYZ2RGB(new CIEXYZ
        {
            CIEX = x,
            CIEY = y,
            CIEZ = z
        });

        Assert.Equal(expectedRed, result.redValue);
        Assert.Equal(expectedGreen, result.greenValue);
        Assert.Equal(expectedBlue, result.blueValue);
    }

    [Fact]
    public void XyToCctApproximatesD65()
    {
        double result = ChromaticityConversion.xy2CCT(new CIExyY
        {
            CIEx = 0.3127,
            CIEy = 0.3290
        });

        Assert.InRange(result, 6504.0, 6506.0);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result);
    }
}
