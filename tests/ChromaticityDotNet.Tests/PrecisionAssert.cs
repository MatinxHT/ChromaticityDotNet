using Xunit;

namespace ChromaticityDotNet.Tests;

internal static class PrecisionAssert
{
    internal static void HasAtMostFourDecimalPlaces(double value)
    {
        Assert.True(double.IsFinite(value));
        Assert.Equal(
            value * 10_000,
            Math.Round(value * 10_000, MidpointRounding.AwayFromZero),
            precision: 7);
    }
}
