using ChromaticityDotNet.Controller;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Tests;

public class RgbDerivedConversionTests
{
    [Theory]
    [InlineData(255, 0, 0, 0, 1, 0.5, 1, 1)]
    [InlineData(255, 255, 0, 60, 1, 0.5, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 0.5, 1, 1)]
    [InlineData(0, 255, 255, 180, 1, 0.5, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 0.5, 1, 1)]
    [InlineData(255, 0, 255, 300, 1, 0.5, 1, 1)]
    [InlineData(102, 51, 153, 270, 0.5, 0.4, 2.0 / 3, 0.6)]
    [InlineData(32, 160, 144, 172.5, 2.0 / 3, 96.0 / 255, 0.8, 160.0 / 255)]
    public void KnownSrgbColorsHaveNormalizedHslAndHsv(byte r, byte g, byte b,
        double h, double sl, double l, double sv, double v)
    {
        var rgb = new CIERGB { redValue = r, greenValue = g, blueValue = b };
        var hsl = ChromaticityConversion.RGBToHSL(rgb);
        var hsv = ChromaticityConversion.RGBToHSV(rgb);
        Assert.Equal(h, hsl.H, 10); Assert.Equal(sl, hsl.S, 10); Assert.Equal(l, hsl.L, 10);
        Assert.Equal(h, hsv.H, 10); Assert.Equal(sv, hsv.S, 10); Assert.Equal(v, hsv.V, 10);
    }

    [Fact]
    public void EntireGrayRampIncludingBlackAndWhiteHasFiniteCoordinates()
    {
        for (var i = 0; i <= 255; i++)
        {
            var rgb = new CIERGB { redValue = (byte)i, greenValue = (byte)i, blueValue = (byte)i };
            var hsl = ChromaticityConversion.RGBToHSL(rgb);
            var hsv = ChromaticityConversion.RGBToHSV(rgb);
            Assert.Equal(0, hsl.H); Assert.Equal(0, hsl.S); Assert.Equal(i / 255.0, hsl.L);
            Assert.Equal(0, hsv.H); Assert.Equal(0, hsv.S); Assert.Equal(i / 255.0, hsv.V);
        }
    }

    [Fact]
    public void SaturationRemainsInRangeForBrightAndDarkColorsDespiteFloatingPointRoundoff()
    {
        for (var i = 0; i <= 255; i++)
        {
            foreach (var rgb in new[]
            {
                new CIERGB { redValue = 255, greenValue = (byte)i },
                new CIERGB { redValue = 255, greenValue = (byte)i, blueValue = 255 },
                new CIERGB { redValue = 0, greenValue = (byte)i, blueValue = (byte)i }
            })
            {
                Assert.InRange(ChromaticityConversion.RGBToHSL(rgb).S, 0, 1);
                Assert.InRange(ChromaticityConversion.RGBToHSV(rgb).S, 0, 1);
            }
        }
    }

    [Fact]
    public void HueJustBeforeRedIsNormalizedBelow360Degrees()
    {
        var rgb = new CIERGB { redValue = 255, blueValue = 1 };
        Assert.Equal(360 - 60.0 / 255, ChromaticityConversion.RGBToHSL(rgb).H, 10);
        Assert.Equal(360 - 60.0 / 255, ChromaticityConversion.RGBToHSV(rgb).H, 10);
    }

    [Theory]
    [InlineData(0, 0, 0, "#000000")]
    [InlineData(255, 255, 255, "#FFFFFF")]
    [InlineData(1, 2, 10, "#01020A")]
    [InlineData(32, 160, 144, "#20A090")]
    public void HexIsUppercaseAndRetainsLeadingZeros(byte r, byte g, byte b, string expected) =>
        Assert.Equal(expected, ChromaticityConversion.RGBToHex(new CIERGB { redValue = r, greenValue = g, blueValue = b }));

    [Fact]
    public void NullRgbIsRejectedByAllThreeConversions()
    {
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.RGBToHSL(null!));
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.RGBToHSV(null!));
        Assert.Throws<ArgumentNullException>(() => ChromaticityConversion.RGBToHex(null!));
    }
}
