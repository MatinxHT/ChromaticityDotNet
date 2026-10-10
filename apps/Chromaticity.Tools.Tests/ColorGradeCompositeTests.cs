using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class ColorGradeCompositeTests
{
    [Theory]
    [InlineData(ColorGradeFormula.Cie76)]
    [InlineData(ColorGradeFormula.Cmc)]
    [InlineData(ColorGradeFormula.Ciede2000)]
    public void ZeroLayerKeepsStandardAtCenterAndReproducesTheOriginalAxes(ColorGradeFormula formula)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", formula, 4, stepDeltaE: 2);
        var plane = ColorGradeCalculations.GenerateComposite(card);
        Assert.Same(card, plane.Source);
        Assert.Equal(81, plane.Chips.Count);
        Assert.Equal(card.Standard, plane.Center);
        Assert.Equal(card.Standard, Cell(plane, 0, 0).Lab);
        Assert.Equal(0, Cell(plane, 0, 0).StandardDeltaE);
        Assert.Equal((-4, 4), (plane.Chips[0].ChromaLevel, plane.Chips[0].HueLevel));
        Assert.Equal((4, -4), (plane.Chips[^1].ChromaLevel, plane.Chips[^1].HueLevel));
        foreach (var axis in new[] { ColorGradeAxis.Chroma, ColorGradeAxis.Hue })
        foreach (var chip in card.Scales.Single(scale => scale.Axis == axis).Chips)
        {
            var composite = axis == ColorGradeAxis.Chroma ? Cell(plane, chip.Level, 0) : Cell(plane, 0, chip.Level);
            Assert.Equal(chip.Lab, composite.Lab);
            Assert.Equal(chip.Hex, composite.Hex);
            Assert.Equal(chip.StandardDeltaE, composite.StandardDeltaE);
        }
    }

    [Theory]
    [InlineData(ColorGradeFormula.Cie76)]
    [InlineData(ColorGradeFormula.Cmc)]
    [InlineData(ColorGradeFormula.Ciede2000)]
    public void EveryLayerUsesIndependentOriginalCAndHGradesAndOneCommonLightness(ColorGradeFormula formula)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", formula, 2, cmcL: 1.5, cmcC: 0.8, stepDeltaE: 1.2);
        var original = ColorGradeCalculations.GenerateComposite(card);
        for (var level = -2; level <= 2; level++)
        {
            var plane = ColorGradeCalculations.GenerateComposite(card, level);
            var lightness = card.Scales[0].Chips.Single(chip => chip.Level == level);
            Assert.Equal(lightness.Lab, plane.Center);
            Assert.Equal(lightness.Hex, Cell(plane, 0, 0).Hex);
            foreach (var chip in plane.Chips)
            {
                var lab = chip.Lab!.Value;
                Assert.Null(chip.UnavailableReason);
                Assert.Equal(lightness.Lab!.Value.L, lab.L);
                Assert.Equal(card.Scales[1].Chips.Single(c => c.Level == chip.ChromaLevel).Lab!.Value.Chroma, lab.Chroma, 10);
                Assert.Equal(card.Scales[2].Chips.Single(h => h.Level == chip.HueLevel).Lab!.Value.Hue, lab.Hue, 10);
                var baseColor = Cell(original, chip.ChromaLevel, chip.HueLevel).Lab!.Value;
                Assert.Equal(baseColor.A, lab.A);
                Assert.Equal(baseColor.B, lab.B);
                Assert.Matches("^#[0-9A-F]{6}$", chip.Hex!);
            }
        }
    }

    [Fact]
    public void PositiveCAndHCellMatchesTheAnalyticCie76LchCombination()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cie76, 1);
        var chip = Cell(ColorGradeCalculations.GenerateComposite(card, 1), 1, 1);
        var chroma = Math.Sqrt(1300);
        var angle = Math.Atan2(20, 30) + 2 * Math.Asin(1.5 / (2 * chroma));
        var lab = chip.Lab!.Value;
        Assert.InRange(Math.Abs(lab.L - 51.5), 0, 0.0001);
        Assert.InRange(Math.Abs(lab.A - (chroma + 1.5) * Math.Cos(angle)), 0, 0.0002);
        Assert.InRange(Math.Abs(lab.B - (chroma + 1.5) * Math.Sin(angle)), 0, 0.0002);
    }

    [Fact]
    public void CompositeDoesNotRecalculateHueAfterChangingChroma()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc);
        var composite = Cell(ColorGradeCalculations.GenerateComposite(card, 1), 1, 1).Lab!.Value;
        var sequential = ColorGradeCalculations.Compose(card, 1, 1, 1).Sample;
        Assert.True(Math.Abs(composite.Hue - sequential.Hue) > 0.001);
        Assert.Equal(card.Scales[2].Chips.Single(chip => chip.Level == 1).Lab!.Value.Hue, composite.Hue, 10);
    }

    [Fact]
    public void HueCrossingZeroAndNegativeGradesRemainOnTheirOriginalAxes()
    {
        var card = ColorGradeCalculations.Generate("50,30,-0.01", ColorGradeFormula.Ciede2000, 2);
        var plane = ColorGradeCalculations.GenerateComposite(card);
        var positive = Cell(plane, -1, 1).Lab!.Value;
        Assert.True(positive.Hue < card.Standard.Hue);
        Assert.Equal(card.Scales[2].Chips.Single(chip => chip.Level == 1).Lab!.Value.Hue, positive.Hue, 10);
        Assert.True(Cell(plane, -1, -1).Lab!.Value.Hue < card.Standard.Hue);
    }

    [Theory]
    [InlineData("50,0,0")]
    [InlineData("0,0,0")]
    [InlineData("100,0,0")]
    public void NeutralStandardHasOnlyACenterAndDoesNotInventHueOrChroma(string lab)
    {
        var card = ColorGradeCalculations.Generate(lab, ColorGradeFormula.Cie76, 2);
        var plane = ColorGradeCalculations.GenerateComposite(card);
        Assert.Equal(25, plane.Chips.Count);
        Assert.Single(plane.Chips, chip => chip.Lab.HasValue);
        Assert.Equal(card.Standard, Cell(plane, 0, 0).Lab);
        Assert.All(plane.Chips.Where(chip => !chip.Lab.HasValue), chip =>
        {
            Assert.Null(chip.Hex); Assert.Null(chip.StandardDeltaE);
            Assert.Null(chip.Comparison);
            Assert.Contains("中性色", chip.UnavailableReason);
        });
    }

    [Fact]
    public void UnavailableAxisGradesStayUnavailableAcrossThePlane()
    {
        var card = ColorGradeCalculations.Generate("0.5,1,0", ColorGradeFormula.Cie76, 2);
        var plane = ColorGradeCalculations.GenerateComposite(card);
        Assert.All(plane.Chips.Where(chip => chip.ChromaLevel < 0 || Math.Abs(chip.HueLevel) == 2), chip =>
        {
            Assert.Null(chip.Lab); Assert.NotNull(chip.UnavailableReason); Assert.Null(chip.Comparison);
        });
        var blockedLayer = ColorGradeCalculations.GenerateComposite(card, -1);
        Assert.Null(blockedLayer.Center);
        Assert.All(blockedLayer.Chips, chip =>
        {
            Assert.Null(chip.Lab); Assert.Null(chip.Hex); Assert.Null(chip.StandardDeltaE); Assert.Null(chip.Comparison);
            Assert.Contains("明度", chip.UnavailableReason);
        });
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(5)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void LayerOutsideTheGeneratedRangeIsRejected(int level)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cie76, 4);
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.GenerateComposite(card, level));
    }

    [Fact]
    public void CompositeLabelsAndUnavailableReasonsTranslateToEnglish()
    {
        var card = ColorGradeCalculations.Generate("0.5,1,0", ColorGradeFormula.Cie76, 2);
        foreach (var level in new[] { 0, -1 })
        foreach (var chip in ColorGradeCalculations.GenerateComposite(card, level).Chips.Where(chip => chip.Lab is null))
            Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(chip.UnavailableReason!, UiLanguage.English));
        foreach (var text in new[]
        {
            "一维色差分级色卡", "复合色差色卡", "当前明度层：L +1 级 · L* 52.3000",
            "所选色块：(1,2,-3)", "标样：(5,5,5)", "坐标系", "参考原点", "555分色法",
            "L −1 级", "L +1 级", "回到 L 0 级", "本层中心",
            "以一个标样为中心，沿明度、彩度、色相分别向两侧各展开 4 级。每向外一级，按所选公式与前一级保持所选等级阈值，默认 ΔE = 1.5。",
            "横轴为彩度 C* 等级（向右增加），纵轴为色相 h° 等级（向上增加）。坐标按 (L,C,h) 等级显示；参考原点的标样为 (0,0,0)，555分色法将三个坐标各加 5，标样为 (5,5,5)。"
        }) Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(text, UiLanguage.English));
    }

    [Theory]
    [InlineData(ColorGradeCoordinateSystem.ReferenceOrigin, 0, 0, 0, "(0,0,0)")]
    [InlineData(ColorGradeCoordinateSystem.ReferenceOrigin, 1, 2, -3, "(1,2,-3)")]
    [InlineData(ColorGradeCoordinateSystem.FiveFiveFive, 0, 0, 0, "(5,5,5)")]
    [InlineData(ColorGradeCoordinateSystem.FiveFiveFive, -4, 0, 4, "(1,5,9)")]
    [InlineData(ColorGradeCoordinateSystem.FiveFiveFive, 1, 2, -3, "(6,7,2)")]
    public void CoordinateSystemsDisplayLightnessChromaHueInThatOrder(ColorGradeCoordinateSystem system,
        int l, int c, int h, string expected)
    {
        Assert.Equal(expected, ColorGradeCoordinates.FromGrades(l, c, h, system).ToString());
    }

    [Fact]
    public void FiveFiveFiveCoordinatesSpanOneThroughNineAndKeepTheSourceGrades()
    {
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc, 4);
        for (var level = -4; level <= 4; level++)
        {
            var plane = ColorGradeCalculations.GenerateComposite(card, level);
            foreach (var chip in plane.Chips)
            {
                var shifted = ColorGradeCoordinates.FromGrades(plane.LightnessLevel, chip.ChromaLevel, chip.HueLevel,
                    ColorGradeCoordinateSystem.FiveFiveFive);
                Assert.InRange(shifted.Lightness, 1, 9);
                Assert.InRange(shifted.Chroma, 1, 9);
                Assert.InRange(shifted.Hue, 1, 9);
                Assert.Equal((level, chip.ChromaLevel, chip.HueLevel),
                    (shifted.Lightness - 5, shifted.Chroma - 5, shifted.Hue - 5));
            }
        }
    }

    [Theory]
    [InlineData(ColorGradeFormula.Cie76)]
    [InlineData(ColorGradeFormula.Cmc)]
    [InlineData(ColorGradeFormula.Ciede2000)]
    public void CompositeEvaluationComparesTheActualSelectedColorWithTheOriginalStandard(ColorGradeFormula formula)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", formula, cmcL: 1.5, cmcC: 0.8);
        var unchanged = Cell(ColorGradeCalculations.GenerateComposite(card), 0, 0);
        Assert.Equal(new[] { "无变化", "无变化", "无变化", "均为有彩色" },
            ColorEvaluationPresentation.Comments(unchanged.Comparison!));
        foreach (var level in new[] { -1, 1 })
        {
            var chip = Cell(ColorGradeCalculations.GenerateComposite(card, level), level, level);
            var comparison = chip.Comparison!;
            Assert.Equal(card.Standard.ToLab().CIEL, comparison.Reference.L);
            Assert.Equal(chip.Lab!.Value.L, comparison.Sample.L);
            Assert.Equal(chip.StandardDeltaE, comparison.Evaluation.Overall.DeltaE);
            Assert.Equal((ComparisonFormula)formula, comparison.Evaluation.Overall.Formula);
            if (formula == ColorGradeFormula.Cmc)
            {
                Assert.Equal(1.5, comparison.AppliedParameters.Cmc.L);
                Assert.Equal(0.8, comparison.AppliedParameters.Cmc.C);
            }
            Assert.Equal(level > 0 ? new[] { "更亮", "更艳", "偏黄", "均为有彩色" }
                : new[] { "更暗", "更灰", "偏红", "均为有彩色" }, ColorEvaluationPresentation.Comments(comparison));
        }
    }

    [Theory]
    [InlineData(51, 0, 1)] // Only the original reference is achromatic.
    [InlineData(48, 0, -1)] // Only the selected color is achromatic.
    [InlineData(0, 50, 1)]
    public void CompositeEvaluationHonorsTheUiAchromaticThresholdsWithoutChangingColors(double l, double c, int layer)
    {
        var options = new ColorComparisonOptions { AchromaticLightnessThreshold = l, AchromaticChromaThreshold = c };
        var card = ColorGradeCalculations.Generate("50,30,20", ColorGradeFormula.Cmc, comparisonOptions: options);
        var normal = Cell(ColorGradeCalculations.GenerateComposite(card, layer), 1, 1);
        var neutral = Cell(ColorGradeCalculations.GenerateComposite(card, layer, options), 1, 1);
        Assert.Equal(normal.Lab, neutral.Lab);
        Assert.Equal(normal.Hex, neutral.Hex);
        Assert.Equal(normal.StandardDeltaE, neutral.StandardDeltaE);
        Assert.Equal("偏黄", ColorEvaluationPresentation.Comments(normal.Comparison!)[2]);
        Assert.Equal("不适用", ColorEvaluationPresentation.Comments(neutral.Comparison!)[2]);
        Assert.Equal(l, neutral.Comparison!.AppliedParameters.AchromaticLightnessThreshold);
        Assert.Equal(c, neutral.Comparison.AppliedParameters.AchromaticChromaThreshold);
        Assert.Equal(ComparisonFormula.Ciede2000, options.Evaluation.Formula);
    }

    [Fact]
    public void CompositeEvaluationRowTranslatesAllThreeComments()
    {
        const string text = "明度 L*：更亮 · 彩度 C*：更艳 · 色相 h°：偏黄";
        Assert.Equal("Lightness L*: Lighter · Chroma C*: Higher chroma · Hue h°: More yellowish",
            UiLanguage.Translate(text, UiLanguage.English));
        const string neutral = "明度 L*：更暗 · 彩度 C*：无变化 · 色相 h°：不适用";
        Assert.Equal("Lightness L*: Darker · Chroma C*: No change · Hue h°: Not applicable",
            UiLanguage.Translate(neutral, UiLanguage.English));
    }

    private static ColorGradeCompositeChip Cell(ColorGradeCompositeResult plane, int c, int h) =>
        plane.Chips.Single(chip => chip.ChromaLevel == c && chip.HueLevel == h);
}
