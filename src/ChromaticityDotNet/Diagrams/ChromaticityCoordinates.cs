using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.Diagrams;

/// <summary>An immutable, unrounded xyY snapshot, independent of any UI framework.</summary>
public readonly struct ChromaticityCoordinates : IEquatable<ChromaticityCoordinates>
{
    public double X { get; }
    public double Y { get; }
    public double Luminance { get; }

    public ChromaticityCoordinates(double x, double y, double luminance = 100)
    {
        DiagramValidation.Finite(x, nameof(x));
        DiagramValidation.Finite(y, nameof(y));
        DiagramValidation.Finite(luminance, nameof(luminance));
        if (luminance < 0) throw new ArgumentOutOfRangeException(nameof(luminance));
        X = x; Y = y; Luminance = luminance;
    }

    public static ChromaticityCoordinates FromXyY(CIExyY value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return new(value.CIEx, value.CIEy, value.CIEY);
    }

    public CIExyY ToXyY() => new() { CIEx = X, CIEy = Y, CIEY = Luminance };
    public bool Equals(ChromaticityCoordinates other) => X.Equals(other.X) && Y.Equals(other.Y) && Luminance.Equals(other.Luminance);
    public override bool Equals(object? obj) => obj is ChromaticityCoordinates point && Equals(point);
    public override int GetHashCode() => unchecked((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Luminance.GetHashCode());
    public static bool operator ==(ChromaticityCoordinates left, ChromaticityCoordinates right) => left.Equals(right);
    public static bool operator !=(ChromaticityCoordinates left, ChromaticityCoordinates right) => !left.Equals(right);
}
