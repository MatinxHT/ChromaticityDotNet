using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests;

public class CieStandardIlluminantTests
{
    public static IEnumerable<object[]> Illuminants => Enum.GetValues<Standardilluminant>().Distinct()
        .Select(light => new object[] { light });

    [Fact]
    public void EnumCoversExactlyTheCatalogAndRetainsCompatibilityAliases()
    {
        var values = Enum.GetValues<Standardilluminant>();
        Assert.Equal(54, values.Length);
        Assert.Equal(54, values.Distinct().Count());
        Assert.Equal(CieSpectralData.Illuminants.Select(info => info.Id).OrderBy(id => id),
            values.Select(CieSpectralData.GetIlluminantId).Distinct().OrderBy(id => id));
        Assert.NotEqual(Standardilluminant.CWF, Standardilluminant.FL2);
        Assert.NotEqual(Standardilluminant.F7, Standardilluminant.FL7);
        Assert.NotEqual(Standardilluminant.TL84, Standardilluminant.FL11);
        Assert.NotEqual(Standardilluminant.U30, Standardilluminant.FL12);
    }

    [Theory]
    [InlineData(Standardilluminant.D65, 0)]
    [InlineData(Standardilluminant.A, 1)]
    [InlineData(Standardilluminant.CWF, 2)]
    [InlineData(Standardilluminant.F7, 3)]
    [InlineData(Standardilluminant.TL84, 4)]
    [InlineData(Standardilluminant.U30, 5)]
    public void OriginalSerializedEnumValuesAreUnchanged(Standardilluminant light, int value)
    {
        Assert.Equal(value, (int)light);
        Assert.Equal(new[] { "D65", "A", "CWF", "F7", "TL84", "U30" }[value], light.ToString());
    }

    [Theory]
    [InlineData(Standardilluminant.CWF, Standardilluminant.FL2, "FL2")]
    [InlineData(Standardilluminant.F7, Standardilluminant.FL7, "FL7")]
    [InlineData(Standardilluminant.TL84, Standardilluminant.FL11, "FL11")]
    [InlineData(Standardilluminant.U30, Standardilluminant.FL12, "FL12")]
    public void CompatibilityNamesAndCanonicalNamesUseTheSameCieData(
        Standardilluminant oldName, Standardilluminant newName, string id)
    {
        Assert.Equal(id, CieSpectralData.GetIlluminantId(oldName));
        Assert.Equal(id, CieSpectralData.GetIlluminantId(newName));
        var legacy = ChromaticityMatch.GetStandardilluminantdata(oldName);
        var canonical = ChromaticityMatch.GetStandardilluminantdata(newName);
        Assert.Equal(oldName, legacy.IlluminantName);
        Assert.Equal(newName, canonical.IlluminantName);
        Assert.Equal(legacy.Spectrum.Spectrums, canonical.Spectrum.Spectrums);
        AssertXyz(legacy.WhitePoint_Degree2.WhitePointXnYnZn!, canonical.WhitePoint_Degree2.WhitePointXnYnZn!);
        AssertXyz(legacy.WhitePoint_Degree10.WhitePointXnYnZn!, canonical.WhitePoint_Degree10.WhitePointXnYnZn!);
    }

    [Theory]
    [MemberData(nameof(Illuminants))]
    public void EveryEnumUsesNativeCieDataForItsSamplesAndFullRangeWhites(Standardilluminant light)
    {
        string id = CieSpectralData.GetIlluminantId(light);
        var native = CieSpectralData.GetIlluminantSpectrum(id);
        var enumSpectrum = CieSpectralData.GetIlluminantSpectrum(light);
        Assert.Equal(native.StartingWavelength, enumSpectrum.StartingWavelength);
        Assert.Equal(native.EndingWavelength, enumSpectrum.EndingWavelength);
        Assert.Equal(native.WavelengthInterval, enumSpectrum.WavelengthInterval);
        Assert.Equal(native.Spectrums, enumSpectrum.Spectrums);
        var data = ChromaticityMatch.GetStandardilluminantdata(light);
        Assert.Same(data, ChromaticityMatch.GetStandardilluminantdata(light));
        Assert.Equal(light, data.IlluminantName);
        var samples = data.Spectrum;
        Assert.Equal(400, samples.StartingWavelength);
        Assert.Equal(700, samples.EndingWavelength);
        Assert.Equal(10, samples.WavelengthInterval);
        Assert.Equal(31, samples.Spectrums!.Length);
        for (int i = 0; i < 31; i++)
            Assert.Equal(native.Spectrums![(400 + i * 10 - native.StartingWavelength) / native.WavelengthInterval],
                samples.Spectrums[i]);

        foreach (var observer in new[] { StandardObserver.Degree2, StandardObserver.Degree10 })
        {
            var expected = ChromaticityMatch.GetStandardWhitePoint(id, observer);
            var white = observer == StandardObserver.Degree2 ? data.WhitePoint_Degree2 : data.WhitePoint_Degree10;
            Assert.Equal(observer, white.Observer);
            AssertXyz(expected, white.WhitePointXnYnZn!);
            AssertXyz(expected, ChromaticityMatch.GetStandardWhitePoint(light, observer));
            // Check that all expanded enum conversion paths use this same reference white.
            var lab = ChromaticityConversion.XYZToLab(expected, light, observer);
            var luv = ChromaticityConversion.XYZToLuv(expected, light, observer);
            Assert.Equal(100, lab.CIEL);
            Assert.Equal(0, lab.CIEC);
            Assert.Equal(100, luv.CIEL);
            Assert.Equal(0, luv.CIEu);
            Assert.Equal(0, luv.CIEv);
            var reflector = new Spectrum
            {
                StartingWavelength = Math.Max(360, native.StartingWavelength),
                EndingWavelength = Math.Min(830, native.EndingWavelength),
                WavelengthInterval = 1
            };
            reflector.Spectrums = Enumerable.Repeat(100.0,
                reflector.EndingWavelength - reflector.StartingWavelength + 1).ToArray();
            var xyz = ChromaticityConversion.REFToXYZ(reflector, light, observer);
            Assert.Equal(Math.Round(expected.CIEX, 4, MidpointRounding.AwayFromZero), xyz.CIEX);
            Assert.Equal(100, xyz.CIEY);
            Assert.Equal(Math.Round(expected.CIEZ, 4, MidpointRounding.AwayFromZero), xyz.CIEZ);
        }
    }

