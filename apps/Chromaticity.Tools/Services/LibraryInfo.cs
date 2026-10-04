using ChromaticityDotNet.Controller;

namespace Chromaticity.Tools.Services;

public static class LibraryInfo
{
    // Read the referenced calculation assembly, never the UI application's version.
    public static string Version => typeof(ChromaticityConversion).Assembly.GetName().Version!.ToString();
}
