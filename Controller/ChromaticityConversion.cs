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
        /// Cover CIE XYZ color to CIE Labch color
        /// </summary>
        /// <param name="XYZ">CIEXYZ color</param>
        /// <param name="LightConditionWhitePoint">StandardWhitePoint,specially take case of observer</param>
        /// <returns>CIE Labch color</returns>
        public static CIELABCH XYZ2Labch(CIEXYZ XYZ, Standardilluminant illuminant, StandardObserver observer)
        {
            CIEXYZ WhitePoint = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);

            double L, a, b;
            double temX = 0, temY = 0, temZ = 0;

            //GetStandXYZ(observer, lightsource_type, &temX, &temY, &temZ);
            temX = XYZ.CIEX / WhitePoint.CIEX; //白点X值
            temY = XYZ.CIEY / WhitePoint.CIEY; //白点Y值
            temZ = XYZ.CIEZ / WhitePoint.CIEZ; //白点Z值

            if (temX > 0.008856)
                temX = Math.Pow(temX, 0.3333333);
            else
                temX = (7.787 * temX) + 0.138;
            if (temY > 0.008856)
            {
                temY = Math.Pow(temY, 0.3333333);
                L = 116 * temY - 16;
            }
            else
            {
                L = 903.3 * temY;
                temY = (7.787 * temY) + 0.138;
            }

            if (temZ > 0.008856)
                temZ = Math.Pow(temZ, 0.3333333);
            else
                temZ = (7.787 * temZ) + 0.138;
            a = 500.0 * (temX - temY);
            b = 200.0 * (temY - temZ);

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

            double yr = XYZ.CIEY / WhitePoint.CIEY;
            double upai = (4 * XYZ.CIEX) / (XYZ.CIEX + 15 * XYZ.CIEY + 3 * XYZ.CIEZ);
            double vpai = (9 * XYZ.CIEY) / (XYZ.CIEX + 15 * XYZ.CIEY + 3 * XYZ.CIEZ);

            double ur = (4 * WhitePoint.CIEX) / (WhitePoint.CIEX + 15 * WhitePoint.CIEY + 3 * WhitePoint.CIEZ);
            double vr = (9 * WhitePoint.CIEY) / (WhitePoint.CIEX + 15 * WhitePoint.CIEY + 3 * WhitePoint.CIEZ);

            double epsilon = 216.0 / 24389.0;
            double kapa = 24389.0 / 27.0;

            double L;
            if (yr > epsilon)
            {
                L = (116 * Math.Pow(yr, 1.0 / 3.0)) - 16;
            }
            else
            {
                L = kapa * yr;
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
        /// Cover CIE XYZ to CIE RGB(1-255 byte)
        /// </summary>
        /// <param name="xyzColor"></param>
        /// <returns>CIE RGB</returns>
        public static CIERGB XYZ2RGB(CIEXYZ xyzColor)
        {
            double X = xyzColor.CIEX;
            double Y = xyzColor.CIEY;
            double Z = xyzColor.CIEZ;

            // The sRGB matrix expects D65 XYZ normalized to the 0-1 range.
            X /= 100.0;
            Y /= 100.0;
            Z /= 100.0;

            double R = 3.2406255 * X - 1.5372080 * Y - 0.4986286 * Z;
            double G = -0.9689307 * X + 1.8757561 * Y + 0.0415175 * Z;
            double B = 0.0557101 * X - 0.2040211 * Y + 1.0569959 * Z;

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
    }
}
