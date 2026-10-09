using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class WavelengthCalculationsTests
{
    private static CIExyY Sample(double x, double y, double luminance = 100) => new() { CIEx = x, CIEy = y, CIEY = luminance };
    private static CIExyY White => Sample(0.3127, 0.3290);

    [Theory]
    [InlineData(0.3, 0.6, "549.13", "—")]
    [InlineData(0.4, 0.2, "—", "510.96")]
    [InlineData(0.3127, 0.329, "—", "—")]
    public void CustomWhiteFixturesArePresentedAndExportedToTwoDecimals(double x, double y, string dominant, string complementary)
    {
        var sample = Sample(x, y);
        var white = White;
        var calculated = WavelengthCalculations.Calculate(sample, StandardObserver.Degree2, Standardilluminant.D65, white);
        Assert.Equal(dominant, calculated.Table.Rows[0][7]);
        Assert.Equal(complementary, calculated.Table.Rows[0][8]);
        Assert.Contains($"\"{dominant}\",\"{complementary}\"", calculated.Table.ToCsv());
        Assert.Contains(dominant + "\t" + complementary, calculated.Table.ToTsv());
        var english = UiLanguage.TranslateTable(calculated.Table, UiLanguage.English);
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", english.ToCsv() + english.Conditions);
        sample.CIEx = 0; white.CIEy = 0;
        Assert.Equal(x, calculated.Sample.X);
        Assert.Equal(0.329, calculated.White.Y);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, Standardilluminant.D65)]
    [InlineData(StandardObserver.Degree10, Standardilluminant.D65)]
    [InlineData(StandardObserver.Degree10, Standardilluminant.A)]
    public void StandardWhiteCalculationCallsTheNewApiAndPreservesReferenceCoordinates(StandardObserver observer, Standardilluminant light)
    {
        var input = Sample(0.4, 0.3);
        var expected = ChromaticityConversion.xyYToWavelengths(input, observer, ToolCalculations.IlluminantId(light));
        var result = WavelengthCalculations.Calculate(input, observer, light);
        Assert.Equal(expected.DominantWavelength, result.Wavelengths.DominantWavelength);
        Assert.Equal(expected.ComplementaryWavelength, result.Wavelengths.ComplementaryWavelength);
        Assert.Equal(WavelengthCalculations.StandardWhite(light, observer), result.White);
        Assert.Contains(result.White.X.ToString("G", System.Globalization.CultureInfo.InvariantCulture), result.Table.ToCsv());
    }

    [Fact]
    public void InvalidGeometryIsRejectedBeforePublishingAResult()
    {
        Assert.Throws<ArgumentException>(() => WavelengthCalculations.Calculate(Sample(0.1, 0.1), StandardObserver.Degree2, Standardilluminant.D65));
        Assert.Throws<ArgumentException>(() => WavelengthCalculations.Calculate(Sample(0.3, 0.6), StandardObserver.Degree2, Standardilluminant.D65, Sample(0.1, 0.1)));
        Assert.Throws<ArgumentException>(() => WavelengthCalculations.Calculate(Sample(0.3, 0.6, -1), StandardObserver.Degree2, Standardilluminant.D65));
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void DiagramBackgroundIsColoredInsideThePhysicalGamutAndTransparentOutside(StandardObserver observer)
    {
        var pixels = ChromaticityDiagramBackground.Create(observer);
        int At(double x, double y)
        {
            int column = (int)(x / ChromaticityDiagramBackground.MaxX * ChromaticityDiagramBackground.Width);
            int row = (int)((1 - y / ChromaticityDiagramBackground.MaxY) * ChromaticityDiagramBackground.Height);
            return (row * ChromaticityDiagramBackground.Width + column) * 4;
        }
        int green = At(0.3, 0.6);
        Assert.Equal(255, pixels[green + 3]);
        Assert.True(pixels[green + 1] > pixels[green]);
        Assert.Equal(0, pixels[At(0.1, 0.1) + 3]);
        Assert.Equal(0, pixels[At(0.7, 0.8) + 3]);
        Assert.Equal(255, pixels[At(0.3127, 0.329) + 3]);
        Assert.Equal(255, pixels[At(0.713, 0.285) + 3]); // Red edge, including the 10-degree retracing case.
    }
}
