namespace ChromaticityDotNet.Model
{
    /// <summary>Read-only CIE LCh coordinates returned by LabToLch, without intermediate rounding.</summary>
    public sealed class CIELCH
    {
        public double CIEL { get; }
        public double CIEC { get; }
        /// <summary>Hue in [0, 360) degrees. Zero chroma uses zero as a placeholder.</summary>
        public double CIEH { get; }

        internal CIELCH(double lightness, double chroma, double hue)
        {
            CIEL = lightness;
            CIEC = chroma;
            CIEH = hue;
        }
    }
}
