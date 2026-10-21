using System.Globalization;

namespace Chromaticity.Tools.Services;

/// <summary>Formats demonstration reflectances only; measured input and calculations retain their precision.</summary>
public static class ReflectanceExamples
{
    public static string Default(bool fraction = false) => Format(Enumerable.Range(0, 41)
        .Select(i => 41.5 - 23.5 * Math.Cos(Math.PI * i / 20)), 380, 10, fraction);

    public static string Format(IEnumerable<double> percentages, int start, int step, bool fraction = false) =>
        string.Join('\n', percentages.Select((value, index) =>
            $"{(start + index * step).ToString(CultureInfo.InvariantCulture)},{Value(value, fraction)}"));

    public static string Value(double percentage, bool fraction = false)
    {
        // Quantize in percent first so both units describe exactly the same example spectrum.
        var rounded = Math.Round((decimal)percentage, 2, MidpointRounding.ToEven);
        return (fraction ? rounded / 100 : rounded).ToString(fraction ? "F4" : "F2", CultureInfo.InvariantCulture);
    }
}
