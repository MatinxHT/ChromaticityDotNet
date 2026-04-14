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
            IStandardilluminant data = GetStandardilluminantdata(illuminant);

            switch (observer)
            {
                case StandardObserver.Degree2:
                    return data.WhitePoint_Degree2.WhitePointXnYnZn;
                case StandardObserver.Degree10:
                default:
                    return data.WhitePoint_Degree10.WhitePointXnYnZn;
            }
        }
    }
}
