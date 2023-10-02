using static ChromaticityDotNet.DataClass;
using static ChromaticityDotNet.StandardChromaticityClass;

namespace ChromaticityDotNet
{
    public class ChromaticityDotNetCore
    {
        public string Version = "1.0.0.0";

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
    }
}