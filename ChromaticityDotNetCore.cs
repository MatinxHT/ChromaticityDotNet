﻿using System;
//using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml.Linq;
using static ChromaticityDotNet.DataClass;
using static ChromaticityDotNet.StandardChromaticityClass;
using static ChromaticityDotNet.StandardChromaticityClass.StandardilluminantClass;

namespace ChromaticityDotNet
{
    /// <summary>
    /// Information of this DLL
    public class ChromaticityDotNetCore
    {
        /// <summary>
        /// DLL Version
        /// </summary>
        public static string Version = GetCoreVersion();

        private static string GetCoreVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

    }

    /// <summary>
    /// For matching standard color data  or .. (will be update...)
    /// </summary>
    public class ChromaticityMatch
    {
        /// <summary>
        /// Finding StandardWhitePoint in choosen illuminant and observer
        /// </summary>
        /// <param name="illuminant">Standard illuminant type</param>
        /// <param name="observer">Standard observer degree</param>
        /// <returns>StandardWhitePoint in choosen illuminant and observer </returns>
        public static StandardWhitePoint GetStandardWhitePoint(Standardilluminant illuminant, StandardObserver observer)
        {
            IStandardilluminant Standardilluminantdata;

            switch (illuminant)
            {
                case (Standardilluminant.D65):
                    switch (observer)
                    {
                        case (StandardObserver.Degree10):
                            Standardilluminantdata = new D65_Degree10();
                            break;
                        case (StandardObserver.Degree2):
                            Standardilluminantdata = new D65_Degree2();
                            break;
                        default:
                            Standardilluminantdata = new D65_Degree10();
                            break;
                    }
                    break;
                case (Standardilluminant.CWF):
                    switch (observer)
                    {
                        case (StandardObserver.Degree10):
                            Standardilluminantdata = new CWF_Degree10();
                            break;
                        case (StandardObserver.Degree2):
                            Standardilluminantdata = new D65_Degree10();
                            //Standardilluminantdata = new CWF_Degree2();
                            break;
                        default:
                            Standardilluminantdata = new CWF_Degree10();
                            break;
                    }
                    break;
                case (Standardilluminant.A):
                    switch (observer)
                    {
                        case (StandardObserver.Degree10):
                            Standardilluminantdata = new A_Degree10();
                            break;
                        case (StandardObserver.Degree2):
                            Standardilluminantdata = new D65_Degree10();
                            //Standardilluminantdata = new A_Degree2();
                            break;
                        default:
                            Standardilluminantdata = new A_Degree10();
                            break;
                    }
                    break;
                default:
                    Standardilluminantdata = new D65_Degree10();
                    break;
            }

            return Standardilluminantdata.whitePoint;
        }
    }

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
            StandardWhitePoint WhitePoint = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);

            double L, a, b, C, H;
            double temX = 0, temY = 0, temZ = 0;
            double[] result = new double[10];

            //GetStandXYZ(observer, lightsource_type, &temX, &temY, &temZ);
            temX = XYZ.CIEX / WhitePoint.Xn; //白点X值
            temY = XYZ.CIEY / WhitePoint.Yn; //白点Y值
            temZ = XYZ.CIEZ / WhitePoint.Zn; //白点Z值

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
                CIEx = Math.Round(XYZ.CIEX / total, 3),
                CIEy = Math.Round(XYZ.CIEY / total, 3),
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
            StandardWhitePoint WhitePoint = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);

            double yr = XYZ.CIEY / WhitePoint.Yn;
            double upai = (4 * XYZ.CIEX) / (XYZ.CIEX + 15 * XYZ.CIEY + 3 * XYZ.CIEZ);
            double vpai = (9 * XYZ.CIEY) / (XYZ.CIEX + 15 * XYZ.CIEY + 3 * XYZ.CIEZ);

            double ur = (4 * WhitePoint.Xn) / (WhitePoint.Xn + 15 * WhitePoint.Yn + 3 * WhitePoint.Zn);
            double vr = (9 * WhitePoint.Yn) / (WhitePoint.Xn + 15 * WhitePoint.Yn + 3 * WhitePoint.Zn);

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

            //double L = 116 * Math.Pow(yr, 1 / 3) - 16;
            //double u = 13 * L * (upai - ur);
            //double v = 13 * L * (vpai - vr);

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
        public static (double u ,double v) CIExySpace2CIEuvSpace(CIExyY xyY)
        {
            double denomx = -2D * xyY.CIEx + 12D * xyY.CIEy + 3D;
            double denomv = -2D * xyY.CIEx + 12D * xyY.CIEy + 3D;
            if (denomv != 0.0D)
            {
                if (denomx != 0.0D)return (((9D * xyY.CIEy) / denomv),(4D * xyY.CIEx) / denomx);
                else return(-1, -1);
            }
            else
                return (-1,-1);
        }

        #endregion
    }

    /// <summary>
    /// CIE de formulations
    /// </summary>
    public class ChromaticityDeltaEFormulations
    {
        /// <summary>
        /// The 1976 formula is the first formula that related a measured color difference to a known set of CIELAB coordinates
        /// </summary>
        /// <param name="standard">standard</param>
        /// <param name="sample">sample</param>
        /// <returns>DeltaE1976</returns>
        public static double DeltaE1976(CIELABCH standard, CIELABCH sample)
        {
            return Math.Sqrt(Math.Pow((sample.CIEL - standard.CIEL), 2) + Math.Pow((sample.CIEA - standard.CIEA), 2) + Math.Pow((sample.CIEB - standard.CIEB), 2));
        }

        /// <summary>
        /// The 1976 definition was extended to address perceptual non-uniformities, while retaining the CIELAB color space, by the introduction of application-specific weights derived from an automotive paint test's tolerance data.（Danger!coding by Github Copilot!!!!）
        /// </summary>
        /// <param name="standard">standard</param>
        /// <param name="sample">sample</param>
        /// <returns>DeltaE1994</returns>
        public static double DeltaE1994(CIELABCH standard,CIELABCH sample)
        {
            double deltae= 0;
            double deltaL = sample.CIEL - standard.CIEL;
            double deltaC = sample.CIEC - standard.CIEC;
            double deltaH = sample.CIEH - standard.CIEH;
            double deltaH2 = Math.Pow(deltaH, 2);
            double deltaC2 = Math.Pow(deltaC, 2);
            double deltaL2 = Math.Pow(deltaL, 2);
            double deltae2 = deltaL2 + deltaC2 + deltaH2;
            double deltae1 = Math.Sqrt(deltae2);
            double deltae3 = deltae1 / (1 + 0.045 * sample.CIEC);
            double deltae4 = deltae3 * (1 + 0.015 * Math.Sqrt(sample.CIEL * sample.CIEL + sample.CIEA * sample.CIEA));
            deltae = deltae4;

            return deltae;
        }

        /// <summary>
        /// CIE deltaE2000
        /// </summary>
        /// <param name="standard">standard</param>
        /// <param name="sample">sample</param>
        /// <param name="kL"></param>
        /// <param name="kC"></param>
        /// <param name="kH"></param>
        /// <returns>DeltaE2000</returns>
        public static double DeltaE2000(CIELABCH standard, CIELABCH sample,double kL,double kC,double kH)
        {
            double Ls = standard.CIEL;
            double As = standard.CIEA;
            double Bs = standard.CIEB;
            double L = sample.CIEL;
            double A = sample.CIEA;
            double B = sample.CIEB;

            //double kL = 1.0f;
            //double kC = 1.0f;
            //double kH = 1.0f;
            double lBarPrime = 0.5f * (Ls + L);
            double c1 = Math.Sqrt(As * As + Bs * Bs);
            double c2 = Math.Sqrt(A * A + B * B);
            double cBar = 0.5f * (c1 + c2);
            double cBar7 = cBar * cBar * cBar * cBar * cBar * cBar * cBar;
            double g = 0.5f * (1.0f - (float)Math.Sqrt(cBar7 / (cBar7 + 6103515625.0)));
            double a1Prime = As * (1.0f + g);
            double a2Prime = A * (1.0f + g);
            double c1Prime = Math.Sqrt(a1Prime * a1Prime + Bs * Bs);
            double c2Prime = Math.Sqrt(a2Prime * a2Prime + B * B);
            double cBarPrime = 0.5f * (c1Prime + c2Prime);
            double h1Prime = (Math.Atan2(Bs, a1Prime) * 180.0) / Math.PI;
            double dhPrime;

            if (h1Prime < 0.0)
                h1Prime += 360.0f;
            double h2Prime = (Math.Atan2(B, a2Prime) * 180.0) / Math.PI;
            if (h2Prime < 0.0)
                h2Prime += 360.0f;
            double hBarPrime = (Math.Abs(h1Prime - h2Prime) > 180.0f)
                ? (0.5f * (h1Prime + h2Prime + 360.0))
                : (0.5 * (h1Prime + h2Prime));
            double t = 1.0f -
                       0.17f * Math.Cos(Math.PI * (hBarPrime - 30.0f) / 180.0f) +
                       0.24f * Math.Cos(Math.PI * (2.0f * hBarPrime) / 180.0f) +
                       0.32f * Math.Cos(Math.PI * (3.0f * hBarPrime + 6.0f) / 180.0f) -
                       0.20f * Math.Cos(Math.PI * (4.0f * hBarPrime - 63.0f) / 180.0f);
            if (Math.Abs(h2Prime - h1Prime) <= 180.0)
                dhPrime = h2Prime - h1Prime;
            else
                dhPrime = (h2Prime <= h1Prime) ? (h2Prime - h1Prime + 360.0f) : (h2Prime - h1Prime - 360.0f);
            double dLPrime = L - Ls;
            double dCPrime = c2Prime - c1Prime;
            double dHPrime = 2.0f * Math.Sqrt(c1Prime * c2Prime) * Math.Sin(Math.PI * (0.5f * dhPrime) / 180.0f);
            double sL = 1.0f + ((0.015f * (lBarPrime - 50.0f) * (lBarPrime - 50.0f)) /
                                Math.Sqrt(20.0f + (lBarPrime - 50.0f) * (lBarPrime - 50.0f)));
            double sC = 1.0f + 0.045f * cBarPrime;
            double sH = 1.0f + 0.015f * cBarPrime * t;
            double dTheta = 30.0f * Math.Exp(-((hBarPrime - 275.0f) / 25.0f) * ((hBarPrime - 275.0f) / 25.0f));
            double cBarPrime7 = cBarPrime * cBarPrime * cBarPrime * cBarPrime * cBarPrime * cBarPrime * cBarPrime;
            double rC = Math.Sqrt(cBarPrime7 / (cBarPrime7 + 6103515625.0f));
            double rT = -2.0f * rC * Math.Sin(Math.PI * (2.0f * dTheta) / 180.0f);
            //DeltaEresult result = new()
            //{
            //    DL = sample.L - standard.L,
            //    DC = c2 - c1,
            //    DH = h2Prime - h1Prime,
            //    DeltaLonly = Math.Round(Math.Sqrt((dLPrime / (kL * sL)) * (dLPrime / (kL * sL))), 2),
            //    DeltaConly = Math.Round(Math.Sqrt((dCPrime / (kC * sC)) * (dCPrime / (kC * sC))), 2),
            //    DeltaHonly = Math.Round(Math.Sqrt((dHPrime / (kH * sH)) * (dHPrime / (kH * sH))), 2),
            //    DeltaE = Math.Round(Math.Sqrt(
            //    (dLPrime / (kL * sL)) * (dLPrime / (kL * sL)) +
            //    (dCPrime / (kC * sC)) * (dCPrime / (kC * sC)) +
            //    (dHPrime / (kH * sH)) * (dHPrime / (kH * sH)) +
            //    (dCPrime / (kC * sC)) * (dHPrime / (kH * sH)) * rT
            //), 2)
            //};

            double DeltaE = Math.Round(Math.Sqrt(
                (dLPrime / (kL * sL)) * (dLPrime / (kL * sL)) +
                (dCPrime / (kC * sC)) * (dCPrime / (kC * sC)) +
                (dHPrime / (kH * sH)) * (dHPrime / (kH * sH)) +
                (dCPrime / (kC * sC)) * (dHPrime / (kH * sH)) * rT
            ), 2);

            return DeltaE;
        }

        /// <summary>
        /// In 1984, the Colour Measurement Committee of the Society of Dyers and Colourists defined a difference measure, also based on the L*C*h color model. Named after the developing committee, their metric is called CMC l:c. The quasimetric has two parameters: lightness (l) and chroma (c), allowing the users to weight the difference based on the ratio of l:c that is deemed appropriate for the application. Commonly used values are 2:1[20] for acceptability and 1:1 for the threshold of imperceptibility.
        /// </summary>
        /// <param name="standard">standard</param>
        /// <param name="sample">sample</param>
        /// <param name="pl"></param>
        /// <param name="pc"></param>
        /// <returns>DeltaEcmc</returns>
        public static double DeltaEcmc(CIELABCH standard, CIELABCH sample, double pl, double pc)
        {

            double Ls = standard.CIEL;
            double As = standard.CIEA;
            double Bs = standard.CIEB;
            double L = sample.CIEL;
            double A = sample.CIEA;
            double B = sample.CIEB;

            double Cab_standard = Math.Sqrt(Math.Pow(As, 2) + Math.Pow(Bs, 2));
            double Cab_sample = Math.Sqrt(Math.Pow(A, 2) + Math.Pow(B, 2));
            //注意角度和值的修正
            double Hab_standrad = Math.Atan2(Bs, As) * (180.0 / Math.PI);
            if (Hab_standrad < 0)
            {
                Hab_standrad += 360.0;
            }
            double Hab_sample = Math.Atan2(B, A) * (180.0 / Math.PI);
            if (Hab_sample < 0)
            {
                Hab_sample += 360.0;
            }

            double deltaL = L - Ls;
            double deltaC = Cab_sample - Cab_standard;

            double q, p;
            double m = Hab_sample - Hab_standrad;
            if (m >= 0.0)
            {
                p = 1.0;
            }
            else
            {
                p = -1.0;
            }
            if (Math.Abs(m) <= 180.0)
            {
                q = 1.0;
            }
            else
            {
                q = -1.0;
            }

            double deltaELab_square = Math.Pow((L - Ls), 2) + Math.Pow((A - As), 2) + Math.Pow((B - Bs), 2);
            double deltaH = p * q * Math.Sqrt(deltaELab_square - Math.Pow(deltaL, 2) - Math.Pow(deltaC, 2));

            double S_L, S_C, S_H, f, T;
            if (Ls < 16.0)
            {
                S_L = 0.511;
            }
            else
            {
                S_L = (0.040975 * Ls) / (1.0 + 0.01765 * Ls);
            }

            S_C = ((0.0638 * Cab_standard) / (1 + 0.0131 * Cab_standard) + 0.638);
            f = Math.Sqrt(Math.Pow(Cab_standard, 4) / (Math.Pow(Cab_standard, 4) + 1900.0));

            if (164.0 <= Hab_sample && Hab_sample <= 345.0)
            {
                T = 0.56 + Math.Abs(0.2 * Math.Cos((Hab_sample + 168.0) * Math.PI / 180.0));
            }
            else
            {
                T = 0.36 + Math.Abs(0.4 * Math.Cos((Hab_sample + 35.0) * Math.PI / 180.0));
            }

            S_H = ((f * T) + 1.0 - f) * S_C;

            double delteEcmc = Math.Round(Math.Sqrt(Math.Pow(deltaL / (pl * S_L), 2) + Math.Pow(deltaC / (pc * S_C), 2) + Math.Pow(deltaH / S_H, 2)), 2);

            return delteEcmc;
        }

    }
}