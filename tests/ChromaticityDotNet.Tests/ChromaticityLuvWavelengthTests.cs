using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests;

public class ChromaticityLuvWavelengthTests
{
    private static CIEXYZ White => new() { CIEX = 100 * 0.3127 / 0.3290, CIEY = 100, CIEZ = 100 * (1 - 0.3127 - 0.3290) / 0.3290 };

    // Analytical, unrounded Luv coordinates, independent of the library's rounded conversions.
    private static CIELuv At(double x, double y, double lightness, CIEXYZ white)
    {
        double denominator = -2 * x + 12 * y + 3;
        double whiteDenominator = white.CIEX + 15 * white.CIEY + 3 * white.CIEZ;
        return new CIELuv
        {
            CIEL = lightness,
            CIEu = 13 * lightness * (4 * x / denominator - 4 * white.CIEX / whiteDenominator),
            CIEv = 13 * lightness * (9 * y / denominator - 9 * white.CIEY / whiteDenominator)
        };
    }

    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    [InlineData(200)]
    [InlineData(0.000001)]
    [InlineData(1e-20)]
    public void IndependentGreenAndPurpleFixturesArePreservedAtEveryLightness(double lightness)
    {
        var green = ChromaticityConversion.LuvToWavelengths(At(0.3, 0.6, lightness, White), StandardObserver.Degree2, White);
        Assert.Equal(549.13, green.DominantWavelength);
        Assert.Null(green.ComplementaryWavelength);
        Assert.False(green.IsAchromatic);
        var purple = ChromaticityConversion.LuvToWavelengths(At(0.4, 0.2, lightness, White), StandardObserver.Degree2, White);
        Assert.Null(purple.DominantWavelength);
        Assert.Equal(510.96, purple.ComplementaryWavelength);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void SpectralSamplesAndSubNanometreInterpolationAvoidCoordinateRounding(StandardObserver observer)
    {
        var locus = CieSpectralData.GetSpectralLocus(observer);
        foreach (int wavelength in new[] { 400, 450, 500, 550, 600, 650 })
        foreach (double fraction in new[] { 0.01, 0.5, 1.0 })
        {
            var p = locus[wavelength - 360];
            double x = 0.3127 + fraction * (p.X - 0.3127), y = 0.3290 + fraction * (p.Y - 0.3290);
            var actual = ChromaticityConversion.LuvToWavelengths(At(x, y, 1e-8, White), observer, White);
            Assert.Equal((double)wavelength, actual.DominantWavelength);
        }
        var a = locus[550 - 360]; var b = locus[551 - 360];
        var subNanometre = At(a.X + 0.256 * (b.X - a.X), a.Y + 0.256 * (b.Y - a.Y), 50, White);
        Assert.Equal(550.26, ChromaticityConversion.LuvToWavelengths(subNanometre, observer, White).DominantWavelength);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void AllCatalogAndEnumOverloadsShareTheLuvReferenceWhite(StandardObserver observer)
    {
        var sample = new CIExyY { CIEx = 0.4, CIEy = 0.3, CIEY = 100 };
        foreach (var info in CieSpectralData.Illuminants)
        {
            var white = ChromaticityMatch.GetStandardWhitePoint(info.Id, observer);
            var luv = At(sample.CIEx, sample.CIEy, 50, white);
            var expected = ChromaticityConversion.xyYToWavelengths(sample, observer, info.Id);
            AssertSame(expected, ChromaticityConversion.LuvToWavelengths(luv, observer, info.Id));
            AssertSame(expected, ChromaticityConversion.LuvToWavelengths(luv, observer, white));
        }
        foreach (Standardilluminant illuminant in Enum.GetValues<Standardilluminant>())
        {
            var white = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
            var luv = At(sample.CIEx, sample.CIEy, 50, white);
            AssertSame(ChromaticityConversion.xyYToWavelengths(sample, observer, illuminant),
                ChromaticityConversion.LuvToWavelengths(luv, observer, illuminant));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void BlackAndNeutralColorsReturnUndefinedWavelengths(double lightness)
    {
        var result = ChromaticityConversion.LuvToWavelengths(new CIELuv { CIEL = lightness }, StandardObserver.Degree2, White);
        Assert.True(result.IsAchromatic);
        Assert.Null(result.DominantWavelength);
        Assert.Null(result.ComplementaryWavelength);
    }

    [Fact]
    public void NearWhiteChromaticityAndLargeWhiteScaleRetainPrecision()
    {
        var sample = new CIExyY { CIEx = 0.3127000001, CIEy = 0.3290000002, CIEY = 100 };
        var expected = ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2,
            new CIExyY { CIEx = 0.3127, CIEy = 0.3290, CIEY = 100 });
        var luv = At(sample.CIEx, sample.CIEy, 50, White);
        AssertSame(expected, ChromaticityConversion.LuvToWavelengths(luv, StandardObserver.Degree2, White));
        var largeWhite = new CIEXYZ { CIEX = White.CIEX * 1e305, CIEY = White.CIEY * 1e305, CIEZ = White.CIEZ * 1e305 };
        AssertSame(expected, ChromaticityConversion.LuvToWavelengths(luv, StandardObserver.Degree2, largeWhite));
        Assert.False(expected.IsAchromatic);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(double.PositiveInfinity, 0, 0)]
    [InlineData(50, double.NaN, 0)]
    [InlineData(50, 0, double.NegativeInfinity)]
    public void NonFiniteAndNegativeCoordinatesAreRejected(double l, double u, double v)
        => Assert.Throws<ArgumentOutOfRangeException>("color", () => ChromaticityConversion.LuvToWavelengths(
            new CIELuv { CIEL = l, CIEu = u, CIEv = v }, StandardObserver.Degree2, White));

    [Fact]
    public void InvalidGeometryWhitesAndConditionsAreRejectedIncludingForBlack()
    {
        var color = At(0.3, 0.6, 50, White);
        Assert.Throws<ArgumentException>("color", () => ChromaticityConversion.LuvToWavelengths(new CIELuv { CIEu = 1 }, StandardObserver.Degree2, White));
        Assert.Throws<ArgumentException>("color", () => ChromaticityConversion.LuvToWavelengths(new CIELuv { CIEL = 50, CIEv = -1000 }, StandardObserver.Degree2, White));
        Assert.Throws<ArgumentOutOfRangeException>("color", () => ChromaticityConversion.LuvToWavelengths(At(0.1, 0.1, 50, White), StandardObserver.Degree2, White));
        Assert.Throws<ArgumentNullException>("color", () => ChromaticityConversion.LuvToWavelengths(null!, StandardObserver.Degree2, White));
        Assert.Throws<ArgumentNullException>("whitePoint", () => ChromaticityConversion.LuvToWavelengths(color, StandardObserver.Degree2, (CIEXYZ)null!));
        Assert.Throws<ArgumentOutOfRangeException>("whitePoint", () => ChromaticityConversion.LuvToWavelengths(color, StandardObserver.Degree2, new CIEXYZ()));
        var outsideWhite = new CIEXYZ { CIEX = 10, CIEY = 10, CIEZ = 80 };
        Assert.Throws<ArgumentOutOfRangeException>("whitePoint", () => ChromaticityConversion.LuvToWavelengths(new CIELuv(), StandardObserver.Degree2, outsideWhite));
        Assert.Throws<ArgumentOutOfRangeException>("observer", () => ChromaticityConversion.LuvToWavelengths(new CIELuv(), (StandardObserver)999, White));
        Assert.Throws<ArgumentOutOfRangeException>("illuminant", () => ChromaticityConversion.LuvToWavelengths(color, StandardObserver.Degree2, (Standardilluminant)999));
        Assert.Throws<ArgumentOutOfRangeException>("illuminantId", () => ChromaticityConversion.LuvToWavelengths(color, StandardObserver.Degree2, "invalid"));
    }

    private static void AssertSame(ChromaticityWavelengthResult expected, ChromaticityWavelengthResult actual)
    {
        Assert.Equal(expected.DominantWavelength, actual.DominantWavelength);
        Assert.Equal(expected.ComplementaryWavelength, actual.ComplementaryWavelength);
        Assert.Equal(expected.IsAchromatic, actual.IsAchromatic);
    }
}
