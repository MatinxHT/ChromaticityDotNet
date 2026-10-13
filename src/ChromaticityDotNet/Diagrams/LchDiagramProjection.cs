using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Separate L* axis and C*/h° plane. Plane coordinates are relative to its radius; +Y is +b*.</summary>
public static class LchDiagramProjection
{
    public static double ChromaMaximum(CIELAB? standard, CIELAB? sample)
    {
        double Chroma(CIELAB? lab) => lab is null ? 0 : ChromaticityConversion.LabToLch(lab).CIEC;
        double peak = Math.Max(Chroma(standard), Chroma(sample));
        double maximum = Math.Max(100, Math.Ceiling(peak / 20 * 1.1) * 20);
        DiagramValidation.Finite(maximum, nameof(sample));
        return maximum;
    }

    public static ChromaticityDiagramPoint Project(CIELAB lab, double chromaMaximum)
    {
        if (lab is null) throw new ArgumentNullException(nameof(lab));
        DiagramValidation.Finite(chromaMaximum, nameof(chromaMaximum));
        if (chromaMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(chromaMaximum));
        // a*/b* preserve the unrounded C*/h° direction, including across the hue wrap.
        return new(lab.CIEA / chromaMaximum, lab.CIEB / chromaMaximum);
    }

    public static double LightnessFraction(double lightness)
    {
        DiagramValidation.Finite(lightness, nameof(lightness));
        return Math.Max(0, Math.Min(1, lightness / 100));
    }
}
