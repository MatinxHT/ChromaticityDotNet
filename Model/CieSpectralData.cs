using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Model
{
    /// <summary>CIE reference spectra used by the wavelength-aware conversion API.
    /// Legacy StandardChromaticityModel tables remain unchanged.</summary>
    public static class CieSpectralData
    {
        /// <summary>All compiled CIE light spectra, in catalog order. Unlike the legacy
        /// white-point enum, this includes the complete archived FL, HP and LED series.</summary>
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

        /// <summary>Returns an independent copy of the official 1 nm illuminant spectrum.
        /// A/D65 cover 300–830 nm; fluorescent illuminants cover 380–780 nm.
        /// Existing library aliases CWF, F7, TL84 and U30 map to FL2, FL7, FL11 and FL12.</summary>
        public static Spectrum GetIlluminantSpectrum(Standardilluminant illuminant)
        {
            switch (illuminant)
            {
                case Standardilluminant.A: return Copy(300, CieReferenceData.A);
                case Standardilluminant.D65: return Copy(300, CieReferenceData.D65);
                case Standardilluminant.CWF: return Copy(380, CieReferenceData.FL2);
                case Standardilluminant.F7: return Copy(380, CieReferenceData.FL7);
                case Standardilluminant.TL84: return Copy(380, CieReferenceData.FL11);
                case Standardilluminant.U30: return Copy(380, CieReferenceData.FL12);
                default: throw new ArgumentOutOfRangeException(nameof(illuminant));
            }
        }

        /// <summary>Returns independent copies of x-bar, y-bar and z-bar, 360–830 nm / 1 nm.
        /// Degree2 selects CIE 1931; Degree10 selects CIE 1964. The latter's undefined
        /// long-wavelength z-bar entries are represented as zero for calculation.</summary>
        public static (Spectrum X, Spectrum Y, Spectrum Z) GetColorMatchingFunctions(StandardObserver observer)
        {
            var (x, y, z) = GetObserverValues(observer);
            return (Copy(360, x), Copy(360, y), Copy(360, z));
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
