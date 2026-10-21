using System.Globalization;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class ColorInconstancyTests
{
    [Theory]
    [InlineData("FL2", StandardObserver.Degree2, 380, 780, 10)]
    [InlineData("FL11", StandardObserver.Degree10, 400, 700, 5)]
    [InlineData("LED-B1", StandardObserver.Degree2, 390, 770, 20)]
    [InlineData("D50", StandardObserver.Degree10, 380, 780, 10)]
    [InlineData("D65", StandardObserver.Degree10, 380, 780, 10)]
    [InlineData("FL12", StandardObserver.Degree2, 380, 780, 10)]
    [InlineData("A", StandardObserver.Degree10, 380, 780, 10)]
    public void FiveLightsUseOriginalLabObserverAndGridForEveryFormula(string original, StandardObserver observer, int start, int end, int step)
    {
        var input = Ramp(start, end, step);
        var reference = ToolCalculations.Reflectance(input, start, end, step, false, original, observer);
        foreach (var formula in Enum.GetValues<ComparisonFormula>())
        {
            var settings = new ColorInconstancySettings(formula, CmcL: 1.5, CmcC: 0.8, KL: 1.5, KC: 0.8, KH: 2);
            var analysis = ColorInconstancyCalculations.Analyze(reference, settings);
            Assert.Equal(new[] { "D65", "CWF", "A", "U3000", "TL84" }, analysis.Rows.Select(row => row[0]));
            Assert.Equal(new[] { "照明体", "照明体参考" }, analysis.Headers[..2]);
            Assert.Equal(new[] { "全球通用平均日光", "北美商业冷白荧光灯", "全球通用钨丝白炽灯", "美国暖白商业荧光灯", "欧洲商业荧光灯" },
                analysis.Rows.Select(row => row[1]));
            Assert.Equal(new[] { "D65", "FL2", "A", "FL12", "FL11" }, analysis.Rows.Select(row => row[9]));
            Assert.Equal(ColorEvaluationPresentation.Headers[..3], analysis.Headers[6..9]);
            Assert.DoesNotContain("状态", analysis.Headers);
            Assert.DoesNotContain("有效", analysis.ToCsv());
            foreach (var row in analysis.Rows)
            {
                var independent = ToolCalculations.Reflectance(input, start, end, step, false, row[9], observer);
                Assert.Equal(independent.Table.Rows[0][4..7], row[2..5]);
                Assert.Equal(original, row[10]);
                Assert.Equal(reference.Table.Rows[0][4..7], row[11..14]);
                Assert.Equal(new[] { start.ToString(), end.ToString(), step.ToString() }, row[15..18]);
                var sample = independent.Lab;
                var expected = formula switch
                {
                    ComparisonFormula.Cie76 => ChromaticityDeltaEFormulations.DeltaE1976(reference.Lab, sample),
                    ComparisonFormula.Cmc => ChromaticityDeltaEFormulations.DeltaEcmc(reference.Lab, sample, 1.5, 0.8),
                    _ => ChromaticityDeltaEFormulations.DeltaE2000(reference.Lab, sample, 1.5, 0.8, 2).DeltaE
                };
                Assert.Equal(ToolCalculations.F(expected), row[5]);
                Assert.Equal(ColorEvaluationPresentation.Comments(ChromaticityMatch.CompareColors(reference.Lab, sample))[..3], row[6..9]);
                var existingPreview = ToolCalculations.DifferenceAuto(string.Join(',', row[11..14]), string.Join(',', row[2..5]), 1, 1, 1, 2, 1);
                Assert.Equal(existingPreview.Rows[0][1..3], row[^3..^1]);
                if (row[9] == original) Assert.Equal("0.0000", row[5]);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    [InlineData(100)]
    public void NeutralSpectraStayNeutralUnderAllLights(double value)
    {
        var reference = ToolCalculations.Reflectance(string.Join('\n', Enumerable.Repeat(value.ToString(CultureInfo.InvariantCulture), 41)),
            380, 780, 10, false, "D65", StandardObserver.Degree10);
        var analysis = ColorInconstancyCalculations.Analyze(reference, new());
        Assert.All(analysis.Rows, row =>
        {
            Assert.Equal("不适用", row[8]);
            Assert.InRange(double.Parse(row[5], CultureInfo.InvariantCulture), 0, 0.002);
        });
    }

    [Fact]
    public void FullObserverRangePreservesD65AndAWithoutClippingFluorescentRanges()
    {
        var reference = ToolCalculations.Reflectance(Ramp(360, 830, 10), 360, 830, 10, false, "D65", StandardObserver.Degree2);
        var analysis = ColorInconstancyCalculations.Analyze(reference, new());
        Assert.Equal("0.0000", analysis.Rows[0][5]);
        Assert.NotEqual("—", analysis.Rows[2][5]);
        foreach (var row in analysis.Rows.Where(row => row[9].StartsWith("FL")))
        {
            Assert.All(row[2..9], value => Assert.Equal("—", value));
            Assert.Contains("380–780 nm", row[^1]);
            Assert.Contains($"光源 {row[0]}：光源仅覆盖 380–780 nm", analysis.Conditions);
            Assert.Equal("—", row[^2]); // Unavailable samples must not get a fabricated swatch.
            Assert.Equal("360", row[15]); Assert.Equal("830", row[16]);
        }
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.TranslateTable(analysis, UiLanguage.English).ToCsv());
    }

    [Fact]
    public void FractionAndPercentInputsProduceIdenticalAnalysis()
    {
        var percent = ToolCalculations.Reflectance(Ramp(380, 780, 10), 380, 780, 10, false, "A", StandardObserver.Degree10);
        var fraction = ToolCalculations.Reflectance(string.Join('\n', percent.Values.Select(value => (value / 100).ToString("R", CultureInfo.InvariantCulture))),
            380, 780, 10, true, "A", StandardObserver.Degree10);
        Assert.Equal(ColorInconstancyCalculations.Analyze(percent, new()).ToCsv(), ColorInconstancyCalculations.Analyze(fraction, new()).ToCsv());
    }

    [Theory]
    [InlineData(ComparisonFormula.Cmc)]
    [InlineData(ComparisonFormula.Ciede2000)]
    public void LargerCoefficientsReduceColorDifferenceWithoutChangingLab(ComparisonFormula formula)
    {
        var reference = ToolCalculations.Reflectance(Ramp(380, 780, 10), 380, 780, 10, false, "FL2", StandardObserver.Degree10);
        var normal = ColorInconstancyCalculations.Analyze(reference, new(formula));
        var weighted = ColorInconstancyCalculations.Analyze(reference, new(formula, CmcL: 4, CmcC: 2, KL: 2, KC: 2, KH: 2));
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(normal.Rows[i][2..5], weighted.Rows[i][2..5]);
            Assert.Equal(normal.Rows[i][6..9], weighted.Rows[i][6..9]);
            Assert.Equal(normal.Rows[i][^3..^1], weighted.Rows[i][^3..^1]);
            Assert.True(Number(weighted.Rows[i][5]) <= Number(normal.Rows[i][5]));
            if (formula == ComparisonFormula.Ciede2000)
                Assert.InRange(Math.Abs(Number(normal.Rows[i][5]) / 2 - Number(weighted.Rows[i][5])), 0, 0.0001);
        }
        Assert.NotEqual(normal.Rows[0][5], weighted.Rows[0][5]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ActiveWeightsMustBeFiniteAndPositive(double value)
    {
        var reference = ToolCalculations.Reflectance(Ramp(380, 780, 10), 380, 780, 10, false, "D65", StandardObserver.Degree10);
        foreach (var settings in new[]
        {
            new ColorInconstancySettings(ComparisonFormula.Cmc, CmcL: value),
            new ColorInconstancySettings(ComparisonFormula.Cmc, CmcC: value),
            new ColorInconstancySettings(KL: value), new ColorInconstancySettings(KC: value), new ColorInconstancySettings(KH: value)
        }) Assert.Throws<ArgumentException>(() => ColorInconstancyCalculations.Analyze(reference, settings));
        Assert.Equal(5, ColorInconstancyCalculations.Analyze(reference, new(ComparisonFormula.Cie76,
            CmcL: value, CmcC: value, KL: value, KC: value, KH: value)).Rows.Length);
        Assert.Throws<ArgumentException>(() => ColorInconstancyCalculations.Analyze(reference, new((ComparisonFormula)99)));
    }

    [Fact]
    public void ExportsKeepReferenceConditionsAndTranslateWithoutChangingNumbers()
    {
        var reference = ToolCalculations.Reflectance(Ramp(400, 700, 5), 400, 700, 5, false, "LED-B1", StandardObserver.Degree2);
        var analysis = ColorInconstancyCalculations.Analyze(reference, new(ComparisonFormula.Cmc, CmcL: 1.5, CmcC: 0.8));
        var translated = UiLanguage.TranslateTable(analysis, UiLanguage.English);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated.ToCsv());
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translated.Conditions);
        Assert.Contains("1.5:0.8", translated.Headers[5]);
        Assert.DoesNotContain("Status", translated.Headers);
        Assert.DoesNotContain("有效", analysis.ToCsv());
        Assert.Equal(6, translated.ToTsv().Split('\n').Length);
        Assert.All(translated.ToTsv().Split('\n'), line => Assert.Equal(analysis.Headers.Length, line.Split('\t').Length));
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(analysis.Rows[i][2..6], translated.Rows[i][2..6]);
            Assert.Equal(analysis.Rows[i][6..9].Select(value => UiLanguage.Translate(value, UiLanguage.English)), translated.Rows[i][6..9]);
            Assert.Equal(analysis.Rows[i][^3..^1], translated.Rows[i][^3..^1]);
            Assert.Matches("^#[0-9A-F]{6}$", translated.Rows[i][^3]);
            Assert.Matches("^#[0-9A-F]{6}$", translated.Rows[i][^2]);
            Assert.Contains(ColorInconstancyCalculations.IlluminantReferenceNotice, analysis.Rows[i][^1]);
            Assert.Contains("LED-B1", translated.Rows[i][^1]);
            Assert.Contains("400–700 nm / 5 nm", translated.Rows[i][^1]);
            Assert.Contains("CIE 1931", translated.Rows[i][^1]);
        }
        Assert.Equal("Spectral color calculation and color inconstancy analysis",
            UiLanguage.Translate("光谱颜色值计算及不恒常性分析", UiLanguage.English));
    }

    internal static string Ramp(int start, int end, int step) => string.Join('\n', Enumerable.Range(0, (end - start) / step + 1)
        .Select(i => $"{start + i * step},{(10 + 0.18 * i * step).ToString("R", CultureInfo.InvariantCulture)}"));

    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}
