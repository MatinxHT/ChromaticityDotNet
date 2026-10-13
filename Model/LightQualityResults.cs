namespace ChromaticityDotNet.Model
{
    /// <summary>Nearest Planckian temperature in CIE 1960 UCS. Temperature is null
    /// outside the implemented 1000–25000 K range or when |Duv| exceeds 0.05.</summary>
    public sealed class Cct1960Result
    {
        public double? Temperature { get; }
        /// <summary>Signed distance from the Planckian locus; positive on its green side.</summary>
        public double Duv { get; }
        public bool IsInTemperatureRange { get; }
        internal Cct1960Result(double? temperature, double duv, bool inRange)
        { Temperature = temperature; Duv = duv; IsInTemperatureRange = inRange; }
    }

    /// <summary>CIE 13.3-1995 rendering of a light-source SPD, with the 1931 2° observer.
    /// Ra is the mean of R1–R8; R9–R14 are special indices. Negative indices are retained.</summary>
    public sealed class ColorRenderingResult
    {
        public Cct1960Result Cct { get; }
        public double? Ra { get; }
        public IReadOnlyList<double> SpecialIndices { get; }
        /// <summary>Test/reference white separation in CIE 1960 uv; must not exceed 0.0054.</summary>
        public double? ReferenceWhiteDistance { get; }
        public string ReferenceType { get; }
        internal ColorRenderingResult(Cct1960Result cct, double? ra, double[] indices,
            double? distance, string referenceType)
        {
            Cct = cct; Ra = ra; SpecialIndices = Array.AsReadOnly(indices);
            ReferenceWhiteDistance = distance; ReferenceType = referenceType;
        }
    }
}
