using System.Globalization;

namespace ChromaticityDotNet.Diagrams;

/// <summary>Full-precision data for a selectable sample, boundary point or blackbody point.</summary>
public sealed class ChromaticityDiagramTarget
{
    public string Name { get; }
    public ChromaticityDiagramSpace Space { get; }
    public ChromaticityDiagramPoint Point { get; }
    public double X => Point.X;
    public double Y => Point.Y;
    public double? Wavelength { get; }
    public double? Temperature { get; }
    public double? Duv { get; }

    public ChromaticityDiagramTarget(string name, ChromaticityDiagramSpace space, ChromaticityDiagramPoint point,
        double? wavelength = null, double? temperature = null, double? duv = null)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        _ = new ChromaticityDiagramProjection(space);
        DiagramValidation.Finite(point.X, nameof(point)); DiagramValidation.Finite(point.Y, nameof(point));
        if (wavelength is { } w) DiagramValidation.Finite(w, nameof(wavelength));
        if (temperature is { } t) DiagramValidation.Finite(t, nameof(temperature));
        if (duv is { } d) DiagramValidation.Finite(d, nameof(duv));
        Name = name; Space = space; Point = point; Wavelength = wavelength; Temperature = temperature; Duv = duv;
    }

    public string TrackerText
    {
        get
        {
            string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
            string x = Space == ChromaticityDiagramSpace.Xy ? "x" : "u′";
            string y = Space == ChromaticityDiagramSpace.Xy ? "y" : "v′";
            string text = $"{Name}\nCIE {x}: {Number(X, "F6")}\nCIE {y}: {Number(Y, "F6")}";
            if (Wavelength is { } wavelength) text += $"\nλ: {Number(wavelength, "F2")} nm";
            if (Temperature is { } temperature) text += $"\nT: {Number(temperature, "0")} K";
            if (Duv is { } duv) text += $"\nDuv: {Number(duv, "+0.000000;-0.000000;0.000000")}";
            return text;
        }
    }
}
