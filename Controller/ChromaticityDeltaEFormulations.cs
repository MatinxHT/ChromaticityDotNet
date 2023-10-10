using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Controller
{
    /// <summary>
    /// CIE de formulations
    /// </summary>
    public class ChromaticityDeltaEFormulations
    {
        public enum DeltaEType
        {
            DeltaE1976,
            DeltaE1994,
            DeltaE2000,
            DeltaEcmc
        }
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
        public static double DeltaE1994(CIELABCH standard, CIELABCH sample)
        {
            double deltae = 0;
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
        public static double DeltaE2000(CIELABCH standard, CIELABCH sample, double kL, double kC, double kH)
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
