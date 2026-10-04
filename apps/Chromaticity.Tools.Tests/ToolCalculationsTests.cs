using System.Globalization;
using Chromaticity.Tools.Services;
using Xunit;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class ToolCalculationsTests
{
    private const Standardilluminant D65 = Standardilluminant.D65;
    private const StandardObserver Two = StandardObserver.Degree2;

    [Theory]
    [InlineData("50,,20")]
    [InlineData("50，，20")]
    [InlineData("50,，20")]
    [InlineData("\t50\t20\t30")]
    [InlineData("50\t20\t")]
    [InlineData("50\t\t20")]
    public void MissingCellsAreNeverShifted(string text) => Assert.Throws<ArgumentException>(() => ToolCalculations.ParseRows(text));

    [Fact]
    public void ExcelPasteAndCsvWorkWithBomAndBlankLines()
    {
        var rows = ToolCalculations.ParseRows("\uFEFF50\t20\t-30\r\n\r\n52,18,-28\n60 10 -20");
        Assert.Equal(3, rows.Length);
        Assert.Equal(new[] { "50", "20", "-30" }, rows[0]);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void ChineseCommasAndLineBreaksWorkAcrossTools(string newline)
    {
        var samples = $"50， 20，-30{newline}52,18，-28";
        var canonical = "50,20,-30\n52,18,-28";
        Assert.Equal(ToolCalculations.ConvertColors(canonical, InputSpace.Lab, D65, Two).ToCsv(),
            ToolCalculations.ConvertColors(samples, InputSpace.Lab, D65, Two).ToCsv());
        Assert.Equal(ToolCalculations.Difference("50,20,-30", canonical, false, 1, 1, 1, 1, 1).ToCsv(),
            ToolCalculations.Difference("50，20，-30", samples, false, 1, 1, 1, 1, 1).ToCsv());
    }

    [Theory]
    [InlineData("NaN,0,0")]
    [InlineData("Infinity,0,0")]
    [InlineData("1e999,0,0")]
    [InlineData("50,20")]
    [InlineData("-1,0,0")]
    public void InvalidLabRejected(string text) => Assert.Throws<ArgumentException>(() => ToolCalculations.ConvertColors(text, InputSpace.Lab, D65, Two));

    [Fact]
    public void BatchModesKeepPreviewsAndDifferencesAligned()
    {
        var table = ToolCalculations.Difference("50,20,-30", "50,20,-30\n52,18,-28", false, 1, 1, 1, 2, 1);
        Assert.Equal(2, table.Rows.Length);
        Assert.Equal("0.0000", table.Rows[0][10]);
        Assert.Equal(table.Rows[0][1], table.Rows[0][2]);
        Assert.Equal(table.Rows[0][1], table.Rows[1][1]);
        Assert.NotEqual(table.Rows[1][1], table.Rows[1][2]);
        Assert.Equal(new[] { "2.0000", "-2.0000", "2.0000" }, table.Rows[1][3..6]);
        Assert.Throws<ArgumentException>(() => ToolCalculations.Difference("50,0,0\n51,0,0", "50,0,0", false, 1, 1, 1, 1, 1));
        Assert.Throws<ArgumentException>(() => ToolCalculations.Difference("50,0,0", "50,0,0\n51,0,0", true, 1, 1, 1, 1, 1));
        var paired = ToolCalculations.Difference("50,0,0\n51,0,0", "50,0,0\n51,0,0", true, 1, 1, 1, 1, 1);
        Assert.All(paired.Rows, r => { Assert.Equal("0.0000", r[10]); Assert.Equal(r[1], r[2]); });
        Assert.NotEqual(paired.Rows[0][1], paired.Rows[1][1]);
    }

    [Fact]
    public void PublishedCiede2000ReferencePairIsPreserved()
    {
        var table = ToolCalculations.Difference("50,2.6772,-79.7751", "50,0,-82.7485", false, 1, 1, 1, 1, 1);
        Assert.Equal("2.0425", table.Rows[0][10]);
    }

    [Fact]
    public void DifferenceColumnsIncludeFiveSignedDeltasAndFormulaParameters()
    {
        var table = ToolCalculations.Difference("50,10,1", "50,10,-1", false, 1.5, 2, 3, 2, 1);
        Assert.Equal(new[] { "序号", "标样 sRGB", "样品 sRGB", "ΔL*", "Δa*", "Δb*", "ΔC*", "Δh°", "ΔE76" }, table.Headers[..9]);
        Assert.Equal(new[] { "0.0000", "0.0000", "-2.0000", "0.0000", "-11.4212" }, table.Rows[0][3..8]);
        Assert.Contains("K1 = 0.045", table.Headers[9]);
        Assert.Contains("1.5:2:3", table.Headers[10]);
        Assert.Contains("2:1", table.Headers[11]);
        Assert.Equal(12, table.Headers.Length);
        Assert.All(table.Rows, row => Assert.Equal(table.Headers.Length, row.Length));
        Assert.DoesNotContain("判定", table.ToCsv());
        Assert.Contains("1.5:2:3", table.ToCsv());
        Assert.Contains(table.Rows[0][1], table.ToCsv());
        var copied = table.ToTsv().Split('\n');
        Assert.Equal(2, copied.Length);
        Assert.All(copied, line => Assert.Equal(12, line.Split('\t').Length));
        Assert.Contains("1.5:2:3", copied[0]);
    }

    [Fact]
    public void SpectralUnitsProduceIdenticalXyz()
    {
        var percent = string.Join('\n', Enumerable.Repeat("18", 41));
        var fraction = string.Join('\n', Enumerable.Range(0, 41).Select(i => $"{380 + 10 * i},0.18"));
        var a = ToolCalculations.Reflectance(percent, 380, 780, 10, false, D65, Two);
        var b = ToolCalculations.Reflectance(fraction, 380, 780, 10, true, D65, Two);
        Assert.Equal("18.0000", a.Table.Rows[0][2]);
        Assert.Equal(a.Table.Rows[0], b.Table.Rows[0]);
        Assert.Equal(a.Values, b.Values);
    }

    [Fact]
    public void SpectrumRejectsMismatchedGridAndUnsupportedCoverage()
    {
        Assert.Throws<ArgumentException>(() => ToolCalculations.Reflectance("380,18\n400,18", 380, 390, 10, false, D65, Two));
        Assert.Throws<ArgumentException>(() => ToolCalculations.Reflectance("18", 380, 780, 10, false, D65, Two));
        var values = string.Join('\n', Enumerable.Repeat("18", 43));
        Assert.Throws<ArgumentException>(() => ToolCalculations.Reflectance(values, 360, 780, 10, false, Standardilluminant.TL84, Two));
    }

    [Fact]
    public void BlackHasUndefinedChromaticityButValidOtherOutputs()
    {
        var row = ToolCalculations.ConvertColors("#000000", InputSpace.HEX, D65, Two).Rows[0];
        Assert.Equal("0.0000", row[4]);
        Assert.Equal("—", row[12]); Assert.Equal("—", row[13]);
        Assert.Equal("#000000", row[17]);
    }

    [Fact]
    public void SrgbConditionsAndByteRangeAreEnforced()
    {
        var row = ToolCalculations.ConvertColors("255,0,0", InputSpace.sRGB, D65, Two).Rows[0];
        Assert.Equal("41.2391", row[1]); Assert.Equal("#FF0000", row[17]);
        Assert.Throws<ArgumentException>(() => ToolCalculations.ConvertColors("256,0,0", InputSpace.sRGB, D65, Two));
        Assert.Throws<ArgumentException>(() => ToolCalculations.ConvertColors("1.5,0,0", InputSpace.sRGB, D65, Two));
        Assert.Throws<ArgumentException>(() => ToolCalculations.ConvertColors("#FFFFFF", InputSpace.HEX, Standardilluminant.A, Two));
        var other = ToolCalculations.ConvertColors("50,20,-30", InputSpace.Lab, Standardilluminant.A, Two).Rows[0];
        Assert.Matches("^#[0-9A-F]{6}$", other[17]);
        Assert.Contains("no chromatic adaptation", other[18]);
        Assert.Matches("^#[0-9A-F]{6}$", ToolCalculations.ConvertColors("50,20,-30", InputSpace.Lab, D65, StandardObserver.Degree10).Rows[0][17]);
        var spectrum = ToolCalculations.Reflectance(string.Join('\n', Enumerable.Repeat("18", 41)), 380, 780, 10, false, Standardilluminant.A, Two);
        Assert.Null(spectrum.Hex);
        Assert.Equal("—", spectrum.Table.Rows[0][17]);
    }

    [Fact]
    public void ParsingAndExportStayInvariantAcrossLocales()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var table = ToolCalculations.Difference("50.5,0,0", "50.5,0,0", false, 1.5, 1, 1, 1, 1);
            Assert.Contains("1.5:1:1", table.Conditions);
            Assert.Contains("1.5:1:1", table.ToCsv());
            Assert.Contains("\"0.0000\"", table.ToCsv());
        }
        finally { CultureInfo.CurrentCulture = previous; }
        var escaped = new CalculationTable(["label"], [["a,\"b\"\nnext"]], "").ToCsv();
        Assert.Contains("\"a,\"\"b\"\"\nnext\"", escaped);
    }

    [Fact]
    public void BatchLimitIsEnforced() => Assert.Throws<ArgumentException>(() =>
        ToolCalculations.ParseRows(string.Join('\n', Enumerable.Repeat("50,0,0", ToolCalculations.MaxRows + 1))));
}
