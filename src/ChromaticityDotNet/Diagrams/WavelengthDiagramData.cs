using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Shared white/sample markers, wavelength rays and CCT data for xy and u'v' diagrams.</summary>
public sealed class WavelengthDiagramData
{
    public ChromaticityCoordinates Sample { get; }
    public ChromaticityCoordinates White { get; }
    public StandardObserver Observer { get; }
    public ChromaticityWavelengthResult Wavelengths { get; }
    public PlanckianDiagramData Planckian { get; }

    public WavelengthDiagramData(ChromaticityCoordinates sample, ChromaticityCoordinates white,
        StandardObserver observer, ChromaticityWavelengthResult wavelengths, PlanckianDiagramData planckian)
    {
        DiagramValidation.Observer(observer);
        Wavelengths = wavelengths ?? throw new ArgumentNullException(nameof(wavelengths));
        Planckian = planckian ?? throw new ArgumentNullException(nameof(planckian));
        if (planckian.Observer != observer || planckian.Sample != sample)
            throw new ArgumentException("The Planckian data must describe the same sample and observer.", nameof(planckian));
        Sample = sample; White = white; Observer = observer;
    }

    public static WavelengthDiagramData Create(CIExyY sample, StandardObserver observer = StandardObserver.Degree2,
        string illuminant = "D65")
    {
        var xyz = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        return FromWhiteXyz(sample, xyz, observer);
    }

    public static WavelengthDiagramData Create(CIExyY sample, StandardObserver observer, Standardilluminant illuminant) =>
        FromWhiteXyz(sample, ChromaticityMatch.GetStandardWhitePoint(illuminant, observer), observer);

    private static WavelengthDiagramData FromWhiteXyz(CIExyY sample, CIEXYZ xyz, StandardObserver observer)
    {
        // XYZToxyY is a rounded presentation API; preserve the integrated white here.
        double sum = xyz.CIEX + xyz.CIEY + xyz.CIEZ;
        return Create(sample, new CIExyY { CIEx = xyz.CIEX / sum, CIEy = xyz.CIEY / sum, CIEY = xyz.CIEY }, observer);
    }

    public static WavelengthDiagramData Create(CIExyY sample, CIExyY white, StandardObserver observer = StandardObserver.Degree2)
    {
        var point = ChromaticityCoordinates.FromXyY(sample);
        var reference = ChromaticityCoordinates.FromXyY(white);
        var wavelengths = ChromaticityConversion.xyYToWavelengths(point.ToXyY(), observer, reference.ToXyY());
        return new(point, reference, observer, wavelengths, PlanckianDiagramData.Create(point, observer));
    }
}
