namespace ChromaticityDotNet.Model
{
    /// <summary>Dominant and complementary wavelengths in nm, rounded to two decimal places.
    /// A null value means that direction meets the purple boundary instead of the spectrum locus.
    /// Both values are null for an achromatic stimulus at the reference white.</summary>
    public sealed class ChromaticityWavelengthResult
    {
        public double? DominantWavelength { get; }
        public double? ComplementaryWavelength { get; }
        public bool IsAchromatic { get; }

        internal ChromaticityWavelengthResult(double? dominant, double? complementary, bool isAchromatic = false)
        {
            DominantWavelength = dominant;
            ComplementaryWavelength = complementary;
            IsAchromatic = isAchromatic;
        }
    }

    /// <summary>An immutable xy point on the spectrum locus, derived from unrounded CIE matching functions.</summary>
    public sealed class CieChromaticityPoint
    {
        public double Wavelength { get; }
        public double X { get; }
        public double Y { get; }

        internal CieChromaticityPoint(double wavelength, double x, double y)
        {
            Wavelength = wavelength;
            X = x;
            Y = y;
        }
    }
}
