using Avalonia.Media;

namespace Chromaticity.Tools.Services;

/// <summary>Display-only wavelength colors from the CIE 1931 observer, mapped into
/// the sRGB gamut and normalized for visibility. These colors do not change spectra.</summary>
public static class WavelengthColors
{
    public const int Start = ChromaticityDotNet.Diagrams.WavelengthColors.Start;
    public const int End = ChromaticityDotNet.Diagrams.WavelengthColors.End;
    public static Color At(int wavelength)
    {
        var color = ChromaticityDotNet.Diagrams.WavelengthColors.At(wavelength);
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
