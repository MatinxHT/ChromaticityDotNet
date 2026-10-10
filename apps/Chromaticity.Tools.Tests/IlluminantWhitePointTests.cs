using System.Globalization;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class IlluminantWhitePointTests
{
    [Theory]
    [MemberData(nameof(CatalogWhitePointTests.LightsAndObservers), MemberType = typeof(CatalogWhitePointTests))]
    public void EveryLightAndObserverUsesOriginalSpectrumAndExportsFourDecimalWhiteCoordinates(string id, StandardObserver observer)
    {
        var expected = ChromaticityMatch.GetStandardWhitePoint(id, observer, 400, 700);
        var sum = expected.CIEX + expected.CIEY + expected.CIEZ;
        string[] coordinates = [F(expected.CIEX), "100.0000", F(expected.CIEZ), F(expected.CIEX / sum), F(expected.CIEY / sum)];
        foreach (var interval in ToolCalculations.IlluminantIntervals)
        {
            var query = ToolCalculations.QueryIlluminant(id, interval, 400, 700, observer);
            var table = query.WhitePointTable;
            var row = table.Rows.Single();
            Assert.Equal("400", row[2]);
            Assert.Equal("700", row[3]);
            Assert.Equal(coordinates, row[4..9]);
            Assert.Equal(observer == StandardObserver.Degree2 ? "2° · CIE 1931" : "10° · CIE 1964", row[1]);
            Assert.Contains("400–700 nm", table.Conditions);
            Assert.Equal(2, query.Table.Headers.Length);
            Assert.Contains(string.Join('\t', coordinates), table.ToTsv());
            Assert.Contains(string.Join(',', coordinates.Select(value => $"\"{value}\"")), table.ToCsv());
            var english = UiLanguage.TranslateTable(table, UiLanguage.English);
            Assert.Equal("White X", english.Headers[4]);
            Assert.Equal("White x", english.Headers[7]);
            Assert.Equal(coordinates, english.Rows[0][4..9]);
            Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", english.Conditions + english.ToCsv());
        }
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void FullQueryClipsToObserverCoverageAndAgreesWithTheWavelengthTool(StandardObserver observer)
    {
        var query = ToolCalculations.QueryIlluminant("D65", 10, observer: observer);
        var wave = WavelengthCalculations.Calculate(new CIExyY { CIEx = 0.5253, CIEy = 0.3485, CIEY = 100 }, observer, "D65");
        Assert.Equal("300", query.Table.Rows[0][0]);
        Assert.Equal("830", query.Table.Rows[^1][0]);
        Assert.Equal("360", query.WhitePointTable.Rows[0][2]);
        Assert.Equal("830", query.WhitePointTable.Rows[0][3]);
        Assert.Equal(wave.Table.Rows[0][4..6], query.WhitePointTable.Rows[0][7..9]);
    }

    [Fact]
    public void UnalignedQueryEndLimitsTheWhiteIntegrationToTheActualLastSample()
    {
        var query = ToolCalculations.QueryIlluminant("D50", 20, 385, 780, StandardObserver.Degree2);
        var expected = ChromaticityMatch.GetStandardWhitePoint("D50", StandardObserver.Degree2, 385, 765);
        Assert.Equal("765", query.Table.Rows[^1][0]);
        Assert.Equal("385", query.WhitePointTable.Rows[0][2]);
        Assert.Equal("765", query.WhitePointTable.Rows[0][3]);
        Assert.Equal(F(expected.CIEX), query.WhitePointTable.Rows[0][4]);
        Assert.Equal(F(expected.CIEZ), query.WhitePointTable.Rows[0][6]);
    }

    [Fact]
    public void ChangingObserverOrRangeChangesWhiteCoordinatesWithoutNormalizingThePower()
    {
        var two = ToolCalculations.QueryIlluminant("D65", 10, 380, 780, StandardObserver.Degree2);
        var ten = ToolCalculations.QueryIlluminant("D65", 10, 380, 780, StandardObserver.Degree10);
        var cropped = ToolCalculations.QueryIlluminant("D65", 10, 400, 600, StandardObserver.Degree2);
        Assert.Equal(two.Values, ten.Values);
        Assert.Equal(49.9755, two.Values[0]);
        Assert.NotEqual(two.WhitePointTable.Rows[0][4], ten.WhitePointTable.Rows[0][4]);
        Assert.NotEqual(two.WhitePointTable.Rows[0][4], cropped.WhitePointTable.Rows[0][4]);
        Assert.Contains("400–600 nm", cropped.WhitePointTable.Conditions);
    }

    [Fact]
    public void UltravioletOnlyQueryKeepsItsSpectrumAndExplainsUnavailableWhiteCoordinates()
    {
        var query = ToolCalculations.QueryIlluminant("D65", 10, 300, 350);
        Assert.Equal(6, query.Values.Length);
        Assert.Equal("300", query.Table.Rows[0][0]);
        Assert.Equal("350", query.Table.Rows[^1][0]);
        Assert.All(query.WhitePointTable.Rows[0][2..9], value => Assert.Equal("—", value));
        Assert.Contains("不重叠，白点未定义", query.WhitePointTable.Conditions);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.TranslateTable(query.WhitePointTable, UiLanguage.English).ToCsv());
    }

    [Theory]
    [InlineData(300, 350)]
    [InlineData(380, 780)]
    public void InvalidObserverIsRejectedEvenWhenThereIsNoSharedRange(int start, int end) =>
        Assert.Throws<ArgumentException>(() => ToolCalculations.QueryIlluminant("D65", 10, start, end, (StandardObserver)99));

    private static string F(double value) => value.ToString("F4", CultureInfo.InvariantCulture);
}
