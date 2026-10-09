using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests;

public class ChromaticityWavelengthTests
{
    private static CIExyY Xy(double x, double y, double luminance = 100) => new() { CIEx = x, CIEy = y, CIEY = luminance };
    private static CIExyY White => Xy(0.3127, 0.3290);

    [Fact]
    public void IndependentlyCalculatedGreenAndPurpleFixturesMatch()
    {
        // Independent Python ray/segment calculation against archived CIE 1931 CMFs:
        // green dominant 549.134015 nm; purple complementary 510.962270 nm.
        var green = ChromaticityConversion.xyYToWavelengths(Xy(0.3, 0.6), StandardObserver.Degree2, White);
        Assert.Equal(549.13, green.DominantWavelength);
        Assert.Null(green.ComplementaryWavelength);
        Assert.False(green.IsAchromatic);
        var purple = ChromaticityConversion.xyYToWavelengths(Xy(0.4, 0.2), StandardObserver.Degree2, White);
        Assert.Null(purple.DominantWavelength);
        Assert.Equal(510.96, purple.ComplementaryWavelength);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void WhiteDilutionPreservesWavelengthAndBothDirectionsCanHaveWavelengths(StandardObserver observer)
    {
        var locus = CieSpectralData.GetSpectralLocus(observer);
        foreach (int wavelength in new[] { 400, 450, 500, 550, 600, 650 })
        foreach (double fraction in new[] { 0.01, 0.5, 1.0 })
        {
            var point = locus[wavelength - 360];
            var sample = Xy(White.CIEx + fraction * (point.X - White.CIEx), White.CIEy + fraction * (point.Y - White.CIEy));
            var result = ChromaticityConversion.xyYToWavelengths(sample, observer, White);
            Assert.Equal((double)wavelength, result.DominantWavelength);
        }
        var red = ChromaticityConversion.xyYToWavelengths(Xy(0.6, 0.33), observer, White);
        Assert.NotNull(red.DominantWavelength);
        Assert.NotNull(red.ComplementaryWavelength);
    }

    [Fact]
    public void SubNanometreInterpolationKeepsFullCoordinatePrecision()
    {
        var locus = CieSpectralData.GetSpectralLocus(StandardObserver.Degree2);
        var a = locus[550 - 360];
        var b = locus[551 - 360];
        var point = Xy(a.X + 0.256 * (b.X - a.X), a.Y + 0.256 * (b.Y - a.Y));
        Assert.Equal(550.26, ChromaticityConversion.xyYToWavelengths(point, StandardObserver.Degree2, White).DominantWavelength);
    }

    [Theory]
    [MemberData(nameof(ChromaticityConversionTests.MeasurementConditions), MemberType = typeof(ChromaticityConversionTests))]
    public void IlluminantOverloadUsesItsUnroundedObserverWhite(Standardilluminant illuminant, StandardObserver observer)
    {
        var xyz = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        double sum = xyz.CIEX + xyz.CIEY + xyz.CIEZ;
        var white = Xy(xyz.CIEX / sum, xyz.CIEY / sum);
        var sample = Xy(0.4, 0.3);
        var expected = ChromaticityConversion.xyYToWavelengths(sample, observer, white);
        var actual = ChromaticityConversion.xyYToWavelengths(sample, observer, illuminant);
        Assert.Equal(expected.DominantWavelength, actual.DominantWavelength);
        Assert.Equal(expected.ComplementaryWavelength, actual.ComplementaryWavelength);
        Assert.True(ChromaticityConversion.xyYToWavelengths(white, observer, illuminant).IsAchromatic);
    }

    [Fact]
    public void AchromaticAndLuminanceSemanticsAreExplicitAndInputsAreNotChanged()
    {
        var neutral = ChromaticityConversion.xyYToWavelengths(White, StandardObserver.Degree2, White);
        Assert.True(neutral.IsAchromatic);
        Assert.Null(neutral.DominantWavelength);
        Assert.Null(neutral.ComplementaryWavelength);
        var sample = Xy(0.4, 0.2, 0);
        var before = ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, White);
        sample.CIEY = 10000;
        var after = ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, White);
        Assert.Equal(before.ComplementaryWavelength, after.ComplementaryWavelength);
        Assert.Equal(0.4, sample.CIEx);
        Assert.Equal(0.2, sample.CIEy);
        Assert.Equal(10000, sample.CIEY);
    }

    [Fact]
    public void PurpleBoundaryAndSpectralEndpointsAreAccepted()
    {
        var locus = CieSpectralData.GetSpectralLocus(StandardObserver.Degree2);
        var purple = Xy((locus[0].X + locus[^1].X) / 2, (locus[0].Y + locus[^1].Y) / 2);
        var result = ChromaticityConversion.xyYToWavelengths(purple, StandardObserver.Degree2, White);
        Assert.Null(result.DominantWavelength);
        Assert.NotNull(result.ComplementaryWavelength);
        var violet = ChromaticityConversion.xyYToWavelengths(Xy(locus[0].X, locus[0].Y), StandardObserver.Degree2, White);
        Assert.NotNull(violet.DominantWavelength);
    }

    [Theory]
    [InlineData(-0.1, 0.3, 100)]
    [InlineData(0.3, -0.1, 100)]
    [InlineData(0.8, 0.8, 100)]
    [InlineData(0.1, 0.1, 100)]
    [InlineData(0.3, 0.3, -1)]
    [InlineData(double.NaN, 0.3, 100)]
    [InlineData(0.3, double.PositiveInfinity, 100)]
    [InlineData(0.3, 0.3, double.NaN)]
    public void InvalidSamplesAreRejected(double x, double y, double luminance) =>
        Assert.Throws<ArgumentOutOfRangeException>("color", () => ChromaticityConversion.xyYToWavelengths(Xy(x, y, luminance), StandardObserver.Degree2, White));

    [Fact]
    public void WhiteMustBeInteriorAndEnumsAndNullsAreValidated()
    {
        var sample = Xy(0.3, 0.6);
        var boundary = CieSpectralData.GetSpectralLocus(StandardObserver.Degree2)[190];
        Assert.Throws<ArgumentOutOfRangeException>("whitePoint", () => ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, Xy(boundary.X, boundary.Y)));
        Assert.Throws<ArgumentOutOfRangeException>("whitePoint", () => ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, Xy(0.1, 0.1)));
        Assert.Throws<ArgumentOutOfRangeException>("observer", () => ChromaticityConversion.xyYToWavelengths(sample, (StandardObserver)999, White));
        Assert.Throws<ArgumentOutOfRangeException>("illuminant", () => ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, (Standardilluminant)999));
        Assert.Throws<ArgumentNullException>("color", () => ChromaticityConversion.xyYToWavelengths(null!, StandardObserver.Degree2, White));
        Assert.Throws<ArgumentNullException>("whitePoint", () => ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, (CIExyY)null!));
    }
}
