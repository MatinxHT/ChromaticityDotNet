using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaticityDotNet
{
    /// <summary>
    /// DataClass
    /// </summary>
    public class DataClass
    {
        #region SPD

        public class StandardWhitePoint
        {
            public double Xn { get; set; }
            public double Yn { get; set; }
            public double Zn { get; set; }
        }

        public class Spectrum
        {
            public int StartingWavelength { get; set; }
            public int WavelengthInterval { get; set; }
            public int EndingWavelength { get; set; }
            public double[]? spectrums { get; set; }
        }

        #endregion

        #region CIE Colro Data
        public class CIEXYZ
        {
            public double CIEX { get; set; }
            public double CIEY { get; set; }
            public double CIEZ { get; set; }
        }

        public class CIELABCH
        {
            public double CIEL { get; set; }
            public double CIEA { get; set; }
            public double CIEB { get; set; }
            public double CIEC { get; set; }
            public double CIEH { get; set; }
        }

        public class CIExyY
        {
            public double CIEx { get; set; }
            public double CIEy { get; set; }
            public double CIEY { get; set; }
        }

        public class CIELuv
        {
            public double CIEL { get; set; }
            public double CIEu { get; set; }
            public double CIEv { get; set; }
        }

        public class CIERGB
        {
            public byte redValue { get; set;}
            public byte greenValue { get; set; }
            public byte blueValue { get; set; }
        }
        #endregion
    }

    /// <summary>
    /// Collection of Standard data
    /// </summary>
    public class StandardChromaticityClass
    {
        /// <summary>
        /// Store the Standard illuminant data
        /// </summary>
        public class StandardilluminantClass
        {
            /// <summary>
            /// class of Standard illuminant format
            /// </summary>
            public interface IStandardilluminant
            {
                Standardilluminant IlluminantName { get; }
                StandardObserver Observer { get; }
                DataClass.StandardWhitePoint whitePoint { get; }
                DataClass.Spectrum spectrum { get; }
            }

            public enum Standardilluminant
            {
                D65,
                A,
                CWF
            }

            public enum StandardObserver
            {
                Degree2,
                Degree10
            }

            public class D65_Degree10 : IStandardilluminant
            { 
                public Standardilluminant IlluminantName => Standardilluminant.D65;
                public StandardObserver Observer => StandardObserver.Degree10;
                public DataClass.StandardWhitePoint whitePoint => new DataClass.StandardWhitePoint() 
                {
                    Xn = 94.81,
                    Yn = 100.00,
                    Zn = 107.32 
                };
                public DataClass.Spectrum spectrum => new DataClass.Spectrum()
                {
                    StartingWavelength = 400,
                    WavelengthInterval = 10,
                    EndingWavelength = 700,
                    spectrums = new double[31]
                    {
                        82.7549, 91.486, 93.4318, 86.6823, 104.865, 117.008, 117.812, 114.861,
                        115.923, 108.811, 109.354, 107.802, 104.79, 107.689, 104.405, 104.046,
                        100.0, 96.3342, 95.788, 88.6856, 90.0062, 89.5991, 87.6987, 83.2886,
                        83.6992, 80.0268, 80.2146, 82.2778, 78.2842, 69.7213, 71.6091
                    }
                };
            }
            
            public class CWF_Degree10 : IStandardilluminant
            {
                public Standardilluminant IlluminantName => Standardilluminant.CWF;
                public StandardObserver Observer => StandardObserver.Degree10;
                public DataClass.StandardWhitePoint whitePoint => new DataClass.StandardWhitePoint()
                {
                    Xn = 103.25,
                    Yn = 100.00,
                    Zn = 68.99
                };
                public DataClass.Spectrum spectrum => new DataClass.Spectrum()
                {
                    StartingWavelength = 400,
                    WavelengthInterval = 10,
                    EndingWavelength = 700,
                    spectrums = new double[31]
                    {
                        03.44,03.85,04.19,5.06,11.81,06.63,07.19,07.54,07.65,07.62,
                        07.28, 07.05, 07.16,08.04,10.01,16.64,16.16,18.62,22.79,18.66,
                        16.54,13.80,10.95,08.40,06.31,04.68,03.45,02.55,01.89,01.53,01.10
                    }
                };
            }

            public class A_Degree10 : IStandardilluminant
            {
                public Standardilluminant IlluminantName => Standardilluminant.A;
                public StandardObserver Observer => StandardObserver.Degree10;
                public DataClass.StandardWhitePoint whitePoint => new DataClass.StandardWhitePoint()
                {
                    Xn = 111.14,
                    Yn = 100.00,
                    Zn = 35.2
                };
                public DataClass.Spectrum spectrum => new DataClass.Spectrum()
                {
                    StartingWavelength = 400,
                    WavelengthInterval = 10,
                    EndingWavelength = 700,
                    spectrums = new double[31]
                    {
                        14.708,17.6753,20.995,24.2873,28.7027,33.0859,37.8121,42.8693,48.2423,
                        53.9132,59.8511,66.0635,72.4959,79.1326,85.947,92.912,100,107.184,114.436,
                        121.731,129.043,136.346,143.618,150.836,157.979,165.028,171.963,178.769,
                        185.429,191.931,198.261
                    }
                };
            }

        }

        public class CIEConstant
        {
            public static DataClass.Spectrum XX_10 => new DataClass.Spectrum()
            {
                StartingWavelength = 400,
                WavelengthInterval = 10,
                EndingWavelength = 700,
                spectrums = new double[31]
                {
                    0.0191, 0.0847, 0.2045, 0.3147, 0.3837, 0.3707, 0.3023,0.1956, 0.0805, 0.0162, 0.0038, 0.037465, 0.117749,
                    0.236491, 0.376772, 0.529826, 0.705224, 0.878655,1.01416, 1.11852, 1.12399, 1.03048, 0.856297,
                    0.647467, 0.431567, 0.268329, 0.152568, 0.0812606,
                    0.0408508, 0.0199413, 0.00957688
                }
            };

            public static DataClass.Spectrum YY_10 => new DataClass.Spectrum()
            {
                StartingWavelength = 400,
                WavelengthInterval = 10,
                EndingWavelength = 700,
                spectrums = new double[31]
                {
                    0.0020044, 0.008756, 0.021391, 0.038676, 0.062077,0.089456, 0.128201, 0.18519, 0.253589, 0.339133,
                    0.460777, 0.606741, 0.761757, 0.875211, 0.961988,0.991761, 0.99734, 0.955552, 0.868934, 0.777405,
                    0.658341, 0.527963, 0.398057, 0.283493, 0.179828,0.107633, 0.060281, 0.0318004, 0.0159051, 0.0077488,0.00371774
                }
            };

            public static DataClass.Spectrum ZZ_10 => new DataClass.Spectrum()
            {
                StartingWavelength = 400,
                WavelengthInterval = 10,
                EndingWavelength = 700,
                spectrums = new double[31]
                {
                    0.0860109, 0.389366, 0.972542, 1.55348, 1.96728,1.9948, 1.74537, 1.31756, 0.772125, 0.415254,
                    0.218502, 0.112044, 0.060709, 0.030451, 0.013676,0.003988, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
                    0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
                }
            };

            public static double[] Wavelength_CIE = new double[]
            {
                380, 385, 390, 395, 400, 405, 410, 415, 420, 425,
                430, 435, 440, 445, 450, 455, 460, 465, 470, 475,
                480, 485, 490, 495, 500, 505, 510, 513, 515, 518,
                519, 520, 522, 525, 530, 535, 540, 545, 550, 555,
                560, 565, 570, 575, 580, 585, 590, 595, 600, 605,
                610, 615, 620, 625, 630, 635, 640, 645, 650, 655,
                660, 665, 670, 675, 680, 685, 690, 695, 700, 705,
                710, 715, 720, 725, 730, 735, 740, 745, 750, 755,
                760, 765, 770, 775, 780, 380
            };

            public static double[] x_CIE = new double[]
            {
            0.1741, 0.1740, 0.1738, 0.1736, 0.1733, 0.1730, 0.1726, 0.1721, 0.1714, 0.1703,
            0.1689, 0.1669, 0.1644, 0.1611, 0.1566, 0.1510, 0.1440, 0.1355, 0.1241, 0.1096,
            0.0913, 0.0687, 0.0454, 0.0235, 0.0082, 0.0039, 0.0139, 0.0229, 0.0389, 0.0491,
            0.0591, 0.0743, 0.0926, 0.1142, 0.1547, 0.1929, 0.2296, 0.2658, 0.3016, 0.3373,
            0.3731, 0.4087, 0.4441, 0.4788, 0.5125, 0.5449, 0.5752, 0.6029, 0.6270, 0.6482,
            0.6658, 0.6801, 0.6915, 0.7001, 0.7079, 0.7140, 0.7190, 0.7230, 0.7260, 0.7283,
            0.7300, 0.7311, 0.7320, 0.7327, 0.7334, 0.7340, 0.7344, 0.7346, 0.7347, 0.7347,
            0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347,
            0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.1741
            };

            public static double[] y_CIE = new double[]
            {
            0.0050, 0.0050, 0.0049, 0.0049, 0.0048, 0.0048, 0.0048, 0.0048, 0.0051, 0.0058,
            0.0069, 0.0086, 0.0109, 0.0138, 0.0177, 0.0227, 0.0297, 0.0399, 0.0578, 0.0868,
            0.1327, 0.2007, 0.2950, 0.4127, 0.5384, 0.6548, 0.7502, 0.7831, 0.8120, 0.8245,
            0.8305, 0.8338, 0.8321, 0.8262, 0.8059, 0.7816, 0.7543, 0.7243, 0.6923, 0.6589,
            0.6245, 0.5896, 0.5547, 0.5202, 0.4866, 0.4544, 0.4242, 0.3965, 0.3725, 0.3514,
            0.3340, 0.3197, 0.3083, 0.2993, 0.2920, 0.2859, 0.2809, 0.2770, 0.2740, 0.2717,
            0.2700, 0.2689, 0.2680, 0.2673, 0.2666, 0.2660, 0.2656, 0.2654, 0.2653, 0.2653,
            0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653,
            0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.0050
            };

            public static double[] CCT_CCT = new double[]
            {
            1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900,
            2000, 2100, 2200, 2300, 2400, 2500, 2600, 2700, 2800, 2900,
            3000, 3100, 3200, 3300, 3400, 3500, 3600, 3700, 3800, 3900,
            4000, 4100, 4200, 4300, 4400, 4500, 4600, 4700, 4800, 4900,
            5000, 5100, 5200, 5300, 5400, 5500, 5600, 5700, 5800, 5900,
            6000, 6100, 6200, 6300, 6400, 6500, 6600, 6700, 6800, 6900,
            7000, 7100, 7200, 7300, 7400, 7500, 7600, 7700, 7800, 7900,
            8000, 8100, 8200, 8300, 8400, 8500, 8600, 8700, 8800, 8900,
            9000, 9100, 9200, 9300, 9400, 9500, 9600, 9700, 9800, 9900,
            10000, 10100, 10200, 10300, 10400, 10500, 10600, 10700, 10800, 10900,
            11000, 11100, 11200, 11300, 11400, 11500, 11600, 11700, 11800, 11900,
            12000, 12100, 12200, 12300, 12400, 12500, 12600, 12700, 12800, 12900,
            13000, 13100, 13200, 13300, 13400, 13500, 13600, 13700, 13800, 13900,
            14000, 14100, 14200, 14300, 14400, 14500, 14600, 14700, 14800, 14900,
            15000, 15100, 15200, 15300, 15400, 15500, 15600, 15700, 15800, 15900,
            16000, 16100, 16200, 16300, 16400, 16500, 16600, 16700, 16800, 16900,
            17000, 17100, 17200, 17300, 17400, 17500, 17600, 17700, 17800, 17900,
            18000, 18100, 18200, 18300, 18400, 18500, 18600, 18700, 18800, 18900,
            19000, 19100, 19200, 19300, 19400, 19500, 19600, 19700, 19800, 19900,
            20000

            };

            public static double[] x_CCT = new double[]
            {
            0.6499, 0.6361, 0.6226, 0.6095, 0.5966, 0.5841, 0.572, 0.5601, 0.5486, 0.5375,
            0.5267, 0.5162, 0.5062, 0.4965, 0.4872, 0.4782, 0.4696, 0.4614, 0.4535, 0.446,
            0.4388, 0.432, 0.4254, 0.4192, 0.4132, 0.4075, 0.4021, 0.3969, 0.3919, 0.3872,
            0.3827, 0.3784, 0.3743, 0.3704, 0.3666, 0.3631, 0.3596, 0.3563, 0.3532, 0.3502,
            0.3473, 0.3446, 0.3419, 0.3394, 0.3369, 0.3346, 0.3323, 0.3302, 0.3281, 0.3261,
            0.3242, 0.3223, 0.3205, 0.3188, 0.3171, 0.3155, 0.314, 0.3125, 0.311, 0.3097,
            0.3083, 0.307, 0.3058, 0.3045, 0.3034, 0.3022, 0.3011, 0.3, 0.299, 0.298,
            0.297, 0.2961, 0.2952, 0.2943, 0.2934, 0.2926, 0.2917, 0.291, 0.2902, 0.2894,
            0.2887, 0.288, 0.2873, 0.2866, 0.286, 0.2853, 0.2847, 0.2841, 0.2835, 0.2829,
            0.2824, 0.2818, 0.2813, 0.2807, 0.2802, 0.2797, 0.2792, 0.2788, 0.2783, 0.2778,
            0.2774, 0.277, 0.2765, 0.2761, 0.2757, 0.2753, 0.2749, 0.2745, 0.2742, 0.2738,
            0.2734, 0.2731, 0.2727, 0.2724, 0.2721, 0.2717, 0.2714, 0.2711, 0.2708, 0.2705,
            0.2702, 0.2699, 0.2696, 0.2694, 0.2691, 0.2688, 0.2686, 0.2683, 0.268, 0.2678,
            0.2675, 0.2673, 0.2671, 0.2668, 0.2666, 0.2664, 0.2662, 0.2659, 0.2657, 0.2655,
            0.2653, 0.2651, 0.2649, 0.2647, 0.2645, 0.2643, 0.2641, 0.2639, 0.2638, 0.2636,
            0.2634, 0.2632, 0.2631, 0.2629, 0.2627, 0.2626, 0.2624, 0.2622, 0.2621, 0.2619,
            0.2618, 0.2616, 0.2615, 0.2613, 0.2612, 0.261, 0.2609, 0.2608, 0.2606, 0.2605,
            0.2604, 0.2602, 0.2601, 0.26, 0.2598, 0.2597, 0.2596, 0.2595, 0.2593, 0.2592,
            0.2591, 0.259, 0.2589, 0.2588, 0.2587, 0.2586, 0.2584, 0.2583, 0.2582, 0.2581,
            0.258
            };

            public static double[] y_CCT = new double[]
            {
            0.3474,0.3594,0.3703,0.3801,0.3887,0.3962,0.4025,0.4076,0.4118,0.415,
            0.4173,0.4188,0.4196,0.4198,0.4194,0.4186,0.4173,0.4158,0.4139,0.4118,
            0.4095,0.407,0.4044,0.4018,0.399,0.3962,0.3934,0.3905,0.3877,0.3849,
            0.382,0.3793,0.3765,0.3738,0.3711,0.3685,0.3659,0.3634,0.3609,0.3585,
            0.3561,0.3538,0.3516,0.3494,0.3472,0.3451,0.3431,0.3411,0.3392,0.3373,
            0.3355,0.3337,0.3319,0.3302,0.3286,0.327,0.3254,0.3238,0.3224,0.3209,
            0.3195,0.3181,0.3168,0.3154,0.3142,0.3129,0.3117,0.3105,0.3094,0.3082,
            0.3071,0.3061,0.305,0.304,0.303,0.302,0.3011,0.3001,0.2992,0.2983,
            0.2975,0.2966,0.2958,0.295,0.2942,0.2934,0.2927,0.2919,0.2912,0.2905,
            0.2898,0.2891,0.2884,0.2878,0.2871,0.2865,0.2859,0.2853,0.2847,0.2841,
            0.2836,0.283,0.2825,0.2819,0.2814,0.2809,0.2804,0.2799,0.2794,0.2789,
            0.2785,0.278,0.2776,0.2771,0.2767,0.2763,0.2758,0.2754,0.275,0.2746,
            0.2742,0.2738,0.2735,0.2731,0.2727,0.2724,0.272,0.2717,0.2713,0.271,
            0.2707,0.2703,0.27,0.2697,0.2694,0.2691,0.2688,0.2685,0.2682,0.2679,
            0.2676,0.2673,0.2671,0.2668,0.2665,0.2663,0.266,0.2657,0.2655,0.2652,
            0.265,0.2648,0.2645,0.2643,0.2641,0.2638,0.2636,0.2634,0.2632,0.2629,
            0.2627,0.2625,0.2623,0.2621,0.2619,0.2617,0.2615,0.2613,0.2611,0.2609,
            0.2607,0.2606,0.2604,0.2602,0.26,0.2598,0.2597,0.2595,0.2593,0.2592,
            0.259,0.2588,0.2587,0.2585,0.2584,0.2582,0.258,0.2579,0.2577,0.2576,
            0.2574
            };
        }

        /// <summary>
        /// Standard color chips data
        /// </summary>
        public class ColorChips
        {
            /// <summary>
            /// ColorFastnessChinaStandard
            /// </summary>
            public enum ColorFastnessChinaStandard
            {
                /// <summary>
                /// 纺织品色牢度试验评定变色用灰色样卡标准，采用欧氏距离色差法
                /// </summary>
                GBT250,
                /// <summary>
                /// 纺织品色牢度试验评定沾色用灰色样卡标准，采用欧氏距离色差法
                /// </summary>
                GBT251,
            }

            /// <summary>
            /// 色卡数据的接口
            /// </summary>
            public interface IColorStandard
            {
                string StandardName { get; }
                string[] FastnessRank { get; }
                double[] DeltaE { get; }
                double[] DeltaEOffset { get; }
                string DeltaEFormula { get; }
                int GBVersion { get; }
            }

            public class GBT250Info : IColorStandard
            {
                public string StandardName => "纺织品色牢度试验评定变色用灰色样卡标准";
                public string[] FastnessRank { get; } = { "5", "4-5", "4", "3-4", "3", "2-3", "2", "1-2", "1" };
                public double[] DeltaE { get; } = { 0, 0.8, 1.7, 2.5, 3.4, 4.8, 6.8, 9.6, 13.6 };
                public double[] DeltaEOffset { get; } = { 0.2, 0.2, 0.3, 0.35, 0.4, 0.5, 0.6, 0.7, 1.0 };
                public string DeltaEFormula => "CIE1976";
                public int GBVersion => 2008;
            }

            public class GBT251Info : IColorStandard
            {
                public string StandardName => "纺织品色牢度试验评定沾色用灰色样卡标准";
                public string[] FastnessRank { get; } = { "5", "4-5", "4", "3-4", "3", "2-3", "2", "1-2", "1" };
                public double[] DeltaE { get; } = { 0, 2.2, 4.3, 6.0, 8.5, 12.0, 16.9, 24.0, 34.1 };
                public double[] DeltaEOffset { get; } = { 0.2, 0.3, 0.3, 0.4, 0.5, 0.7, 1.0, 1.5, 2.0 };
                public string DeltaEFormula => "CIE1976";
                public int GBVersion => 2008;
            }
        }
    }

}
