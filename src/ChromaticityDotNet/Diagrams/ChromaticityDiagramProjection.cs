using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Diagrams;

public enum ChromaticityDiagramSpace { Xy, UvPrime }

/// <summary>Unrounded xy / CIE 1976 u'v' projection. Both diagrams use the same physical xy boundary.</summary>
public sealed class ChromaticityDiagramProjection
{
    public ChromaticityDiagramSpace Space { get; }
    public double MaxX => Space == ChromaticityDiagramSpace.Xy ? 0.9 : 0.7;
    public double MaxY => MaxX;
    public int Height => ChromaticityDiagramBackground.Width;

    public ChromaticityDiagramProjection(ChromaticityDiagramSpace space)
    {
        if (space != ChromaticityDiagramSpace.Xy && space != ChromaticityDiagramSpace.UvPrime)
            throw new ArgumentOutOfRangeException(nameof(space));
        Space = space;
    }

    public ChromaticityDiagramPoint Project(double x, double y)
    {
        DiagramValidation.Finite(x, nameof(x)); DiagramValidation.Finite(y, nameof(y));
        if (Space == ChromaticityDiagramSpace.Xy) return new(x, y);
        double denominator = -2.0 * x + 12.0 * y + 3.0;
        return FinitePoint(4.0 * x / denominator, 9.0 * y / denominator);
    }

    public ChromaticityDiagramPoint ToXy(double x, double y)
    {
        DiagramValidation.Finite(x, nameof(x)); DiagramValidation.Finite(y, nameof(y));
        if (Space == ChromaticityDiagramSpace.Xy) return new(x, y);
        double denominator = 6.0 * x - 16.0 * y + 12.0;
        return FinitePoint(9.0 * x / denominator, 4.0 * y / denominator);
    }

    private static ChromaticityDiagramPoint FinitePoint(double x, double y)
    {
        if (!DiagramValidation.IsFinite(x) || !DiagramValidation.IsFinite(y))
            throw new ArgumentOutOfRangeException("coordinates", "The projection is undefined at these coordinates.");
        return new(x, y);
    }

    public IReadOnlyList<ChromaticityDiagramPoint> GetBoundary(StandardObserver observer) =>
        Array.AsReadOnly(CieSpectralData.GetChromaticityBoundary(observer).Select(p => Project(p.X, p.Y)).ToArray());

    public IReadOnlyList<ChromaticityDiagramPoint> GetPlanckianLocus(StandardObserver observer) =>
        Array.AsReadOnly(ChromaticityConversion.GetPlanckianLocus(observer).Select(p => Project(p.X, p.Y)).ToArray());

    /// <summary>Intersect both rays in xy, then project their endpoints. No wavelength interpolation in u'v'.</summary>
    public (ChromaticityDiagramPoint Forward, ChromaticityDiagramPoint Reverse)? Intersections(WavelengthDiagramData data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (data.Wavelengths.IsAchromatic) return null;
        var boundary = CieSpectralData.GetChromaticityBoundary(data.Observer);
        double wx = data.White.X, wy = data.White.Y;
        double dx = data.Sample.X - wx, dy = data.Sample.Y - wy;
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
            if (!DiagramValidation.IsFinite(nearest)) throw new ArgumentException("The ray does not intersect the chromaticity boundary.", nameof(data));
            return Project(wx + direction * nearest * dx, wy + direction * nearest * dy);
        }
        return (Hit(1), Hit(-1));
    }
}
