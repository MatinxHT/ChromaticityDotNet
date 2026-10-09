using System.Globalization;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ColorComparisonTests
{
    private static CIELABCH AtHue(double hue, double l = 50, double c = 30) =>
        new(l, c * Math.Cos(hue * Math.PI / 180), c * Math.Sin(hue * Math.PI / 180));

    [Fact]
    public void DefaultsCalculateAllThreeFormulasWithUnitWeights()
    {
        var reference = new CIELABCH(50, 20, 20);
        var sample = new CIELABCH(52, 18, 24);
        var result = ChromaticityMatch.CompareColors(reference, sample);

        Assert.Equal(ChromaticityDeltaEFormulations.DeltaE1976(reference, sample), result.DeltaE1976);
        Assert.Equal(ChromaticityDeltaEFormulations.DeltaEcmc(reference, sample, 1, 1), result.DeltaECmc);
        Assert.Equal(ChromaticityDeltaEFormulations.DeltaE2000(reference, sample, 1, 1, 1).DeltaE, result.DeltaE2000);
        Assert.Equal(1, result.AppliedParameters.Cmc.L);
        Assert.Equal(1, result.AppliedParameters.Cmc.C);
        Assert.Equal(ColorDifferenceDirection.Higher, result.Evaluation.Lightness.Direction);
        Assert.Equal(ColorDifferenceDirection.Higher, result.Evaluation.Chroma.Direction);
        Assert.Equal("Yellow", result.Evaluation.Hue.TargetAxis!.Name);
        Assert.Equal("More yellowish", result.Evaluation.Hue.DescriptionEnglish);
        Assert.Equal("Lighter", result.Evaluation.LightnessCommentsEnglish);
        Assert.Equal("Higher chroma", result.Evaluation.ChromaCommentsEnglish);
        Assert.Equal("More yellowish", result.Evaluation.HueCommentsEnglish);
        Assert.False(result.Evaluation.IsAchromatic);
        Assert.Equal("Both chromatic", result.Evaluation.AchromaticCommentsEnglish);
        Assert.Equal("Not evaluated", result.Evaluation.TotalCommentsEnglish);
        Assert.Equal(ColorAcceptanceStatus.NotEvaluated, result.Evaluation.Overall.Acceptance);
        Assert.Equal(ColorPerceptibilityStatus.NotEvaluated, result.Evaluation.Overall.Perceptibility);
    }

    [Fact]
    public void BothWeightSetsAreIndependentAndDoNotChangeDirection()
    {
        var reference = new CIELABCH(50, 20, 20);
        var sample = new CIELABCH(60, 18, 24);
        var normal = ChromaticityMatch.CompareColors(reference, sample);
        var weighted = ChromaticityMatch.CompareColors(reference, sample, new ColorComparisonOptions
        {
            Cmc = new CmcParameters(2, 0.5),
            Ciede2000 = new Ciede2000Parameters(2, 3, 4)
        });
        Assert.Equal(normal.DeltaE1976, weighted.DeltaE1976);
        Assert.Equal(ChromaticityDeltaEFormulations.DeltaEcmc(reference, sample, 2, 0.5), weighted.DeltaECmc);
        Assert.Equal(ChromaticityDeltaEFormulations.DeltaE2000(reference, sample, 2, 3, 4).DeltaE, weighted.DeltaE2000);
        Assert.NotEqual(normal.DeltaECmc, weighted.DeltaECmc);
        Assert.NotEqual(normal.DeltaE2000, weighted.DeltaE2000);
        Assert.Equal(normal.Evaluation.Hue.DescriptionEnglish, weighted.Evaluation.Hue.DescriptionEnglish);
        Assert.Equal(normal.Evaluation.Lightness.Direction, weighted.Evaluation.Lightness.Direction);
        Assert.Equal(normal.Evaluation.Chroma.Direction, weighted.Evaluation.Chroma.Direction);
    }

    [Theory]
    [InlineData(9.9999, 30, true)]
    [InlineData(50, 4.99999, true)]
    [InlineData(10, 5, false)]
    [InlineData(50, 30, false)]
    public void AchromaticClassificationUsesStrictOrAndUnroundedChroma(double l, double c, bool expected)
    {
        var result = ChromaticityMatch.CompareColors(new CIELABCH(50, 30, 0), new CIELABCH(l, c, 0));
        Assert.Equal(expected, result.Sample.IsAchromatic);
        Assert.Equal(expected, result.Evaluation.Neutrality.SampleIsAchromatic);
        Assert.Equal(expected ? HueShiftStatus.NotApplicable : HueShiftStatus.Unchanged, result.Evaluation.Hue.Status);
        Assert.Equal(expected, result.Differences.HueAngleDifferenceDegrees is null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EitherAchromaticInputSkipsHueButStillCalculatesDifferences(bool neutralReference)
    {
        var chromatic = AtHue(80);
        var achromatic = AtHue(20, l: 9);
        var result = ChromaticityMatch.CompareColors(neutralReference ? achromatic : chromatic,
            neutralReference ? chromatic : achromatic);
        Assert.Equal(HueShiftStatus.NotApplicable, result.Evaluation.Hue.Status);
        Assert.Null(result.Evaluation.Hue.TargetAxis);
        Assert.Null(result.Evaluation.Hue.SampleMainAxis);
        Assert.Null(result.Differences.HueAngleDifferenceDegrees);
        Assert.True(result.DeltaE1976 > 0);
        Assert.True(result.DeltaECmc > 0);
        Assert.True(result.DeltaE2000 > 0);
    }

    [Fact]
    public void AchromaticThresholdsCanBeAdjustedAndZeroChromaStillHasNoHue()
    {
        var reference = AtHue(40, l: 8, c: 3);
        var sample = AtHue(50, l: 8, c: 3);
        Assert.Equal(HueShiftStatus.NotApplicable, ChromaticityMatch.CompareColors(reference, sample).Evaluation.Hue.Status);
        var options = new ColorComparisonOptions { AchromaticLightnessThreshold = 5, AchromaticChromaThreshold = 2 };
        Assert.Equal(HueShiftStatus.Shifted, ChromaticityMatch.CompareColors(reference, sample, options).Evaluation.Hue.Status);

        options.AchromaticLightnessThreshold = 0;
        options.AchromaticChromaThreshold = 0;
        var zero = ChromaticityMatch.CompareColors(new CIELABCH(50, 0, 0), sample, options);
        Assert.False(zero.Reference.IsAchromatic);
        Assert.Equal(HueShiftStatus.NotApplicable, zero.Evaluation.Hue.Status);
    }

    [Theory]
    [InlineData(40, 50, 10, "Yellow", HueShiftDirection.Increasing)]
    [InlineData(60, 50, -10, "Red", HueShiftDirection.Decreasing)]
    [InlineData(359, 1, 2, "Red", HueShiftDirection.Increasing)]
    [InlineData(1, 359, -2, "Purple", HueShiftDirection.Decreasing)]
    [InlineData(85, 95, 10, "Green", HueShiftDirection.Increasing)]
    [InlineData(270, 260, -10, "Green", HueShiftDirection.Decreasing)]
    [InlineData(330, 350, 20, "Red", HueShiftDirection.Increasing)]
    public void HueSelectsOneMainAxisInTheLchDifferenceDirection(double referenceHue, double sampleHue,
        double delta, string axis, HueShiftDirection direction)
    {
        var result = ChromaticityMatch.CompareColors(AtHue(referenceHue), AtHue(sampleHue));
        Assert.Equal(delta, result.Differences.HueAngleDifferenceDegrees!.Value, 4);
        Assert.Equal(direction, result.Evaluation.Hue.Direction);
        Assert.Equal(axis, result.Evaluation.Hue.TargetAxis!.Name);
        Assert.Equal(HueShiftStatus.Shifted, result.Evaluation.Hue.Status);
    }

    [Fact]
    public void ExactAxisAndOppositeHueUseDocumentedDirectionRules()
    {
        var increasing = ChromaticityMatch.CompareColors(AtHue(80), AtHue(85));
        var decreasing = ChromaticityMatch.CompareColors(AtHue(90), AtHue(85));
        Assert.Equal("Green", increasing.Evaluation.Hue.TargetAxis!.Name);
        Assert.Equal("Red", decreasing.Evaluation.Hue.TargetAxis!.Name);
        var opposite = ChromaticityMatch.CompareColors(new CIELABCH(50, 30, 0), new CIELABCH(50, -30, 0));
        Assert.Equal(180, opposite.Differences.HueAngleDifferenceDegrees);
        Assert.Equal(HueShiftDirection.Increasing, opposite.Evaluation.Hue.Direction);
    }

    [Fact]
    public void ChromaOnlyChangeDoesNotGenerateHueBias()
    {
        var result = ChromaticityMatch.CompareColors(new CIELABCH(50, 20, 20), new CIELABCH(50, 40, 40));
        Assert.Equal(HueShiftStatus.Unchanged, result.Evaluation.Hue.Status);
        Assert.Null(result.Evaluation.Hue.TargetAxis);
        Assert.Equal(0, result.Differences.HueAngleDifferenceDegrees);
        Assert.Equal(ColorDifferenceDirection.Higher, result.Evaluation.Chroma.Direction);
    }

    [Fact]
    public void CustomAxesAcceptUnsortedCoordinatesAndCustomEnglishLabels()
    {
        var options = new ColorComparisonOptions
        {
            HueAxes = new List<HueAxis>
            {
                new("Cool", 200, "More cool-toned"),
                new("Warm", 60, "More warm-toned")
            }
        };
        var result = ChromaticityMatch.CompareColors(AtHue(30), AtHue(40), options);
        Assert.Equal("Warm", result.Evaluation.Hue.TargetAxis!.Name);
        Assert.Equal("More warm-toned", result.Evaluation.Hue.DescriptionEnglish);
        Assert.Equal(60, result.AppliedParameters.HueAxes[0].HueAngleDegrees);
        Assert.Equal("Cool", options.HueAxes[0].Name);
    }

    [Fact]
    public void TolerancesSuppressTinyDirectionChangesWithoutSuppressingNumericResults()
    {
        var options = new ColorComparisonOptions
        {
            Evaluation = new ColorEvaluationOptions { LightnessTolerance = 1, ChromaTolerance = 1, HueAngleToleranceDegrees = 1 }
        };
        var result = ChromaticityMatch.CompareColors(AtHue(40), AtHue(40.5, l: 50.5, c: 30.5), options);
        Assert.Equal(ColorDifferenceDirection.Unchanged, result.Evaluation.Lightness.Direction);
        Assert.Equal(ColorDifferenceDirection.Unchanged, result.Evaluation.Chroma.Direction);
        Assert.Equal(HueShiftStatus.Unchanged, result.Evaluation.Hue.Status);
        Assert.Equal(0.5, result.Differences.DeltaL);
        Assert.Equal(0.5, result.Differences.HueAngleDifferenceDegrees);
        Assert.True(result.DeltaE1976 > 0);
    }

    [Theory]
    [InlineData(ComparisonFormula.Cie76)]
    [InlineData(ComparisonFormula.Cmc)]
    [InlineData(ComparisonFormula.Ciede2000)]
    public void OverallAssessmentUsesTheSelectedFormulaAndIncludesThresholdBoundaries(ComparisonFormula formula)
    {
        var reference = new CIELABCH(50, 30, 0);
        var sample = new CIELABCH(53, 30, 0);
        var initial = ChromaticityMatch.CompareColors(reference, sample);
        double selected = formula switch
        {
            ComparisonFormula.Cie76 => initial.DeltaE1976,
            ComparisonFormula.Cmc => initial.DeltaECmc,
            _ => initial.DeltaE2000
        };
        var options = new ColorComparisonOptions
        {
            Evaluation = new ColorEvaluationOptions { Formula = formula, AcceptanceTolerance = selected, PerceptibilityThreshold = selected }
        };
        var result = ChromaticityMatch.CompareColors(reference, sample, options);
        Assert.Equal(selected, result.Evaluation.Overall.DeltaE);
        Assert.Equal(ColorAcceptanceStatus.WithinTolerance, result.Evaluation.Overall.Acceptance);
        Assert.Equal(ColorPerceptibilityStatus.AtOrAboveThreshold, result.Evaluation.Overall.Perceptibility);
        options.Evaluation.AcceptanceTolerance = selected - 0.0001;
        options.Evaluation.PerceptibilityThreshold = selected + 0.0001;
        result = ChromaticityMatch.CompareColors(reference, sample, options);
        Assert.Equal(ColorAcceptanceStatus.OutsideTolerance, result.Evaluation.Overall.Acceptance);
        Assert.Equal(ColorPerceptibilityStatus.BelowThreshold, result.Evaluation.Overall.Perceptibility);
    }

    [Fact]
    public void ResultsSnapshotMutableInputsAndOptionsAndDefaultListsAreIndependent()
    {
        var reference = AtHue(40);
        var sample = AtHue(50);
        var options = new ColorComparisonOptions();
        var result = ChromaticityMatch.CompareColors(reference, sample, options);
        string description = result.Evaluation.HueCommentsEnglish;
        reference.CIEL = 0; sample.CIEA = 0;
        options.Cmc = new CmcParameters(2, 1);
        options.AchromaticLightnessThreshold = 100;
        options.Evaluation.AcceptanceTolerance = 1;
        options.HueAxes.Clear();
        Assert.Equal(50, result.Reference.L);
        Assert.Equal(50, result.Sample.L);
        Assert.Equal(1, result.AppliedParameters.Cmc.L);
        Assert.Equal(10, result.AppliedParameters.AchromaticLightnessThreshold);
        Assert.Null(result.AppliedParameters.AcceptanceTolerance);
        Assert.Equal(5, result.AppliedParameters.HueAxes.Count);
        Assert.Equal(description, result.Evaluation.HueCommentsEnglish);
        Assert.Throws<NotSupportedException>(() => ((IList<HueAxis>)result.AppliedParameters.HueAxes).Clear());
        Assert.Equal(5, new ColorComparisonOptions().HueAxes.Count);
        var copiedLab = result.Reference.ToLab();
        copiedLab.CIEL = 0;
        Assert.Equal(50, result.Reference.L);
    }

    [Fact]
    public void StructuredResultIsIndependentOfCurrentCulture()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            var reference = AtHue(40);
            var sample = AtHue(50, l: 51.25);
            var expected = System.Text.Json.JsonSerializer.Serialize(ChromaticityMatch.CompareColors(reference, sample));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, System.Text.Json.JsonSerializer.Serialize(ChromaticityMatch.CompareColors(reference, sample)));
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void WeightConstructorsRejectInvalidValues(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CmcParameters(value, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CmcParameters(1, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ciede2000Parameters(value, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ciede2000Parameters(1, value, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ciede2000Parameters(1, 1, value));
    }

    [Fact]
    public void InvalidInputsOptionsAndNonfiniteFormulaResultsAreRejected()
    {
        var valid = new CIELABCH(50, 30, 0);
        Assert.Throws<ArgumentNullException>(() => ChromaticityMatch.CompareColors(null!, valid));
        Assert.Throws<ArgumentNullException>(() => ChromaticityMatch.CompareColors(valid, null!));
        foreach (var invalid in new[] { new CIELABCH(-1, 0, 0), new CIELABCH(double.NaN, 0, 0),
            new CIELABCH(50, double.PositiveInfinity, 0), new CIELABCH(50, 0, double.NaN) })
            Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityMatch.CompareColors(invalid, valid));
        Assert.Throws<ArgumentException>(() => ChromaticityMatch.CompareColors(new CIELABCH(50, 1e200, 0), valid));
        Assert.Throws<ArgumentException>(() => ChromaticityMatch.CompareColors(valid, new CIELABCH(60, 30, 0),
            new ColorComparisonOptions { Cmc = new CmcParameters(double.Epsilon, 1) }));

        foreach (var options in new[]
        {
            new ColorComparisonOptions { Cmc = null! },
            new ColorComparisonOptions { Ciede2000 = null! },
            new ColorComparisonOptions { Evaluation = null! },
            new ColorComparisonOptions { HueAxes = null! },
            new ColorComparisonOptions { HueAxes = new List<HueAxis>() },
            new ColorComparisonOptions { HueAxes = new List<HueAxis> { null!, new("A", 0, "More A") } },
            new ColorComparisonOptions { HueAxes = new List<HueAxis> { new("A", 0, "More A"), new("B", 0, "More B") } },
            new ColorComparisonOptions { HueAxes = new List<HueAxis> { new("A", 0, "More A"), new("a", 90, "More A") } }
        }) Assert.Throws<ArgumentException>(() => ChromaticityMatch.CompareColors(valid, valid, options));

        foreach (var options in new[]
        {
            new ColorComparisonOptions { AchromaticLightnessThreshold = -1 },
            new ColorComparisonOptions { AchromaticChromaThreshold = double.NaN },
            new ColorComparisonOptions { Evaluation = new ColorEvaluationOptions { Formula = (ComparisonFormula)99 } },
            new ColorComparisonOptions { Evaluation = new ColorEvaluationOptions { AcceptanceTolerance = -1 } },
            new ColorComparisonOptions { Evaluation = new ColorEvaluationOptions { PerceptibilityThreshold = double.PositiveInfinity } },
            new ColorComparisonOptions { Evaluation = new ColorEvaluationOptions { LightnessTolerance = -1 } },
            new ColorComparisonOptions { Evaluation = new ColorEvaluationOptions { ChromaTolerance = double.NaN } },
            new ColorComparisonOptions { Evaluation = new ColorEvaluationOptions { HueAngleToleranceDegrees = 181 } }
        }) Assert.Throws<ArgumentOutOfRangeException>(() => ChromaticityMatch.CompareColors(valid, valid, options));
        Assert.Throws<ArgumentException>(() => new HueAxis("", 20, "More reddish"));
        Assert.Throws<ArgumentException>(() => new HueAxis("Red", 20, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HueAxis("Red", 360, "More reddish"));
    }

    [Fact]
    public void LabToLchUsesOriginalCoordinatesInsteadOfRoundedDerivedProperties()
    {
        var lab = new CIELABCH(50, 4.99999, 0);
        Assert.Equal(5, lab.CIEC);
        var lch = ChromaticityConversion.LabToLch(lab);
        Assert.Equal(4.99999, lch.CIEC);
        Assert.Equal(0, lch.CIEH);
        Assert.Equal(270, ChromaticityConversion.LabToLch(new CIELABCH(50, 0, -30)).CIEH);
        Assert.Equal(0, ChromaticityConversion.LabToLch(new CIELABCH(50, 0, 0)).CIEH);
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.LabToLch(null!));
    }
}
