using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ChromaticitySpectralConversionTests
{
    // Independently calculated from archived CSV decimal strings with Python Decimal (50 digits),
    // inclusive 1 nm rectangular sums, then rounded once to four decimals, away from zero.
    [Theory]
    [InlineData(Standardilluminant.D65, StandardObserver.Degree2, 360, 830, 95.0471, 108.8829)]
    [InlineData(Standardilluminant.D65, StandardObserver.Degree10, 360, 830, 94.8111, 107.3047)]
    [InlineData(Standardilluminant.A, StandardObserver.Degree2, 360, 830, 109.8503, 35.5849)]
    [InlineData(Standardilluminant.A, StandardObserver.Degree10, 360, 830, 111.1439, 35.1999)]
    [InlineData(Standardilluminant.CWF, StandardObserver.Degree2, 380, 780, 99.1858, 67.3938)]
    [InlineData(Standardilluminant.CWF, StandardObserver.Degree10, 380, 780, 103.2800, 69.0068)]
    [InlineData(Standardilluminant.TL84, StandardObserver.Degree2, 380, 780, 100.9610, 64.3506)]
    [InlineData(Standardilluminant.TL84, StandardObserver.Degree10, 380, 780, 103.8612, 65.5902)]
    public void PerfectReflectorsMatchIndependentOfficialDataFixtures(Standardilluminant illuminant,
        StandardObserver observer, int start, int end, double x, double z)
    {
        AssertXyz(ChromaticityConversion.REFToXYZ(Grid(start, end, 1, _ => 100), illuminant, observer), x, 100, z);
    }

    [Theory]
    [InlineData(Standardilluminant.D65, StandardObserver.Degree2, 360, 830, 41.8121, 41.8681, 22.1442)]
    [InlineData(Standardilluminant.D65, StandardObserver.Degree10, 360, 830, 41.0267, 40.6617, 21.1062)]
    [InlineData(Standardilluminant.A, StandardObserver.Degree2, 360, 830, 55.0471, 45.1557, 7.7209)]
    [InlineData(Standardilluminant.A, StandardObserver.Degree10, 360, 830, 54.9628, 44.5783, 7.3489)]
    [InlineData(Standardilluminant.CWF, StandardObserver.Degree2, 380, 780, 47.5644, 46.3035, 12.0110)]
    [InlineData(Standardilluminant.CWF, StandardObserver.Degree10, 380, 780, 49.0265, 45.4667, 11.8321)]
    [InlineData(Standardilluminant.TL84, StandardObserver.Degree2, 380, 780, 49.7844, 46.2208, 11.5453)]
    [InlineData(Standardilluminant.TL84, StandardObserver.Degree10, 380, 780, 50.6034, 45.4935, 11.3746)]
    public void TenNanometreRampInterpolatesToIndependentOneNanometreFixture(Standardilluminant illuminant,
        StandardObserver observer, int start, int end, double x, double y, double z)
    {
        AssertXyz(ChromaticityConversion.REFToXYZ(Grid(start, end, 10, w => 100.0 * (w - start) / (end - start)), illuminant, observer), x, y, z);
    }

    [Theory]
    [MemberData(nameof(ChromaticityConversionTests.MeasurementConditions), MemberType = typeof(ChromaticityConversionTests))]
    public void BlackAndGreyPreserveReflectanceScale(Standardilluminant illuminant, StandardObserver observer)
    {
        AssertXyz(ChromaticityConversion.REFToXYZ(Grid(380, 780, 5, _ => 0), illuminant, observer), 0, 0, 0);
        Assert.Equal(18, ChromaticityConversion.REFToXYZ(Grid(380, 780, 5, _ => 18), illuminant, observer).CIEY);
    }

    [Fact]
    public void CustomIlluminantInterpolationAndScaleAreConsistent()
    {
        var reflectance = Grid(401, 701, 5, w => 10 + (w - 401) * 0.2);
        var coarse = Grid(400, 710, 10, w => 1 + (w - 400) * 0.01);
        var fine = Grid(400, 710, 1, w => (1 + (w - 400) * 0.01) * 1000);
        var expected = ChromaticityConversion.REFToXYZ(reflectance, fine, StandardObserver.Degree2);
        AssertXyz(ChromaticityConversion.REFToXYZ(reflectance, coarse, StandardObserver.Degree2), expected.CIEX, expected.CIEY, expected.CIEZ);
    }

    [Theory]
    [InlineData(360)]
    [InlineData(555)]
    [InlineData(830)]
    public void SpdUsesItsActualWavelengthIncludingEndpoints(int wavelength)
    {
        var (x, y, z) = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);
        int i = wavelength - 360;
        AssertXyz(ChromaticityConversion.SPDToXYZ(Grid(wavelength, wavelength, 1, _ => 1), StandardObserver.Degree2),
            Math.Round(x.Spectrums![i], 4, MidpointRounding.AwayFromZero),
            Math.Round(y.Spectrums![i], 4, MidpointRounding.AwayFromZero),
            Math.Round(z.Spectrums![i], 4, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void SpdScaleRemainsUnnormalizedPendingTodo()
    {
        var spd = Grid(500, 510, 10, _ => 1);
        // Official 1931 functions at 500 and 510 nm; deliberately no 10 nm factor.
        AssertXyz(ChromaticityConversion.SPDToXYZ(spd, StandardObserver.Degree2), 0.0142, 0.8260, 0.4302);
    }

    public static IEnumerable<object[]> InvalidSpectra()
    {
        yield return new object[] { new Spectrum() };
        yield return new object[] { new Spectrum { StartingWavelength = 380, EndingWavelength = 780, WavelengthInterval = 0, Spectrums = new double[401] } };
        yield return new object[] { new Spectrum { StartingWavelength = 780, EndingWavelength = 380, WavelengthInterval = 1, Spectrums = new double[401] } };
        yield return new object[] { new Spectrum { StartingWavelength = 380, EndingWavelength = 780, WavelengthInterval = 1, Spectrums = new double[31] } };
        yield return new object[] { new Spectrum { StartingWavelength = 380, EndingWavelength = 781, WavelengthInterval = 10, Spectrums = new double[41] } };
        foreach (double value in new[] { -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            yield return new object[] { Grid(380, 780, 10, _ => value) };
        yield return new object[] { Grid(359, 780, 1, _ => 1) };
        yield return new object[] { Grid(380, 831, 1, _ => 1) };
    }

    [Theory]
    [MemberData(nameof(InvalidSpectra))]
    public void InvalidSpectraFailBeforeCalculation(Spectrum spectrum)
    {
        Assert.ThrowsAny<ArgumentException>(() => ChromaticityConversion.REFToXYZ(spectrum, Standardilluminant.D65, StandardObserver.Degree2));
        Assert.ThrowsAny<ArgumentException>(() => ChromaticityConversion.SPDToXYZ(spectrum, StandardObserver.Degree2));
    }

    [Fact]
    public void NullUnknownConditionsAndUncoveredRangesAreRejected()
    {
        var valid = Grid(380, 780, 1, _ => 100);
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.REFToXYZ((Spectrum)null!, Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.SPDToXYZ((Spectrum)null!, StandardObserver.Degree2));
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.REFToXYZ(valid, (Spectrum)null!, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.REFToXYZ(valid, (Standardilluminant)999, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.REFToXYZ(valid, Standardilluminant.D65, (StandardObserver)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.SPDToXYZ(valid, (StandardObserver)999));
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.REFToXYZ(Grid(360, 830, 1, _ => 100), Standardilluminant.CWF, StandardObserver.Degree2));
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.REFToXYZ(valid, Grid(400, 700, 1, _ => 1), StandardObserver.Degree2));
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.REFToXYZ(valid, Grid(380, 780, 1, _ => 0), StandardObserver.Degree2));
    }

    [Fact]
    public void NumericOverflowIsRejected()
    {
        var huge = Grid(380, 780, 1, _ => double.MaxValue);
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.SPDToXYZ(huge, StandardObserver.Degree2));
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.REFToXYZ(huge, huge, StandardObserver.Degree2));
    }

    // Frozen results from the pre-migration 31-point tables and formula for a 0–100% ramp.
    [Theory]
    [InlineData(Standardilluminant.D65, StandardObserver.Degree2, 52.7555, 52.2437, 20.1821)]
    [InlineData(Standardilluminant.D65, StandardObserver.Degree10, 51.5940, 50.3591, 18.7843)]
    [InlineData(Standardilluminant.A, StandardObserver.Degree2, 71.4030, 57.3710, 7.3447)]
    [InlineData(Standardilluminant.A, StandardObserver.Degree10, 71.1704, 56.4776, 6.8185)]
    [InlineData(Standardilluminant.CWF, StandardObserver.Degree2, 58.0116, 55.3997, 10.3739)]
    [InlineData(Standardilluminant.CWF, StandardObserver.Degree10, 59.7008, 54.3464, 9.9821)]
    [InlineData(Standardilluminant.F7, StandardObserver.Degree2, 53.1660, 52.7526, 18.2019)]
    [InlineData(Standardilluminant.F7, StandardObserver.Degree10, 52.5713, 51.0197, 17.0058)]
    [InlineData(Standardilluminant.TL84, StandardObserver.Degree2, 62.8265, 55.6147, 9.6576)]
    [InlineData(Standardilluminant.TL84, StandardObserver.Degree10, 63.6618, 54.7495, 9.3176)]
    [InlineData(Standardilluminant.U30, StandardObserver.Degree2, 70.9662, 57.5904, 5.2202)]
    [InlineData(Standardilluminant.U30, StandardObserver.Degree10, 72.1867, 57.1176, 4.9489)]
    public void LegacyFastPathRetainsItsNumericalResults(Standardilluminant illuminant, StandardObserver observer, double x, double y, double z)
    {
        double[] values = Enumerable.Range(0, 31).Select(i => i * 100.0 / 30).ToArray();
        AssertXyz(ChromaticityConversion.REFToXYZ(values, illuminant, observer), x, y, z);
    }

    private static Spectrum Grid(int start, int end, int step, Func<int, double> value) => new()
    {
        StartingWavelength = start, EndingWavelength = end, WavelengthInterval = step,
        Spectrums = Enumerable.Range(0, (end - start) / step + 1).Select(i => value(start + step * i)).ToArray()
    };

    private static void AssertXyz(CIEXYZ actual, double x, double y, double z)
    {
        Assert.Equal(x, actual.CIEX);
        Assert.Equal(y, actual.CIEY);
        Assert.Equal(z, actual.CIEZ);
    }
}
