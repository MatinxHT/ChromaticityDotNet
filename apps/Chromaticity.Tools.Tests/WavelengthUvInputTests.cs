using System.Globalization;
using Chromaticity.Tools.Services;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class WavelengthUvInputTests
{
    private static CIEuv Uv(double x, double y)
    {
        double denominator = -2 * x + 12 * y + 3;
        return new() { CIEu = 4 * x / denominator, CIEv = 9 * y / denominator };
    }

    [Theory]
    [InlineData(0.5253, 0.3485, StandardObserver.Degree2)]
    [InlineData(0.5253, 0.3485, StandardObserver.Degree10)]
    [InlineData(0.3, 0.6, StandardObserver.Degree2)]
    [InlineData(0.3, 0.6, StandardObserver.Degree10)]
    [InlineData(0.4, 0.2, StandardObserver.Degree2)]
    [InlineData(0.4, 0.2, StandardObserver.Degree10)]
    public void UvInputMatchesXyWavelengthsAndBothDiagrams(double x, double y, StandardObserver observer)
    {
        var xy = WavelengthCalculations.Calculate(new CIExyY { CIEx = x, CIEy = y, CIEY = 100 }, observer, "D65");
        var input = Uv(x, y);
        var uv = WavelengthCalculations.CalculateUv(input, observer, "D65");
        Assert.Equal(x, uv.Sample.X, 12);
        Assert.Equal(y, uv.Sample.Y, 12);
        Assert.Equal(100, uv.Sample.Luminance);
        Assert.Equal(xy.Table.Rows[0][7..9], uv.Table.Rows[0][7..9]);
        Assert.Equal(xy.White, uv.White);
        foreach (var space in new[] { ChromaticityDiagramSpace.Xy, ChromaticityDiagramSpace.UvPrime })
        {
            var projection = new ChromaticityDiagramProjection(space);
            var expected = projection.Intersections(xy)!.Value;
            var actual = projection.Intersections(uv)!.Value;
            Assert.Equal(expected.Forward.X, actual.Forward.X, 10);
            Assert.Equal(expected.Forward.Y, actual.Forward.Y, 10);
            Assert.Equal(expected.Reverse.X, actual.Reverse.X, 10);
            Assert.Equal(expected.Reverse.Y, actual.Reverse.Y, 10);
        }
        Assert.Equal(input.CIEu.ToString("G", CultureInfo.InvariantCulture), uv.Table.Rows[0][0]);
        Assert.Equal(input.CIEv.ToString("G", CultureInfo.InvariantCulture), uv.Table.Rows[0][1]);
        var english = UiLanguage.TranslateTable(uv.Table, UiLanguage.English);
        Assert.Contains("Sample u′", english.ToCsv());
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", english.ToCsv() + english.Conditions);
        input.CIEu = 0;
        Assert.Equal(x, uv.Sample.X, 12);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void CustomWhiteUvIsAcceptedAndRetainedWithoutRounding(StandardObserver observer)
    {
        var sample = Uv(0.31272, 0.32904);
        var white = Uv(0.31274, 0.32901);
        var result = WavelengthCalculations.CalculateUv(sample, observer, "D65", white);
        Assert.False(result.Wavelengths.IsAchromatic);
        Assert.Equal(0.31274, result.White.X, 12);
        Assert.Equal(0.32901, result.White.Y, 12);
        Assert.Equal(white.CIEu.ToString("G", CultureInfo.InvariantCulture), result.Table.Rows[0][4]);
        Assert.Equal(white.CIEv.ToString("G", CultureInfo.InvariantCulture), result.Table.Rows[0][5]);
        Assert.Contains(result.Table.Rows[0][4], result.Table.ToTsv());
        var neutral = WavelengthCalculations.CalculateUv(white, observer, "D65", white);
        Assert.True(neutral.Wavelengths.IsAchromatic);
        Assert.Equal("—", neutral.Table.Rows[0][7]);
        Assert.Equal("—", neutral.Table.Rows[0][8]);
    }

    [Theory]
    [InlineData(double.NaN, 0.4)]
    [InlineData(0.2, double.PositiveInfinity)]
    [InlineData(-0.1, 0.4)]
    [InlineData(0.2, 0)]
    [InlineData(0, 0.75)]
    [InlineData(0.2, 1)]
    [InlineData(0.05, 0.1)]
    public void InvalidUvInputsAreRejectedWithTranslatedErrors(double u, double v)
    {
        var invalid = new CIEuv { CIEu = u, CIEv = v };
        var sampleError = Assert.Throws<ArgumentException>(() =>
            WavelengthCalculations.CalculateUv(invalid, StandardObserver.Degree2, "D65"));
        var whiteError = Assert.Throws<ArgumentException>(() =>
            WavelengthCalculations.CalculateUv(Uv(0.3, 0.6), StandardObserver.Degree2, "D65", invalid));
        Assert.Contains("样品", sampleError.Message);
        Assert.Contains("白点", whiteError.Message);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(sampleError.Message, UiLanguage.English));
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(whiteError.Message, UiLanguage.English));
    }
}
