namespace ChromaticityDotNet.Model
{
    /// <summary>Unrounded Planckian chromaticity for an observer, in CIE 1960 UCS and xy.</summary>
    public sealed class PlanckianChromaticityPoint
    {
        public double Temperature { get; }
        public double U { get; }
        public double V { get; }
        public double X { get; }
        public double Y { get; }
        internal PlanckianChromaticityPoint(double temperature, double u, double v)
        {
            Temperature = temperature; U = u; V = v;
            double denominator = 2 * u - 8 * v + 4;
            X = 3 * u / denominator; Y = 2 * v / denominator;
        }
    }

    /// <summary>Nearest Planckian temperature in CIE 1960 UCS. Temperature is null
    /// outside the implemented 1000–25000 K range or when |Duv| exceeds 0.05.</summary>
    public sealed class Cct1960Result
    {
        public double? Temperature { get; }
        /// <summary>Signed distance from the Planckian locus; positive on its green side.</summary>
        public double Duv { get; }
        public bool IsInTemperatureRange { get; }
        /// <summary>Nearest point on the searched 1000–25000 K locus, even when CCT is
        /// inapplicable. An endpoint is a constrained nearest point when the search is clipped.</summary>
        public PlanckianChromaticityPoint NearestPoint { get; }
        internal Cct1960Result(double? temperature, double duv, bool inRange, PlanckianChromaticityPoint nearestPoint)
        { Temperature = temperature; Duv = duv; IsInTemperatureRange = inRange; NearestPoint = nearestPoint; }
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
