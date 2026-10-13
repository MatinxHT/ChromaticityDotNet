using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Display-only CIE 1931 wavelength colors, clipped to sRGB and normalized for visibility.</summary>
public static class WavelengthColors
{
    public const int Start = 380;
    public const int End = 780;
    private static readonly DiagramColor[] Colors = CreateColors();
    public static DiagramColor At(int wavelength) => wavelength >= Start && wavelength <= End
        ? Colors[wavelength - Start] : new(0, 0, 0, 0);

    private static DiagramColor[] CreateColors()
    {
        var (x, y, z) = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);
        return Enumerable.Range(Start, End - Start + 1).Select(wavelength =>
        {
            int i = wavelength - x.StartingWavelength;
            double sum = x.Spectrums![i] + y.Spectrums![i] + z.Spectrums![i];
            var rgb = ChromaticityConversion.XYZToRGB(new CIEXYZ
            {
                CIEX = x.Spectrums[i] / sum * 100, CIEY = y.Spectrums[i] / sum * 100, CIEZ = z.Spectrums[i] / sum * 100
            });
            double max = Math.Max(rgb.redValue, Math.Max(rgb.greenValue, rgb.blueValue));
            if (max == 0) return new DiagramColor(0, 0, 0);
            byte Channel(byte value) => (byte)Math.Round(value * 255.0 / max);
            return new DiagramColor(Channel(rgb.redValue), Channel(rgb.greenValue), Channel(rgb.blueValue));
        }).ToArray();
    }
}
