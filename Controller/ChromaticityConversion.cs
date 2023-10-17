using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Controller
{
    /// <summary>
    /// For color conversion
    /// </summary>
    public class ChromaticityConversion
    {

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

            double L, a, b, C, H;
            double temX = 0, temY = 0, temZ = 0;
            double[] result = new double[10];

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
            C = Math.Sqrt(a * a + b * b);
            if ((a == 0) && (b > 0))
                H = 90;
            else if ((a == 0) && (b < 0))
                H = 270;
            else if ((a >= 0) && (b == 0))
                H = 0;
            else if ((a < 0) && (b == 0))
                H = 180;
            else
            {
                H = Math.Atan(b / a);
                H = H * 57.3;
                if ((a > 0) && (b > 0))
                    H = H;
                else if (a < 0)
                    H = 180 + H;
                else
                    H = 360 + H;
            }

            double[] labch = new double[7];
            labch[0] = L;
            labch[1] = a;
            labch[2] = b;
            labch[3] = C;
            labch[4] = H;

            double[] lab = new double[3];
            lab[0] = Math.Round(labch[0], 2); // 将 L 值保留小数点后两位
            lab[1] = Math.Round(labch[1], 2);
            lab[2] = Math.Round(labch[2], 2);

            return new CIELABCH
            {
                CIEL = labch[0],
                CIEA = labch[1],
                CIEB = labch[2],
                CIEC = labch[3],
                CIEH = labch[4]
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
                CIEx = Math.Round(XYZ.CIEX / total, 4),
                CIEy = Math.Round(XYZ.CIEY / total, 4),
                CIEY = Math.Round(XYZ.CIEY, 2)
            };
        }

        /// <summary>
        /// Cover CIE XYZ to CIE Lu'v'
        /// </summary>
        /// <param name="XYZ">CIEXYZ color</param>
        /// <param name="illuminant"></param>
        /// <param name="observer"></param>
        /// <returns>CIE Lu'v' color</returns>
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
                CIEL = Math.Round(L, 4),
                CIEu = Math.Round(upai, 4),
                CIEv = Math.Round(vpai, 4)
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

            // D65 illuminant (white point used in sRGB and CIE XYZ)
            double referenceX = 95.047;
            double referenceY = 100.000;
            double referenceZ = 108.883;

            X = X / referenceX;
            Y = Y / referenceY;
            Z = Z / referenceZ;

            double R = 3.2404542 * X - 1.5371385 * Y - 0.4985314 * Z;
            double G = -0.9692660 * X + 1.8760108 * Y + 0.0415560 * Z;
            double B = 0.0556434 * X - 0.2040259 * Y + 1.0572252 * Z;

            static double GammaCorrection(double value)
            {
                return value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
            }

            R = GammaCorrection(R);
            G = GammaCorrection(G);
            B = GammaCorrection(B);

            // Convert to 8-bit integer (0-255 range)
            int rInt = (int)Math.Round(R * 255);
            int gInt = (int)Math.Round(G * 255);
            int bInt = (int)Math.Round(B * 255);

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
        public static double CIExy2CCT(CIExyY xyy)
        {
            double n = (xyy.CIEx - 0.332) / (0.1858 - xyy.CIEy);
            double cct = (4.37 * Math.Pow(n, 3)) + (3601 * Math.Pow(n, 2)) + 6861 * n + 5517;

            return Math.Round(cct, 0);
        }

        /// <summary>
        /// For CIE1931 xy space coordinate  to CIE1976 uv space coordinate 
        /// </summary>
        /// <param name="xyY"></param>
        /// <returns>CIE1976 uv space coordinate </returns>
        public static CIELuv CIExy2CIEuv(CIExyY xyY)
        {
            CIELuv luv = new CIELuv
            {
                CIEL = 1,
                CIEu = -1,
                CIEv = -1,
            };
            double denom = -2D * xyY.CIEx + 12D * xyY.CIEy + 3D;
            if (denom != 0.0D)
            {
                luv.CIEu = ((4D * xyY.CIEx) / denom);
                luv.CIEv = ((9D * xyY.CIEy) / denom);
                return luv;
            }
            return luv;
        }

        #endregion
    }
}
