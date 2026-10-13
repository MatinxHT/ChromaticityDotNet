using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Screen previews for the L* axis and fixed-lightness C*/h° plane. They do not affect measurements.</summary>
public static class LchDiagramBackground
{
    public const double DefaultLightness = 70;

    /// <summary>Top-to-bottom RGBA8888 pixels, transparent outside the chroma circle.
    /// Default preview uses the library's integrated D65 / 10° white, with sRGB gamut clipping.</summary>
    public static byte[] Create(double chromaMaximum, int width = 256, double lightness = DefaultLightness,
        string illuminant = "D65", StandardObserver observer = StandardObserver.Degree10)
    {
        DiagramValidation.Finite(chromaMaximum, nameof(chromaMaximum));
        DiagramValidation.Finite(lightness, nameof(lightness));
        if (chromaMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(chromaMaximum));
        if (width <= 0 || width > 4096) throw new ArgumentOutOfRangeException(nameof(width));
        if (lightness < 0 || lightness > 100) throw new ArgumentOutOfRangeException(nameof(lightness));
        var white = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        var pixels = new byte[width * width * 4];
        for (int y = 0; y < width; y++)
        for (int x = 0; x < width; x++)
        {
            double a = 2 * (x + 0.5) / width - 1, b = 1 - 2 * (y + 0.5) / width;
            if (a * a + b * b > 1) continue;
            var rgb = ChromaticityConversion.XYZToRGB(ChromaticityConversion.LabToXYZ(
                new CIELAB(lightness, a * chromaMaximum, b * chromaMaximum), white));
            int offset = (y * width + x) * 4;
            pixels[offset] = rgb.redValue; pixels[offset + 1] = rgb.greenValue;
            pixels[offset + 2] = rgb.blueValue; pixels[offset + 3] = 255;
        }
        return pixels;
    }

    public static DiagramColor NeutralColor(double lightness)
    {
        DiagramValidation.Finite(lightness, nameof(lightness));
        double fy = (Math.Max(0, Math.Min(100, lightness)) + 16) / 116;
        double y = fy > 6d / 29 ? fy * fy * fy : 3 * Math.Pow(6d / 29, 2) * (fy - 4d / 29);
        double srgb = y <= 0.0031308 ? 12.92 * y : 1.055 * Math.Pow(y, 1d / 2.4) - 0.055;
        byte channel = (byte)Math.Max(0, Math.Min(255, Math.Round(srgb * 255)));
        return new(channel, channel, channel);
    }
}
