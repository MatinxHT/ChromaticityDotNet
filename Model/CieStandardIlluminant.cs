using System.Collections.Concurrent;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Model
{
    /// <summary>One shared, lazily calculated snapshot per canonical illuminant.</summary>
    internal sealed class CieStandardIlluminant : IStandardilluminant
    {
        private static readonly ConcurrentDictionary<Standardilluminant, CieStandardIlluminant> Cache = new();
        private static readonly ConcurrentDictionary<string, Lazy<CachedData>> Snapshots = new(StringComparer.Ordinal);
        private readonly Lazy<CachedData> data;

        private CieStandardIlluminant(Standardilluminant illuminant)
        {
            IlluminantName = illuminant;
            string id = CieSpectralData.GetIlluminantId(illuminant);
            data = Snapshots.GetOrAdd(id,
                canonicalId => new Lazy<CachedData>(() => new CachedData(CieSpectralData.GetIlluminantSpectrum(canonicalId))));
        }

        internal static IStandardilluminant Get(Standardilluminant illuminant) =>
            Cache.GetOrAdd(illuminant, name => new CieStandardIlluminant(name));

        public Standardilluminant IlluminantName { get; }
        public Spectrum Spectrum => new Spectrum
        {
            StartingWavelength = 400,
            EndingWavelength = 700,
            WavelengthInterval = 10,
            Spectrums = (double[])data.Value.Samples.Clone()
        };
        public StandardWhitePoint WhitePoint_Degree2 => CopyWhite(data.Value.White2, StandardObserver.Degree2);
        public StandardWhitePoint WhitePoint_Degree10 => CopyWhite(data.Value.White10, StandardObserver.Degree10);

        private static StandardWhitePoint CopyWhite(CIEXYZ white, StandardObserver observer) => new StandardWhitePoint
        {
            Observer = observer,
            WhitePointXnYnZn = new CIEXYZ { CIEX = white.CIEX, CIEY = white.CIEY, CIEZ = white.CIEZ }
        };

        private sealed class CachedData
        {
            internal double[] Samples { get; }
            internal CIEXYZ White2 { get; }
            internal CIEXYZ White10 { get; }

            internal CachedData(Spectrum native)
            {
                SpectralCalculations.ValidateSpectrum(native, nameof(native));
                if (native.StartingWavelength > 400 || native.EndingWavelength < 700)
                    throw new ArgumentException("Illuminant must cover 400–700 nm.", nameof(native));
                Samples = Enumerable.Range(0, 31)
                    .Select(i => SpectralCalculations.Interpolate(native, 400 + i * 10)).ToArray();
                // Whites always use the original spectrum and full common coverage, never Samples.
                White2 = SpectralCalculations.CalculateWhitePoint(native, StandardObserver.Degree2);
                White10 = SpectralCalculations.CalculateWhitePoint(native, StandardObserver.Degree10);
            }
        }
    }
}
