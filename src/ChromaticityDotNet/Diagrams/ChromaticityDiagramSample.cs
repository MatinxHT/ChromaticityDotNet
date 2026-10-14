namespace ChromaticityDotNet.Diagrams;

/// <summary>A named, full-precision xy sample shared by xy and u′v′ diagrams.</summary>
public sealed class ChromaticityDiagramSample
{
    public string Name { get; }
    public ChromaticityCoordinates Coordinates { get; }
    /// <summary>ARGB marker color, also used by the sample's legend symbol.</summary>
    public uint MarkerArgb { get; }

    public ChromaticityDiagramSample(string name, ChromaticityCoordinates coordinates, uint markerArgb = 0xFF2563EB)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A sample name is required.", nameof(name));
        Name = name; Coordinates = coordinates; MarkerArgb = markerArgb;
    }
}
