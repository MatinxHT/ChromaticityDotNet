using ChromaticityDotNet.Model;

namespace Chromaticity.Tools.Services;

public enum ChromaticityDiagramSpace { Xy, UvPrime }

public readonly record struct ChromaticityDiagramPoint(double X, double Y);

/// <summary>Unrounded diagram coordinates. Both diagrams share the API's xy physical boundary.</summary>
public sealed class ChromaticityDiagramProjection(ChromaticityDiagramSpace space)
{
    public double MaxX => space == ChromaticityDiagramSpace.Xy ? ChromaticityDiagramBackground.MaxX : 0.7;
    public double MaxY => space == ChromaticityDiagramSpace.Xy ? ChromaticityDiagramBackground.MaxY : 0.7;
    public int Height => (int)Math.Round(ChromaticityDiagramBackground.Width * MaxY / MaxX);

    public ChromaticityDiagramPoint Project(double x, double y)
    {
        if (space == ChromaticityDiagramSpace.Xy) return new(x, y);
        double denominator = -2.0 * x + 12.0 * y + 3.0;
        return new(4.0 * x / denominator, 9.0 * y / denominator);
    }

    public ChromaticityDiagramPoint ToXy(double x, double y)
    {
        if (space == ChromaticityDiagramSpace.Xy) return new(x, y);
        double denominator = 6.0 * x - 16.0 * y + 12.0;
        return new(9.0 * x / denominator, 4.0 * y / denominator);
    }

    /// <summary>Intersects both xy rays with the physical boundary, then projects their endpoints.
    /// Projection preserves the lines, without interpolating wavelengths again in u'v'.</summary>
    public (ChromaticityDiagramPoint Forward, ChromaticityDiagramPoint Reverse)? Intersections(WavelengthCalculation calculation)
    {
        if (calculation.Wavelengths.IsAchromatic) return null;
        var boundary = CieSpectralData.GetChromaticityBoundary(calculation.Observer);
        double wx = calculation.White.X, wy = calculation.White.Y;
        double dx = calculation.Sample.X - wx, dy = calculation.Sample.Y - wy;
        ChromaticityDiagramPoint Hit(double direction)
        {
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < boundary.Count; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count];
                double ex = b.X - a.X, ey = b.Y - a.Y;
                double cross = direction * (dx * ey - dy * ex);
                if (cross == 0) continue;
                double ax = a.X - wx, ay = a.Y - wy;
                double distance = (ax * ey - ay * ex) / cross;
                double fraction = direction * (ax * dy - ay * dx) / cross;
                if (distance > 0 && fraction >= -1e-10 && fraction <= 1 + 1e-10)
                    nearest = Math.Min(nearest, distance);
            }
            return Project(wx + direction * nearest * dx, wy + direction * nearest * dy);
        }
        return (Hit(1), Hit(-1));
    }
}
