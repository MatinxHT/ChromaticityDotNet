using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Controller
{
    /// <summary>
    /// For matching standard color data  or .. (will be update...)
    /// </summary>
    public class ChromaticityMatch
    {
        private static readonly IReadOnlyDictionary<Standardilluminant, IStandardilluminant> _illuminantRegistry =
            new Dictionary<Standardilluminant, IStandardilluminant>
            {
                [Standardilluminant.D65]  = new D65(),
                [Standardilluminant.A]    = new A(),
                [Standardilluminant.CWF]  = new CWF(),
                [Standardilluminant.F7]   = new F7(),
                [Standardilluminant.TL84] = new TL84(),
                [Standardilluminant.U30]  = new U30(),
            };

        public static IStandardilluminant GetStandardilluminantdata(Standardilluminant illuminant)
        {
            return _illuminantRegistry.TryGetValue(illuminant, out var data) ? data : _illuminantRegistry[Standardilluminant.D65];
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
