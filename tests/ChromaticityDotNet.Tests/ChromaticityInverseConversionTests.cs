using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ChromaticityInverseConversionTests
{
    // Independently calculated in Python using the CIE piecewise functions and
    // an explicit conventional D65/2-degree white (95.047, 100, 108.883).
    // Formula: https://www.w3.org/TR/css-color-4/#color-conversion-code
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(100, 0, 0, 95.047, 100, 108.883)]
    [InlineData(50, 20, -30, 21.464289520092, 18.418651851244, 40.465439576136)]
    [InlineData(5, 3, -4, 0.599346763869, 0.553528229940, 0.882350080774)]
    [InlineData(7.9999, 0, 0, 0.841768640498, 0.885634097339, 0.964304974206)]
    [InlineData(8, 0, 0, 0.841779162737, 0.885645167904, 0.964317028168)]
    [InlineData(8.0001, 0, 0, 0.841789685021, 0.885656238514, 0.964329082182)]
    [InlineData(50.123456, 20.654321, -30.987654, 21.716184182882, 18.522204085580, 41.487446324279)]
    [InlineData(120, 0, 0, 153.172630612161, 161.154618885563, 175.469983681168)]
    [InlineData(8, -20, 20, 0.353547248350, 0.885645167904, -0.433942662676)]
    public void LabToXyzMatchesReferenceValues(double l, double a, double b, double x, double y, double z)
    {
        CIEXYZ result = ChromaticityConversion.LabToXYZ(
            new CIELAB(l, a, b), ConventionalD65White);

        AssertReferenceXyz(result, x, y, z);
    }

    // Independently calculated in Python using the same white as the Lab fixtures.
    // Formula: https://colour.readthedocs.io/en/develop/_modules/colour/models/cie_luv.html
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(100, 0, 0, 95.047, 100, 108.883)]
    [InlineData(50, 20, -30, 22.440555520624, 18.418651851244, 31.308250596980)]
    [InlineData(5, 3, -4, 0.747002773624, 0.553528229940, 1.065446179053)]
    [InlineData(7.9999, 0, 0, 0.841768640498, 0.885634097339, 0.964304974206)]
    [InlineData(8, 0, 0, 0.841779162737, 0.885645167904, 0.964317028168)]
    [InlineData(8.0001, 0, 0, 0.841789685021, 0.885656238514, 0.964329082182)]
    [InlineData(50.123456, 20.654321, -30.987654, 22.733861013595, 18.522204085580, 31.867107642431)]
    [InlineData(120, 0, 0, 153.172630612161, 161.154618885563, 175.469983681168)]
    public void LuvToXyzMatchesReferenceValues(double l, double u, double v, double x, double y, double z)
    {
        CIEXYZ result = ChromaticityConversion.LuvToXYZ(
            new CIELuv { CIEL = l, CIEu = u, CIEv = v },
            ConventionalD65White);

        AssertReferenceXyz(result, x, y, z);
    }

    // Python evaluation of the W3C sRGB transfer function and rational matrix.
    // Includes adjacent byte values on either side of the 0.04045 transfer threshold.
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(255, 255, 255, 95.045592705167, 100.0, 108.905775075988)]
    [InlineData(255, 0, 0, 41.239079926596, 21.263900587151, 1.933081871559)]
    [InlineData(0, 255, 0, 35.758433938388, 71.516867876776, 11.919477979463)]
    [InlineData(0, 0, 255, 18.048078840183, 7.219231536073, 95.053215224966)]
    [InlineData(128, 128, 128, 20.516589174959, 21.586050011390, 23.508455073195)]
    [InlineData(10, 10, 10, 0.288489020534, 0.303526983549, 0.330558413999)]
    [InlineData(11, 11, 11, 0.318073475189, 0.334653576390, 0.364457071187)]
    [InlineData(1, 2, 3, 0.050658709502, 0.056442460678, 0.094376155984)]
    [InlineData(12, 34, 56, 1.437351751064, 1.507675114229, 3.956771269278)]
    [InlineData(255, 128, 32, 49.218596987804, 36.805840904802, 5.878960123318)]
    public void RgbToXyzMatchesReferenceValues(byte r, byte g, byte b, double x, double y, double z)
    {
        CIEXYZ result = ChromaticityConversion.RGBToXYZ(
            new CIERGB { redValue = r, greenValue = g, blueValue = b });

        AssertReferenceXyz(result, x, y, z);
    }

    [Theory]
    [MemberData(nameof(ChromaticityConversionTests.MeasurementConditions), MemberType = typeof(ChromaticityConversionTests))]
    public void NeutralWhiteUsesSelectedIlluminantAndObserver(Standardilluminant illuminant, StandardObserver observer)
    {
        CIEXYZ white = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        CIEXYZ labResult = ChromaticityConversion.LabToXYZ(new CIELAB(100, 0, 0), illuminant, observer);
        CIEXYZ luvResult = ChromaticityConversion.LuvToXYZ(new CIELuv { CIEL = 100 }, illuminant, observer);

        AssertReferenceXyz(labResult, white.CIEX, white.CIEY, white.CIEZ);
        AssertReferenceXyz(luvResult, white.CIEX, white.CIEY, white.CIEZ);
    }

    [Theory]
    [MemberData(nameof(ChromaticityConversionTests.MeasurementConditions), MemberType = typeof(ChromaticityConversionTests))]
    public void XyzRoundTripsThroughLabAndLuv(Standardilluminant illuminant, StandardObserver observer)
    {
        CIEXYZ[] colors =
        {
            new() { CIEX = 0, CIEY = 0, CIEZ = 0 },
            new() { CIEX = 0.01, CIEY = 0.02, CIEZ = 0.03 },
            new() { CIEX = 1.2, CIEY = 1.1, CIEZ = 0.9 },
            new() { CIEX = 20, CIEY = 30, CIEZ = 10 },
            new() { CIEX = 50, CIEY = 40, CIEZ = 70 }
        };

        foreach (CIEXYZ color in colors)
        {
            CIELAB lab = ChromaticityConversion.XYZToLab(color, illuminant, observer);
            CIELuv luv = ChromaticityConversion.XYZToLuv(color, illuminant, observer);
            CIEXYZ labResult = ChromaticityConversion.LabToXYZ(lab, illuminant, observer);
            CIEXYZ luvResult = ChromaticityConversion.LuvToXYZ(luv, illuminant, observer);

            // Each public conversion rounds to four decimals; allow accumulated rounding.
            AssertCloseXyz(color, labResult, 0.0003);
            AssertCloseXyz(color, luvResult, 0.0003);
        }
    }

    [Fact]
    public void SrgbBytesRoundTripThroughXyz()
    {
        byte[] levels = { 0, 1, 10, 11, 12, 64, 128, 192, 254, 255 };
        foreach (byte r in levels)
        foreach (byte g in levels)
        foreach (byte b in levels)
            AssertRgbRoundTrip(r, g, b);

        for (int gray = 0; gray <= 255; gray++)
            AssertRgbRoundTrip((byte)gray, (byte)gray, (byte)gray);
    }

    [Theory]
    [InlineData(0.008856, -0.0018)]
    [InlineData(216.0 / 24389.0, 0.0)]
    [InlineData(0.008857, 0.0021)]
    public void XyzToLabIsAccurateAcrossDarkBranchBoundary(double relativeX, double expectedA)
    {
        // Y/Yn and Z/Zn are at the exact CIE threshold; a* was calculated independently in Python.
        CIELAB result = ChromaticityConversion.XYZToLab(
            new CIEXYZ { CIEX = 95.047 * relativeX, CIEY = 100 * (216.0 / 24389.0), CIEZ = 108.883 * (216.0 / 24389.0) },
            ConventionalD65White);

        Assert.Equal(8.0, result.CIEL);
        Assert.Equal(expectedA, result.CIEA);
        Assert.Equal(0.0, result.CIEB);
    }

    [Fact]
    public void InverseConversionsRejectNull()
    {
        Assert.Throws<ArgumentNullException>("labColor", () => ChromaticityConversion.LabToXYZ(null!, Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentNullException>("luvColor", () => ChromaticityConversion.LuvToXYZ(null!, Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentNullException>("rgbColor", () => ChromaticityConversion.RGBToXYZ(null!));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InverseConversionsRejectEveryNonFiniteCoordinate(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>("labColor", () => ChromaticityConversion.LabToXYZ(new CIELAB(value, 0, 0), Standardilluminant.D65, StandardObserver.Degree2));
        // The Lab model now rejects non-finite a*/b* before conversion.
        Assert.Throws<ArgumentOutOfRangeException>("CIEA", () => new CIELAB(50, value, 0));
        Assert.Throws<ArgumentOutOfRangeException>("CIEB", () => new CIELAB(50, 0, value));

        foreach (CIELuv color in new[] { new CIELuv { CIEL = value }, new CIELuv { CIEL = 50, CIEu = value }, new CIELuv { CIEL = 50, CIEv = value } })
            Assert.Throws<ArgumentOutOfRangeException>("luvColor", () => ChromaticityConversion.LuvToXYZ(color, Standardilluminant.D65, StandardObserver.Degree2));
    }

    [Fact]
    public void InverseConversionsRejectNegativeLightnessAndUndefinedConditions()
    {
        Assert.Throws<ArgumentOutOfRangeException>("labColor", () => ChromaticityConversion.LabToXYZ(new CIELAB(-1, 0, 0), Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>("luvColor", () => ChromaticityConversion.LuvToXYZ(new CIELuv { CIEL = -1 }, Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>("illuminant", () => ChromaticityConversion.LabToXYZ(new CIELAB(), (Standardilluminant)999, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>("illuminant", () => ChromaticityConversion.LuvToXYZ(new CIELuv(), (Standardilluminant)999, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>("observer", () => ChromaticityConversion.LabToXYZ(new CIELAB(), Standardilluminant.D65, (StandardObserver)999));
        Assert.Throws<ArgumentOutOfRangeException>("observer", () => ChromaticityConversion.LuvToXYZ(new CIELuv(), Standardilluminant.D65, (StandardObserver)999));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    public void LuvToXyzRejectsNonzeroChromaAtZeroLightness(double u, double v)
    {
        Assert.Throws<ArgumentException>("luvColor", () => ChromaticityConversion.LuvToXYZ(
            new CIELuv { CIEL = 0, CIEu = u, CIEv = v }, Standardilluminant.D65, StandardObserver.Degree2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LuvToXyzRejectsNonpositiveReconstructedVPrime(double offset)
    {
        double referenceV = 900.0 / (95.047 + 1500.0 + 3.0 * 108.883);
        CIELuv color = new() { CIEL = 50, CIEv = -13.0 * 50.0 * referenceV + offset };

        Assert.Throws<ArgumentException>("luvColor", () => ChromaticityConversion.LuvToXYZ(color, ConventionalD65White));
    }

    [Fact]
    public void InverseConversionsRejectOverflow()
    {
        Assert.Throws<ArgumentException>("labColor", () => ChromaticityConversion.LabToXYZ(new CIELAB(double.MaxValue, 0, 0), Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentException>("labColor", () => ChromaticityConversion.LabToXYZ(new CIELAB(50, double.MaxValue, 0), Standardilluminant.D65, StandardObserver.Degree2));
        Assert.Throws<ArgumentException>("luvColor", () => ChromaticityConversion.LuvToXYZ(new CIELuv { CIEL = double.MaxValue }, Standardilluminant.D65, StandardObserver.Degree2));
    }

    private static CIEXYZ ConventionalD65White => new() { CIEX = 95.047, CIEY = 100, CIEZ = 108.883 };

    private static void AssertReferenceXyz(CIEXYZ result, double x, double y, double z)
    {
        Assert.Equal(Math.Round(x, 4, MidpointRounding.AwayFromZero), result.CIEX);
        Assert.Equal(Math.Round(y, 4, MidpointRounding.AwayFromZero), result.CIEY);
        Assert.Equal(Math.Round(z, 4, MidpointRounding.AwayFromZero), result.CIEZ);
        AssertCloseXyz(new CIEXYZ { CIEX = x, CIEY = y, CIEZ = z }, result, 0.00005);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEX);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEY);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.CIEZ);
    }

    private static void AssertCloseXyz(CIEXYZ expected, CIEXYZ actual, double tolerance)
    {
        Assert.InRange(Math.Abs(actual.CIEX - expected.CIEX), 0, tolerance);
        Assert.InRange(Math.Abs(actual.CIEY - expected.CIEY), 0, tolerance);
        Assert.InRange(Math.Abs(actual.CIEZ - expected.CIEZ), 0, tolerance);
    }

    private static void AssertRgbRoundTrip(byte r, byte g, byte b)
    {
        CIERGB result = ChromaticityConversion.XYZToRGB(ChromaticityConversion.RGBToXYZ(
            new CIERGB { redValue = r, greenValue = g, blueValue = b }));

        Assert.Equal(r, result.redValue);
        Assert.Equal(g, result.greenValue);
        Assert.Equal(b, result.blueValue);
    }
}
