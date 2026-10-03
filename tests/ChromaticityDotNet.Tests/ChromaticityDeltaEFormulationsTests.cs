using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using Xunit;

namespace ChromaticityDotNet.Tests;

public class ChromaticityDeltaEFormulationsTests
{
    public static IEnumerable<object[]> Ciede2000ReferenceData()
    {
        yield return Row(50.0000, 2.6772, -79.7751, 50.0000, 0.0000, -82.7485, 2.0425);
        yield return Row(50.0000, 3.1571, -77.2803, 50.0000, 0.0000, -82.7485, 2.8615);
        yield return Row(50.0000, 2.8361, -74.0200, 50.0000, 0.0000, -82.7485, 3.4412);
        yield return Row(50.0000, -1.3802, -84.2814, 50.0000, 0.0000, -82.7485, 1.0000);
        yield return Row(50.0000, -1.1848, -84.8006, 50.0000, 0.0000, -82.7485, 1.0000);
        yield return Row(50.0000, -0.9009, -85.5211, 50.0000, 0.0000, -82.7485, 1.0000);
        yield return Row(50.0000, 0.0000, 0.0000, 50.0000, -1.0000, 2.0000, 2.3669);
        yield return Row(50.0000, -1.0000, 2.0000, 50.0000, 0.0000, 0.0000, 2.3669);
        yield return Row(50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0009, 7.1792);
        yield return Row(50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0010, 7.1792);
        yield return Row(50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0011, 7.2195);
        yield return Row(50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0012, 7.2195);
        yield return Row(50.0000, -0.0010, 2.4900, 50.0000, 0.0009, -2.4900, 4.8045);
        yield return Row(50.0000, -0.0010, 2.4900, 50.0000, 0.0010, -2.4900, 4.8045);
        yield return Row(50.0000, -0.0010, 2.4900, 50.0000, 0.0011, -2.4900, 4.7461);
        yield return Row(50.0000, 2.5000, 0.0000, 50.0000, 0.0000, -2.5000, 4.3065);
        yield return Row(50.0000, 2.5000, 0.0000, 73.0000, 25.0000, -18.0000, 27.1492);
        yield return Row(50.0000, 2.5000, 0.0000, 61.0000, -5.0000, 29.0000, 22.8977);
        yield return Row(50.0000, 2.5000, 0.0000, 56.0000, -27.0000, -3.0000, 31.9030);
        yield return Row(50.0000, 2.5000, 0.0000, 58.0000, 24.0000, 15.0000, 19.4535);
        yield return Row(50.0000, 2.5000, 0.0000, 50.0000, 3.1736, 0.5854, 1.0000);
        yield return Row(50.0000, 2.5000, 0.0000, 50.0000, 3.2972, 0.0000, 1.0000);
        yield return Row(50.0000, 2.5000, 0.0000, 50.0000, 1.8634, 0.5757, 1.0000);
        yield return Row(50.0000, 2.5000, 0.0000, 50.0000, 3.2592, 0.3350, 1.0000);
        yield return Row(60.2574, -34.0099, 36.2677, 60.4626, -34.1751, 39.4387, 1.2644);
        yield return Row(63.0109, -31.0961, -5.8663, 62.8187, -29.7946, -4.0864, 1.2630);
        yield return Row(61.2901, 3.7196, -5.3901, 61.4292, 2.2480, -4.9620, 1.8731);
        yield return Row(35.0831, -44.1164, 3.7933, 35.0232, -40.0716, 1.5901, 1.8645);
        yield return Row(22.7233, 20.0904, -46.6940, 23.0331, 14.9730, -42.5619, 2.0373);
        yield return Row(36.4612, 47.8580, 18.3852, 36.2715, 50.5065, 21.2231, 1.4146);
        yield return Row(90.8027, -2.0831, 1.4410, 91.1528, -1.6435, 0.0447, 1.4441);
        yield return Row(90.9257, -0.5406, -0.9208, 88.6381, -0.8985, -0.7239, 1.5381);
        yield return Row(6.7747, -0.2908, -2.4247, 5.8714, -0.0985, -2.2286, 0.6377);
        yield return Row(2.0776, 0.0795, -1.1350, 0.9033, -0.0636, -0.5514, 0.9082);
    }

