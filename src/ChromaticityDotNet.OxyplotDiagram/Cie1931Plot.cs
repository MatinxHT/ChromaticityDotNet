using ChromaticityDotNet.Diagrams;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.OxyplotDiagram;

/// <summary>CIE xy plot, with observer-specific boundary and Planckian locus.</summary>
public sealed class Cie1931Plot : ChromaticityPlot
{
    public Cie1931Plot(StandardObserver observer = StandardObserver.Degree2) : base(ChromaticityDiagramSpace.Xy, observer) { }
    public void UpdatePoint(CIExyY point) => UpdateXyPoints(new[] { point });
    public void UpdatePoints(IEnumerable<CIExyY> points) => UpdateXyPoints(points);
    public void UpdateWavelengthMeasurement(CIExyY sample, StandardObserver observer, Standardilluminant illuminant) =>
        SetData(WavelengthDiagramData.Create(sample, observer, illuminant));
    public void Reset() => UpdatePoint(new CIExyY { CIEx = 0.35, CIEy = 0.35, CIEY = 1 });
}
