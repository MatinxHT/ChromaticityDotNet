using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class ColorGradeComparisonTests
{
    [Theory]
    [InlineData(ColorGradeFormula.Cie76, 0.5)]
    [InlineData(ColorGradeFormula.Cie76, 2)]
    [InlineData(ColorGradeFormula.Cmc, 0.75)]
    [InlineData(ColorGradeFormula.Cmc, 2)]
    [InlineData(ColorGradeFormula.Ciede2000, 1)]
    [InlineData(ColorGradeFormula.Ciede2000, 2)]
    public void CustomThresholdControlsEveryScaleAndItsExport(ColorGradeFormula formula, double threshold)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", formula, stepDeltaE: threshold);
        Assert.Equal(threshold, card.Settings.StepDeltaE);
        Assert.All(card.Scales.SelectMany(scale => scale.Chips).Where(chip => chip.Level != 0), chip =>
            Assert.Equal(threshold, chip.StepDeltaE));
        Assert.All(card.Table.Rows, row => Assert.Equal(ToolCalculations.F(threshold),
            row[Array.IndexOf(card.Table.Headers, "目标级间 ΔE")]));
    }

    [Fact]
    public void Cie76CompositionMatchesIndependentPolarSolution()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cie76);
        var result = ColorGradeCalculations.Compose(card, 2, 1, -3);
        var chroma = Math.Sqrt(1300) + 1.5;
        var hue = Math.Atan2(20, 30) - 3 * 2 * Math.Asin(1.5 / (2 * chroma));
        Assert.Equal(53, result.Sample.L, 6);
        Assert.Equal(chroma * Math.Cos(hue), result.Sample.A, 3);
        Assert.Equal(chroma * Math.Sin(hue), result.Sample.B, 3);
        Assert.Equal(new int?[] { 2, 1, -3 }, result.Components.Select(c => c.Grade));
        Assert.Equal(result.Components[0].End, result.Components[1].Start);
        Assert.Equal(result.Components[1].End, result.Components[2].Start);
        Assert.NotEqual(card.Scales[2].Chips.Single(c => c.Level == -3).Lab!.Value, result.Sample);
    }

    [Theory]
    [InlineData(ColorGradeFormula.Cie76, 0.5, 2, 1, -3, 1, 1)]
    [InlineData(ColorGradeFormula.Cmc, 1.5, 2, 1, -3, 2, 1)]
    [InlineData(ColorGradeFormula.Cmc, 0.8, -3, -2, 4, 1.5, 0.8)]
    [InlineData(ColorGradeFormula.Ciede2000, 1.5, 2, 1, -3, 1, 1)]
    [InlineData(ColorGradeFormula.Ciede2000, 2, -2, -1, 3, 1, 1)]
    public void CompositionPreservesEachStageAndAnalysisRecoversRequestedGrades(ColorGradeFormula formula,
        double threshold, int l, int c, int h, double cmcL, double cmcC)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", formula, 4, cmcL, cmcC, threshold);
        var result = ColorGradeCalculations.Compose(card, l, c, h);
        foreach (var component in result.Components)
        {
            Assert.Equal(Math.Abs(new[] { l, c, h }[(int)component.Axis]), component.Steps.Count);
            foreach (var step in component.Steps)
            {
                Assert.Equal(threshold, Difference(step.Reference, step.Sample, card.Settings));
                switch (component.Axis)
                {
                    case ColorGradeAxis.Lightness:
                        Assert.Equal(step.Reference.A, step.Sample.A); Assert.Equal(step.Reference.B, step.Sample.B); break;
                    case ColorGradeAxis.Chroma:
                        Assert.Equal(step.Reference.L, step.Sample.L); Assert.Equal(step.Reference.Hue, step.Sample.Hue, 8); break;
                    case ColorGradeAxis.Hue:
                        Assert.Equal(step.Reference.L, step.Sample.L); Assert.Equal(step.Reference.Chroma, step.Sample.Chroma, 8); break;
                }
            }
        }
        var analyzed = ColorGradeCalculations.Analyze(LabText(card.Standard), LabText(result.Sample), card.Settings);
        foreach (var component in analyzed.Components)
            Assert.InRange(Math.Abs(component.RawGrade!.Value - new[] { l, c, h }[(int)component.Axis]), 0, 0.0002);
        Assert.Equal(new int?[] { l, c, h }, analyzed.Components.Select(component => component.Grade));
        Assert.Equal(result.DeltaE, analyzed.DeltaE);
        Assert.Equal(result.StandardHex, analyzed.StandardHex); Assert.Equal(result.SampleHex, analyzed.SampleHex);
    }

    [Fact]
    public void FractionalGradesUseTheRemainingStepRatherThanRoundAwayDeviation()
    {
        var chroma = Math.Sqrt(1300) + 2.25;
        var hue = Math.Atan2(20, 30) - 2 * 2 * Math.Asin(1.5 / (2 * chroma)) - 2 * Math.Asin(0.75 / (2 * chroma));
        var sample = new ColorGradeLab(53.75, chroma * Math.Cos(hue), chroma * Math.Sin(hue));
        var analyzed = ColorGradeCalculations.Analyze("50,30,20", LabText(sample), new(ColorGradeFormula.Cie76));
        foreach (var (component, expected) in analyzed.Components.Zip(new[] { 2.5, 1.5, -2.5 }))
            Assert.InRange(Math.Abs(component.RawGrade!.Value - expected), 0, 0.0002);
        Assert.Equal(0.75, analyzed.Components[0].Steps.Last().DeltaE);
    }

    [Fact]
    public void CmcAnalysisUsesSequentialReferencesAndDoesNotDivideCumulativeDeltaByThreshold()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc);
        var composed = ColorGradeCalculations.Compose(card, 4, 0, 0);
        var analyzed = ColorGradeCalculations.Analyze("50,30,20", LabText(composed.Sample), card.Settings);
        Assert.InRange(analyzed.Components[0].Grade!.Value, 3.9999, 4.0001);
        Assert.True(Math.Abs(analyzed.Components[0].DeltaE / 1.5 - 4) > 0.01);
        var step = composed.Components[0].Steps[1];
        Assert.NotEqual(1.5, Difference(step.Sample, step.Reference, card.Settings));
    }

    [Theory]
    [InlineData(359, 1, 1)]
    [InlineData(1, 359, -1)]
    public void HueAnalysisCrossesZeroByShortestSignedAngle(double standardHue, double sampleHue, int direction)
    {
        ColorGradeLab At(double hue) => new(50, 30 * Math.Cos(hue * Math.PI / 180), 30 * Math.Sin(hue * Math.PI / 180));
        var result = ColorGradeCalculations.Analyze(LabText(At(standardHue)), LabText(At(sampleHue)), new(ColorGradeFormula.Cie76));
        Assert.Equal(direction * 2, result.DeltaHue!.Value, 7);
        Assert.Equal(direction, Math.Sign(result.Components[2].Grade!.Value));
        Assert.Equal(0, result.Components[0].Grade); Assert.InRange(Math.Abs(result.Components[1].Grade!.Value), 0, 1e-8);
    }

    [Fact]
    public void NeutralAnalysisKeepsQuantificationButDoesNotInventUndefinedGrades()
    {
        var outgoing = ColorGradeCalculations.Analyze("50,0,0", "53,10,20", new(ColorGradeFormula.Cie76));
        Assert.Equal(2, outgoing.Components[0].Grade);
        Assert.Null(outgoing.Components[1].Grade); Assert.Null(outgoing.Components[2].Grade); Assert.Null(outgoing.DeltaHue);
        Assert.Contains("中性色", outgoing.Components[1].Status);
        Assert.True(outgoing.Components[1].DeltaE > 0);
        var incoming = ColorGradeCalculations.Analyze("50,3,4", "50,0,0", new(ColorGradeFormula.Cie76));
        Assert.InRange(incoming.Components[1].RawGrade!.Value, -3.3335, -3.3331);
        Assert.Equal(-3, incoming.Components[1].Grade);
        Assert.Null(incoming.Components[2].Grade);
        var unchanged = ColorGradeCalculations.Analyze("50,0,0", "50,0,0", new(ColorGradeFormula.Cie76));
        Assert.Equal(0, unchanged.DeltaE); Assert.Equal(0, unchanged.Components[1].Grade);
    }

    [Fact]
    public void LowChromaHueCanHavePartialGradeEvenWhenNoWholeStepIsAvailable()
    {
        var result = ColorGradeCalculations.Analyze("50,0.3,0", "50,0,0.3", new(ColorGradeFormula.Cie76));
        Assert.InRange(result.Components[2].RawGrade!.Value, 0.28, 0.29);
        Assert.Equal(0, result.Components[2].Grade);
    }

    [Fact]
    public void BoundaryAndOutOfCardRequestsAreRejectedWithoutClippingTheTarget()
    {
        var card = ColorGradeCalculations.Generate("99,0.2,0", ColorGradeFormula.Cie76);
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Compose(card, 1, 0, 0));
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Compose(card, 0, -1, 0));
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Compose(card, 0, 0, 1));
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Compose(card, int.MaxValue, 0, 0));
        var neutral = ColorGradeCalculations.Generate("50,0,0", ColorGradeFormula.Cie76);
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Compose(neutral, 0, 1, 0));
        Assert.Equal(neutral.Standard, ColorGradeCalculations.Compose(neutral, 0, 0, 0).Sample);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.00001)]
    [InlineData(1.12345)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidThresholdIsRejectedForGenerationAndAnalysis(double threshold)
    {
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cie76, stepDeltaE: threshold));
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Analyze("50,30,20", "50,30,20", new(ColorGradeFormula.Cie76, threshold)));
    }

    [Theory]
    [InlineData("50,0")]
    [InlineData("101,0,0")]
    [InlineData("50,NaN,0")]
    [InlineData("50,1e300,0")]
    public void InvalidSampleIsRejected(string sample) => Assert.Throws<ArgumentException>(() =>
        ColorGradeCalculations.Analyze("50,30,20", sample, new(ColorGradeFormula.Cie76)));

    [Fact]
    public void AnalysisBeyondCardExtentIsAllowedButWorkIsBoundedForVerySmallThresholds()
    {
        var result = ColorGradeCalculations.Analyze("50,30,20", "80,30,20", new(ColorGradeFormula.Cie76));
        Assert.InRange(result.Components[0].Grade!.Value, 19.9999, 20.0001);
        var tiny = ColorGradeCalculations.Analyze("50,30,20", "80,30,20", new(ColorGradeFormula.Cie76, 0.0001));
        Assert.Null(tiny.Components[0].Grade); Assert.Contains("1000", tiny.Components[0].Status);
    }

    [Fact]
    public void ComparisonExportContainsBothColorsIntermediateCoordinatesGradesAndConditions()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc, cmcL: 1.5, cmcC: 0.8, stepDeltaE: 2);
        var result = ColorGradeCalculations.Compose(card, 2, 1, -3);
        Assert.Equal(4, result.Table.Rows.Length);
        Assert.All(result.Table.Rows, row => Assert.Equal(result.Table.Headers.Length, row.Length));
        var csv = result.Table.ToCsv();
        Assert.Contains(result.StandardHex, csv); Assert.Contains(result.SampleHex, csv);
        Assert.Contains("CMC l:c=1.5:0.8", csv); Assert.Contains("grade threshold ΔE=2", csv);
        Assert.Contains("order: L -> C -> h", csv);
        Assert.Equal("2", result.Table.Rows[1][Array.IndexOf(result.Table.Headers, "本方向有符号等级")]);
        Assert.Equal("-3", result.Table.Rows[3][Array.IndexOf(result.Table.Headers, "本方向有符号等级")]);
    }

    private static string LabText(ColorGradeLab lab) => FormattableString.Invariant($"{lab.L:G17},{lab.A:G17},{lab.B:G17}");
    private static double Difference(ColorGradeLab standard, ColorGradeLab sample, ColorGradeSettings settings) => settings.Formula switch
    {
        ColorGradeFormula.Cie76 => ChromaticityDeltaEFormulations.DeltaE1976(standard.ToLab(), sample.ToLab()),
        ColorGradeFormula.Cmc => ChromaticityDeltaEFormulations.DeltaEcmc(standard.ToLab(), sample.ToLab(), settings.CmcL, settings.CmcC),
        _ => ChromaticityDeltaEFormulations.DeltaE2000(standard.ToLab(), sample.ToLab(), 1, 1, 1).DeltaE
    };
}
