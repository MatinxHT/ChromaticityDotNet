using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Controller
{
    public partial class ChromaticityConversion
    {
        /// <summary>Preferred reflection conversion using wavelength-tagged reflectance percentages
        /// and official CIE illuminant/observer data. A perfect reflector has Y = 100.</summary>
        /// <remarks>Linearly interpolates reflectance to 1 nm and sums over its inclusive range.
        /// The complete input range must be covered by both the observer and illuminant;
        /// no extrapolation or silent clipping is performed. Results are rounded to four decimals.</remarks>
        public static CIEXYZ REFtoXYZ(Spectrum reflectance, Standardilluminant illuminant, StandardObserver standardObserver)
        {
            return REFtoXYZ(reflectance, CieSpectralData.GetIlluminantSpectrum(illuminant), standardObserver);
        }

        /// <summary>Converts wavelength-tagged reflectance percentages under a supplied relative
        /// illuminant spectrum. Both inputs are linearly interpolated onto a 1 nm grid.</summary>
        /// <remarks>Uses the reflectance's inclusive range, wholly within 360–830 nm and the
        /// illuminant's range. Values must be finite and nonnegative; reflectance above 100% is
        /// accepted. Relative illuminant scale cancels in the Y = 100 reference-white normalization.</remarks>
        public static CIEXYZ REFtoXYZ(Spectrum reflectance, Spectrum illuminant, StandardObserver standardObserver)
        {
            ValidateSpectrum(reflectance, nameof(reflectance));
            ValidateSpectrum(illuminant, nameof(illuminant));
            var (xx, yy, zz) = CieSpectralData.GetObserverValues(standardObserver);
            ValidateObserverRange(reflectance, nameof(reflectance));
            if (reflectance.StartingWavelength < illuminant.StartingWavelength ||
                reflectance.EndingWavelength > illuminant.EndingWavelength)
                throw new ArgumentException("Illuminant must cover the complete reflectance range.", nameof(illuminant));

            double x = 0, y = 0, z = 0, whiteY = 0;
            for (int wavelength = reflectance.StartingWavelength; wavelength <= reflectance.EndingWavelength; wavelength++)
            {
                int i = wavelength - 360;
                double light = Interpolate(illuminant, wavelength);
                double stimulus = light * (Interpolate(reflectance, wavelength) / 100.0);
                x += stimulus * xx[i];
                y += stimulus * yy[i];
                z += stimulus * zz[i];
                whiteY += light * yy[i];
            }
            if (whiteY <= 0 || double.IsInfinity(whiteY) || double.IsNaN(whiteY))
                throw new ArgumentException("Illuminant must produce a positive finite reference luminance over the input range.", nameof(illuminant));
            return CreateRoundedXyz(x / whiteY * 100, y / whiteY * 100, z / whiteY * 100, nameof(reflectance));
        }

        /// <summary>Returns wavelength-aware, unnormalized SPD sample sums using official CIE
        /// matching functions at the supplied wavelengths.</summary>
        /// <remarks>TODO: Define self-luminous SPD units, wavelength-step weighting and XYZ
        /// normalization. Until then this preserves the legacy sample-sum convention (no delta-lambda
        /// factor, no Y = 100 normalization, no photometric factor). Results depend on sampling interval
        /// and are not absolute photometric XYZ. This method does not resample the SPD.</remarks>
        public static CIEXYZ SPDtoXYZ(Spectrum spd, StandardObserver standardObserver)
        {
            ValidateSpectrum(spd, nameof(spd));
            var (xx, yy, zz) = CieSpectralData.GetObserverValues(standardObserver);
            ValidateObserverRange(spd, nameof(spd));
            double x = 0, y = 0, z = 0;
            for (int i = 0; i < spd.Spectrums!.Length; i++)
            {
                int index = spd.StartingWavelength + i * spd.WavelengthInterval - 360;
                x += spd.Spectrums[i] * xx[index];
                y += spd.Spectrums[i] * yy[index];
                z += spd.Spectrums[i] * zz[index];
            }
            return CreateRoundedXyz(x, y, z, nameof(spd));
        }

        private static void ValidateSpectrum(Spectrum spectrum, string paramName)
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

        private static void ValidateObserverRange(Spectrum spectrum, string paramName)
        {
            if (spectrum.StartingWavelength < 360 || spectrum.EndingWavelength > 830)
                throw new ArgumentOutOfRangeException(paramName, "CIE observer data cover 360–830 nm; select an explicit input range within it.");
        }

        private static double Interpolate(Spectrum spectrum, int wavelength)
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
