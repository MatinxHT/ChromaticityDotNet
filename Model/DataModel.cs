using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

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

        /// <summary>
        /// CIELAB 1976 Lab color space.Chroma and Hue will be auto calculate.
        /// </summary>
        public class CIELABCH
        {
            private double _ciel;
            private double _ciea;
            private double _cieb;
            private double _ciec;
            private double _cieh;

            public double CIEL
            {
                get { return _ciel; }
                set { _ciel = value; }
            }
            public double CIEA
            {
                get { return _ciea; }
                set { _ciea = value; UpdateCH(); }
            }

            public double CIEB
            {
                get { return _cieb; }
                set { _cieb = value; UpdateCH(); }
            }

            public double CIEC
            {
                get { return _ciec; }
                private set { _ciec = value; }
            }

            public double CIEH
            {
                get { return _cieh; }
                private set { _cieh = value; }
            }


            // 构造函数，初始化属性
            public CIELABCH(double ciel, double ciea, double cieb)
            {
                CIEL = ciel;
                CIEA = ciea;
                CIEB = cieb;
            }

            public CIELABCH()
            {

            }

            private void UpdateCH()
            {
                _ciec = Math.Round(Math.Sqrt(_ciea * _ciea + _cieb * _cieb), 2);  // 计算色度
                _cieh = Math.Round(Math.Atan2(_cieb, _ciea) * (180 / Math.PI), 2); // 计算色调，转换为度
                if (_cieh < 0) _cieh += 360; // 确保色调为正值
            }
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

    public class ConfigEnum
    {
        public enum DeltaEType
        {
            DeltaE1976,
            DeltaE1994,
            DeltaE2000,
            DeltaEcmc
        }
    }

}
