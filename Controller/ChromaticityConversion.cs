using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Controller
{
    /// <summary>
    /// For color conversion
    /// </summary>
    public class ChromaticityConversion
    {
        private const double CieEpsilon = 216.0 / 24389.0;
        private const double CieKappa = 24389.0 / 27.0;

        #region From Ref to ...

        /// <summary>
        /// Measuring luminescent material (self-luminescent)
        /// </summary>
        /// <param name="SPD"></param>
        /// <param name="standardObserver"></param>
        /// <returns></returns>
        public static CIEXYZ SPDtoXYZ(double[] SPD,StandardObserver standardObserver)
        {
            double[] xx;
            double[] yy;
            double[] zz;

            switch (standardObserver)
            {
                case StandardObserver.Degree2:
                    xx = CIEConstant.XX_2.Spectrums;
                    yy = CIEConstant.YY_2.Spectrums;
                    zz = CIEConstant.ZZ_2.Spectrums;
                    break;

                default:
                    xx = CIEConstant.XX_10.Spectrums;
                    yy = CIEConstant.YY_10.Spectrums;
                    zz = CIEConstant.ZZ_10.Spectrums;
                    break;
            }

            double X = 0;
            double Y = 0;
            double Z = 0;

            for (int i = 0; i < 31; i++)
            {
                X += SPD[i] * xx[i];
                Y += SPD[i] * yy[i];
                Z += SPD[i] * zz[i];
            }

            return new CIEXYZ
            {
                CIEX = NumericPrecision.Round(X),
                CIEY = NumericPrecision.Round(Y),
                CIEZ = NumericPrecision.Round(Z)
            };
        }

        /// <summary>
        /// Measure the color of the reflector
        /// </summary>
        /// <param name="REFDATA"></param>
        /// <param name="illuminant"></param>
        /// <param name="standardObserver"></param>
        /// <returns></returns>
        public static CIEXYZ REFtoXYZ(double[] REFDATA, Standardilluminant illuminant, StandardObserver standardObserver)
        {
            IStandardilluminant StandaredIlluminant = ChromaticityMatch.GetStandardilluminantdata(illuminant);


            int i;
            double[] ligh_temp = new double[41];
            double[] xx = new double[31];
            double[] yy = new double[31];
            double[] zz = new double[31];
            double[] XYZn = new double[3];
            double k;

            switch (standardObserver)
            {
                case StandardObserver.Degree2:
                    {
                        //will be update..
                        xx = CIEConstant.XX_2.Spectrums;
                        yy = CIEConstant.YY_2.Spectrums;
                        zz = CIEConstant.ZZ_2.Spectrums;
                        break;
                    }
                case StandardObserver.Degree10:
                    {
                        xx = CIEConstant.XX_10.Spectrums;
                        yy = CIEConstant.YY_10.Spectrums;
                        zz = CIEConstant.ZZ_10.Spectrums;
                        break;
                    }

            }

            ligh_temp = StandaredIlluminant.Spectrum.Spectrums;


            // 计算 k 的值
            double sumX = 0.0;
            double sumY = 0.0;
            double sumZ = 0.0;
            for (i = 0; i < 31; i++)
            {
                sumX += xx[i] * ligh_temp[i];
                sumY += yy[i] * ligh_temp[i];
                sumZ += zz[i] * ligh_temp[i];
            }
            k = 100.0 / sumY; // 根据代码1的计算方式推导出 k 的计算表达式

            double[] XYZ = new double[6];
            XYZ[0] = XYZ[1] = XYZ[2] = 0;
            for (i = 0; i < 31; i++)
            {
                XYZ[0] += xx[i] * (REFDATA[i] * 0.01) * ligh_temp[i];
                XYZ[1] += yy[i] * (REFDATA[i] * 0.01) * ligh_temp[i];
                XYZ[2] += zz[i] * (REFDATA[i] * 0.01) * ligh_temp[i];
                XYZn[0] += xx[i] * ligh_temp[i];
                XYZn[1] += yy[i] * ligh_temp[i];
                XYZn[2] += zz[i] * ligh_temp[i];
            }

            //魔法数字
            //k = 0.086082;

            XYZ[0] = XYZ[0] * k;    // X
            XYZ[1] = XYZ[1] * k;    // Y
            XYZ[2] = XYZ[2] * k;    // Z
            XYZ[3] = XYZn[0];    // Xn
            XYZ[4] = XYZn[1];    // Yn
            XYZ[5] = XYZn[2];    // Zn

            //return true;
            return new CIEXYZ
            {
                CIEX = NumericPrecision.Round(XYZ[0]),
                CIEY = NumericPrecision.Round(XYZ[1]),
                CIEZ = NumericPrecision.Round(XYZ[2])
            };
        }

        #endregion

        #region From XYZ to ...
        /// <summary>
        /// Converts CIE XYZ (reference white Y = 100) to CIE Lab and derived chroma/hue.
        /// </summary>
        /// <param name="XYZ">CIEXYZ color</param>
        /// <param name="illuminant">Reference illuminant used for the XYZ values.</param>
        /// <param name="observer">Standard observer used for the XYZ values.</param>
        /// <returns>CIE Labch color</returns>
        public static CIELABCH XYZ2Labch(CIEXYZ XYZ, Standardilluminant illuminant, StandardObserver observer)
        {
            CIEXYZ WhitePoint = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);

            double temX = LabFunction(XYZ.CIEX / WhitePoint.CIEX);
            double temY = LabFunction(XYZ.CIEY / WhitePoint.CIEY);
            double temZ = LabFunction(XYZ.CIEZ / WhitePoint.CIEZ);
            double L = 116.0 * temY - 16.0;
            double a = 500.0 * (temX - temY);
            double b = 200.0 * (temY - temZ);

            if (L < 0) L = 0.00;

            return new CIELABCH
            {
                CIEL = NumericPrecision.Round(L),
                CIEA = NumericPrecision.Round(a),
                CIEB = NumericPrecision.Round(b)
            };

        }

        /// <summary>
        /// Covcer CIE XYZ to CIE xy
        /// </summary>
        /// <param name="XYZ">CIEXYZ color</param>
        /// <returns>CIE xyY color</returns>
        public static CIExyY XYZ2xyY(CIEXYZ XYZ)
        {
            double total = XYZ.CIEX + XYZ.CIEY + XYZ.CIEZ;
            return new CIExyY()
            {
                CIEx = NumericPrecision.Round(XYZ.CIEX / total),
                CIEy = NumericPrecision.Round(XYZ.CIEY / total),
                CIEY = NumericPrecision.Round(XYZ.CIEY)
            };
        }

        /// <summary>
        /// Convert CIE XYZ to CIE 1976 L*u*v*
        /// </summary>
        /// <param name="XYZ">CIEXYZ color</param>
        /// <param name="illuminant"></param>
        /// <param name="observer"></param>
        /// <returns>CIE 1976 L*u*v* color</returns>
        public static CIELuv XYZ2Luv(CIEXYZ XYZ, Standardilluminant illuminant, StandardObserver observer)
        {
            CIEXYZ WhitePoint = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);

            // Black has no chromaticity, but its L*u*v* coordinates are all zero.
            if (XYZ.CIEX == 0.0 && XYZ.CIEY == 0.0 && XYZ.CIEZ == 0.0)
                return new CIELuv();

            double yr = XYZ.CIEY / WhitePoint.CIEY;
            double upai = (4 * XYZ.CIEX) / (XYZ.CIEX + 15 * XYZ.CIEY + 3 * XYZ.CIEZ);
            double vpai = (9 * XYZ.CIEY) / (XYZ.CIEX + 15 * XYZ.CIEY + 3 * XYZ.CIEZ);

            double ur = (4 * WhitePoint.CIEX) / (WhitePoint.CIEX + 15 * WhitePoint.CIEY + 3 * WhitePoint.CIEZ);
            double vr = (9 * WhitePoint.CIEY) / (WhitePoint.CIEX + 15 * WhitePoint.CIEY + 3 * WhitePoint.CIEZ);

            double L;
            if (yr > CieEpsilon)
            {
                L = (116 * Math.Pow(yr, 1.0 / 3.0)) - 16;
            }
            else
            {
                L = CieKappa * yr;
            }

            CIELuv Luv = new CIELuv()
            {
                CIEL = NumericPrecision.Round(L),
                CIEu = NumericPrecision.Round(13 * L * (upai - ur)),
                CIEv = NumericPrecision.Round(13 * L * (vpai - vr))
            };

            return Luv;
        }

        /// <summary>
        /// Converts D65 CIE XYZ (reference white Y = 100) to 8-bit sRGB, clipping to its gamut.
        /// </summary>
        /// <param name="xyzColor">D65 XYZ values; no chromatic adaptation is performed.</param>
        /// <returns>Gamma-encoded sRGB channels in the range 0 to 255.</returns>
        public static CIERGB XYZ2RGB(CIEXYZ xyzColor)
        {
            double X = xyzColor.CIEX;
            double Y = xyzColor.CIEY;
            double Z = xyzColor.CIEZ;

            // The sRGB matrix expects D65 XYZ normalized to the 0-1 range.
            X /= 100.0;
            Y /= 100.0;
            Z /= 100.0;

            // W3C CSS Color 4 matrices use the sRGB D65 white (x=0.3127, y=0.3290).
            // This is the inverse of the matrix in RGB2XYZ.
            double R = (12831.0 / 3959.0) * X - (329.0 / 214.0) * Y - (1974.0 / 3959.0) * Z;
            double G = -(851781.0 / 878810.0) * X + (1648619.0 / 878810.0) * Y + (36519.0 / 878810.0) * Z;
            double B = (705.0 / 12673.0) * X - (2585.0 / 12673.0) * Y + (705.0 / 667.0) * Z;

            R = Math.Min(Math.Max(R, 0.0), 1.0);
            G = Math.Min(Math.Max(G, 0.0), 1.0);
            B = Math.Min(Math.Max(B, 0.0), 1.0);

            static double GammaCorrection(double value)
            {
                return value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
            }

            R = GammaCorrection(R);
            G = GammaCorrection(G);
            B = GammaCorrection(B);

            // Convert to 8-bit integer (0-255 range)
            int rInt = (int)Math.Round(R * 255, MidpointRounding.AwayFromZero);
            int gInt = (int)Math.Round(G * 255, MidpointRounding.AwayFromZero);
            int bInt = (int)Math.Round(B * 255, MidpointRounding.AwayFromZero);

            //set limit
            rInt = Math.Min(rInt, 255);
            gInt = Math.Min(gInt, 255);
            bInt = Math.Min(bInt, 255);

            rInt = Math.Max(rInt, 0);
            gInt = Math.Max(gInt, 0);
            bInt = Math.Max(bInt, 0);

            byte redValue = (byte)rInt;
            byte greenValue = (byte)gInt;
            byte blueValue = (byte)bInt;

            return new CIERGB()
            {
                redValue = redValue,
                greenValue = greenValue,
                blueValue = blueValue,
            };

        }

        #endregion

        #region To XYZ

        /// <summary>
        /// Converts CIE Lab to CIE XYZ using the selected reference white.
        /// </summary>
        /// <param name="labColor">Finite L*, a*, b* coordinates with L* greater than or equal to zero. Derived C/h are ignored.</param>
        /// <param name="illuminant">Reference illuminant of the Lab color.</param>
        /// <param name="observer">Standard observer of the Lab color.</param>
        /// <returns>XYZ with reference white Y = 100, rounded to four decimal places away from zero at midpoints.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="labColor"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A coordinate is non-finite, L* is negative, or an enum value is undefined.</exception>
        /// <exception cref="ArgumentException">The coordinates overflow the finite XYZ range.</exception>
        /// <remarks>Intermediate values retain full precision. Extended colors are not clipped; L* above 100 is allowed.</remarks>
        public static CIEXYZ Labch2XYZ(CIELABCH labColor, Standardilluminant illuminant, StandardObserver observer)
        {
            if (labColor is null)
                throw new ArgumentNullException(nameof(labColor));

            ValidateLightness(labColor.CIEL, nameof(labColor));
            ValidateFinite(labColor.CIEA, nameof(labColor));
            ValidateFinite(labColor.CIEB, nameof(labColor));
            CIEXYZ whitePoint = GetConversionWhitePoint(illuminant, observer);

            double fy = (labColor.CIEL + 16.0) / 116.0;
            double fx = fy + labColor.CIEA / 500.0;
            double fz = fy - labColor.CIEB / 200.0;

            return CreateRoundedXyz(
                whitePoint.CIEX * InverseLabFunction(fx),
                whitePoint.CIEY * RelativeLuminance(labColor.CIEL),
                whitePoint.CIEZ * InverseLabFunction(fz),
                nameof(labColor));
        }

        /// <summary>
        /// Converts 8-bit, gamma-encoded sRGB to D65 CIE XYZ.
        /// </summary>
        /// <param name="rgbColor">sRGB channels in the range 0 to 255.</param>
        /// <returns>XYZ with reference white Y = 100, rounded to four decimal places away from zero at midpoints.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rgbColor"/> is null.</exception>
        /// <remarks>Uses the sRGB D65 white (x=0.3127, y=0.3290). No chromatic adaptation is performed.</remarks>
        public static CIEXYZ RGB2XYZ(CIERGB rgbColor)
        {
            if (rgbColor is null)
                throw new ArgumentNullException(nameof(rgbColor));

            static double DecodeSrgb(byte channel)
            {
                double value = channel / 255.0;
                return value <= 0.04045
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            double r = DecodeSrgb(rgbColor.redValue);
            double g = DecodeSrgb(rgbColor.greenValue);
            double b = DecodeSrgb(rgbColor.blueValue);

            // Rational coefficients from W3C CSS Color 4, scaled from Y=1 to Y=100.
            return CreateRoundedXyz(
                100.0 * ((506752.0 / 1228815.0) * r + (87881.0 / 245763.0) * g + (12673.0 / 70218.0) * b),
                100.0 * ((87098.0 / 409605.0) * r + (175762.0 / 245763.0) * g + (12673.0 / 175545.0) * b),
                100.0 * ((7918.0 / 409605.0) * r + (87881.0 / 737289.0) * g + (1001167.0 / 1053270.0) * b),
                nameof(rgbColor));
        }

        /// <summary>
        /// Converts CIE 1976 L*u*v* to CIE XYZ using the selected reference white.
        /// </summary>
        /// <param name="luvColor">Finite L*, u*, v* coordinates with L* greater than or equal to zero.</param>
        /// <param name="illuminant">Reference illuminant of the Luv color.</param>
        /// <param name="observer">Standard observer of the Luv color.</param>
        /// <returns>XYZ with reference white Y = 100, rounded to four decimal places away from zero at midpoints.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="luvColor"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A coordinate is non-finite, L* is negative, or an enum value is undefined.</exception>
        /// <exception cref="ArgumentException">L*=0 has nonzero u*/v*, reconstructed v' is nonpositive, or XYZ overflows.</exception>
        /// <remarks>Luv(0,0,0) maps to black. L* above 100 is allowed; output XYZ is not clipped.</remarks>
        public static CIEXYZ Luv2XYZ(CIELuv luvColor, Standardilluminant illuminant, StandardObserver observer)
        {
            if (luvColor is null)
                throw new ArgumentNullException(nameof(luvColor));

            ValidateLightness(luvColor.CIEL, nameof(luvColor));
            ValidateFinite(luvColor.CIEu, nameof(luvColor));
            ValidateFinite(luvColor.CIEv, nameof(luvColor));
            CIEXYZ whitePoint = GetConversionWhitePoint(illuminant, observer);

            if (luvColor.CIEL == 0.0)
            {
                if (luvColor.CIEu != 0.0 || luvColor.CIEv != 0.0)
                    throw new ArgumentException("L*=0 requires u*=0 and v*=0.", nameof(luvColor));
                return new CIEXYZ();
            }

            double denominator = whitePoint.CIEX + 15.0 * whitePoint.CIEY + 3.0 * whitePoint.CIEZ;
            double referenceU = 4.0 * whitePoint.CIEX / denominator;
            double referenceV = 9.0 * whitePoint.CIEY / denominator;
            double uPrime = luvColor.CIEu / luvColor.CIEL / 13.0 + referenceU;
            double vPrime = luvColor.CIEv / luvColor.CIEL / 13.0 + referenceV;
            if (vPrime <= 0.0)
                throw new ArgumentException("The reconstructed v' chromaticity must be positive.", nameof(luvColor));

            double y = whitePoint.CIEY * RelativeLuminance(luvColor.CIEL);
            return CreateRoundedXyz(
                y * (9.0 * uPrime) / (4.0 * vPrime),
                y,
                y * (12.0 - 3.0 * uPrime - 20.0 * vPrime) / (4.0 * vPrime),
                nameof(luvColor));
        }

        #endregion

        #region From xy to ..
        /// <summary>
        /// computing correlated color temperature
        /// </summary>
        /// <param name="xyy"></param>
        /// <returns>correlated color temperature</returns>
        public static double xy2CCT(CIExyY xyy)
        {
            double n = (xyy.CIEx - 0.3320) / (xyy.CIEy - 0.1858);

            double cct =
                -449 * Math.Pow(n, 3)
                + 3525 * Math.Pow(n, 2)
                - 6823.3 * n
                + 5520.33;

            return NumericPrecision.Round(cct);
        }

        /// <summary>
        /// For CIE1931 xy space coordinate  to CIEXYZ space
        /// </summary>
        /// <param name="xyY"></param>
        /// <returns>CIEXYZ</returns>
        public static CIEXYZ xy2XYZ(CIExyY xyY)
        {
            return new CIEXYZ()
            {
                CIEX = NumericPrecision.Round(xyY.CIEx * xyY.CIEY / xyY.CIEy),
                CIEY = NumericPrecision.Round(xyY.CIEY),
                CIEZ = NumericPrecision.Round((1 - xyY.CIEx - xyY.CIEy) * xyY.CIEY / xyY.CIEy)
            };
        }

        /// <summary>
        /// For CIE1931 xy space coordinate  to CIE1976 uv space coordinate 
        /// </summary>
        /// <param name="xyY"></param>
        /// <returns>CIE1976 u'v' chromaticity coordinates</returns>
        public static CIEuv xy2uv(CIExyY xyY)
        {
            double denom = -2D * xyY.CIEx + 12D * xyY.CIEy + 3D;
            if (denom != 0.0D)
            {
                return new CIEuv
                {
                    CIEu = NumericPrecision.Round((4D * xyY.CIEx) / denom),
                    CIEv = NumericPrecision.Round((9D * xyY.CIEy) / denom),
                };
            }
            return new CIEuv { CIEu = -1, CIEv = -1 };
        }

        #endregion

        private static double LabFunction(double value)
        {
            return value > CieEpsilon
                ? Math.Pow(value, 1.0 / 3.0)
                : (CieKappa * value + 16.0) / 116.0;
        }

        private static double InverseLabFunction(double value)
        {
            return value > 6.0 / 29.0
                ? value * value * value
                : (116.0 * value - 16.0) / CieKappa;
        }

        private static double RelativeLuminance(double lightness)
        {
            return lightness > 8.0
                ? Math.Pow((lightness + 16.0) / 116.0, 3.0)
                : lightness / CieKappa;
        }

        private static void ValidateFinite(double value, string paramName)
        {
            // double.IsFinite is not available on netstandard2.0.
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(paramName, "Color coordinates must be finite.");
        }

        private static void ValidateLightness(double lightness, string paramName)
        {
            ValidateFinite(lightness, paramName);
            if (lightness < 0.0)
                throw new ArgumentOutOfRangeException(paramName, "Lightness must be greater than or equal to zero.");
        }

        private static CIEXYZ GetConversionWhitePoint(Standardilluminant illuminant, StandardObserver observer)
        {
            if (!Enum.IsDefined(typeof(Standardilluminant), illuminant))
                throw new ArgumentOutOfRangeException(nameof(illuminant), illuminant, "Unknown standard illuminant.");
            if (!Enum.IsDefined(typeof(StandardObserver), observer))
                throw new ArgumentOutOfRangeException(nameof(observer), observer, "Unknown standard observer.");
            return ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        }

        private static CIEXYZ CreateRoundedXyz(double x, double y, double z, string paramName)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) ||
                double.IsNaN(y) || double.IsInfinity(y) ||
                double.IsNaN(z) || double.IsInfinity(z))
                throw new ArgumentException("Color coordinates cannot be converted to finite XYZ values.", paramName);

            return new CIEXYZ
            {
                CIEX = NumericPrecision.Round(x),
                CIEY = NumericPrecision.Round(y),
                CIEZ = NumericPrecision.Round(z)
            };
        }
    }
}
