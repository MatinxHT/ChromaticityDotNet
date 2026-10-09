using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Model
{
    /// <summary>
    /// DataClass
    /// </summary>
    public class DataModel
    {
        #region SPD

        public class StandardWhitePoint
        {
            public CIEXYZ? WhitePointXnYnZn { get; set; }

            public StandardObserver Observer { get; set; }
        }

        /// <summary>Uniformly sampled spectrum, with an inclusive wavelength range in nanometres.
        /// Sample count must equal (EndingWavelength - StartingWavelength) / WavelengthInterval + 1.
        /// Values are reflectance percentages for REFToXYZ, or nonnegative spectral values for SPD.</summary>
        public class Spectrum
        {
            public int StartingWavelength { get; set; }
            public int WavelengthInterval { get; set; }
            public int EndingWavelength { get; set; }
            public double[]? Spectrums { get; set; }
        }

        #endregion

        #region CIE Colro Data
        public class CIEXYZ
        {
            public double CIEX { get; set; }
            public double CIEY { get; set; }
            public double CIEZ { get; set; }
        }

        /// <summary>
        /// CIELAB 1976 coordinates with safely derived, four-decimal chroma and normalized hue.
        /// </summary>
        /// <remarks>Nonfinite a*/b* and finite-chroma overflow are rejected. Failed component updates
        /// preserve all existing values. LabToLch uses the same calculation with unrounded output.</remarks>
        public class CIELAB
        {
            private double _ciel;
            private double _ciea;
            private double _cieb;
            private double _ciec;
            private double _cieh;

            public double CIEL
            {
                get { return _ciel; }
                set { _ciel = value; }
            }
            public double CIEA
            {
                get { return _ciea; }
                set { UpdateCH(value, _cieb, nameof(CIEA)); _ciea = value; }
            }

            public double CIEB
            {
                get { return _cieb; }
                set { UpdateCH(_ciea, value, nameof(CIEB)); _cieb = value; }
            }

            public double CIEC
            {
                get { return _ciec; }
                private set { _ciec = value; }
            }

            public double CIEH
            {
                get { return _cieh; }
                private set { _cieh = value; }
            }


            // 构造函数，初始化属性
            public CIELAB(double ciel, double ciea, double cieb)
            {
                CIEL = ciel;
                CIEA = ciea;
                CIEB = cieb;
            }

            public CIELAB()
            {

            }

            private void UpdateCH(double a, double b, string parameter)
            {
                var polar = LabPolarCoordinates.Calculate(a, b, parameter);
                _ciec = NumericPrecision.Round(polar.Chroma);
                _cieh = NumericPrecision.Round(polar.Hue);
                if (_cieh >= 360.0) _cieh = 0;
            }
        }

        public class CIExyY
        {
            public double CIEx { get; set; }
            public double CIEy { get; set; }
            public double CIEY { get; set; }
        }

        /// <summary>
        /// CIE 1976 L*u*v* color space.
        /// </summary>
        public class CIELuv
        {
            public double CIEL { get; set; }
            public double CIEu { get; set; }
            public double CIEv { get; set; }
        }

        /// <summary>
        /// CIE 1976 UCS u'v' chromaticity coordinates (not L*u*v*).
        /// Use this for chromaticity diagram coordinates; use <see cref="CIELuv"/> for the full L*u*v* color space.
        /// </summary>
        public class CIEuv
        {
            public double CIEu { get; set; }
            public double CIEv { get; set; }
        }

        /// <summary>
        /// RGB data format as byte(0~255),in 'system.windows.media' u can use 'Color newColor = Color.FromRgb(redValue, greenValue, blueValue);'to bursh it
        /// </summary>
        public class CIERGB
        {
            public byte redValue { get; set; }
            public byte greenValue { get; set; }
            public byte blueValue { get; set; }
        }

        /// <summary>HSL derived from gamma-encoded sRGB; not a CIE color space.</summary>
        public class CIEHSL
        {
            /// <summary>Hue in [0, 360) degrees; zero for gray.</summary>
            public double H { get; set; }
            /// <summary>Saturation in [0, 1].</summary>
            public double S { get; set; }
            /// <summary>Lightness in [0, 1].</summary>
            public double L { get; set; }
        }

        /// <summary>HSV derived from gamma-encoded sRGB; not a CIE color space.</summary>
        public class CIEHSV
        {
            /// <summary>Hue in [0, 360) degrees; zero for gray.</summary>
            public double H { get; set; }
            /// <summary>Saturation in [0, 1].</summary>
            public double S { get; set; }
            /// <summary>Value in [0, 1].</summary>
            public double V { get; set; }
        }

        #endregion

    }

    public class ColorDifferenceEquationResults
    {
        /// <summary>
        /// 整体色差差异
        /// </summary>
        public double DeltaE { get; set; }
        /// <summary>
        /// 明度方向的色差情况
        /// </summary>
        public double DeltaLonly { get; set; }
        /// <summary>
        /// 饱和度方向的色差情况
        /// </summary>
        public double DeltaConly { get; set; }
        /// <summary>
        /// 色相方向的色差情况
        /// </summary>
        public double DeltaHonly { get; set; }
        /// <summary>
        /// 明度数值差
        /// </summary>
        public double DL { get; set; }
        public double DA { get; set; }
        public double DB { get; set; }

        /// <summary>
        /// 饱和度数值差
        /// </summary>
        public double DC { get; set; }
        /// <summary>
        /// 色相数值差
        /// </summary>
        public double DH { get; set; }

    }

    public class ConfigEnum
    {
        public enum DeltaEType
        {
            DeltaE1976,
            DeltaE1994,
            DeltaE2000,
            DeltaEcmc
        }
    }

}
