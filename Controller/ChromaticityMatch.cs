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
    /// For matching standard color data  or .. (will be update...)
    /// </summary>
    public class ChromaticityMatch
    {
        public static IStandardilluminant GetStandardilluminantdata(Standardilluminant illuminant)
        {
            IStandardilluminant Standardilluminantdata;
            switch (illuminant)
            {
                case (Standardilluminant.D65):
                    Standardilluminantdata = new D65();
                    return Standardilluminantdata;
                case (Standardilluminant.CWF):
                    Standardilluminantdata = new CWF();
                    return Standardilluminantdata;
                case (Standardilluminant.F7):
                    Standardilluminantdata = new F7();
                    return Standardilluminantdata;
                case (Standardilluminant.TL84):
                    Standardilluminantdata = new TL84();
                    return Standardilluminantdata;
                case (Standardilluminant.U30):
                    Standardilluminantdata = new U30();
                    return Standardilluminantdata;
                case (Standardilluminant.A):
                    Standardilluminantdata = new A();
                    return Standardilluminantdata;
                default:
                    Standardilluminantdata = new D65();
                    return Standardilluminantdata;
            }
        }

        /// <summary>
        /// Finding StandardWhitePoint in choosen illuminant and observer
        /// </summary>
        /// <param name="illuminant">Standard illuminant type</param>
        /// <param name="observer">Standard observer degree</param>
        /// <returns>StandardWhitePoint in choosen illuminant and observer </returns>
        public static CIEXYZ GetStandardWhitePoint(Standardilluminant illuminant, StandardObserver observer)
        {
            IStandardilluminant Standardilluminantdata;

            switch (observer)
            {
                case (StandardObserver.Degree10):
                    switch (illuminant)
                    {
                        case (Standardilluminant.D65):
                            Standardilluminantdata = new D65();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                        case (Standardilluminant.CWF):
                            Standardilluminantdata = new CWF();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                        case (Standardilluminant.F7):
                            Standardilluminantdata = new F7();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                        case (Standardilluminant.TL84):
                            Standardilluminantdata = new TL84();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                        case (Standardilluminant.U30):
                            Standardilluminantdata = new U30();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                        case (Standardilluminant.A):
                            Standardilluminantdata = new A();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                        default:
                            Standardilluminantdata = new D65();
                            return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
                    }
                case (StandardObserver.Degree2):
                    switch (illuminant)
                    {
                        case Standardilluminant.D65:
                            Standardilluminantdata = new D65();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                        case Standardilluminant.CWF:
                            Standardilluminantdata = new CWF();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                        case Standardilluminant.F7:
                            Standardilluminantdata = new F7();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                        case Standardilluminant.TL84:
                            Standardilluminantdata = new TL84();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                        case Standardilluminant.U30:
                            Standardilluminantdata = new U30();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                        case Standardilluminant.A:
                            Standardilluminantdata = new A();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                        default:
                            Standardilluminantdata = new D65();
                            return Standardilluminantdata.WhitePoint_Degree2.WhitePointXnYnZn;
                    }
                default:
                    Standardilluminantdata = new D65();
                    return Standardilluminantdata.WhitePoint_Degree10.WhitePointXnYnZn;
            }

        }
    }
}
