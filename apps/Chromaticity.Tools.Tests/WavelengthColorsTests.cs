using Chromaticity.Tools.Services;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class WavelengthColorsTests
{
    [Fact]
    public void VisibleColorsFollowWavelengthOrderAndDoNotColorUvOrInfrared()
    {
        Assert.Equal(0, WavelengthColors.At(379).A);
        Assert.Equal(0, WavelengthColors.At(781).A);
        var blue = WavelengthColors.At(450);
        var green = WavelengthColors.At(550);
        var red = WavelengthColors.At(650);
        Assert.True(blue.B > blue.R && blue.B > blue.G);
        Assert.True(green.G > green.R && green.G > green.B);
        Assert.True(red.R > red.G && red.R > red.B);
        foreach (var wavelength in Enumerable.Range(380, 401))
        {
            var color = WavelengthColors.At(wavelength);
            Assert.Equal(255, color.A);
            Assert.Equal(255, Math.Max(color.R, Math.Max(color.G, color.B)));
        }
    }
}