    [Theory]
    [MemberData(nameof(Illuminants))]
    public void ReturnedMutableObjectsCannotChangeTheCachedSnapshot(Standardilluminant light)
    {
        var data = ChromaticityMatch.GetStandardilluminantdata(light);
        var spectrum = data.Spectrum;
        double first = spectrum.Spectrums![0];
        spectrum.Spectrums[0] = -1;
        spectrum.StartingWavelength = 1;
        Assert.Equal(first, data.Spectrum.Spectrums![0]);
        Assert.Equal(400, data.Spectrum.StartingWavelength);
        foreach (var observer in new[] { StandardObserver.Degree2, StandardObserver.Degree10 })
        {
            var white = observer == StandardObserver.Degree2 ? data.WhitePoint_Degree2 : data.WhitePoint_Degree10;
            double expectedX = white.WhitePointXnYnZn!.CIEX;
            white.WhitePointXnYnZn.CIEX = -1;
            white.Observer = (StandardObserver)99;
            white.WhitePointXnYnZn = null;
            var next = observer == StandardObserver.Degree2 ? data.WhitePoint_Degree2 : data.WhitePoint_Degree10;
            Assert.Equal(expectedX, next.WhitePointXnYnZn!.CIEX);
            Assert.Equal(observer, next.Observer);
        }
    }

    [Theory]
    [MemberData(nameof(StandardChromaticityModelTests.Illuminants), MemberType = typeof(StandardChromaticityModelTests))]
    public void DirectConstructionOfLegacyClassesUsesTheSharedCieSnapshot(
        IStandardilluminant legacy, Standardilluminant light)
    {
        var data = ChromaticityMatch.GetStandardilluminantdata(light);
        Assert.Equal(data.Spectrum.Spectrums, legacy.Spectrum.Spectrums);
        AssertXyz(data.WhitePoint_Degree2.WhitePointXnYnZn!, legacy.WhitePoint_Degree2.WhitePointXnYnZn!);
        AssertXyz(data.WhitePoint_Degree10.WhitePointXnYnZn!, legacy.WhitePoint_Degree10.WhitePointXnYnZn!);
        var changed = legacy.Spectrum;
        changed.Spectrums![0] = -1;
        Assert.True(data.Spectrum.Spectrums![0] >= 0);
    }

    [Fact]
    public void ConcurrentLookupsShareAStableSnapshotAndReturnIndependentCopies()
    {
        var expected = ChromaticityMatch.GetStandardWhitePoint("LED-BH1", StandardObserver.Degree10);
        var results = new IStandardilluminant[64];
        Parallel.For(0, results.Length, i =>
        {
            var data = ChromaticityMatch.GetStandardilluminantdata(Standardilluminant.LED_BH1);
            results[i] = data;
            AssertXyz(expected, data.WhitePoint_Degree10.WhitePointXnYnZn!);
            data.Spectrum.Spectrums![0] = -1;
        });
        Assert.All(results, data => Assert.Same(results[0], data));
        Assert.True(results[0].Spectrum.Spectrums![0] >= 0);
    }

    [Fact]
    public void ReferenceWhitesUseNativeFullRangeDataRatherThanThe31PointSpectrum()
    {
        var data = new TL84();
        var full = data.WhitePoint_Degree2.WhitePointXnYnZn!;
        var resampled = ChromaticityMatch.GetStandardWhitePoint(data.Spectrum, StandardObserver.Degree2);
        Assert.True(Math.Abs(full.CIEZ - resampled.CIEZ) > 1);
        Assert.Equal(100, full.CIEY);
        Assert.Throws<ArgumentOutOfRangeException>(() => CieSpectralData.GetIlluminantId((Standardilluminant)999));
    }

    private static void AssertXyz(CIEXYZ expected, CIEXYZ actual)
    {
        Assert.Equal(expected.CIEX, actual.CIEX);
        Assert.Equal(expected.CIEY, actual.CIEY);
        Assert.Equal(expected.CIEZ, actual.CIEZ);
    }
}
