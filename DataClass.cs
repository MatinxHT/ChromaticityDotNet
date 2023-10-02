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
        public class StandardWhitePoint
        {
            public double CIEXn { get; set; }
            public double CIEYn { get; set; }
            public double CIEZn { get; set; }
        }
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

    }

    /// <summary>
    /// Collection of Standard data
    /// </summary>
    public class StandardChromaticityClass
    {
        /// <summary>
        /// 反射率换算时的计算所需光源条件单独存放于本类别当中
        /// </summary>
        public class StandardilluminantClass
        {
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



            /// <summary>
            /// D65 10度下的光源白点坐标
            /// </summary>
            public static double[] D65_10_WhitePoint = new double[] { 94.81, 100.00, 107.32 };
            public class D65
            {
                public class Degree10
                {
                    public static double Xn = 94.81;
                    public static double Yn = 100.00;
                    public static double Zn = 107.32;
                }

                public class Degree2
                {
                    public static double Xn = 95.04;
                    public static double Yn = 100.00;
                    public static double Zn = 108.88;
                }
            }

            /// <summary>
            /// CWF 10度下的光源白点坐标
            /// </summary>
            public static double[] CWF_10_WhitePoint = new double[] { 103.25, 100.00, 68.99 };
            /// <summary>
            /// A 10度下的光源白点坐标
            /// </summary>
            public static double[] A_10_WhitePoint = new double[] { 111.14, 100.00, 35.2 };

            /// <summary>
            /// 10°X值
            /// </summary>
            public static double[] xx_10 = new double[31]
            {
            0.0191, 0.0847, 0.2045, 0.3147, 0.3837, 0.3707, 0.3023,
            0.1956, 0.0805, 0.0162, 0.0038, 0.037465, 0.117749,
            0.236491, 0.376772, 0.529826, 0.705224, 0.878655,
            1.01416, 1.11852, 1.12399, 1.03048, 0.856297,
            0.647467, 0.431567, 0.268329, 0.152568, 0.0812606,
            0.0408508, 0.0199413, 0.00957688
            };

            /// <summary>
            /// 10°Y值
            /// </summary>
            public static double[] yy_10 = new double[31]
            {
            0.0020044, 0.008756, 0.021391, 0.038676, 0.062077,
            0.089456, 0.128201, 0.18519, 0.253589, 0.339133,
            0.460777, 0.606741, 0.761757, 0.875211, 0.961988,
            0.991761, 0.99734, 0.955552, 0.868934, 0.777405,
            0.658341, 0.527963, 0.398057, 0.283493, 0.179828,
            0.107633, 0.060281, 0.0318004, 0.0159051, 0.0077488,
            0.00371774
            };

            /// <summary>
            /// 10°Z值
            /// </summary>
            public static double[] zz_10 = new double[31]
            {
            0.0860109, 0.389366, 0.972542, 1.55348, 1.96728,
            1.9948, 1.74537, 1.31756, 0.772125, 0.415254,
            0.218502, 0.112044, 0.060709, 0.030451, 0.013676,
            0.003988, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
            0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
            };

            /// <summary>
            /// D65光源数据
            /// </summary>
            public static double[] D65Standardilluminant = new double[31]
            {
            82.7549, 91.486, 93.4318, 86.6823, 104.865, 117.008, 117.812, 114.861,
            115.923, 108.811, 109.354, 107.802, 104.79, 107.689, 104.405, 104.046,
            100.0, 96.3342, 95.788, 88.6856, 90.0062, 89.5991, 87.6987, 83.2886,
            83.6992, 80.0268, 80.2146, 82.2778, 78.2842, 69.7213, 71.6091
            };

            /// <summary>
            /// CWF光源数据
            /// </summary>
            public static double[] CWFStandardilluminant = new double[31]
            {
            03.44,03.85,04.19,5.06,11.81,06.63,07.19,07.54,07.65,07.62,
            07.28, 07.05, 07.16,08.04,10.01,16.64,16.16,18.62,22.79,18.66,
            16.54,13.80,10.95,08.40,06.31,04.68,03.45,02.55,01.89,01.53,01.10
            };

            public static double[] AStandardilluminant = new double[31]
            {
            14.708,17.6753,20.995,24.2873,28.7027,33.0859,37.8121,42.8693,48.2423,
            53.9132,59.8511,66.0635,72.4959,79.1326,85.947,92.912,100,107.184,114.436,
            121.731,129.043,136.346,143.618,150.836,157.979,165.028,171.963,178.769,
            185.429,191.931,198.261
            };

        }
    }

}
