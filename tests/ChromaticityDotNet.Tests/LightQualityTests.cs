using System.Text.Json;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests;

public class LightQualityTests
{
    private static JsonElement Fixtures
    {
        get
        {
            using var document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "light-quality.json")));
            return document.RootElement.Clone();
        }
    }
    public static IEnumerable<object[]> Catalog => CieSpectralData.Illuminants.Select(info => new object[] { info.Id });

    public static IEnumerable<object[]> CctFixtures => Fixtures.GetProperty("cct").EnumerateArray().Select(item => new object[]
    {
        item.GetProperty("observer").GetInt32() == 2 ? StandardObserver.Degree2 : StandardObserver.Degree10,
        item.GetProperty("x").GetDouble(), item.GetProperty("y").GetDouble(), item.GetProperty("cct").GetDouble()
    });

    public static IEnumerable<object[]> CriFixtures => Fixtures.GetProperty("cri").EnumerateArray().Select(item => new object[]
    {
        item.GetProperty("id").GetString()!, item.GetProperty("cct").GetDouble(), item.GetProperty("ra").GetDouble(),
        item.GetProperty("indices").EnumerateArray().Select(value => value.GetDouble()).ToArray()
    });

    public static IEnumerable<object[]> PlanckFixtures => Fixtures.GetProperty("cct").EnumerateArray()
        .Where(item => item.TryGetProperty("duv", out var duv) && duv.GetDouble() == 0).Select(item => new object[]
        {
            item.GetProperty("observer").GetInt32() == 2 ? StandardObserver.Degree2 : StandardObserver.Degree10,
            item.GetProperty("cct").GetDouble(), item.GetProperty("x").GetDouble(), item.GetProperty("y").GetDouble()
        });

    [Theory]
    [MemberData(nameof(PlanckFixtures))]
    public void DiagramPlanckPointsMatchIndependentSpectralFixtures(StandardObserver observer, double temperature, double x, double y)
    {
        var point = ChromaticityConversion.GetPlanckianChromaticity(temperature, observer);
        Assert.Equal(temperature, point.Temperature);
        Assert.InRange(Math.Abs(point.X - x), 0, 1e-11);
        Assert.InRange(Math.Abs(point.Y - y), 0, 1e-11);
        var uv = ChromaticityConversion.xyToLuv1960(new CIExyY { CIEx = point.X, CIEy = point.Y });
        Assert.InRange(Math.Abs(point.U - uv.CIEu), 0, 1e-14);
        Assert.InRange(Math.Abs(point.V - uv.CIEv), 0, 1e-14);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void DiagramLocusIsImmutableAndCoversTheCctSearchRange(StandardObserver observer)
    {
        var locus = ChromaticityConversion.GetPlanckianLocus(observer);
        Assert.Equal(961, locus.Count);
        Assert.Equal(25000, locus[0].Temperature);
        Assert.Equal(1000, locus[^1].Temperature);
        Assert.All(locus, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
        var first = ChromaticityConversion.GetPlanckianChromaticity(25000, observer);
        Assert.Equal(first.U, locus[0].U);
        Assert.Equal(first.V, locus[0].V);
        Assert.Throws<NotSupportedException>(() => ((IList<PlanckianChromaticityPoint>)locus)[0] = first);
        Assert.NotEqual(ChromaticityConversion.GetPlanckianLocus(StandardObserver.Degree2)[0].X,
            ChromaticityConversion.GetPlanckianLocus(StandardObserver.Degree10)[0].X);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, 0.3127, 0.329)]
    [InlineData(StandardObserver.Degree10, 0.3127, 0.329)]
    [InlineData(StandardObserver.Degree2, 0.3, 0.6)]
    [InlineData(StandardObserver.Degree10, 0.4, 0.2)]
    public void NearestPointReconstructsDuvEvenForInapplicableCct(StandardObserver observer, double x, double y)
    {
        var uv = ChromaticityConversion.xyToLuv1960(new CIExyY { CIEx = x, CIEy = y });
        var result = ChromaticityConversion.luv1960ToCCTResult(uv, observer);
        var nearest = result.NearestPoint;
        var expected = ChromaticityConversion.GetPlanckianChromaticity(nearest.Temperature, observer);
        Assert.Equal(expected.U, nearest.U);
        Assert.Equal(expected.V, nearest.V);
        double distance = Math.Sqrt(Math.Pow(uv.CIEu - nearest.U, 2) + Math.Pow(uv.CIEv - nearest.V, 2));
        Assert.Equal(Math.Abs(result.Duv), distance, 14);
        if (result.Temperature.HasValue) Assert.InRange(Math.Abs(result.Temperature.Value - nearest.Temperature), 0, 0.00005);
        else Assert.True(distance > 0.05 || !result.IsInTemperatureRange);
    }

    [Fact]
    public void OutOfRangeTemperatureUsesAConstrainedEndpointWithoutReportingValidCct()
    {
        var xyz = ChromaticityMatch.GetStandardWhitePoint(BlackBody(30000), StandardObserver.Degree2);
        var result = ChromaticityConversion.luv1960ToCCTResult(
            ChromaticityConversion.xyToLuv1960(ChromaticityConversion.XYZToxyY(xyz)));
        Assert.False(result.IsInTemperatureRange);
        Assert.Null(result.Temperature);
        Assert.InRange(result.NearestPoint.Temperature, 24999.99, 25000);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(25001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidPlanckTemperaturesAreRejected(double temperature) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.GetPlanckianChromaticity(temperature));

    [Fact]
    public void PlanckDiagramApisRejectInvalidObservers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.GetPlanckianLocus((StandardObserver)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.GetPlanckianChromaticity(6500, (StandardObserver)99));
    }

    [Fact]
    public void Uv1960UsesSixYAndPreservesCoordinatePrecision()
    {
        var xy = new CIExyY { CIEx = 0.3127, CIEy = 0.329, CIEY = 100 };
        var uv = ChromaticityConversion.xyToLuv1960(xy);
        Assert.Equal(0.1978300066428368, uv.CIEu, 14);
        Assert.Equal(0.312213329959194, uv.CIEv, 14);
        var prime = ChromaticityConversion.xyTouv(xy);
        Assert.InRange(Math.Abs(prime.CIEv - uv.CIEv * 1.5), 0, 0.00005);
        Assert.NotEqual(prime.CIEv, uv.CIEv);
        xy.CIEY = 0;
        Assert.Equal(uv.CIEv, ChromaticityConversion.xyToLuv1960(xy).CIEv);
        Assert.InRange(ChromaticityConversion.xyToCCT(xy), 6504, 6506);
    }

    [Theory]
    [MemberData(nameof(CctFixtures))]
    public void MinimumSearchMatchesIndependentScipyAndPlanckFixtures(StandardObserver observer, double x, double y, double expected)
    {
        var uv = ChromaticityConversion.xyToLuv1960(new CIExyY { CIEx = x, CIEy = y });
        var result = ChromaticityConversion.luv1960ToCCTResult(uv, observer);
        Assert.NotNull(result.Temperature);
        Assert.InRange(Math.Abs(result.Temperature.Value - expected), 0, 0.02);
        Assert.Equal(result.Temperature, ChromaticityConversion.luv1960ToCCT(uv, observer));
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.Temperature.Value);
    }

    [Theory]
    [MemberData(nameof(CriFixtures))]
    public void CriMatchesIndependentColourScienceForAllFourteenSamples(string id, double cct, double ra, double[] indices)
    {
        var result = ChromaticityConversion.SPDToCRI(id);
        Assert.NotNull(result.Ra);
        Assert.InRange(Math.Abs(result.Cct.Temperature!.Value - cct), 0, 0.02);
        Assert.InRange(Math.Abs(result.Ra.Value - ra), 0, 0.0002);
        Assert.Equal(14, result.SpecialIndices.Count);
        for (int i = 0; i < 14; i++) Assert.InRange(Math.Abs(result.SpecialIndices[i] - indices[i]), 0, 0.0002);
        Assert.InRange(Math.Abs(result.Ra.Value - result.SpecialIndices.Take(8).Average()), 0, 0.0001);
    }

    [Fact]
    public void DuvHasTheExpectedSignAndFarChromaticitiesDoNotHaveCct()
    {
        var d65 = ChromaticityConversion.xyToLuv1960(new CIExyY { CIEx = 0.3127, CIEy = 0.329 });
        Assert.InRange(ChromaticityConversion.luv1960ToCCTResult(d65).Duv, 0.003, 0.004);
        d65.CIEv -= 0.01;
        Assert.True(ChromaticityConversion.luv1960ToCCTResult(d65).Duv < 0);
        var green = ChromaticityConversion.xyToLuv1960(new CIExyY { CIEx = 0.3, CIEy = 0.6 });
        Assert.Null(ChromaticityConversion.luv1960ToCCTResult(green).Temperature);
        Assert.True(double.IsNaN(ChromaticityConversion.luv1960ToCCT(green)));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(2856)]
    [InlineData(4500)]
    [InlineData(25000)]
    public void PlanckSpectraRecoverTemperatureAndSub5000Rendering(double temperature)
    {
        var spectrum = BlackBody(temperature);
        var white = ChromaticityMatch.GetStandardWhitePoint(spectrum, StandardObserver.Degree2);
        double sum = white.CIEX + white.CIEY + white.CIEZ;
        var cct = ChromaticityConversion.luv1960ToCCTResult(ChromaticityConversion.xyToLuv1960(
            new CIExyY { CIEx = white.CIEX / sum, CIEy = white.CIEY / sum }));
        Assert.InRange(Math.Abs(cct.Temperature!.Value - temperature), 0, 0.01);
        if (temperature > 1000 && temperature < 5000)
            Assert.InRange(ChromaticityConversion.SPDToCRI(spectrum).Ra!.Value, 99.999, 100);
    }

    [Theory]
    [InlineData(4999, "Planckian")]
    [InlineData(5001, "CIE daylight")]
    public void CriReferenceChangesAt5000Kelvin(double temperature, string reference) =>
        Assert.Equal(reference, ChromaticityConversion.SPDToCRI(BlackBody(temperature)).ReferenceType);

    [Fact]
    public void CriIsScaleInvariantKeepsNegativeIndicesAndDoesNotMutateInput()
    {
        var spectrum = CieSpectralData.GetIlluminantSpectrum("FL2");
        var original = (double[])spectrum.Spectrums!.Clone();
        var expected = ChromaticityConversion.SPDToCRI(spectrum);
        Assert.True(expected.SpecialIndices[8] < 0);
        Assert.Equal(original, spectrum.Spectrums);
        foreach (var factor in new[] { 1e-200, 1e200 })
        {
            spectrum.Spectrums = original.Select(value => value * factor).ToArray();
            var scaled = ChromaticityConversion.SPDToCRI(spectrum);
            Assert.Equal(expected.Ra, scaled.Ra);
            Assert.Equal(expected.SpecialIndices, scaled.SpecialIndices);
        }
        Assert.Equal(expected.Ra, ChromaticityConversion.SPDToCRI(Standardilluminant.CWF).Ra);
    }

    [Theory]
    [MemberData(nameof(Catalog))]
    public void EveryCatalogLightReportsValidIndicesOrAnExplicitUnavailableState(string id)
    {
        var result = ChromaticityConversion.SPDToCRI(id);
        if (result.Ra.HasValue)
        {
            Assert.Equal(14, result.SpecialIndices.Count);
            Assert.All(result.SpecialIndices, value => Assert.True(double.IsFinite(value)));
            Assert.InRange(result.ReferenceWhiteDistance!.Value, 0, 0.0054);
        }
        else
        {
            Assert.Empty(result.SpecialIndices);
            Assert.True(result.Cct.Temperature is null || result.ReferenceWhiteDistance > 0.0054);
        }
    }

    [Fact]
    public void FarWhiteOrExcessiveReferenceSeparationMakesCriUnavailable()
    {
        var monochromatic = new Spectrum { StartingWavelength = 380, EndingWavelength = 780,
            WavelengthInterval = 1, Spectrums = Enumerable.Range(380, 401).Select(w => w == 550 ? 1d : 0d).ToArray() };
        var result = ChromaticityConversion.SPDToCRI(monochromatic);
        Assert.Null(result.Ra);
        Assert.Empty(result.SpecialIndices);
        var mixed = BlackBody(6500);
        mixed.Spectrums![190] += 50; // Non-Planckian green power at 550 nm.
        result = ChromaticityConversion.SPDToCRI(mixed);
        Assert.NotNull(result.Cct.Temperature);
        Assert.True(result.ReferenceWhiteDistance > 0.0054);
        Assert.Null(result.Ra);
    }

    [Fact]
    public void InvalidCoordinatesSpectraAndIncompleteCoverageAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.xyToLuv1960(null!));
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.luv1960ToCCT(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.xyToLuv1960(new CIExyY { CIEx = double.NaN }));
        foreach (var uv in new[] { new CIEuv1960 { CIEu = double.PositiveInfinity, CIEv = 0.3 },
            new CIEuv1960 { CIEu = -0.1, CIEv = 0.3 }, new CIEuv1960 { CIEu = 0.2, CIEv = 0 } })
            Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.luv1960ToCCT(uv));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityConversion.luv1960ToCCT(new CIEuv1960 { CIEu = 0.2, CIEv = 0.3 }, (StandardObserver)99));
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.SPDToCRI((Spectrum)null!));
        var spectrum = new Spectrum { StartingWavelength = 400, EndingWavelength = 700, WavelengthInterval = 10,
            Spectrums = Enumerable.Repeat(1d, 31).ToArray() };
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.SPDToCRI(spectrum));
        spectrum.StartingWavelength = 380; spectrum.EndingWavelength = 780;
        spectrum.Spectrums = new double[41];
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.SPDToCRI(spectrum));
        spectrum.Spectrums[0] = double.NaN;
        Assert.Throws<ArgumentException>(() => ChromaticityConversion.SPDToCRI(spectrum));
    }

    private static Spectrum BlackBody(double temperature) => new()
    {
        StartingWavelength = 360, EndingWavelength = 830, WavelengthInterval = 1,
        Spectrums = Enumerable.Range(360, 471).Select(w => Math.Pow(560.0 / w, 5)
            * (Math.Exp(0.014388 / (560e-9 * temperature)) - 1) / (Math.Exp(0.014388 / (w * 1e-9 * temperature)) - 1)).ToArray()
    };
}