    [Fact]
    public void DeltaE1976UsesEuclideanLabDistance()
    {
        CIELABCH standard = new(50.0, 0.0, 0.0);
        CIELABCH sample = new(53.0, 4.0, 12.0);

        double result = ChromaticityDeltaEFormulations.DeltaE1976(standard, sample);

        Assert.Equal(13.0, result);
    }

    [Fact]
    public void DeltaE1994HandlesLightnessChromaAndHueWeights()
    {
        Assert.Equal(
            1.0,
            ChromaticityDeltaEFormulations.DeltaE1994(
                new CIELABCH(50.0, 20.0, 0.0),
                new CIELABCH(51.0, 20.0, 0.0)));
        Assert.Equal(
            0.5263,
            ChromaticityDeltaEFormulations.DeltaE1994(
                new CIELABCH(50.0, 20.0, 0.0),
                new CIELABCH(50.0, 21.0, 0.0)));
        Assert.Equal(
            21.7571,
            ChromaticityDeltaEFormulations.DeltaE1994(
                new CIELABCH(50.0, 20.0, 0.0),
                new CIELABCH(50.0, 0.0, 20.0)));
    }

    [Fact]
    public void DeltaE1994HandlesHueWrapWithoutArtificialLargeDifference()
    {
        double result = ChromaticityDeltaEFormulations.DeltaE1994(
            new CIELABCH(50.0, 1.0, -0.01),
            new CIELABCH(50.0, 1.0, 0.01));

        Assert.Equal(0.0197, result);
    }

