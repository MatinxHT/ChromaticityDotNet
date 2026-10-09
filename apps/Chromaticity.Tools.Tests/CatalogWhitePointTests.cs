using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using System.Globalization;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class CatalogWhitePointTests
{
    public static IEnumerable<object[]> LightsAndObservers => CieSpectralData.Illuminants.SelectMany(info =>
        new[] { StandardObserver.Degree2, StandardObserver.Degree10 }.Select(observer => new object[] { info.Id, observer }));

    [Fact]
    public void ReferenceSelectionsContainTheWholeCatalogAndKeepD65AsDefault()
    {
        Assert.Equal("D65", ToolCalculations.Illuminants[0].Id);
        Assert.Equal(50, ToolCalculations.Illuminants.Length);
        Assert.Equal(CieSpectralData.Illuminants.Select(info => info.Id).OrderBy(id => id),
            ToolCalculations.Illuminants.Select(info => info.Id).OrderBy(id => id));
        Assert.Equal("FL2", ToolCalculations.IlluminantId(Standardilluminant.CWF));
        Assert.Equal("FL7", ToolCalculations.IlluminantId(Standardilluminant.F7));
        Assert.Equal("FL11", ToolCalculations.IlluminantId(Standardilluminant.TL84));
        Assert.Equal("FL12", ToolCalculations.IlluminantId(Standardilluminant.U30));
    }

    [Theory]
    [MemberData(nameof(LightsAndObservers))]
    public void AllThreeToolsUseIntegratedCatalogWhites(string id, StandardObserver observer)
    {
        var spectrum = CieSpectralData.GetIlluminantSpectrum(id);
        int start = Math.Max(360, spectrum.StartingWavelength), end = Math.Min(830, spectrum.EndingWavelength);
        var gray = ToolCalculations.Reflectance(string.Join('\n', Enumerable.Repeat("18", end - start + 1)),
            start, end, 1, false, id, observer).Table;
        double Coordinate(int column) => double.Parse(gray.Rows[0][column], CultureInfo.InvariantCulture);
        Assert.Equal(18, Coordinate(2));
        // Warm whites with small Z amplify the preceding four-decimal XYZ quantization.
        Assert.InRange(Math.Abs(Coordinate(5)), 0, 0.001);
        Assert.InRange(Math.Abs(Coordinate(6)), 0, 0.001);
        Assert.InRange(Math.Abs(Coordinate(10)), 0, 0.0006);
        Assert.InRange(Math.Abs(Coordinate(11)), 0, 0.0006);
        Assert.Contains($"{start}–{end} nm (Y=100)", gray.Conditions);
        var white = ChromaticityMatch.GetStandardWhitePoint(id, observer);
        var conversion = ToolCalculations.ConvertColors("100,0,0", InputSpace.Lab, id, observer);
        Assert.Equal(white.CIEX.ToString("F4", CultureInfo.InvariantCulture), conversion.Rows[0][1]);
        Assert.Equal(white.CIEZ.ToString("F4", CultureInfo.InvariantCulture), conversion.Rows[0][3]);
        var sample = new CIExyY { CIEx = 0.3, CIEy = 0.6, CIEY = 100 };
        var wave = WavelengthCalculations.Calculate(sample, observer, id);
        var expected = ChromaticityConversion.xyYToWavelengths(sample, observer, id);
        Assert.Equal(expected.DominantWavelength, wave.Wavelengths.DominantWavelength);
        Assert.Equal(expected.ComplementaryWavelength, wave.Wavelengths.ComplementaryWavelength);
        Assert.Equal(white.CIEX / (white.CIEX + white.CIEY + white.CIEZ), wave.White.X);
        Assert.Contains($"{start}–{end} nm", wave.Table.Conditions);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(wave.Table.Conditions, UiLanguage.English));
    }

    [Theory]
    [InlineData("所选光源 HP1 可用波段：380–780 nm。请调整输入波段。")]
    [InlineData("参考照明体包含全部 50 条 CIE 光源，白点由光谱与所选观察者积分计算。")]
    [InlineData("Lab / Luv 使用与反射光谱相同波段的积分白点。")]
    [InlineData("标准光源可选全部 50 条 CIE 光源；白点在其与观察者的共同波段积分，Y = 100。")]
    [InlineData("ChromaticityDotNet v1.0.4.0 · 代码 MIT · CIE 数据 CC BY-SA 4.0\n波长保留两位小数，其他计算保留四位小数。参考白点由光源光谱与所选观察者积分得到。色块仅为屏幕近似，不能代替仪器测量。")]
    public void NewCoverageAndWhitePointMessagesTranslateCompletely(string chinese) =>
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(chinese, UiLanguage.English));
}
