using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

/// <summary>Display-only sRGB approximation, masked by the same physical gamut used by the API.</summary>
public static class ChromaticityDiagramBackground
{
    public const double MaxX = 0.8;
    public const double MaxY = 0.9;
    public const int Width = 400;
    public const int Height = 450;

    public static byte[] Create(StandardObserver observer)
    {
        var boundary = CieSpectralData.GetChromaticityBoundary(observer);
        var pixels = new byte[Width * Height * 4];
        for (int row = 0; row < Height; row++)
        {
            double y = MaxY * (1 - (row + 0.5) / Height);
            var intersections = new List<double>();
            for (int i = 0; i < boundary.Count; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count];
                if ((a.Y > y) != (b.Y > y))
                    intersections.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }
            intersections.Sort();
            for (int pair = 0; pair + 1 < intersections.Count; pair += 2)
            {
                int first = Math.Max(0, (int)Math.Ceiling(intersections[pair] / MaxX * Width - 0.5));
                int last = Math.Min(Width - 1, (int)Math.Floor(intersections[pair + 1] / MaxX * Width - 0.5));
                for (int column = first; column <= last; column++)
                {
                    double x = MaxX * (column + 0.5) / Width;
                    // Normalize the clipped screen RGB for visibility, as in WavelengthColors.
                    // This background is illustrative and does not feed into the wavelength API.
                    var rgb = ChromaticityConversion.XYZToRGB(new CIEXYZ
                    {
                        CIEX = x * 100, CIEY = y * 100, CIEZ = (1 - x - y) * 100
                    });
                    double maximum = Math.Max(rgb.redValue, Math.Max(rgb.greenValue, rgb.blueValue));
                    int offset = (row * Width + column) * 4;
                    pixels[offset] = (byte)Math.Round(rgb.redValue * 255.0 / maximum);
                    pixels[offset + 1] = (byte)Math.Round(rgb.greenValue * 255.0 / maximum);
                    pixels[offset + 2] = (byte)Math.Round(rgb.blueValue * 255.0 / maximum);
                    pixels[offset + 3] = 255;
                }
            }
        }
        return pixels;
    }
}
