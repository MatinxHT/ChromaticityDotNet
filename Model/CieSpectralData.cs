using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Model
{
    /// <summary>CIE reference spectra shared by the wavelength-aware API and legacy illuminant interface.</summary>
    public static class CieSpectralData
    {
        /// <summary>All 50 compiled CIE light spectra, in catalog order, without compatibility aliases.</summary>
        public static IReadOnlyList<CieIlluminantInfo> Illuminants { get; } =
            Array.AsReadOnly(CieReferenceData.IlluminantEntries.Select(entry => entry.Info).ToArray());

        private static readonly Dictionary<string, (CieIlluminantInfo Info, double[] Values)> Catalog =
            CieReferenceData.IlluminantEntries.ToDictionary(entry => entry.Info.Id, StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns an independent copy of a catalog spectrum on its original
        /// wavelength grid (1 or 5 nm). No interpolation or extrapolation is performed.</summary>
        public static Spectrum GetIlluminantSpectrum(string illuminantId)
        {
            if (illuminantId is null) throw new ArgumentNullException(nameof(illuminantId));
            if (!Catalog.TryGetValue(illuminantId, out var entry))
                throw new ArgumentOutOfRangeException(nameof(illuminantId), "Unknown CIE illuminant ID.");
            return new Spectrum
            {
                StartingWavelength = entry.Info.StartingWavelength,
                EndingWavelength = entry.Info.EndingWavelength,
                WavelengthInterval = entry.Info.WavelengthInterval,
                Spectrums = (double[])entry.Values.Clone()
            };
        }

        /// <summary>Returns the canonical catalog ID for any illuminant enum value, including
        /// the original CWF, F7, TL84 and U30 names. Do not use enum ToString() for catalog IDs.</summary>
        public static string GetIlluminantId(Standardilluminant illuminant)
        {
            if (!Enum.IsDefined(typeof(Standardilluminant), illuminant))
                throw new ArgumentOutOfRangeException(nameof(illuminant));
            switch (illuminant)
            {
                case Standardilluminant.CWF: return "FL2";
                case Standardilluminant.F7: return "FL7";
                case Standardilluminant.TL84: return "FL11";
                case Standardilluminant.U30: return "FL12";
                default:
                    string name = illuminant.ToString();
                    return name.StartsWith("FL3_", StringComparison.Ordinal)
                        ? name.Replace('_', '.') : name.Replace('_', '-');
            }
        }

        /// <summary>Returns an independent copy of the CIE spectrum on its native grid (1 or 5 nm).
        /// All catalog entries are supported; compatibility names map to the same canonical spectrum.</summary>
        public static Spectrum GetIlluminantSpectrum(Standardilluminant illuminant) =>
            GetIlluminantSpectrum(GetIlluminantId(illuminant));

        /// <summary>Returns independent copies of x-bar, y-bar and z-bar, 360–830 nm / 1 nm.
        /// Degree2 selects CIE 1931; Degree10 selects CIE 1964. The latter's undefined
        /// long-wavelength z-bar entries are represented as zero for calculation.</summary>
        public static (Spectrum X, Spectrum Y, Spectrum Z) GetColorMatchingFunctions(StandardObserver observer)
        {
            var (x, y, z) = GetObserverValues(observer);
            return (Copy(360, x), Copy(360, y), Copy(360, z));
        }

        private static readonly IReadOnlyList<CieChromaticityPoint> Locus2 = CreateLocus(StandardObserver.Degree2);
        private static readonly IReadOnlyList<CieChromaticityPoint> Locus10 = CreateLocus(StandardObserver.Degree10);
        private static readonly IReadOnlyList<CieChromaticityPoint> Boundary2 = CreateBoundary(Locus2);
        private static readonly IReadOnlyList<CieChromaticityPoint> Boundary10 = CreateBoundary(Locus10);

        /// <summary>Returns the immutable 360–830 nm / 1 nm spectrum locus for the selected observer.
        /// Coordinates retain double precision. Use GetChromaticityBoundary for the enclosing physical gamut;
        /// the long-wave 10-degree locus doubles back along the red edge.</summary>
        public static IReadOnlyList<CieChromaticityPoint> GetSpectralLocus(StandardObserver observer)
        {
            switch (observer)
            {
                case StandardObserver.Degree2: return Locus2;
                case StandardObserver.Degree10: return Locus10;
                default: throw new ArgumentOutOfRangeException(nameof(observer));
            }
        }

        /// <summary>Returns the immutable convex boundary of all spectral chromaticities in diagram order.
        /// This encloses physically realizable nonnegative spectral mixtures, including red-end retracing
        /// and small tabulation irregularities. The closing straight edge is the purple boundary.</summary>
        public static IReadOnlyList<CieChromaticityPoint> GetChromaticityBoundary(StandardObserver observer)
        {
            switch (observer)
            {
                case StandardObserver.Degree2: return Boundary2;
                case StandardObserver.Degree10: return Boundary10;
                default: throw new ArgumentOutOfRangeException(nameof(observer));
            }
        }

        private static IReadOnlyList<CieChromaticityPoint> CreateBoundary(IReadOnlyList<CieChromaticityPoint> locus)
        {
            var sorted = locus.OrderBy(point => point.X).ThenBy(point => point.Y).ToArray();
            var hull = new List<CieChromaticityPoint>();
            double Turn(CieChromaticityPoint a, CieChromaticityPoint b, CieChromaticityPoint c) =>
                (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            foreach (var point in sorted)
            {
                while (hull.Count >= 2 && Turn(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lowerCount = hull.Count;
            for (int i = sorted.Length - 2; i >= 0; i--)
            {
                while (hull.Count > lowerCount && Turn(hull[hull.Count - 2], hull[hull.Count - 1], sorted[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(sorted[i]);
            }
            hull.RemoveAt(hull.Count - 1);
            // Start at the violet endpoint and follow increasing wavelength round the horseshoe,
            // leaving the purple segment as the final closing edge for plotting.
            double violetWavelength = hull.Min(point => point.Wavelength);
            int violet = hull.FindIndex(point => point.Wavelength == violetWavelength);
            var ordered = Enumerable.Range(0, hull.Count).Select(i => hull[(violet - i + hull.Count) % hull.Count]).ToArray();
            return Array.AsReadOnly(ordered);
        }

        private static IReadOnlyList<CieChromaticityPoint> CreateLocus(StandardObserver observer)
        {
            var (x, y, z) = GetObserverValues(observer);
            return Array.AsReadOnly(Enumerable.Range(0, x.Length).Select(i =>
            {
                double sum = x[i] + y[i] + z[i];
                return new CieChromaticityPoint(360 + i, x[i] / sum, y[i] / sum);
            }).ToArray());
        }

        internal static (double[] X, double[] Y, double[] Z) GetObserverValues(StandardObserver observer)
        {
            switch (observer)
            {
                case StandardObserver.Degree2:
                    return (CieReferenceData.X2, CieReferenceData.Y2, CieReferenceData.Z2);
                case StandardObserver.Degree10:
                    return (CieReferenceData.X10, CieReferenceData.Y10, CieReferenceData.Z10);
                default: throw new ArgumentOutOfRangeException(nameof(observer));
            }
        }

        private static Spectrum Copy(int start, double[] values) => new Spectrum
        {
            StartingWavelength = start,
            EndingWavelength = start + values.Length - 1,
            WavelengthInterval = 1,
            Spectrums = (double[])values.Clone()
        };
    }
}
