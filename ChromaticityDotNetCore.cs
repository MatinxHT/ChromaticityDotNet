using System;
//using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml.Linq;

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