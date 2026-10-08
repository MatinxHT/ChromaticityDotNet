using Chromaticity.Tools.Services;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class ColorGradeRoundingTests
{
    [Theory]
    [InlineData(1.2, ColorGradeRounding.Nearest, 1)]
    [InlineData(-1.2, ColorGradeRounding.Nearest, -1)]
    [InlineData(1.2, ColorGradeRounding.Ceiling, 2)]
    [InlineData(-1.2, ColorGradeRounding.Ceiling, -2)]
    [InlineData(1.5, ColorGradeRounding.Nearest, 2)]
    [InlineData(-1.5, ColorGradeRounding.Nearest, -2)]
    [InlineData(2.5, ColorGradeRounding.Nearest, 3)]
    [InlineData(-2.5, ColorGradeRounding.Nearest, -3)]
    [InlineData(0.2, ColorGradeRounding.Nearest, 0)]
    [InlineData(-0.2, ColorGradeRounding.Nearest, 0)]
    [InlineData(0.2, ColorGradeRounding.Ceiling, 1)]
    [InlineData(-0.2, ColorGradeRounding.Ceiling, -1)]
    [InlineData(0, ColorGradeRounding.Ceiling, 0)]
    [InlineData(2, ColorGradeRounding.Ceiling, 2)]
    [InlineData(-2, ColorGradeRounding.Ceiling, -2)]
    [InlineData(2.00000000001, ColorGradeRounding.Ceiling, 2)]
    [InlineData(2.0001, ColorGradeRounding.Ceiling, 3)]
    public void IntegerGradesRoundMagnitudeThenRestoreDirection(double raw, ColorGradeRounding mode, int expected) =>
        Assert.Equal(expected, ColorGradeCalculations.RoundGrade(raw, mode));

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AllThreeFractionalDirectionsBecomeIntegersWithoutChangingMeasuredColor(int direction)
    {
        var chroma = Math.Sqrt(1300) + direction * 1.8;
        var hue = Math.Atan2(20, 30) + direction * (2 * Math.Asin(1.5 / (2 * chroma)) + 2 * Math.Asin(0.3 / (2 * chroma)));
        var sample = FormattableString.Invariant($"{50 + direction * 1.8:G17},{chroma * Math.Cos(hue):G17},{chroma * Math.Sin(hue):G17}");
        var settings = new ColorGradeSettings(ColorGradeFormula.Cie76);
        var nearest = ColorGradeCalculations.Analyze("50,30,20", sample, settings, ColorGradeRounding.Nearest);
        var ceiling = ColorGradeCalculations.Analyze("50,30,20", sample, settings, ColorGradeRounding.Ceiling);
        Assert.All(nearest.Components, component => Assert.Equal(direction, component.Grade));
        Assert.All(ceiling.Components, component => Assert.Equal(2 * direction, component.Grade));
        foreach (var (a, b) in nearest.Components.Zip(ceiling.Components))
        {
            Assert.InRange(Math.Abs(a.RawGrade!.Value - direction * 1.2), 0, 0.0002);
            Assert.Equal(a.RawGrade, b.RawGrade);
            Assert.Equal(a.DeltaE, b.DeltaE); Assert.Equal(a.End, b.End);
        }
        Assert.Equal(nearest.Sample, ceiling.Sample);
        Assert.Equal(nearest.SampleHex, ceiling.SampleHex);
        Assert.Equal(nearest.DeltaE, ceiling.DeltaE);
        Assert.Equal(ColorGradeRounding.Nearest, nearest.Rounding);
        Assert.Equal(ColorGradeRounding.Ceiling, ceiling.Rounding);
    }

    [Fact]
    public void BothModesRetainUndefinedNeutralGrades()
    {
        foreach (var mode in Enum.GetValues<ColorGradeRounding>())
        {
            var result = ColorGradeCalculations.Analyze("50,0,0", "53,10,20", new(ColorGradeFormula.Cmc), mode);
            Assert.Null(result.Components[1].Grade); Assert.Null(result.Components[2].Grade);
        }
    }

    [Theory]
    [InlineData(ColorGradeFormula.Cie76)]
    [InlineData(ColorGradeFormula.Cmc)]
    [InlineData(ColorGradeFormula.Ciede2000)]
    public void CeilingRecoversExactComposedGradesForAllFormulas(ColorGradeFormula formula)
    {
        var card = ColorGradeCalculations.Generate("50,30,20", formula);
        var target = ColorGradeCalculations.Compose(card, -2, 1, 3);
        var sample = FormattableString.Invariant($"{target.Sample.L:G17},{target.Sample.A:G17},{target.Sample.B:G17}");
        var result = ColorGradeCalculations.Analyze("50,30,20", sample, card.Settings, ColorGradeRounding.Ceiling);
        Assert.Equal(new int?[] { -2, 1, 3 }, result.Components.Select(component => component.Grade));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1002)]
    public void InvalidRawGradeIsRejected(double value) => Assert.Throws<ArgumentException>(() =>
        ColorGradeCalculations.RoundGrade(value, ColorGradeRounding.Nearest));

    [Fact]
    public void UnknownRoundingModeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.RoundGrade(1.2, (ColorGradeRounding)99));
        Assert.Throws<ArgumentException>(() => ColorGradeCalculations.Analyze("50,30,20", "53,30,20",
            new(ColorGradeFormula.Cmc), (ColorGradeRounding)99));
    }
}
