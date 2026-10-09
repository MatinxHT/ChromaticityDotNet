namespace ChromaticityDotNet.Model
{
    /// <summary>Shared full-precision Lab chroma/hue calculation for the model and conversions.</summary>
    internal static class LabPolarCoordinates
    {
        internal static (double Chroma, double Hue) Calculate(double a, double b, string parameter)
        {
            if (double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(b) || double.IsInfinity(b))
                throw new ArgumentOutOfRangeException(parameter, "Lab a* and b* must be finite.");
            // Scaling prevents squaring from overflowing or underflowing finite coordinates.
            double maximum = Math.Max(Math.Abs(a), Math.Abs(b));
            if (maximum == 0) return (0, 0);
            double ratio = Math.Min(Math.Abs(a), Math.Abs(b)) / maximum;
            double chroma = maximum * Math.Sqrt(1 + ratio * ratio);
            if (double.IsInfinity(chroma))
                throw new ArgumentException("Lab coordinates overflow the finite chroma range.", parameter);
            double hue = Math.Atan2(b, a) * 180.0 / Math.PI;
            if (hue < 0) hue += 360.0;
            if (hue >= 360.0 || hue == 0) hue = 0;
            return (chroma, hue);
        }
    }
}