    [Theory]
    [MemberData(nameof(Ciede2000ReferenceData))]
    public void DeltaE2000MatchesPublishedReferenceDataset(
        double l1,
        double a1,
        double b1,
        double l2,
        double a2,
        double b2,
        double expected)
    {
        ColorDifferenceEquationResults result =
            ChromaticityDeltaEFormulations.DeltaE2000(
                new CIELABCH(l1, a1, b1),
                new CIELABCH(l2, a2, b2),
                1.0,
                1.0,
                1.0);

        Assert.Equal(expected, result.DeltaE);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result.DeltaE);
    }

    [Fact]
    public void DeltaE2000ReturnsZeroComponentsForIdenticalColors()
    {
        CIELABCH color = new(50.0, 10.0, -20.0);

        ColorDifferenceEquationResults result =
            ChromaticityDeltaEFormulations.DeltaE2000(color, color, 1.0, 1.0, 1.0);

        Assert.Equal(0.0, result.DeltaE);
        Assert.Equal(0.0, result.DeltaLonly);
        Assert.Equal(0.0, result.DeltaConly);
        Assert.Equal(0.0, result.DeltaHonly);
        Assert.Equal(0.0, result.DL);
        Assert.Equal(0.0, result.DA);
        Assert.Equal(0.0, result.DB);
        Assert.Equal(0.0, result.DC);
        Assert.Equal(0.0, result.DH);
    }

    [Fact]
    public void CmcReturnsZeroForIdenticalColors()
    {
        CIELABCH color = new(50.0, 10.0, -20.0);

        double result =
            ChromaticityDeltaEFormulations.DeltaEcmc(color, color, 1.0, 1.0);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CmcAppliesReferenceLightnessWeight()
    {
        double result = ChromaticityDeltaEFormulations.DeltaEcmc(
            new CIELABCH(50.0, 0.0, 0.0),
            new CIELABCH(51.0, 0.0, 0.0),
            1.0,
            1.0);

        Assert.Equal(0.9189, result);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result);
    }

    // Reference values from Colour's CMC tests (l defaults to 2 in Colour):
    // https://github.com/colour-science/colour/blob/develop/colour/difference/tests/test_delta_e.py
    [Theory]
    [InlineData(48.99183622, -0.10561667, 400.65619925, 50.65907324, -0.11671910, 402.82235718, 2, 1, 0.899699975683419)]
    [InlineData(100, 21.57210357, 272.22819350, 100, 426.67945353, 72.39590835, 2, 1, 172.70477129)]
    [InlineData(100, 21.57210357, 272.22819350, 100, 74.05216981, 276.45318193, 2, 1, 20.59732717)]
    [InlineData(100, 21.57210357, 272.22819350, 100, 8.32281957, -73.58297716, 1, 1, 121.71841479)]
    // Additional values independently calculated in Python from the reference formula:
    // https://colour.readthedocs.io/en/develop/_modules/colour/difference/delta_e.html#delta_E_CMC
    // Reference/sample reversal, chroma weights, L=16, achromatic colors, hue wrap,
    // and inputs with more than four decimal places.
    [InlineData(50, 20, 0, 50, 0, 20, 1, 1, 24.875171696352)]
    [InlineData(50, 0, 20, 50, 20, 0, 1, 1, 28.979466483172)]
    [InlineData(50, 20, 0, 50, 21, 0, 1, 1, 0.606393754240)]
    [InlineData(50, 20, 0, 50, 21, 0, 1, 2, 0.303196877120)]
    [InlineData(15.9999, 0, 0, 17, 0, 0, 1, 1, 1.957142857143)]
    [InlineData(16, 0, 0, 17, 0, 0, 1, 1, 1.956070774863)]
    [InlineData(16.0001, 0, 0, 17, 0, 0, 1, 1, 1.955865635546)]
    [InlineData(50, 0, 0, 50, 3, 4, 1, 1, 7.836990595611)]
    [InlineData(50, 3, 4, 50, 0, 0, 1, 1, 5.333959424864)]
    [InlineData(50, 20, -0.01, 50, 20, 0.01, 1, 1, 0.017586493853)]
    [InlineData(50, 20, 0.01, 50, 20, -0.01, 1, 1, 0.017592312276)]
    [InlineData(50.123456, 20.654321, -30.987654, 51.234567, 21.765432, -29.876543, 1, 1, 1.498812744944)]
    [InlineData(50.123456, 20.654321, -30.987654, 51.234567, 21.765432, -29.876543, 2, 1, 1.211087702355)]
    [InlineData(50.123456, 20.654321, -30.987654, 51.234567, 21.765432, -29.876543, 1.5, 0.5, 1.309463001293)]
    public void CmcMatchesReferenceValuesToFourDecimalPlaces(
        double l1, double a1, double b1,
        double l2, double a2, double b2,
        double pl, double pc, double expected)
    {
        double result = ChromaticityDeltaEFormulations.DeltaEcmc(
            new CIELABCH(l1, a1, b1), new CIELABCH(l2, a2, b2), pl, pc);

        Assert.Equal(Math.Round(expected, 4, MidpointRounding.AwayFromZero), result);
        Assert.InRange(Math.Abs(result - expected), 0.0, 0.00005);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result);
    }

    // Independently calculated with the same Python reference as above.
    // The neighboring hues round to the boundary in CIELABCH.CIEH, so these
    // also verify that CMC uses unrounded Lab-derived hue for branch selection.
    [Theory]
    [InlineData(163.99999, 19.733031297774)]
    [InlineData(164.0, 19.776032028101)]
    [InlineData(164.00001, 19.776033881825)]
    [InlineData(344.99999, 26.013639132704)]
    [InlineData(345.0, 26.013636836779)]
    [InlineData(345.00001, 26.095143893211)]
    public void CmcUsesReferenceHueAtPiecewiseBoundaries(double hue, double expected)
    {
        double radians = hue * (Math.PI / 180.0);
        CIELABCH standard = new(50.0, 20.0 * Math.Cos(radians), 20.0 * Math.Sin(radians));

        double result = ChromaticityDeltaEFormulations.DeltaEcmc(
            standard, new CIELABCH(50.0, 0.0, 20.0), 1.0, 1.0);

        Assert.Equal(Math.Round(expected, 4, MidpointRounding.AwayFromZero), result);
        Assert.InRange(Math.Abs(result - expected), 0.0, 0.00005);
        PrecisionAssert.HasAtMostFourDecimalPlaces(result);
    }

    [Theory]
    [InlineData(0.000025549489, 0.0)]
    [InlineData(0.000025550511, 0.0001)]
    [InlineData(0.00022995, 0.0005)]
    public void CmcRoundsOnlyFinalResultAwayFromZero(double sampleLightness, double expected)
    {
        // With an achromatic black reference, DeltaE = sampleLightness / 0.511.
        // These inputs straddle 0.00005 and include the midpoint 0.00045.
        double result = ChromaticityDeltaEFormulations.DeltaEcmc(
            new CIELABCH(0.0, 0.0, 0.0),
            new CIELABCH(sampleLightness, 0.0, 0.0), 1.0, 1.0);

        Assert.Equal(expected, result);
    }

    private static object[] Row(
        double l1,
        double a1,
        double b1,
        double l2,
        double a2,
        double b2,
        double expected)
    {
        return new object[] { l1, a1, b1, l2, a2, b2, expected };
    }
}
