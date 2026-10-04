using System.Globalization;
using Chromaticity.Tools.Services;
using Xunit;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class IlluminantQueryTests
{
    [Theory]
    [InlineData(Standardilluminant.D65, 300, 830, 531)]
    [InlineData(Standardilluminant.A, 300, 830, 531)]
    [InlineData(Standardilluminant.CWF, 380, 780, 401)]
    [InlineData(Standardilluminant.F7, 380, 780, 401)]
    [InlineData(Standardilluminant.TL84, 380, 780, 401)]
    [InlineData(Standardilluminant.U30, 380, 780, 401)]
    public void EveryIlluminantUsesItsCompleteAvailableRange(Standardilluminant light, int start, int end, int count)
    {
        var full = ToolCalculations.QueryIlluminant(light, 1);
        Assert.Equal(count, full.Values.Length);
        Assert.Equal(start.ToString(), full.Table.Rows[0][0]);
        Assert.Equal(end.ToString(), full.Table.Rows[^1][0]);
        foreach (var interval in new[] { 5, 10, 20 })
        {
            var query = ToolCalculations.QueryIlluminant(light, interval);
            Assert.Equal((count - 1) / interval + 1, query.Table.Rows.Length);
            Assert.Equal(query.Values.Length, query.Table.Rows.Length);
            for (var i = 0; i < query.Values.Length; i++)
            {
                Assert.Equal(full.Values[i * interval], query.Values[i]);
                Assert.Equal((start + i * interval).ToString(), query.Table.Rows[i][0]);
                Assert.Equal(query.Values[i], double.Parse(query.Table.Rows[i][1], CultureInfo.InvariantCulture));
            }
            Assert.True(int.Parse(query.Table.Rows[^1][0]) <= end);
        }
    }

    [Fact]
    public void QueryPreservesReferencePowerAndExportsBothColumns()
    {
        var d65 = ToolCalculations.QueryIlluminant(Standardilluminant.D65, 10);
        var a = ToolCalculations.QueryIlluminant(Standardilluminant.A, 1);
        Assert.Equal("0.0341", d65.Table.Rows[0][1]);
        Assert.Equal("0.930483", a.Table.Rows[0][1]);
        Assert.Equal("100", d65.Table.Rows.Single(row => row[0] == "560")[1]);
        Assert.Equal("100", a.Table.Rows.Single(row => row[0] == "560")[1]);
        Assert.Equal(55, d65.Table.ToTsv().Split('\n').Length);
        Assert.Contains("300\t0.0341", d65.Table.ToTsv());
        Assert.Contains("\"300\",\"0.0341\"", d65.Table.ToCsv());
        var coarse = ToolCalculations.QueryIlluminant(Standardilluminant.D65, 20);
        Assert.Equal("820", coarse.Table.Rows[^1][0]);
        Assert.Contains("300–830", coarse.Table.Conditions);
        Assert.Contains("300–820", coarse.Table.Conditions);
        Assert.Contains("approximated", ToolCalculations.QueryIlluminant(Standardilluminant.TL84, 1).Table.Conditions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void InvalidIntervalsAreRejected(int interval) => Assert.Throws<ArgumentException>(() =>
        ToolCalculations.QueryIlluminant(Standardilluminant.D65, interval));
}

public class IlluminantRangeTests
{
    public static IEnumerable<object[]> Lights => ChromaticityDotNet.Model.CieSpectralData.Illuminants.Select(info => new object[] { info.Id });

    [Theory]
    [MemberData(nameof(Lights))]
    public void EveryCatalogLightCanBeQueriedInsideTheVisibleRange(string id)
    {
        var query = ToolCalculations.QueryIlluminant(id, 10, 400, 700);
        Assert.Equal(31, query.Values.Length);
        Assert.Equal("400", query.Table.Rows[0][0]);
        Assert.Equal("700", query.Table.Rows[^1][0]);
        Assert.Equal(400, query.Start);
        Assert.Equal(10, query.Step);
        Assert.All(query.Values, value => Assert.True(double.IsFinite(value) && value >= 0));
    }

    [Fact]
    public void CroppedRangeControlsBothPlotAndExportAndDoesNotNormalizePower()
    {
        var query = ToolCalculations.QueryIlluminant("D65", 10, 380, 780);
        Assert.Equal(41, query.Values.Length);
        Assert.Equal(49.9755, query.Values[0]);
        Assert.Equal("380", query.Table.Rows[0][0]);
        Assert.Equal("780", query.Table.Rows[^1][0]);
        Assert.Equal(42, query.Table.ToTsv().Split('\n').Length);
        Assert.DoesNotContain("\"300\",", query.Table.ToCsv());
        var unalignedEnd = ToolCalculations.QueryIlluminant("D50", 20, 385, 780);
        Assert.Equal("765", unalignedEnd.Table.Rows[^1][0]);
        Assert.Contains("不额外补齐末点", unalignedEnd.Table.Conditions);
    }

    [Fact]
    public void NativeFiveNmTablesAreInterpolatedOnlyBetweenTheirSourceSamples()
    {
        var source = ChromaticityDotNet.Model.CieSpectralData.GetIlluminantSpectrum("HP1");
        var query = ToolCalculations.QueryIlluminant("HP1", 1, 381, 390);
        Assert.Equal(source.Spectrums![0] * .8 + source.Spectrums[1] * .2, query.Values[0], 12);
        Assert.Equal(source.Spectrums[1], query.Values[4]);
        Assert.Contains("线性插值", query.Table.Conditions);
        Assert.Contains("5 nm", query.Table.Conditions);
        var native = ToolCalculations.QueryIlluminant("HP1", 5, 380, 390);
        Assert.Equal(source.Spectrums.Take(3), native.Values);
        Assert.DoesNotContain("线性插值", native.Table.Conditions);
    }

    [Theory]
    [InlineData(299, 830)]
    [InlineData(300, 831)]
    [InlineData(830, 830)]
    [InlineData(800, 700)]
    [InlineData(825, 830)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void InvalidOrTooNarrowRangesAreRejected(int start, int end) =>
        Assert.Throws<ArgumentException>(() => ToolCalculations.QueryIlluminant("D65", 10, start, end));
}
