using System.Reflection;

namespace ChromaticityDotNet
{
    /// <summary>
    /// Information of this DLL
    public class ChromaticityDotNetCore
    {
        /// <summary>
        /// DLL Version
        /// </summary>
        public static string Version = GetCoreVersion();

        private static string GetCoreVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

    } 
}