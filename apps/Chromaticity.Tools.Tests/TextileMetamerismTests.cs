using System.Globalization;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class TextileMetamerismTests
{
    [Theory]
    [InlineData(StandardObserver.Degree2, 5)]
    [InlineData(StandardObserver.Degree10, 10)]
    [InlineData(StandardObserver.Degree10, 20)]
    public void EveryRowComparesBothSpectraUnderItsOwnIlluminant(StandardObserver observer, int step)
    {
        var standard = ColorInconstancyTests.Ramp(400, 700, step);
        var sample = string.Join('\n', Enumerable.Range(0, 300 / step + 1)
            .Select(i => FormattableString.Invariant($"{400 + i * step},{28 + 12 * Math.Cos(i * step / 50.0)}")));
        var input = TextileMetamerismCalculations.Parse(standard, sample, 400, 700, step, false, observer);
        foreach (var formula in Enum.GetValues<ComparisonFormula>())
        {
            var settings = new ColorInconstancySettings(formula, CmcL: 1.5, CmcC: 0.8, KL: 2, KC: 0.7, KH: 1.2);
            var table = TextileMetamerismCalculations.Analyze(input, settings);
            Assert.Equal(new[] { "D65", "CWF", "A", "U3000", "TL84" }, table.Rows.Select(row => row[0]));
            Assert.Equal(new[] { "D65", "FL2", "A", "FL12", "FL11" }, table.Rows.Select(row => row[15]));
            foreach (var row in table.Rows)
            {
                var reference = ToolCalculations.Reflectance(standard, 400, 700, step, false, row[15], observer);
                var candidate = ToolCalculations.Reflectance(sample, 400, 700, step, false, row[15], observer);
                Assert.Equal(reference.Table.Rows[0][4..7], row[2..5]);
                Assert.Equal(candidate.Table.Rows[0][4..7], row[5..8]);
                var expected = formula switch
                {
                    ComparisonFormula.Cie76 => ChromaticityDeltaEFormulations.DeltaE1976(reference.Lab, candidate.Lab),
                    ComparisonFormula.Cmc => ChromaticityDeltaEFormulations.DeltaEcmc(reference.Lab, candidate.Lab, 1.5, 0.8),
                    _ => ChromaticityDeltaEFormulations.DeltaE2000(reference.Lab, candidate.Lab, 2, 0.7, 1.2).DeltaE
                };
                Assert.Equal(ToolCalculations.F(expected), row[8]);
                var comparison = ChromaticityMatch.CompareColors(reference.Lab, candidate.Lab);
                Assert.Equal(ColorEvaluationPresentation.Comments(comparison)[..3], row[9..12]);
                Assert.Equal(ToolCalculations.F(comparison.Differences.DeltaL), row[12]);
                Assert.Equal(ToolCalculations.F(comparison.Differences.DeltaChroma), row[13]);
                Assert.Equal(table.Headers.Length, row.Length);
                Assert.Equal(new[] { "400", "700", step.ToString(CultureInfo.InvariantCulture) }, row[17..20]);
            }
        }
    }

    [Fact]
    public void DemonstrationMatchesD65ButSeparatesUnderOtherLights()
    {
        var table = Example();
        Assert.InRange(Number(table.Rows[0][8]), double.Epsilon, 0.1);
        Assert.True(Number(table.Rows[2][8]) > 1, "The A illuminant must reveal the metameric mismatch.");
        Assert.True(table.Rows.Skip(1).Max(row => Number(row[8])) > 2);
        Assert.NotEqual(TextileMetamerismExamples.Standard, TextileMetamerismExamples.MetamericSample);
        var differentObserver = TextileMetamerismCalculations.Analyze(TextileMetamerismCalculations.Parse(
            TextileMetamerismExamples.Standard, TextileMetamerismExamples.MetamericSample,
            380, 780, 10, false, StandardObserver.Degree2), new());
        Assert.NotEqual(table.Rows[0][8], differentObserver.Rows[0][8]);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void IdenticalSpectraHaveZeroDifferenceUnderEveryIlluminant(StandardObserver observer)
    {
        var input = TextileMetamerismCalculations.Parse(TextileMetamerismExamples.Standard,
            TextileMetamerismExamples.Standard, 380, 780, 10, false, observer);
        foreach (var formula in Enum.GetValues<ComparisonFormula>())
            Assert.All(TextileMetamerismCalculations.Analyze(input, new(formula)).Rows, row =>
            {
                Assert.Equal("0.0000", row[8]);
                Assert.Equal(row[2..5], row[5..8]);
                Assert.Equal(row[22], row[23]);
                Assert.All(row[9..12], comment => Assert.Equal("无变化", comment));
            });
    }

    [Fact]
    public void FractionsMatchPercentAndReflectanceScaleIsPreserved()
    {
        static string Fraction(string text) => string.Join('\n', ToolCalculations.ParseRows(text)
            .Select(row => (Number(row[1]) / 100).ToString("R", CultureInfo.InvariantCulture)));
        var fractions = TextileMetamerismCalculations.Parse(Fraction(TextileMetamerismExamples.Standard),
            Fraction(TextileMetamerismExamples.MetamericSample), 380, 780, 10, true, StandardObserver.Degree10);
        var result = TextileMetamerismCalculations.Analyze(fractions, new());
        Assert.Equal(Example().Rows.Select(row => row[2..15]), result.Rows.Select(row => row[2..15]));
        var different = TextileMetamerismCalculations.Analyze(TextileMetamerismCalculations.Parse(
            TextileMetamerismExamples.Standard, TextileMetamerismExamples.DifferentSample, 380, 780, 10, false, StandardObserver.Degree10), new());
        Assert.All(different.Rows, row => Assert.True(Number(row[8]) > 5));
        Assert.All(different.Rows, row => Assert.True(Number(row[12]) < 0));
    }

    [Theory]
    [InlineData("50,20,-30", "标准品光谱")]
    [InlineData("NaN", "标准品光谱")]
    [InlineData("-1\n2", "标准品光谱")]
    [InlineData("400,18\n410,20", "标准品光谱")]
    [InlineData("", "标准品光谱")]
    public void RejectsInvalidSpectraWithInputLabel(string standard, string label)
    {
        var error = Assert.Throws<ArgumentException>(() => TextileMetamerismCalculations.Parse(
            standard, "18\n20", 380, 390, 10, false, StandardObserver.Degree10));
        Assert.Contains(label, error.Message);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(error.Message, UiLanguage.English));
        error = Assert.Throws<ArgumentException>(() => TextileMetamerismCalculations.Parse(
            "18\n20", standard, 380, 390, 10, false, StandardObserver.Degree10));
        Assert.Contains("样品光谱", error.Message);
    }

    [Theory]
    [InlineData(380, 780, 0)]
    [InlineData(380, 780, -10)]
    [InlineData(380, 780, 7)]
    [InlineData(780, 380, 10)]
    [InlineData(350, 780, 10)]
    [InlineData(380, 840, 10)]
    public void RejectsInvalidGrids(int start, int end, int step) =>
        Assert.Throws<ArgumentException>(() => TextileMetamerismCalculations.Parse("18", "18", start, end, step, false, StandardObserver.Degree10));

    [Fact]
    public void UnavailableFluorescentCoverageDoesNotDiscardD65AndA()
    {
        var text = ColorInconstancyTests.Ramp(360, 830, 10);
        var input = TextileMetamerismCalculations.Parse(text, text, 360, 830, 10, false, StandardObserver.Degree10);
        var table = TextileMetamerismCalculations.Analyze(input, new());
        Assert.Equal("0.0000", table.Rows[0][8]); Assert.Equal("0.0000", table.Rows[2][8]);
        foreach (var row in table.Rows.Where(row => row[15].StartsWith("FL")))
        {
            Assert.All(row[2..15], value => Assert.Equal("—", value));
            Assert.All(row[22..24], value => Assert.Equal("—", value));
            Assert.Contains("380–780 nm", row[^1]);
        }
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.TranslateTable(table, UiLanguage.English).ToCsv());
    }

    [Fact]
    public void ExportRetainsBothLabsFormulaConditionsAndPreviewHexInBothLanguages()
    {
        var table = Example();
        var english = UiLanguage.TranslateTable(table, UiLanguage.English);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", english.ToCsv());
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", english.Conditions);
        Assert.Equal(6, table.ToTsv().Split('\n').Length);
        foreach (var row in table.Rows)
        {
            Assert.Contains("380–780 nm / 10 nm", row[^1]);
            Assert.Contains("CIE 1964", row[^1]);
            Assert.Contains("1:1:1", row[^1]);
            Assert.Contains("D65 / 10°", row[^1]);
            Assert.Matches("^#[0-9A-F]{6}$", row[22]); Assert.Matches("^#[0-9A-F]{6}$", row[23]);
        }
    }

    [Fact]
    public async Task DownloadableExamplesMatchBuiltinDataAndUseTheExistingCsvImporter()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "ChromaticityDotNet.sln"))) directory = directory.Parent!;
        var folder = Path.Combine(directory.FullName, "apps/Chromaticity.Tools.Browser/wwwroot/examples/metamerism");
        foreach (var fraction in new[] { false, true })
        {
            async Task<string> Read(string name)
            {
                var fileName = name + (fraction ? "-fraction" : "") + ".csv";
                await using var stream = File.OpenRead(Path.Combine(folder, fileName));
                return await CsvDataFiles.ReadAsync(fileName, stream, CsvInputKind.Spectrum);
            }
            Assert.Equal(TextileMetamerismExamples.GetStandard(fraction), await Read("standard"));
            Assert.Equal(TextileMetamerismExamples.GetMetamericSample(fraction), await Read("metameric-sample"));
            Assert.Equal(TextileMetamerismExamples.GetDifferentSample(fraction), await Read("different-sample"));
        }
    }

    private static CalculationTable Example() => TextileMetamerismCalculations.Analyze(TextileMetamerismCalculations.Parse(
        TextileMetamerismExamples.Standard, TextileMetamerismExamples.MetamericSample, 380, 780, 10, false, StandardObserver.Degree10), new());
    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}
