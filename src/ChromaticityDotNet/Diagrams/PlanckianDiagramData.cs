using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Sample xyY and its nearest Planckian point in CIE 1960 UCS, before diagram projection.</summary>
public sealed class PlanckianDiagramData
{
    public ChromaticityCoordinates Sample { get; }
    public StandardObserver Observer { get; }
    public Cct1960Result Cct { get; }

    private PlanckianDiagramData(ChromaticityCoordinates sample, StandardObserver observer, Cct1960Result cct)
    { Sample = sample; Observer = observer; Cct = cct; }

    public static PlanckianDiagramData Create(ChromaticityCoordinates sample, StandardObserver observer)
    {
        DiagramValidation.Observer(observer);
        return new(sample, observer, ChromaticityConversion.luv1960ToCCTResult(
            ChromaticityConversion.xyToLuv1960(sample.ToXyY()), observer));
    }
}
