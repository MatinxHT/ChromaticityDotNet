using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class ColorEvaluationIntegrationTests
{
    [Fact]
    public void DifferenceExportsSeparateShortCommentsAndAppliedWeights()
    {
        var table = ToolCalculations.DifferenceAuto("50,20,10", "52,20,12", 1.5, 2, 3, 1.2, 0.8);
        Assert.Equal(ColorEvaluationPresentation.Headers, table.Headers[17..]);
        Assert.Equal(new[] { "更亮", "更艳", "偏黄", "均为有彩色" }, table.Rows[0][17..]);
        Assert.Contains("1.2:0.8", table.Headers[15]);
        Assert.Contains("1.5:2:3", table.Headers[16]);
        Assert.Contains("偏黄", table.ToCsv());
        Assert.Contains("偏黄", table.ToTsv());
        Assert.DoesNotContain("The reference", table.ToCsv());
    }

    [Theory]
    [InlineData("9,20,10", "9,20,12", 10, 5, false)]
    [InlineData("9,20,10", "9,20,12", 0, 5, true)]
    [InlineData("50,2,1", "50,2,2", 10, 5, false)]
    [InlineData("50,2,1", "50,2,2", 10, 0, true)]
    [InlineData("50,20,10", "50,2,1", 10, 5, false)]
    public void DifferenceUsesEitherAchromaticThresholdForEitherColor(string standard, string sample,
        double l, double c, bool hueApplicable)
    {
        var table = ToolCalculations.DifferenceAuto(standard, sample, 1, 1, 1, 1, 1, l, c);
        Assert.Equal(hueApplicable, table.Rows[0][13] != "—");
        Assert.Equal(!hueApplicable, table.Rows[0][Array.IndexOf(table.Headers, "色相偏色")] == "不适用");
    }

    [Fact]
    public void GradeEvaluationUsesSelectedFormulaAndCmcFactorsWithoutMutatingOptions()
    {
        var options = new ColorComparisonOptions { AchromaticLightnessThreshold = 60, AchromaticChromaThreshold = 0 };
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc, cmcL: 1.5, cmcC: 0.8,
            comparisonOptions: options);
        var result = ColorGradeCalculations.Compose(card, 1, 1, -1, options);
        var comparison = result.ColorComparison;
        Assert.Equal(1.5, comparison.AppliedParameters.Cmc.L);
        Assert.Equal(0.8, comparison.AppliedParameters.Cmc.C);
        Assert.Equal(ComparisonFormula.Cmc, comparison.Evaluation.Overall.Formula);
        Assert.Equal(result.DeltaE, comparison.Evaluation.Overall.DeltaE);
        Assert.Equal(HueShiftStatus.NotApplicable, comparison.Evaluation.Hue.Status);
        Assert.Null(result.DeltaHue);
        Assert.Equal(ComparisonFormula.Ciede2000, options.Evaluation.Formula);
        Assert.Equal(1, options.Cmc.L);
        Assert.All(card.Table.Rows.Where(row => row[12] == "有效"), row =>
            Assert.Equal("不适用", row[Array.IndexOf(card.Table.Headers, "色相偏色")]));
        Assert.Contains("不适用", result.Table.ToCsv());
        Assert.Equal("Not applicable", comparison.Evaluation.HueCommentsEnglish);
    }

    [Fact]
    public void GradeThresholdChangesEvaluationWithoutChangingPathOrGrades()
    {
        var settings = new ColorGradeSettings(ColorGradeFormula.Cie76);
        var normal = ColorGradeCalculations.Analyze("50,30,20", "53,32,18", settings);
        var neutral = ColorGradeCalculations.Analyze("50,30,20", "53,32,18", settings,
            comparisonOptions: new() { AchromaticChromaThreshold = 50 });
        Assert.Equal(normal.Sample, neutral.Sample);
        Assert.Equal(normal.DeltaE, neutral.DeltaE);
        Assert.Equal(normal.Components.Select(c => c.Grade), neutral.Components.Select(c => c.Grade));
        Assert.Equal(HueShiftStatus.Shifted, normal.ColorComparison.Evaluation.Hue.Status);
        Assert.Equal(HueShiftStatus.NotApplicable, neutral.ColorComparison.Evaluation.Hue.Status);
    }

    [Fact]
    public void GradeHueStripAndSampleUseActualBiasLabels()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc);
        var hue = card.Scales.Single(scale => scale.Axis == ColorGradeAxis.Hue);
        Assert.Equal("偏红", hue.NegativeLabel);
        Assert.Equal("偏黄", hue.PositiveLabel);
        Assert.Equal("More reddish", hue.Chips.Single(chip => chip.Level == -1).Comparison!.Evaluation.HueCommentsEnglish);
        var sample = ColorGradeCalculations.Analyze("50,30,20", "53,32,18", card.Settings);
        Assert.Equal(new[] { "更亮", "更艳", "偏红", "均为有彩色" }, ColorEvaluationPresentation.Comments(sample.ColorComparison));
        var neutral = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc,
            comparisonOptions: new() { AchromaticChromaThreshold = 50 });
        Assert.Equal("不适用", neutral.Scales.Single(scale => scale.Axis == ColorGradeAxis.Hue).NegativeLabel);
    }

    [Theory]
    [InlineData(InputSpace.sRGB, "255,0,0")]
    [InlineData(InputSpace.HEX, "#FF0000")]
    public void DefaultObserverIsTenDegreesIncludingRgbInputAndPreviews(InputSpace space, string input)
    {
        Assert.Equal(StandardObserver.Degree10, ToolCalculations.DefaultObserver);
        var ten = ToolCalculations.ConvertColors(input, space, Standardilluminant.D65, ToolCalculations.DefaultObserver);
        var two = ToolCalculations.ConvertColors(input, space, Standardilluminant.D65, StandardObserver.Degree2);
        Assert.Contains("D65 / 10°", ten.Conditions);
        Assert.Equal(two.Rows[0][17], ten.Rows[0][17]);
        Assert.Equal(two.Rows[0][19..], ten.Rows[0][19..]);
        Assert.NotEqual(two.Rows[0][5], ten.Rows[0][5]);
        Assert.True(ToolCalculations.CanPreview(Standardilluminant.D65, ToolCalculations.DefaultObserver));
        var spectrum = ToolCalculations.Reflectance(string.Join('\n', Enumerable.Repeat("18", 41)),
            380, 780, 10, false, Standardilluminant.D65, ToolCalculations.DefaultObserver);
        Assert.NotNull(spectrum.Hex);
        var difference = ToolCalculations.DifferenceAuto("50,30,20", "53,32,18", 1, 1, 1, 1, 1);
        Assert.Contains("preview: D65 / 10°", difference.Conditions);
        var expectedHex = ChromaticityDotNet.Controller.ChromaticityConversion.RGBToHex(
            ChromaticityDotNet.Controller.ChromaticityConversion.XYZ2RGB(
                ChromaticityDotNet.Controller.ChromaticityConversion.Labch2XYZ(new(50,30,20), Standardilluminant.D65, ToolCalculations.DefaultObserver)));
        Assert.Equal(expectedHex, difference.Rows[0][1]);
        Assert.Equal(expectedHex, ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cie76).Scales[0].Chips.Single(chip => chip.Level == 0).Hex);
    }

    [Theory]
    [InlineData("255,0,0", InputSpace.sRGB, "0.0000", "100.0000", "50.0000", "100.0000", "100.0000")]
    [InlineData("#20A090", InputSpace.HEX, "172.5000", "66.6667", "37.6471", "80.0000", "62.7451")]
    [InlineData("#808080", InputSpace.HEX, "0.0000", "0.0000", "50.1961", "0.0000", "50.1961")]
    [InlineData("#000000", InputSpace.HEX, "0.0000", "0.0000", "0.0000", "0.0000", "0.0000")]
    public void ConversionAndExportsIncludeHslHsvInDegreesAndPercent(string input, InputSpace space,
        string hue, string sl, string l, string sv, string v)
    {
        var table = ToolCalculations.ConvertColors(input, space, Standardilluminant.D65, StandardObserver.Degree2);
        string Value(string header) => table.Rows[0][Array.IndexOf(table.Headers, header)];
        Assert.Equal(hue, Value("H (HSL) / °")); Assert.Equal(hue, Value("H (HSV) / °"));
        Assert.Equal(sl, Value("S (HSL) / %")); Assert.Equal(l, Value("L (HSL) / %"));
        Assert.Equal(sv, Value("S (HSV) / %")); Assert.Equal(v, Value("V (HSV) / %"));
        Assert.Equal(table.Headers.Length, table.Rows[0].Length);
        Assert.Contains("H (HSL) / °", table.ToCsv()); Assert.Contains("V (HSV) / %", table.ToTsv());
    }
}
