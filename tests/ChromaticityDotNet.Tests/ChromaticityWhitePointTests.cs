using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests;

public class ChromaticityWhitePointTests
{
    public static IEnumerable<object[]> CatalogObservers => CieSpectralData.Illuminants.SelectMany(info =>
        new[] { StandardObserver.Degree2, StandardObserver.Degree10 }.Select(observer => new object[] { info.Id, observer }));

    [Theory]
    [MemberData(nameof(CatalogObservers))]
    public void EveryCatalogWhiteNormalizesReflectorsAndSupportsBothConversionDirections(string id, StandardObserver observer)
    {
        var white = ChromaticityMatch.GetStandardWhitePoint(id, observer);
        Assert.True(double.IsFinite(white.CIEX) && white.CIEX > 0);
        Assert.True(double.IsFinite(white.CIEZ) && white.CIEZ > 0);
        Assert.Equal(100, white.CIEY);
        var spectrum = CieSpectralData.GetIlluminantSpectrum(id);
        int start = Math.Max(360, spectrum.StartingWavelength), end = Math.Min(830, spectrum.EndingWavelength);
        var reflector = new Spectrum { StartingWavelength = start, EndingWavelength = end,
            WavelengthInterval = 1, Spectrums = Enumerable.Repeat(100.0, end - start + 1).ToArray() };
        var xyz = ChromaticityConversion.REFToXYZ(reflector, id, observer);
        Assert.Equal(Math.Round(white.CIEX, 4, MidpointRounding.AwayFromZero), xyz.CIEX);
        Assert.Equal(Math.Round(white.CIEZ, 4, MidpointRounding.AwayFromZero), xyz.CIEZ);
        var lab = ChromaticityConversion.XYZToLab(white, id, observer);
        var luv = ChromaticityConversion.XYZToLuv(white, id, observer);
        Assert.Equal(100, lab.CIEL); Assert.Equal(0, lab.CIEC);
        Assert.Equal(100, luv.CIEL); Assert.Equal(0, luv.CIEu); Assert.Equal(0, luv.CIEv);
        Assert.Equal(xyz.CIEX, ChromaticityConversion.LabToXYZ(lab, id, observer).CIEX);
        Assert.Equal(xyz.CIEZ, ChromaticityConversion.LuvToXYZ(luv, id, observer).CIEZ);
        var sample = new CIELAB(50, 20, -30);
        var roundTrip = ChromaticityConversion.XYZToLab(ChromaticityConversion.LabToXYZ(sample, id, observer), id, observer);
        // Four-decimal XYZ quantization is amplified when converting back to Lab.
        Assert.InRange(Math.Abs(roundTrip.CIEA - 20), 0, 0.001);
        Assert.InRange(Math.Abs(roundTrip.CIEB + 30), 0, 0.001);
    }

    [Theory]
    [InlineData("D65", StandardObserver.Degree2, 0.3127, 0.3290)]
    [InlineData("D65", StandardObserver.Degree10, 0.3138, 0.3310)]
    [InlineData("D50", StandardObserver.Degree2, 0.3457, 0.3585)]
    [InlineData("D50", StandardObserver.Degree10, 0.3477, 0.3595)]
    [InlineData("A", StandardObserver.Degree2, 0.4476, 0.4074)]
    public void IntegratedChromaticitiesMatchStandardRoundedWhiteCoordinates(string id, StandardObserver observer, double x, double y)
    {
        var white = ChromaticityMatch.GetStandardWhitePoint(id, observer);
        double sum = white.CIEX + white.CIEY + white.CIEZ;
        Assert.InRange(Math.Abs(white.CIEX / sum - x), 0, 0.00005);
        Assert.InRange(Math.Abs(white.CIEY / sum - y), 0, 0.00005);
        var sample = new CIExyY { CIEx = white.CIEX / sum, CIEy = white.CIEY / sum, CIEY = 30 };
        Assert.True(ChromaticityConversion.xyYToWavelengths(sample, observer, id).IsAchromatic);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void ExplicitReflectanceRangeKeepsGrayNeutral(StandardObserver observer)
    {
        var white = ChromaticityMatch.GetStandardWhitePoint("D50", observer, 420, 700);
        var reflector = new Spectrum { StartingWavelength = 420, EndingWavelength = 700,
            WavelengthInterval = 10, Spectrums = Enumerable.Repeat(18.0, 29).ToArray() };
        var xyz = ChromaticityConversion.REFToXYZ(reflector, "D50", observer);
        var lab = ChromaticityConversion.XYZToLab(xyz, white);
        var luv = ChromaticityConversion.XYZToLuv(xyz, white);
        Assert.InRange(Math.Abs(lab.CIEA), 0, 0.0002); Assert.InRange(Math.Abs(lab.CIEB), 0, 0.0002);
        Assert.InRange(Math.Abs(luv.CIEu), 0, 0.0006); Assert.InRange(Math.Abs(luv.CIEv), 0, 0.0006);
    }

    [Fact]
    public void FiveNanometerIlluminantsAreInterpolatedAndRelativeScaleCancels()
    {
        var spectrum = new Spectrum { StartingWavelength = 360, EndingWavelength = 830, WavelengthInterval = 5,
            Spectrums = Enumerable.Range(0, 95).Select(i => 10.0 + i).ToArray() };
        var cmf = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < 471; i++)
        {
            double power = 10 + i / 5.0;
            x += power * cmf.X.Spectrums![i]; y += power * cmf.Y.Spectrums![i]; z += power * cmf.Z.Spectrums![i];
        }
        var white = ChromaticityMatch.GetStandardWhitePoint(spectrum, StandardObserver.Degree2);
        Assert.Equal(x / y * 100, white.CIEX, 11); Assert.Equal(z / y * 100, white.CIEZ, 11);
        spectrum.Spectrums = spectrum.Spectrums.Select(value => value * 7).ToArray();
        var scaled = ChromaticityMatch.GetStandardWhitePoint(spectrum, StandardObserver.Degree2);
        Assert.Equal(white.CIEX, scaled.CIEX, 11); Assert.Equal(white.CIEZ, scaled.CIEZ, 11);
    }

    [Fact]
    public void WhitePointQueriesRejectInvalidInputsAndReturnIndependentResults()
    {
        Assert.Throws<ArgumentNullException>(() => ChromaticityMatch.GetStandardWhitePoint((string)null!, StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityMatch.GetStandardWhitePoint("unknown", StandardObserver.Degree2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityMatch.GetStandardWhitePoint("D65", (StandardObserver)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityMatch.GetStandardWhitePoint("FL2", StandardObserver.Degree2, 360, 780));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityMatch.GetStandardWhitePoint("D65", StandardObserver.Degree2, 780, 380));
        var zero = new Spectrum { StartingWavelength = 380, EndingWavelength = 780, WavelengthInterval = 10, Spectrums = new double[41] };
        Assert.Throws<ArgumentException>(() => ChromaticityMatch.GetStandardWhitePoint(zero, StandardObserver.Degree2));
        zero.Spectrums![0] = double.NaN;
        Assert.Throws<ArgumentException>(() => ChromaticityMatch.GetStandardWhitePoint(zero, StandardObserver.Degree2));
        var white = ChromaticityMatch.GetStandardWhitePoint("d65", StandardObserver.Degree2);
        white.CIEX = 0;
        Assert.True(ChromaticityMatch.GetStandardWhitePoint("D65", StandardObserver.Degree2).CIEX > 0);
    }
}
