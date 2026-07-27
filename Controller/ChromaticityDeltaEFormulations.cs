using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Controller
{
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
            return NumericPrecision.Round(Math.Sqrt(
                Math.Pow(sample.CIEL - standard.CIEL, 2) +
                Math.Pow(sample.CIEA - standard.CIEA, 2) +
                Math.Pow(sample.CIEB - standard.CIEB, 2)));
        }

        /// <summary>
        /// CIE94 color difference using the graphic-arts weighting factors.
        /// </summary>
        /// <param name="standard">standard</param>
        /// <param name="sample">sample</param>
        /// <returns>DeltaE1994</returns>
        public static double DeltaE1994(CIELABCH standard, CIELABCH sample)
        {
            double deltaL = sample.CIEL - standard.CIEL;
            double standardChroma = Math.Sqrt(
                standard.CIEA * standard.CIEA +
                standard.CIEB * standard.CIEB);
            double sampleChroma = Math.Sqrt(
                sample.CIEA * sample.CIEA +
                sample.CIEB * sample.CIEB);
            double deltaC = sampleChroma - standardChroma;
            double deltaA = sample.CIEA - standard.CIEA;
            double deltaB = sample.CIEB - standard.CIEB;
            double deltaHSquared = Math.Max(
                0.0,
                deltaA * deltaA + deltaB * deltaB - deltaC * deltaC);
            double deltaH = Math.Sqrt(deltaHSquared);

            const double kL = 1.0;
            const double kC = 1.0;
            const double kH = 1.0;
            double sC = 1.0 + 0.045 * standardChroma;
            double sH = 1.0 + 0.015 * standardChroma;

            return NumericPrecision.Round(Math.Sqrt(
                Math.Pow(deltaL / kL, 2) +
                Math.Pow(deltaC / (kC * sC), 2) +
                Math.Pow(deltaH / (kH * sH), 2)));
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
        public static ColorDifferenceEquationResults DeltaE2000(CIELABCH standard, CIELABCH sample, double kL, double kC, double kH)
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
            double lBarPrime = 0.5 * (Ls + L);
            double c1 = Math.Sqrt(As * As + Bs * Bs);
            double c2 = Math.Sqrt(A * A + B * B);
            double cBar = 0.5 * (c1 + c2);
            double cBar7 = cBar * cBar * cBar * cBar * cBar * cBar * cBar;
            double g = 0.5 * (1.0 - Math.Sqrt(cBar7 / (cBar7 + 6103515625.0)));
            double a1Prime = As * (1.0 + g);
            double a2Prime = A * (1.0 + g);
            double c1Prime = Math.Sqrt(a1Prime * a1Prime + Bs * Bs);
            double c2Prime = Math.Sqrt(a2Prime * a2Prime + B * B);
            double cBarPrime = 0.5 * (c1Prime + c2Prime);
            double h1Prime = (Math.Atan2(Bs, a1Prime) * 180.0) / Math.PI;
            double dhPrime;

            if (h1Prime < 0.0)
                h1Prime += 360.0;
            double h2Prime = (Math.Atan2(B, a2Prime) * 180.0) / Math.PI;
            if (h2Prime < 0.0)
                h2Prime += 360.0;
            double hBarPrime = (Math.Abs(h1Prime - h2Prime) > 180.0)
                ? (0.5 * (h1Prime + h2Prime + 360.0))
                : (0.5 * (h1Prime + h2Prime));
            double t = 1.0 -
                       0.17 * Math.Cos(Math.PI * (hBarPrime - 30.0) / 180.0) +
                       0.24 * Math.Cos(Math.PI * (2.0 * hBarPrime) / 180.0) +
                       0.32 * Math.Cos(Math.PI * (3.0 * hBarPrime + 6.0) / 180.0) -
                       0.20 * Math.Cos(Math.PI * (4.0 * hBarPrime - 63.0) / 180.0);
            if (Math.Abs(h2Prime - h1Prime) <= 180.0)
                dhPrime = h2Prime - h1Prime;
            else
                dhPrime = (h2Prime <= h1Prime) ? (h2Prime - h1Prime + 360.0) : (h2Prime - h1Prime - 360.0);
            double dLPrime = L - Ls;
            double dCPrime = c2Prime - c1Prime;
            double dHPrime = 2.0 * Math.Sqrt(c1Prime * c2Prime) * Math.Sin(Math.PI * (0.5 * dhPrime) / 180.0);
            double sL = 1.0 + ((0.015 * (lBarPrime - 50.0) * (lBarPrime - 50.0)) /
                               Math.Sqrt(20.0 + (lBarPrime - 50.0) * (lBarPrime - 50.0)));
            double sC = 1.0 + 0.045 * cBarPrime;
            double sH = 1.0 + 0.015 * cBarPrime * t;
            double dTheta = 30.0 * Math.Exp(-((hBarPrime - 275.0) / 25.0) * ((hBarPrime - 275.0) / 25.0));
            double cBarPrime7 = cBarPrime * cBarPrime * cBarPrime * cBarPrime * cBarPrime * cBarPrime * cBarPrime;
            double rC = Math.Sqrt(cBarPrime7 / (cBarPrime7 + 6103515625.0));
            double rT = -2.0 * rC * Math.Sin(Math.PI * (2.0 * dTheta) / 180.0);

            ColorDifferenceEquationResults results = new()
            {
                DeltaE = NumericPrecision.Round(Math.Sqrt(
                (dLPrime / (kL * sL)) * (dLPrime / (kL * sL)) +
                (dCPrime / (kC * sC)) * (dCPrime / (kC * sC)) +
                (dHPrime / (kH * sH)) * (dHPrime / (kH * sH)) +
                (dCPrime / (kC * sC)) * (dHPrime / (kH * sH)) * rT
            )),
                DA = NumericPrecision.Round(sample.CIEA - standard.CIEA),
                DB = NumericPrecision.Round(sample.CIEB - standard.CIEB),
                DC = NumericPrecision.Round(c2 - c1),
                DL = NumericPrecision.Round(sample.CIEL - standard.CIEL),
                DH = NumericPrecision.Round(h2Prime - h1Prime),
                DeltaConly = NumericPrecision.Round(Math.Abs(dCPrime / (kC * sC))),
                DeltaHonly = NumericPrecision.Round(Math.Abs(dHPrime / (kH * sH))),
                DeltaLonly = NumericPrecision.Round(Math.Abs(dLPrime / (kL * sL))),

                
            };

            return results;
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
            double deltaH = p * q * Math.Sqrt(Math.Max(0.0, deltaELab_square - Math.Pow(deltaL, 2) - Math.Pow(deltaC, 2)));

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

            double delteEcmc = NumericPrecision.Round(Math.Sqrt(Math.Pow(deltaL / (pl * S_L), 2) + Math.Pow(deltaC / (pc * S_C), 2) + Math.Pow(deltaH / S_H, 2)));

            return delteEcmc;
        }

    }
}
