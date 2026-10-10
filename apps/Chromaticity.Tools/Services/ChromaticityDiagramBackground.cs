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

    public static byte[] Create(StandardObserver observer, ChromaticityDiagramSpace space = ChromaticityDiagramSpace.Xy)
    {
        var projection = new ChromaticityDiagramProjection(space);
        var boundary = CieSpectralData.GetChromaticityBoundary(observer).Select(point => projection.Project(point.X, point.Y)).ToArray();
        int height = projection.Height;
        var pixels = new byte[Width * height * 4];
        for (int row = 0; row < height; row++)
        {
            double y = projection.MaxY * (1 - (row + 0.5) / height);
            var intersections = new List<double>();
            for (int i = 0; i < boundary.Length; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Length];
                if ((a.Y > y) != (b.Y > y))
                    intersections.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }
            intersections.Sort();
            for (int pair = 0; pair + 1 < intersections.Count; pair += 2)
            {
                int first = Math.Max(0, (int)Math.Ceiling(intersections[pair] / projection.MaxX * Width - 0.5));
                int last = Math.Min(Width - 1, (int)Math.Floor(intersections[pair + 1] / projection.MaxX * Width - 0.5));
                for (int column = first; column <= last; column++)
                {
                    double x = projection.MaxX * (column + 0.5) / Width;
                    var xy = projection.ToXy(x, y);
                    // Normalize the clipped screen RGB for visibility, as in WavelengthColors.
                    // This background is illustrative and does not feed into the wavelength API.
                    var rgb = ChromaticityConversion.XYZToRGB(new CIEXYZ
                    {
                        CIEX = xy.X * 100, CIEY = xy.Y * 100, CIEZ = (1 - xy.X - xy.Y) * 100
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
