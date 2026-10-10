using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Model
{
    /// <summary>Shared spectral validation, interpolation and reference-white integration.</summary>
    internal static class SpectralCalculations
    {
        internal static CIEXYZ CalculateWhitePoint(Spectrum illuminant, StandardObserver observer, int? start = null, int? end = null)
        {
            ValidateSpectrum(illuminant, nameof(illuminant));
            var (xx, yy, zz) = CieSpectralData.GetObserverValues(observer);
            int first = start ?? Math.Max(360, illuminant.StartingWavelength);
            int last = end ?? Math.Min(830, illuminant.EndingWavelength);
            if (first < 360 || first < illuminant.StartingWavelength || last > 830 ||
                last > illuminant.EndingWavelength || first > last)
                throw new ArgumentOutOfRangeException(nameof(start), "White-point range must be covered by both illuminant and observer.");
            double x = 0, y = 0, z = 0;
            for (int wavelength = first; wavelength <= last; wavelength++)
            {
                double power = Interpolate(illuminant, wavelength);
                int i = wavelength - 360;
                x += power * xx[i]; y += power * yy[i]; z += power * zz[i];
            }
            if (y <= 0 || double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z))
                throw new ArgumentException("Illuminant must produce finite XYZ and positive reference luminance.", nameof(illuminant));
            double normalizedX = x / y * 100, normalizedZ = z / y * 100;
            if (double.IsInfinity(normalizedX) || double.IsInfinity(normalizedZ))
                throw new ArgumentException("Illuminant reference white overflows finite XYZ.", nameof(illuminant));
            // Preserve full precision so neutral reflectors and bidirectional conversions use the same white.
            return new CIEXYZ { CIEX = normalizedX, CIEY = 100, CIEZ = normalizedZ };
        }

        internal static void ValidateSpectrum(Spectrum spectrum, string paramName)
        {
            if (spectrum is null) throw new ArgumentNullException(paramName);
            if (spectrum.Spectrums is null || spectrum.Spectrums.Length == 0)
                throw new ArgumentException("Spectrum must contain samples.", paramName);
            if (spectrum.StartingWavelength <= 0 || spectrum.EndingWavelength < spectrum.StartingWavelength || spectrum.WavelengthInterval <= 0)
                throw new ArgumentException("Spectrum requires positive wavelengths and interval, and an ordered range.", paramName);
            long span = (long)spectrum.EndingWavelength - spectrum.StartingWavelength;
            if (span % spectrum.WavelengthInterval != 0 || span / spectrum.WavelengthInterval + 1 != spectrum.Spectrums.Length)
                throw new ArgumentException("Sample count must match the inclusive wavelength range and interval.", paramName);
            foreach (double value in spectrum.Spectrums)
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                    throw new ArgumentException("Spectral samples must be finite and nonnegative.", paramName);
        }

        internal static double Interpolate(Spectrum spectrum, int wavelength)
        {
            int offset = wavelength - spectrum.StartingWavelength;
            int index = offset / spectrum.WavelengthInterval;
            int remainder = offset % spectrum.WavelengthInterval;
            double first = spectrum.Spectrums![index];
            if (remainder == 0) return first;
            double fraction = (double)remainder / spectrum.WavelengthInterval;
            return first * (1 - fraction) + spectrum.Spectrums[index + 1] * fraction;
        }
    }
}
