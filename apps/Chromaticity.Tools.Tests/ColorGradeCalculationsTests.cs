using System.Globalization;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class ColorGradeCalculationsTests
{
    [Theory]
    [InlineData(ColorGradeFormula.Cie76, 1, 1)]
    [InlineData(ColorGradeFormula.Cmc, 1, 1)]
    [InlineData(ColorGradeFormula.Cmc, 2, 1)]
    [InlineData(ColorGradeFormula.Cmc, 1.5, 0.8)]
    [InlineData(ColorGradeFormula.Ciede2000, 1, 1)]
    public void EveryOutwardStepHasRequestedDifferenceAndPreservesItsAxis(ColorGradeFormula formula, double l, double c)
    {
        var result = ColorGradeCalculations.Generate("50，20，-30", formula, 4, l, c);
        Assert.Equal(3, result.Scales.Count);
        Assert.Equal(27, result.Table.Rows.Length);
        var origin = new ColorGradeLab(50, 20, -30);
        foreach (var scale in result.Scales)
        {
            Assert.Equal(Enumerable.Range(-4, 9), scale.Chips.Select(chip => chip.Level));
            Assert.Equal(origin, scale.Chips.Single(chip => chip.Level == 0).Lab);
            foreach (var direction in new[] { -1, 1 })
            {
                var previous = origin;
                foreach (var chip in scale.Chips.Where(chip => Math.Sign(chip.Level) == direction).OrderBy(chip => Math.Abs(chip.Level)))
                {
                    Assert.Null(chip.UnavailableReason);
                    Assert.NotNull(chip.Lab);
                    var lab = chip.Lab.Value;
                    Assert.InRange(Difference(previous, lab, formula, l, c), 1.4999, 1.5001);
                    Assert.Equal(Difference(previous, lab, formula, l, c), chip.StepDeltaE);
                    Assert.Equal(Difference(origin, lab, formula, l, c), chip.StandardDeltaE);
                    Assert.Matches("^#[0-9A-F]{6}$", chip.Hex!);
                    switch (scale.Axis)
                    {
                        case ColorGradeAxis.Lightness:
                            Assert.Equal(origin.A, lab.A); Assert.Equal(origin.B, lab.B);
                            Assert.True(direction * (lab.L - previous.L) > 0);
                            break;
                        case ColorGradeAxis.Chroma:
                            Assert.Equal(origin.L, lab.L);
                            Assert.InRange(Math.Abs(lab.Hue - origin.Hue), 0, 1e-10);
                            Assert.True(direction * (lab.Chroma - previous.Chroma) > 0);
                            break;
                        case ColorGradeAxis.Hue:
                            Assert.Equal(origin.L, lab.L);
                            Assert.InRange(Math.Abs(lab.Chroma - origin.Chroma), 0, 1e-10);
                            Assert.True(direction * Math.IEEERemainder(lab.Hue - previous.Hue, 360) > 0);
                            break;
                    }
                    previous = lab;
                }
            }
        }
    }

    [Fact]
    public void DefaultStandardCie76HasSixUnitsAcrossFourLightnessAndChromaGrades()
    {
        var result = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cie76);
        foreach (var chip in result.Scales[0].Chips)
            Assert.Equal(50 + 1.5 * chip.Level, chip.Lab!.Value.L, 6);
        var standardChroma = Math.Sqrt(30 * 30 + 20 * 20);
        foreach (var chip in result.Scales[1].Chips)
            Assert.Equal(standardChroma + 1.5 * chip.Level, chip.Lab!.Value.Chroma, 6);
    }

    [Fact]
    public void CmcUsesPreviousOutwardChipAsReferenceAndRetainsCumulativeDifference()
    {
        var scale = ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Cmc).Scales[0];
        var first = scale.Chips.Single(chip => chip.Level == 1).Lab!.Value;
        var second = scale.Chips.Single(chip => chip.Level == 2);
        Assert.Equal(1.5, Difference(first, second.Lab!.Value, ColorGradeFormula.Cmc, 2, 1));
        Assert.NotEqual(1.5, Difference(second.Lab.Value, first, ColorGradeFormula.Cmc, 2, 1));
        Assert.NotEqual(3, second.StandardDeltaE);
    }

    [Fact]
    public void CmcRatioChangesLightnessAndChromaSpacing()
    {
        var equal = ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Cmc, 1, 1, 1);
        var doubledL = ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Cmc, 1, 2, 1);
        var doubledC = ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Cmc, 1, 1, 2);
        Assert.InRange((Positive(doubledL, 0).L - 50) / (Positive(equal, 0).L - 50), 1.9998, 2.0002);
        Assert.InRange((Positive(doubledC, 1).Chroma - Math.Sqrt(1300)) /
            (Positive(equal, 1).Chroma - Math.Sqrt(1300)), 1.9998, 2.0002);
        Assert.Equal(Positive(equal, 2), Positive(doubledC, 2));
    }

    [Fact]
    public void E00AlwaysUsesFixedUnitWeightsAndIgnoresInactiveCmcSettings()
    {
        var result = ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Ciede2000, 4, double.NaN, 0);
        Assert.Equal(ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Ciede2000).Table.ToCsv(), result.Table.ToCsv());
        Assert.Contains("kL:kC:kH=1:1:1", result.Table.ToCsv());
    }

    [Theory]
    [InlineData(ColorGradeFormula.Cie76)]
    [InlineData(ColorGradeFormula.Cmc)]
    [InlineData(ColorGradeFormula.Ciede2000)]
    public void BlackWhiteAndNeutralHaveHonestUnavailableGrades(ColorGradeFormula formula)
    {
        foreach (var l in new[] { 0, 50, 100 })
        {
            var result = ColorGradeCalculations.Generate($"{l},0,0", formula);
            foreach (var scale in result.Scales.Skip(1))
            {
                Assert.Single(scale.Chips, chip => chip.Lab is not null);
                Assert.All(scale.Chips.Where(chip => chip.Level != 0), chip =>
                {
                    Assert.Null(chip.Hex); Assert.Null(chip.StepDeltaE); Assert.Null(chip.StandardDeltaE);
                    Assert.Contains("中性色", chip.UnavailableReason);
                });
            }
            Assert.All(result.Scales[0].Chips.Where(chip => chip.Lab is not null), chip => Assert.InRange(chip.Lab!.Value.L, 0, 100));
            if (l is 0 or 100)
                Assert.All(result.Scales[0].Chips.Where(chip => l == 0 ? chip.Level < 0 : chip.Level > 0), chip => Assert.Null(chip.Lab));
        }
    }

    [Fact]
    public void InsufficientChromaOrLightnessDoesNotGenerateShortStepsOrCrossZero()
    {
        var result = ColorGradeCalculations.Generate("0.5,0.2,0", ColorGradeFormula.Cie76);
        Assert.All(result.Scales[0].Chips.Where(chip => chip.Level < 0), chip => Assert.Null(chip.Lab));
        Assert.All(result.Scales[1].Chips.Where(chip => chip.Level < 0), chip => Assert.Null(chip.Lab));
        Assert.All(result.Scales[2].Chips.Where(chip => chip.Level != 0), chip => Assert.Null(chip.Lab));
        Assert.NotNull(result.Scales[0].Chips.Single(chip => chip.Level == 1).Lab);
        Assert.NotNull(result.Scales[1].Chips.Single(chip => chip.Level == 1).Lab);
    }

    [Theory]
    [InlineData(ColorGradeFormula.Cie76)]
    [InlineData(ColorGradeFormula.Cmc)]
    [InlineData(ColorGradeFormula.Ciede2000)]
    public void HueWrapKeepsRequestedStepsAndStaysInsideHalfCircle(ColorGradeFormula formula)
    {
        var result = ColorGradeCalculations.Generate("50,30,-0.01", formula, 10);
        var origin = result.Scales[2].Chips.Single(chip => chip.Level == 0).Lab!.Value;
        var previous = origin;
        foreach (var chip in result.Scales[2].Chips.Where(chip => chip.Level > 0))
        {
            Assert.NotNull(chip.Lab);
            var lab = chip.Lab.Value;
            Assert.InRange(lab.Hue, 0, 360);
            Assert.InRange(Math.IEEERemainder(lab.Hue - origin.Hue, 360), 0, 180);
            Assert.Equal(1.5, Difference(previous, lab, formula, 2, 1));
            previous = lab;
        }
        Assert.True(previous.Hue < origin.Hue);
    }

    [Fact]
    public void LowChromaHueStopsAtHalfCircleInsteadOfWrappingBack()
    {
        var result = ColorGradeCalculations.Generate("50,1,0", ColorGradeFormula.Cie76, 10);
        var hue = result.Scales[2];
        Assert.Equal(3, hue.Chips.Count(chip => chip.Lab is not null));
        Assert.All(hue.Chips.Where(chip => Math.Abs(chip.Level) >= 2), chip => Assert.Null(chip.Lab));
    }

    [Fact]
    public void ExportRetainsActualNumbersParametersAndUnavailableReasonsAcrossLocales()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var table = ColorGradeCalculations.Generate("0.5,0.2,0", ColorGradeFormula.Cmc, 2, 1.5, 0.8).Table;
            Assert.Equal(15, table.Rows.Length);
            Assert.All(table.Rows, row => Assert.Equal(table.Headers.Length, row.Length));
            Assert.Contains("CMC l:c=1.5:0.8", table.ToCsv());
            Assert.Contains("outward adjacent ΔE=1.5;", table.ToCsv());
            Assert.Contains("reference: previous chip", table.ToCsv());
            Assert.Contains("无法", table.ToCsv());
            Assert.All(table.Rows, row => Assert.Equal("1.5000", row[Array.IndexOf(table.Headers, "目标级间 ΔE")]));
            Assert.All(table.ToTsv().Split('\n'), line => Assert.Equal(table.Headers.Length, line.Split('\t').Length));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Theory]
    [InlineData("")]
    [InlineData("50,20")]
    [InlineData("50,20,30,40")]
    [InlineData("50,,30")]
    [InlineData("50,20,30\n50,20,30")]
    [InlineData("-1,0,0")]
    [InlineData("101,0,0")]
    [InlineData("NaN,0,0")]
    [InlineData("50,Infinity,0")]
    [InlineData("50,1e300,0")]
    public void InvalidStandardIsRejected(string text) =>
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Generate(text, ColorGradeFormula.Cie76));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void InvalidLevelCountIsRejected(int count) =>
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Cie76, count));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.PositiveInfinity)]
    public void InvalidCmcWeightIsRejected(double l, double c) =>
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Generate("50,20,-30", ColorGradeFormula.Cmc, 4, l, c));

    [Fact]
    public void UnknownFormulaIsRejected() => Assert.Throws<ArgumentException>(() =>
        ColorGradeCalculations.Generate("50,20,-30", (ColorGradeFormula)99));

    private static ColorGradeLab Positive(ColorGradeResult result, int axis) => result.Scales[axis].Chips.Single(chip => chip.Level == 1).Lab!.Value;
    private static double Difference(ColorGradeLab standard, ColorGradeLab sample, ColorGradeFormula formula, double l, double c) => formula switch
    {
        ColorGradeFormula.Cie76 => ChromaticityDeltaEFormulations.DeltaE1976(standard.ToLab(), sample.ToLab()),
        ColorGradeFormula.Cmc => ChromaticityDeltaEFormulations.DeltaEcmc(standard.ToLab(), sample.ToLab(), l, c),
        _ => ChromaticityDeltaEFormulations.DeltaE2000(standard.ToLab(), sample.ToLab(), 1, 1, 1).DeltaE
    };
}
