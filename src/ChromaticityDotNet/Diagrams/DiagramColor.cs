namespace ChromaticityDotNet.Diagrams;

/// <summary>An sRGB display color, with byte channels, independent of UI color types.</summary>
public readonly struct DiagramColor
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }
    public DiagramColor(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }
}
