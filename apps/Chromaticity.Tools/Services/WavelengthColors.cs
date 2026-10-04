using Avalonia.Media;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

/// <summary>Display-only wavelength colors from the CIE 1931 observer, mapped into
/// the sRGB gamut and normalized for visibility. These colors do not change spectra.</summary>
public static class WavelengthColors
{
    public const int Start = 380;
    public const int End = 780;
    private static readonly Color[] Colors = CreateColors();

    public static Color At(int wavelength) => wavelength is >= Start and <= End
        ? Colors[wavelength - Start] : Avalonia.Media.Colors.Transparent;

    private static Color[] CreateColors()
    {
        var (x, y, z) = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);
        return Enumerable.Range(Start, End - Start + 1).Select(wavelength =>
        {
            var i = wavelength - x.StartingWavelength;
            var sum = x.Spectrums![i] + y.Spectrums![i] + z.Spectrums![i];
            var rgb = ChromaticityConversion.XYZ2RGB(new CIEXYZ
            {
                CIEX = x.Spectrums[i] / sum * 100,
                CIEY = y.Spectrums[i] / sum * 100,
                CIEZ = z.Spectrums[i] / sum * 100
            });
            var max = Math.Max(rgb.redValue, Math.Max(rgb.greenValue, rgb.blueValue));
            byte Channel(byte value) => (byte)Math.Round(value * 255.0 / max);
            return Color.FromRgb(Channel(rgb.redValue), Channel(rgb.greenValue), Channel(rgb.blueValue));
        }).ToArray();
    }
}
