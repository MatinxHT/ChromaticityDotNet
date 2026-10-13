using ChromaticityDotNet.Diagrams;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.OxyplotDiagram;

/// <summary>CIE 1976 UCS u'v' plot. Inputs are chromaticity coordinates, not L*u*v* components.</summary>
public sealed class Cie1976Plot : ChromaticityPlot
{
    public Cie1976Plot(StandardObserver observer = StandardObserver.Degree2) : base(ChromaticityDiagramSpace.UvPrime, observer) { }
    public void UpdatePoint(CIEuv point) => UpdatePoints(new[] { point });
    public void UpdatePoints(IEnumerable<CIEuv> points)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        UpdateProjectedPoints(points.Select(point => point is null ? throw new ArgumentException("A point cannot be null.", nameof(points))
            : new ChromaticityDiagramPoint(point.CIEu, point.CIEv)));
    }
    public void Reset() => UpdatePoint(new CIEuv { CIEu = 0.20, CIEv = 0.47 });
}
