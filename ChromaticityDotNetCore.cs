using System;
using System.Text;
using System.Xml.Linq;
using static ChromaticityDotNet.DataClass;
using static ChromaticityDotNet.StandardChromaticityClass;
using static ChromaticityDotNet.StandardChromaticityClass.StandardilluminantClass;

namespace ChromaticityDotNet
{
    public class ChromaticityDotNetCore
    {
        public string Version = "1.0.0.0";

    }

    public class ChromaticityMatch
    {
        public static StandardWhitePoint GetStandardWhitePoint(Standardilluminant illuminant, StandardObserver observer)
        {
            double Xn, Yn, Zn;

            switch (illuminant)
            {
                case (Standardilluminant.D65):
                    switch (observer)
                    {
                        case(StandardObserver.Degree10):
                            Xn = D65.Degree10.Xn;
                            Yn = D65.Degree10.Yn;
                            Zn = D65.Degree10.Zn;
                            break;
                        case(StandardObserver.Degree2):
                            Xn = D65.Degree2.Xn;
                            Yn = D65.Degree2.Yn;
                            Zn = D65.Degree2.Zn;
                            break;
                        default:
                            Xn = 0;
                            Yn = 0;
                            Zn = 0;
                            break;
                    }
                    break;
                default:
                    Xn = 0;
                    Yn = 0;
                    Zn = 0;
                    break;
            }

            return new StandardWhitePoint
            {
                Xn = Xn,
                Yn = Yn,
                Zn = Zn
            };
        }

    public class ChromaticityConversion
    {
        public static CIELABCH XYZ2Labch(CIEXYZ XYZ, double[] LightConditionWhitePoint)
        {
            double L, a, b, C, H;
            double temX = 0, temY = 0, temZ = 0;
            double[] result = new double[10];

            //GetStandXYZ(observer, lightsource_type, &temX, &temY, &temZ);
            temX = XYZ.CIEX / LightConditionWhitePoint[0]; //白点X值
            temY = XYZ.CIEY / LightConditionWhitePoint[1]; //白点Y值
            temZ = XYZ.CIEZ / LightConditionWhitePoint[2]; //白点Z值

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

        public static CIExyY XYZtoxyY(CIEXYZ XYZ)
        {
            double total = XYZ.CIEX + XYZ.CIEY + XYZ.CIEZ;
            return new CIExyY()
            {
                CIEx = Math.Round(XYZ.CIEX / total, 3),
                CIEy = Math.Round(XYZ.CIEY / total, 3),
                CIEY = Math.Round(XYZ.CIEY, 2)
            };
        }

        public static CIELuv XYZtoLuv(CIEXYZ XYZ, Standardilluminant illuminant, StandardObserver observer)
        {
            StandardWhitePoint WhitePoint = GetStandardWhitePoint(illuminant, observer);

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

        public static double CIExyYtoCCT(CIExyY xyy)
        {
            double n = (xyy.CIEx - 0.332) / (0.1858 - xyy.CIEy);
            double cct = (4.37 * Math.Pow(n, 3)) + (3601 * Math.Pow(n, 2)) + 6861 * n + 5517;

            return Math.Round(cct, 0);
        }

    }

    /// <summary>
    /// 统一存放色差方程的类，且统一输入参数的颜色空间为LAB空间
    /// </summary>
    public class CIEdeformulations
    {
        /// <summary>
        /// CIE deltaE2000方程
        /// </summary>
        /// <param name="standard"></param>
        /// <param name="sample"></param>
        /// <param name="kL"></param>
        /// <param name="kC"></param>
        /// <param name="kH"></param>
        /// <returns>色差计算结果</returns>
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
        /// CIEdeltaEcmc方程，参数l:C可调,纺织品采用(l:c)=(2:1)但东奥新厂采用(1:1)。本函数基于deltaE1976计算色相差
        /// </summary>
        /// <param name="standard">CIELAB类型的标准品LAB数据</param>
        /// <param name="sample">CIELAB类型的样本LAB数据</param>
        /// <param name="pl">double类型的l参数</param>
        /// <param name="pc">double类型的c参数</param>
        /// <returns>色差计算结果</returns>
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