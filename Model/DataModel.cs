using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;
using static ChromaticityDotNet.ChromaticityDeltaEFormulations;

namespace ChromaticityDotNet.Model
{
    /// <summary>
    /// DataClass
    /// </summary>
    public class DataModel
    {
        #region SPD

        public class StandardWhitePoint
        {
            public CIEXYZ? WhitePointXnYnZn { get; set; }

            public StandardObserver Observer { get; set; }
        }

        public class Spectrum
        {
            public int StartingWavelength { get; set; }
            public int WavelengthInterval { get; set; }
            public int EndingWavelength { get; set; }
            public double[]? Spectrums { get; set; }
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

        /// <summary>
        /// RGB data format as byte(0~255),in 'system.windows.media' u can use 'Color newColor = Color.FromRgb(redValue, greenValue, blueValue);'to bursh it
        /// </summary>
        public class CIERGB
        {
            public byte redValue { get; set; }
            public byte greenValue { get; set; }
            public byte blueValue { get; set; }
        }

        #endregion
    }

}
