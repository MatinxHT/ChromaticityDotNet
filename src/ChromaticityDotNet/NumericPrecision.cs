namespace ChromaticityDotNet
{
    internal static class NumericPrecision
    {
        internal const int DecimalPlaces = 4;

        internal static double Round(double value)
        {
            return Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
        }
    }
}
