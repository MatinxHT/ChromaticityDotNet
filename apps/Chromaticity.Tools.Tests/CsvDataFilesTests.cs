using System.Text;
using Chromaticity.Tools.Services;
using Xunit;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class CsvDataFilesTests
{
    [Theory]
    [InlineData("50,20,-30", "50,20,-30")]
    [InlineData("\uFEFF\r\n任意列名,其他名称,备注\r\n50,20,-30\r\n", "50,20,-30")]
    [InlineData("乱码�,�,???\n50,20,-30", "50,20,-30")]
    [InlineData("\"名称,含逗号\",\"名称\"\"含引号\",\"多行\n标题\"\n\"50\",\"20\",\"-30\"", "50,20,-30")]
    [InlineData("甲，乙，丙\r50，20,-30\r52,18，-28", "50,20,-30\n52,18,-28")]
    [InlineData("任意;标题;名称\n\"50\";20;-30", "50,20,-30")]
    public void HeadersAreOptionalAndLabelsAreNeverMatched(string text, string expected) =>
        Assert.Equal(expected, CsvDataFiles.Normalize(text, CsvInputKind.Lab));

    [Theory]
    [InlineData("utf-8", false)]
    [InlineData("utf-8", true)]
    [InlineData("utf-16", true)]
    [InlineData("utf-16", false)]
    [InlineData("utf-16BE", true)]
    [InlineData("utf-16BE", false)]
    [InlineData("utf-32", true)]
    [InlineData("gb2312", false)]
    [InlineData("gbk", false)]
    [InlineData("gb18030", false)]
    [InlineData("windows-1252", false)]
    public async Task OfficeAndWpsEncodingsHaveTheSameNumericContent(string encodingName, bool bom)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(encodingName);
        var text = encodingName == "windows-1252" ? "Lumière,a,b\r\n50,20,-30\r\n" : "亮度,红绿,黄蓝\r\n50,20,-30\r\n";
        using var stream = new MemoryStream((bom ? encoding.GetPreamble() : []).Concat(encoding.GetBytes(text)).ToArray());
        Assert.Equal("50,20,-30", await CsvDataFiles.ReadAsync("office.CSV", stream, CsvInputKind.Lab));
    }

    [Fact]
    public async Task GbkChineseCommasAreDecodedBeforeParsing()
    {
        var bytes = CodePagesEncodingProvider.Instance.GetEncoding(936)!.GetBytes("亮度，红绿，黄蓝\n50，20，-30");
        using var stream = new MemoryStream(bytes);
        Assert.Equal("50,20,-30", await CsvDataFiles.ReadAsync("wps.csv", stream, CsvInputKind.Lab));
    }

    [Theory]
    [InlineData("data.tsv")]
    [InlineData("data.txt")]
    [InlineData("data.xlsx")]
    [InlineData("data.csv.txt")]
    [InlineData("data")]
    public async Task NonCsvExtensionsAreRejectedEvenWithValidData(string fileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("50,20,-30"));
        await Assert.ThrowsAsync<ArgumentException>(() => CsvDataFiles.ReadAsync(fileName, stream, CsvInputKind.Lab));
    }

    [Theory]
    [InlineData("50,,20")]
    [InlineData("50， ，20")]
    [InlineData("50,20")]
    [InlineData("50,20,-30,1")]
    [InlineData("50\t20\t-30")]
    [InlineData("50 20 -30")]
    [InlineData("NaN,NaN,NaN\n50,20,-30")]
    [InlineData("Infinity,0,0\n50,20,-30")]
    [InlineData("1e999,0,0")]
    [InlineData("oops,20,-30\n50,20,-30")]
    [InlineData(",,\n50,20,-30")]
    [InlineData("a,b,c\n50,20,-30\ninvalid,row,values")]
    [InlineData("\"50,20,-30")]
    [InlineData("\"50\"x,20,-30")]
    [InlineData("5\"0,20,-30")]
    [InlineData("L,a,b")]
    [InlineData("")]
    public void InvalidContentIsRejectedWithoutSilentlyDroppingData(string text) =>
        Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize(text, CsvInputKind.Lab));

    [Fact]
    public void ErrorsKeepPhysicalCsvLineNumbersAfterSkippingHeaderAndBlankLines()
    {
        var error = Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize("\nL,a,b\n\n50,,30", CsvInputKind.Lab));
        Assert.Contains("第 4 行", error.Message);
    }

    [Fact]
    public void SpectrumAcceptsOneOrTwoColumnsAndRejectsInconsistentRows()
    {
        Assert.Equal("18\n20", CsvDataFiles.Normalize("任意表头\n18\n20", CsvInputKind.Spectrum));
        Assert.Equal("380,18\n390,20", CsvDataFiles.Normalize("甲,乙\n380,18\n390,20", CsvInputKind.Spectrum));
        Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize("380,18,1", CsvInputKind.Spectrum));
        Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize("380,18\n20", CsvInputKind.Spectrum));
    }

    [Theory]
    [InlineData("#GGGGGG\n#20A090")]
    [InlineData("HEX\n#123")]
    [InlineData("#20A090,1")]
    public void InvalidHexIsNotMistakenForAHeader(string text) =>
        Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize(text, CsvInputKind.Color, InputSpace.HEX));

    [Theory]
    [InlineData(380, 780, 10, false)]
    [InlineData(400, 700, 5, true)]
    public async Task SpectrumTemplateRoundTripsWithTheCurrentGridAndUnits(int start, int end, int step, bool fraction)
    {
        var template = CsvDataFiles.SpectrumTemplate(start, end, step, fraction);
        using var stream = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(template.Csv)).ToArray());
        var text = await CsvDataFiles.ReadAsync(template.FileName, stream, CsvInputKind.Spectrum);
        var result = ToolCalculations.Reflectance(text, start, end, step, fraction, Standardilluminant.D65, StandardObserver.Degree2);
        Assert.Equal((end - start) / step + 1, result.Values.Length);
        Assert.All(result.Values, value => Assert.Equal(18, value));
        Assert.Equal("18.0000", result.Table.Rows[0][2]);
    }

    [Fact]
    public void SingleSixColumnDifferenceTemplateRoundTripsAsOneToMany()
    {
        var template = CsvDataFiles.DifferenceTemplate();
        Assert.Equal("chromaticity-difference-template.csv", template.FileName);
        var input = CsvDataFiles.ParseDifference(template.Csv);
        Assert.Equal(1, input.StandardRows);
        Assert.Equal(2, input.SampleRows);
        Assert.False(input.Paired);
        Assert.Equal("50,20,-30", input.Standards);
        var result = ToolCalculations.DifferenceAuto(input.Standards, input.Samples, 1, 1, 1, 2, 1);
        Assert.Equal(2, result.Rows.Length);
        Assert.Equal(result.Rows[0][1], result.Rows[1][1]);
        Assert.Equal(ToolCalculations.Difference(input.Standards, input.Samples, false, 1, 1, 1, 2, 1).ToCsv(), result.ToCsv());
    }

    [Fact]
    public void FillingAnotherStandardRowAutomaticallySelectsPairedComparisons()
    {
        var csv = CsvDataFiles.DifferenceTemplate().Csv.Replace("\"\",\"\",\"\"", "\"55\",\"10\",\"20\"");
        var input = CsvDataFiles.ParseDifference(csv);
        Assert.Equal(2, input.StandardRows);
        Assert.Equal(2, input.SampleRows);
        Assert.True(input.Paired);
        var result = ToolCalculations.DifferenceAuto(input.Standards, input.Samples, 1, 1, 1, 2, 1);
        Assert.NotEqual(result.Rows[0][1], result.Rows[1][1]);
        Assert.Equal(ToolCalculations.Difference(input.Standards, input.Samples, true, 1, 1, 1, 2, 1).ToCsv(), result.ToCsv());
    }

    [Theory]
    [InlineData("50,20,-30,52,18,-28\r\n,,,60,10,-20")]
    [InlineData("甲,乙,丙,丁,戊,己\n\"50\",\"20\",\"-30\",\"52\",\"18\",\"-28\"\n,,,60,10,-20")]
    [InlineData("乱码�，�，�，�，�，�\r50，20，-30，52，18，-28\r，，，60，10，-20")]
    [InlineData("a;b;c;d;e;f\n50;20;-30;52;18;-28\n;;;60;10;-20")]
    [InlineData("50,20,-30,52,18,-28\n,,,,,\n,,,60,10,-20")]
    [InlineData(",,,,,\na,b,c,d,e,f\n50,20,-30,52,18,-28\n,,,60,10,-20")]
    public void SixColumnImportsKeepOptionalHeadersEncodingAndEmptyStandardGroups(string csv)
    {
        var input = CsvDataFiles.ParseDifference(csv);
        Assert.Equal("50,20,-30", input.Standards);
        Assert.Equal("52,18,-28\n60,10,-20", input.Samples);
    }

    [Theory]
    [InlineData("utf-8", false)]
    [InlineData("utf-8", true)]
    [InlineData("utf-16", true)]
    [InlineData("gbk", false)]
    [InlineData("gb18030", false)]
    public async Task SixColumnFileImportsKeepOfficeAndWpsCompatibility(string encodingName, bool bom)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(encodingName);
        var text = "标样亮度，红绿，黄蓝，样品亮度，红绿，黄蓝\r\n50，20，-30，52，18，-28\r\n，，，60，10，-20";
        using var stream = new MemoryStream((bom ? encoding.GetPreamble() : []).Concat(encoding.GetBytes(text)).ToArray());
        var input = await CsvDataFiles.ReadDifferenceAsync("combined.CSV", stream);
        Assert.Equal(1, input.StandardRows);
        Assert.Equal(2, input.SampleRows);
    }

    [Theory]
    [InlineData("50,20,-30")]
    [InlineData("50,20,-30,52,18")]
    [InlineData("50,20,-30,52,18,-28,1")]
    [InlineData("50,,-30,52,18,-28")]
    [InlineData("50,20,-30,52,,-28")]
    [InlineData(",,,52,18,-28")]
    [InlineData("50,20,-30,,,")]
    [InlineData("NaN,20,-30,52,18,-28\n50,20,-30,60,10,-20")]
    [InlineData("50,20,-30,52,Infinity,-28")]
    [InlineData("50,20,-30,52,18,1e999")]
    [InlineData("50,20,-30,52,18,-28\n50,20,-30,60,10,-20\n,,,65,10,20")]
    [InlineData("50,20,-30,52,18,-28\n55,10,20,,,\n,,,60,10,-20")]
    [InlineData("a,b,c,d,e,f\n50,20,-30,52,18,-28\ninvalid,row,values,60,10,-20")]
    [InlineData("a,b,c,d,e,f\n,,,,,")]
    public void InvalidCombinedDataIsRejectedWithoutChangingPairAlignment(string csv) =>
        Assert.Throws<ArgumentException>(() => CsvDataFiles.ParseDifference(csv));

    [Fact]
    public void CombinedImportErrorsKeepOriginalLineAndColumnNumbers()
    {
        var error = Assert.Throws<ArgumentException>(() => CsvDataFiles.ParseDifference("\na,b,c,d,e,f\n50,20,-30,52,18,-28\n,,,60,NaN,-20"));
        Assert.Contains("第 4 行第 5 列", error.Message);
    }

    [Fact]
    public void AutomaticComparisonUsesStandardRowCountWithoutDeduplicatingEqualStandards()
    {
        var input = CsvDataFiles.ParseDifference("50,20,-30,52,18,-28\n50,20,-30,60,10,-20");
        Assert.True(input.Paired);
        Assert.Equal(2, input.StandardRows);
        Assert.Throws<ArgumentException>(() => ToolCalculations.DifferenceAuto(input.Standards, "52,18,-28", 1, 1, 1, 2, 1));
    }

    [Fact]
    public async Task CombinedImportAlsoRejectsNonCsvFiles()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("50,20,-30,52,18,-28"));
        await Assert.ThrowsAsync<ArgumentException>(() => CsvDataFiles.ReadDifferenceAsync("combined.tsv", stream));
    }

    [Theory]
    [InlineData(InputSpace.XYZ)]
    [InlineData(InputSpace.Lab)]
    [InlineData(InputSpace.Luv)]
    [InlineData(InputSpace.xyY)]
    [InlineData(InputSpace.sRGB)]
    [InlineData(InputSpace.HEX)]
    public void EveryConversionTemplateRoundTripsThroughItsCalculation(InputSpace space)
    {
        var template = CsvDataFiles.ColorTemplate(space);
        var text = CsvDataFiles.Normalize(template.Csv, CsvInputKind.Color, space);
        var result = ToolCalculations.ConvertColors(text, space, Standardilluminant.D65, StandardObserver.Degree2);
        Assert.Equal(2, result.Rows.Length);
        Assert.EndsWith(".csv", template.FileName);
    }

    [Fact]
    public void RowAndTextLimitsAllowTheOptionalHeaderButStillBoundTheImport()
    {
        var text = "L,a,b\n" + string.Join('\n', Enumerable.Repeat("50,20,-30", ToolCalculations.MaxRows));
        Assert.Equal(ToolCalculations.MaxRows, ToolCalculations.ParseRows(CsvDataFiles.Normalize(text, CsvInputKind.Lab)).Length);
        Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize(text + "\n50,20,-30", CsvInputKind.Lab));
        Assert.Throws<ArgumentException>(() => CsvDataFiles.Normalize(new string(' ', ToolCalculations.MaxTextLength + 1), CsvInputKind.Lab));
    }
}
