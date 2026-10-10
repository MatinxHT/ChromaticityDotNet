using System.Globalization;

namespace Chromaticity.Tools.Services;

public enum ColorGradeCoordinateSystem { ReferenceOrigin, FiveFiveFive }

/// <summary>Display coordinates in L, C, h order; the underlying color grades stay unchanged.</summary>
public readonly record struct ColorGradeCoordinates(int Lightness, int Chroma, int Hue)
{
    public static ColorGradeCoordinates FromGrades(int lightness, int chroma, int hue,
        ColorGradeCoordinateSystem system = ColorGradeCoordinateSystem.ReferenceOrigin)
    {
        var offset = system switch
        {
            ColorGradeCoordinateSystem.ReferenceOrigin => 0,
            ColorGradeCoordinateSystem.FiveFiveFive => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(system))
        };
        return new(lightness + offset, chroma + offset, hue + offset);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({Lightness},{Chroma},{Hue})");
}
