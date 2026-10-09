using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class DataModelTests
{
    [Fact]
    public void LabConstructorCalculatesChromaAndHueToFourPlaces()
    {
        CIELAB color = new(50.0, 1.0, 1.0);

        Assert.Equal(50.0, color.CIEL);
        Assert.Equal(1.4142, color.CIEC);
        Assert.Equal(45.0, color.CIEH);
        PrecisionAssert.HasAtMostFourDecimalPlaces(color.CIEC);
        PrecisionAssert.HasAtMostFourDecimalPlaces(color.CIEH);
    }

    [Theory]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(0.0, 1.0, 90.0)]
    [InlineData(-1.0, 0.0, 180.0)]
    [InlineData(0.0, -1.0, 270.0)]
    [InlineData(1.0, -1.0, 315.0)]
    public void LabHueIsNormalizedToZeroThrough360(double a, double b, double expectedHue)
    {
        CIELAB color = new(50.0, a, b);

        Assert.Equal(expectedHue, color.CIEH);
    }

    [Fact]
    public void ChangingLabComponentsRecalculatesDerivedValues()
    {
        CIELAB color = new(50.0, 3.0, 4.0);
        Assert.Equal(5.0, color.CIEC);

        color.CIEA = 5.0;
        color.CIEB = 12.0;

        Assert.Equal(13.0, color.CIEC);
        Assert.Equal(67.3801, color.CIEH);
    }

    [Theory]
    [InlineData(0.0, -0.0)]
    [InlineData(-0.0, -0.0)]
    [InlineData(1.0, -1e-10)]
    public void NeutralAndAlmostFullCircleHuesStayInTheNormalizedRange(double a, double b)
    {
        var color = new CIELAB(50, a, b);
        Assert.Equal(0, color.CIEH);
        Assert.InRange(ChromaticityDotNet.Controller.ChromaticityConversion.LabToLch(color).CIEH, 0, 359.99999999999999);
    }

    [Theory]
    [InlineData(1e200, 1e200)]
    [InlineData(1e-200, -1e-200)]
    public void ChromaAvoidsIntermediateSquareOverflowAndUnderflow(double a, double b)
    {
        var color = new CIELAB(50, a, b);
        var lch = ChromaticityDotNet.Controller.ChromaticityConversion.LabToLch(color);
        Assert.Equal(Math.Sqrt(2), lch.CIEC / Math.Abs(a), 14);
        Assert.Equal(Math.Round(lch.CIEC, 4, MidpointRounding.AwayFromZero), color.CIEC);
        Assert.Equal(Math.Round(lch.CIEH, 4, MidpointRounding.AwayFromZero), color.CIEH);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidComponentUpdatesLeaveTheEntireLabUnchanged(double value)
    {
        var color = new CIELAB(50, 3, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => color.CIEA = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => color.CIEB = value);
        Assert.Equal(3, color.CIEA); Assert.Equal(4, color.CIEB);
        Assert.Equal(5, color.CIEC); Assert.Equal(53.1301, color.CIEH);
    }

    [Fact]
    public void TrueChromaOverflowIsRejectedWithoutLeavingStaleDerivedValues()
    {
        var color = new CIELAB(50, double.MaxValue, 0);
        Assert.Equal(double.MaxValue, color.CIEC);
        Assert.Throws<ArgumentException>(() => color.CIEB = double.MaxValue);
        Assert.Equal(0, color.CIEB); Assert.Equal(0, color.CIEH);
        Assert.Equal(double.MaxValue, color.CIEC);
    }

    [Fact]
    public void SpectrumAndWhitePointStoreTheirMetadata()
    {
        Spectrum spectrum = new()
        {
            StartingWavelength = 400,
            EndingWavelength = 700,
            WavelengthInterval = 10,
            Spectrums = new[] { 1.0, 2.0 }
        };
        StandardWhitePoint whitePoint = new()
        {
            Observer = StandardObserver.Degree2,
            WhitePointXnYnZn = new CIEXYZ { CIEX = 95.047, CIEY = 100, CIEZ = 108.883 }
        };

        Assert.Equal(400, spectrum.StartingWavelength);
        Assert.Equal(700, spectrum.EndingWavelength);
        Assert.Equal(10, spectrum.WavelengthInterval);
        Assert.Equal(new[] { 1.0, 2.0 }, spectrum.Spectrums);
        Assert.Equal(StandardObserver.Degree2, whitePoint.Observer);
        Assert.Equal(100, whitePoint.WhitePointXnYnZn!.CIEY);
    }
}
