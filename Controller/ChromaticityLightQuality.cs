using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.SpectralCalculations;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Controller
{
    public partial class ChromaticityConversion
    {
        /// <summary>Converts xy to unrounded CIE 1960 UCS chromaticity (u,v).
        /// The requested historical name does not denote CIE 1976 L*u*v*.</summary>
        public static CIEuv1960 xyToLuv1960(CIExyY xyY)
        {
            if (xyY is null) throw new ArgumentNullException(nameof(xyY));
            ValidateChromaticity(xyY, nameof(xyY));
            double denominator = -2 * xyY.CIEx + 12 * xyY.CIEy + 3;
            return new CIEuv1960 { CIEu = 4 * xyY.CIEx / denominator, CIEv = 6 * xyY.CIEy / denominator };
        }

        /// <summary>Computes CCT by minimizing distance to a Planckian locus in CIE 1960 UCS.
        /// Returns NaN when CCT is undefined; use luv1960ToCCTResult for validity and Duv.</summary>
        /// <remarks>The standard result uses the CIE 1931 2° observer. Degree10 is an explicit
        /// observer-specific estimate. Search covers 1000–25000 K, 360–830 nm / 1 nm;
        /// c2 = 0.014388 m K. No xy polynomial or intermediate coordinate rounding is used.</remarks>
        public static double luv1960ToCCT(CIEuv1960 uv, StandardObserver observer = StandardObserver.Degree2) =>
            luv1960ToCCTResult(uv, observer).Temperature ?? double.NaN;

        /// <summary>Detailed CIE 1960 CCT result; invalid coordinates throw, undefined CCT is null.</summary>
        public static Cct1960Result luv1960ToCCTResult(CIEuv1960 uv, StandardObserver observer = StandardObserver.Degree2)
        {
            ValidateUv1960(uv);
            var locus = observer switch
            {
                StandardObserver.Degree2 => PlanckLocus2.Value,
                StandardObserver.Degree10 => PlanckLocus10.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(observer))
            };
            return FindCct(uv, observer, 360, 830, locus);
        }

        /// <summary>CIE 13.3-1995 CRI for a relative light-source spectrum.
        /// Requires coverage of 380–780 nm; linearly resamples SPD and all 14 official TCS
        /// reflectances to 1 nm. Scale cancels, each white has Y=100. No extrapolation.</summary>
        /// <remarks>Uses the CIE 1931 2° observer, 1960 uv adaptation and 1964 UVW differences.
        /// Below 5000 K the reference is Planckian; at/above 5000 K it is CIE daylight.
        /// Ra/Ri are unavailable if CCT is undefined or reference separation exceeds 0.0054.
        /// Reflectance and chromaticity alone cannot determine CRI.</remarks>
        public static ColorRenderingResult SPDToCRI(Spectrum spd)
        {
            ValidateSpectrum(spd, nameof(spd));
            if (spd.StartingWavelength > 380 || spd.EndingWavelength < 780)
                throw new ArgumentException("CRI requires a light-source spectrum covering 380–780 nm.", nameof(spd));
            // Normalize before integration so arbitrary finite SPD scales do not overflow.
            double maximum = spd.Spectrums!.Max();
            if (maximum <= 0) throw new ArgumentException("Light-source spectrum must contain positive power.", nameof(spd));
            var test = Enumerable.Range(380, 401).Select(w => Interpolate(spd, w) / maximum).ToArray();
            var testWhite = QualityXyz(test);
            var testUv = XyzUv1960(testWhite);
            var cct = FindCct(testUv, StandardObserver.Degree2, 380, 780, PlanckLocusCri.Value);
            if (!cct.Temperature.HasValue) return new ColorRenderingResult(cct, null, Array.Empty<double>(), null, "");
            double temperature = cct.Temperature.Value;
            bool planck = temperature < 5000;
            var reference = planck ? PlanckSpectrum(temperature, 380, 780) : DaylightSpectrum(temperature);
            var referenceWhite = QualityXyz(reference);
            var referenceUv = XyzUv1960(referenceWhite);
            double separation = Math.Sqrt(UvDistanceSquared(testUv, referenceUv));
            string referenceType = planck ? "Planckian" : "CIE daylight";
            if (separation > 0.0054)
                return new ColorRenderingResult(cct, null, Array.Empty<double>(), separation, referenceType);
            var indices = new double[14];
            double ct = AdaptC(testUv), dt = AdaptD(testUv);
            double cr = AdaptC(referenceUv), dr = AdaptD(referenceUv);
            for (int i = 0; i < indices.Length; i++)
            {
                var testColor = QualityXyz(test, CriSamples[i], testWhite.CIEY);
                var referenceColor = QualityXyz(reference, CriSamples[i], referenceWhite.CIEY);
                var uv = XyzUv1960(testColor);
                double c = cr / ct * AdaptC(uv), d = dr / dt * AdaptD(uv);
                double denominator = 16.518 + 1.481 * c - d;
                var adapted = new CIEuv1960
                {
                    CIEu = (10.872 + 0.404 * c - 4 * d) / denominator,
                    CIEv = 5.52 / denominator
                };
                var t = QualityUvw(testColor.CIEY, adapted, referenceUv);
                var r = QualityUvw(referenceColor.CIEY, XyzUv1960(referenceColor), referenceUv);
                double delta = Math.Sqrt(Math.Pow(t.U - r.U, 2) + Math.Pow(t.V - r.V, 2) + Math.Pow(t.W - r.W, 2));
                indices[i] = 100 - 4.6 * delta;
            }
            double ra = NumericPrecision.Round(indices.Take(8).Average());
            return new ColorRenderingResult(cct, ra, indices.Select(NumericPrecision.Round).ToArray(), separation, referenceType);
        }

        public static ColorRenderingResult SPDToCRI(string illuminantId) => SPDToCRI(CieSpectralData.GetIlluminantSpectrum(illuminantId));
        public static ColorRenderingResult SPDToCRI(Standardilluminant illuminant) => SPDToCRI(CieSpectralData.GetIlluminantSpectrum(illuminant));

        private static readonly double[][] CriSamples =
        {
            CieReferenceData.TCS01, CieReferenceData.TCS02, CieReferenceData.TCS03, CieReferenceData.TCS04,
            CieReferenceData.TCS05, CieReferenceData.TCS06, CieReferenceData.TCS07, CieReferenceData.TCS08,
            CieReferenceData.TCS09, CieReferenceData.TCS10, CieReferenceData.TCS11, CieReferenceData.TCS12,
            CieReferenceData.TCS13, CieReferenceData.TCS14
        };
        private static readonly Lazy<CIEuv1960[]> PlanckLocus2 = new(() => CreatePlanckLocus(StandardObserver.Degree2, 360, 830));
        private static readonly Lazy<CIEuv1960[]> PlanckLocus10 = new(() => CreatePlanckLocus(StandardObserver.Degree10, 360, 830));
        private static readonly Lazy<CIEuv1960[]> PlanckLocusCri = new(() => CreatePlanckLocus(StandardObserver.Degree2, 380, 780));

        private static void ValidateUv1960(CIEuv1960 uv)
        {
            if (uv is null) throw new ArgumentNullException(nameof(uv));
            ValidateFinite(uv.CIEu, nameof(uv)); ValidateFinite(uv.CIEv, nameof(uv));
            double denominator = 2 * uv.CIEu - 8 * uv.CIEv + 4;
            double x = 3 * uv.CIEu / denominator, y = 2 * uv.CIEv / denominator;
            if (double.IsInfinity(denominator) || denominator <= 0 || double.IsNaN(x) || double.IsNaN(y)
                || x < 0 || y <= 0 || x + y > 1 + 1e-10)
                throw new ArgumentOutOfRangeException(nameof(uv), "CIE 1960 uv must represent finite xy with x>=0, y>0 and x+y<=1.");
        }

        // One reciprocal-kelvin step between 40 and 1000 mired brackets the global minimum.
        private static CIEuv1960[] CreatePlanckLocus(StandardObserver observer, int start, int end) =>
            Enumerable.Range(40, 961).Select(m => PlanckUv(1e6 / m, observer, start, end)).ToArray();

        private static Cct1960Result FindCct(CIEuv1960 uv, StandardObserver observer, int start, int end, CIEuv1960[] locus)
        {
            int best = 0;
            for (int i = 1; i < locus.Length; i++)
                if (UvDistanceSquared(uv, locus[i]) < UvDistanceSquared(uv, locus[best])) best = i;
            double a = 40 + Math.Max(0, best - 1), b = 40 + Math.Min(locus.Length - 1, best + 1);
            const double ratio = 0.6180339887498948482;
            double c = b - ratio * (b - a), d = a + ratio * (b - a);
            double Distance(double mired) => UvDistanceSquared(uv, PlanckUv(1e6 / mired, observer, start, end));
            double fc = Distance(c), fd = Distance(d);
            for (int i = 0; i < 40; i++)
            {
                if (fc < fd) { b = d; d = c; fd = fc; c = b - ratio * (b - a); fc = Distance(c); }
                else { a = c; c = d; fc = fd; d = a + ratio * (b - a); fd = Distance(d); }
            }
            double m = (a + b) / 2, temperature = 1e6 / m;
            var nearest = PlanckUv(temperature, observer, start, end);
            var lower = PlanckUv(1e6 / (m + 0.001), observer, start, end);
            var upper = PlanckUv(1e6 / (m - 0.001), observer, start, end);
            double distance = Math.Sqrt(UvDistanceSquared(uv, nearest));
            double cross = (upper.CIEu - lower.CIEu) * (uv.CIEv - nearest.CIEv)
                - (upper.CIEv - lower.CIEv) * (uv.CIEu - nearest.CIEu);
            double duv = cross <= 0 ? distance : -distance;
            // A minimum clipped by a search endpoint does not define an in-range CCT.
            bool inRange = (m > 40 + 1e-6 && m < 1000 - 1e-6) || distance < 1e-8;
            return new Cct1960Result(inRange && distance <= 0.05 ? NumericPrecision.Round(temperature) : (double?)null,
                duv, inRange);
        }

        private static CIEuv1960 PlanckUv(double temperature, StandardObserver observer, int start, int end)
        {
            var spectrum = PlanckSpectrum(temperature, start, end);
            var (xx, yy, zz) = CieSpectralData.GetObserverValues(observer);
            double x = 0, y = 0, z = 0;
            for (int i = 0; i < spectrum.Length; i++)
            { int w = start - 360 + i; x += spectrum[i] * xx[w]; y += spectrum[i] * yy[w]; z += spectrum[i] * zz[w]; }
            return XyzUv1960(new CIEXYZ { CIEX = x, CIEY = y, CIEZ = z });
        }

        private static double[] PlanckSpectrum(double temperature, int start, int end)
        {
            double normalization = Math.Exp(0.014388 / (560e-9 * temperature)) - 1;
            var values = new double[end - start + 1];
            for (int i = 0; i < values.Length; i++)
            {
                double wavelength = start + i;
                values[i] = Math.Pow(560 / wavelength, 5) * normalization
                    / (Math.Exp(0.014388 / (wavelength * 1e-9 * temperature)) - 1);
            }
            return values;
        }

        private static double[] DaylightSpectrum(double temperature)
        {
            double x = temperature <= 7000
                ? -4.607e9 / Math.Pow(temperature, 3) + 2.9678e6 / (temperature * temperature) + 99.11 / temperature + 0.244063
                : -2.0064e9 / Math.Pow(temperature, 3) + 1.9018e6 / (temperature * temperature) + 247.48 / temperature + 0.237040;
            double y = -3 * x * x + 2.87 * x - 0.275, m = 0.0241 + 0.2562 * x - 0.7341 * y;
            double m1 = Math.Round((-1.3515 - 1.7703 * x + 5.9114 * y) / m, 3, MidpointRounding.AwayFromZero);
            double m2 = Math.Round((0.03 - 31.4424 * x + 30.0717 * y) / m, 3, MidpointRounding.AwayFromZero);
            return Enumerable.Range(380, 401).Select(w => Grid5(CieReferenceData.DaylightS0, 300, w)
                + m1 * Grid5(CieReferenceData.DaylightS1, 300, w) + m2 * Grid5(CieReferenceData.DaylightS2, 300, w)).ToArray();
        }

        private static double Grid5(double[] values, int start, int wavelength)
        {
            int offset = wavelength - start, i = offset / 5, remainder = offset % 5;
            return remainder == 0 ? values[i] : values[i] + (values[i + 1] - values[i]) * remainder / 5.0;
        }

        private static CIEXYZ QualityXyz(double[] power, double[]? reflectance = null, double whiteY = 1)
        {
            double x = 0, y = 0, z = 0;
            for (int i = 0; i < power.Length; i++)
            {
                double stimulus = power[i] * (reflectance is null ? 1 : Grid5(reflectance, 360, 380 + i));
                x += stimulus * CieReferenceData.X2[20 + i];
                y += stimulus * CieReferenceData.Y2[20 + i];
                z += stimulus * CieReferenceData.Z2[20 + i];
            }
            double scale = reflectance is null ? 1 : 100 / whiteY;
            return new CIEXYZ { CIEX = x * scale, CIEY = y * scale, CIEZ = z * scale };
        }

        private static CIEuv1960 XyzUv1960(CIEXYZ xyz)
        {
            double denominator = xyz.CIEX + 15 * xyz.CIEY + 3 * xyz.CIEZ;
            if (denominator <= 0 || xyz.CIEY <= 0)
                throw new ArgumentException("Light-source spectrum must produce positive luminance.", "spd");
            return new CIEuv1960 { CIEu = 4 * xyz.CIEX / denominator, CIEv = 6 * xyz.CIEY / denominator };
        }
        private static double UvDistanceSquared(CIEuv1960 a, CIEuv1960 b) =>
            Math.Pow(a.CIEu - b.CIEu, 2) + Math.Pow(a.CIEv - b.CIEv, 2);
        private static double AdaptC(CIEuv1960 uv) => (4 - uv.CIEu - 10 * uv.CIEv) / uv.CIEv;
        private static double AdaptD(CIEuv1960 uv) => (1.708 * uv.CIEv + 0.404 - 1.481 * uv.CIEu) / uv.CIEv;
        private static (double U, double V, double W) QualityUvw(double y, CIEuv1960 uv, CIEuv1960 white)
        {
            double w = 25 * Math.Pow(y, 1.0 / 3) - 17;
            return (13 * w * (uv.CIEu - white.CIEu), 13 * w * (uv.CIEv - white.CIEv), w);
        }
    }
}
