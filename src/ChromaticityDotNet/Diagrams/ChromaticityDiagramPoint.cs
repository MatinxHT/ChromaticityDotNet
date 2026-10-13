namespace ChromaticityDotNet.Diagrams;

/// <summary>A full-precision point in a diagram's coordinate space.</summary>
public readonly struct ChromaticityDiagramPoint : IEquatable<ChromaticityDiagramPoint>
{
    public double X { get; }
    public double Y { get; }
    public ChromaticityDiagramPoint(double x, double y) { X = x; Y = y; }
    public bool Equals(ChromaticityDiagramPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is ChromaticityDiagramPoint point && Equals(point);
    public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
    public static bool operator ==(ChromaticityDiagramPoint left, ChromaticityDiagramPoint right) => left.Equals(right);
    public static bool operator !=(ChromaticityDiagramPoint left, ChromaticityDiagramPoint right) => !left.Equals(right);
}
