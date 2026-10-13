using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class LightQualityPresentationTests
{
    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void WavelengthExportsBothLibraryCctsForXyAndUvInputs(StandardObserver observer)
    {
        var xy = new CIExyY { CIEx = 0.3127, CIEy = 0.329, CIEY = 100 };
        var result = WavelengthCalculations.Calculate(xy, observer, "D65");
        double d = -2 * xy.CIEx + 12 * xy.CIEy + 3;
        var uv = WavelengthCalculations.CalculateUv(new CIEuv { CIEu = 4 * xy.CIEx / d, CIEv = 9 * xy.CIEy / d }, observer, "D65");
        foreach (var table in new[] { result.Table, uv.Table })
        {
            string Value(string header) => table.Rows[0][Array.IndexOf(table.Headers, header)];
            Assert.Equal(ToolCalculations.F(ChromaticityConversion.xyToCCT(xy)), Value(LightQualityPresentation.CctHeaders[0]));
            Assert.Equal(ToolCalculations.F(ChromaticityConversion.luv1960ToCCT(ChromaticityConversion.xyToLuv1960(xy), observer)), Value(LightQualityPresentation.CctHeaders[1]));
            Assert.Equal(table.Headers.Length, table.Rows[0].Length);
            Assert.Contains("CCT (McCamy) / K", table.ToCsv());
            Assert.Contains("CCT (CIE 1960 UCS) / K", table.ToTsv());
            var english = UiLanguage.TranslateTable(table, UiLanguage.English);
            Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", english.ToCsv());
        }
    }

    [Theory]
    [InlineData(InputSpace.xyY, "0.3127,0.329,100")]
    [InlineData(InputSpace.XYZ, "95.047,100,108.883")]
    public void ConversionColourRowsIncludeCctAndKeepAllColumnsAligned(InputSpace space, string input)
    {
        var table = ToolCalculations.ConvertColors(input, space, "D65", StandardObserver.Degree2);
        Assert.Equal(table.Headers.Length, table.Rows[0].Length);
        Assert.NotEqual("—", table.Rows[0][Array.IndexOf(table.Headers, LightQualityPresentation.CctHeaders[1])]);
        var black = ToolCalculations.ConvertColors("0,0,0", InputSpace.XYZ, "D65", StandardObserver.Degree2);
        Assert.Equal("—", black.Rows[0][Array.IndexOf(black.Headers, LightQualityPresentation.CctHeaders[1])]);
    }

    [Fact]
    public void IlluminantCriUsesThe1931ObserverAndOriginalSpectrumAtEveryQueryInterval()
    {
        foreach (var interval in ToolCalculations.IlluminantIntervals)
        foreach (var observer in new[] { StandardObserver.Degree2, StandardObserver.Degree10 })
        {
            var table = ToolCalculations.QueryIlluminant("FL2", interval, 380, 780, observer).WhitePointTable;
            Assert.Equal(ToolCalculations.F(ChromaticityConversion.SPDToCRI("FL2").Ra!.Value),
                table.Rows[0][Array.IndexOf(table.Headers, "CRI Ra")]);
            Assert.Equal(table.Headers.Length, table.Rows[0].Length);
            Assert.Contains("1931 2°", table.Rows[0][^1]);
            Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.TranslateTable(table, UiLanguage.English).ToCsv());
        }
        var narrow = ToolCalculations.QueryIlluminant("FL2", 10, 400, 700).WhitePointTable;
        Assert.Equal("—", narrow.Rows[0][Array.IndexOf(narrow.Headers, "CRI Ra")]);
        Assert.Contains("波段不足", narrow.Rows[0][^1]);
    }
}
