namespace ChromaticityDotNet.Diagrams;

/// <summary>A named, full-precision xy sample shared by xy and u′v′ diagrams.</summary>
public sealed class ChromaticityDiagramSample
{
    public string Name { get; }
    public ChromaticityCoordinates Coordinates { get; }
    /// <summary>ARGB marker color, also used by the sample's legend symbol.</summary>
    public uint MarkerArgb { get; }
    /// <summary>Circle radius in Avalonia device-independent pixels. Larger reference points remain visible behind smaller samples.</summary>
    public double MarkerRadius { get; }

    public ChromaticityDiagramSample(string name, ChromaticityCoordinates coordinates, uint markerArgb = 0xFF2563EB)
        : this(name, coordinates, markerArgb, 5) { }

    public ChromaticityDiagramSample(string name, ChromaticityCoordinates coordinates, uint markerArgb, double markerRadius)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A sample name is required.", nameof(name));
        DiagramValidation.Finite(markerRadius, nameof(markerRadius));
        if (markerRadius <= 0) throw new ArgumentOutOfRangeException(nameof(markerRadius));
        Name = name; Coordinates = coordinates; MarkerArgb = markerArgb; MarkerRadius = markerRadius;
    }
}
