using ChromaticityDotNet.Model;

namespace Chromaticity.Tools.Services;

public sealed record ColorGradeCompositeChip(int ChromaLevel, int HueLevel, ColorGradeLab? Lab,
    string? Hex, double? StandardDeltaE, string? UnavailableReason, ColorComparisonResult? Comparison = null);

public sealed record ColorGradeCompositeResult(ColorGradeResult Source, int LightnessLevel,
    ColorGradeLab? Center, IReadOnlyList<ColorGradeCompositeChip> Chips);

public static partial class ColorGradeCalculations
{
    /// <summary>
    /// Combines the original one-dimensional L/C/h coordinates into a C/h plane at one L grade.
    /// The axes are sampled independently from the original standard, not solved successively.
    /// </summary>
    public static ColorGradeCompositeResult GenerateComposite(ColorGradeResult card, int lightnessLevel = 0,
        ColorComparisonOptions? comparisonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (Math.Abs((long)lightnessLevel) > card.Levels)
            throw new ArgumentException($"所选等级必须在已生成色卡的 -{card.Levels} 至 +{card.Levels} 级内。");

        var lightness = card.Scales.Single(scale => scale.Axis == ColorGradeAxis.Lightness)
            .Chips.Single(chip => chip.Level == lightnessLevel);
        var chroma = card.Scales.Single(scale => scale.Axis == ColorGradeAxis.Chroma).Chips;
        var hue = card.Scales.Single(scale => scale.Axis == ColorGradeAxis.Hue).Chips;
        var chips = new List<ColorGradeCompositeChip>();
        foreach (var h in hue.OrderByDescending(chip => chip.Level))
        foreach (var c in chroma.OrderBy(chip => chip.Level))
        {
            var unavailable = lightness.Lab is null ? (ColorGradeAxis.Lightness, lightness) :
                c.Lab is null ? (ColorGradeAxis.Chroma, c) :
                h.Lab is null ? (ColorGradeAxis.Hue, h) : ((ColorGradeAxis, ColorGradeChip)?)null;
            if (unavailable is { } blocked)
            {
                chips.Add(new(c.Level, h.Level, null, null, null,
                    $"{AxisTitle(blocked.Item1)}：{blocked.Item2.UnavailableReason}"));
                continue;
            }

            var l = lightness.Lab!.Value.L;
            var cLab = c.Lab!.Value;
            var hLab = h.Lab!.Value;
            var angle = hLab.Hue * Math.PI / 180;
            // Retain the exact original chips on the axes, including the neutral center.
            var lab = h.Level == 0 ? new ColorGradeLab(l, cLab.A, cLab.B) :
                c.Level == 0 ? new ColorGradeLab(l, hLab.A, hLab.B) :
                new ColorGradeLab(l, cLab.Chroma * Math.Cos(angle), cLab.Chroma * Math.Sin(angle));
            chips.Add(new(c.Level, h.Level, lab, Hex(lab), Difference(card.Standard, lab, card.Settings), null,
                Compare(card.Standard, lab, card.Settings, comparisonOptions)));
        }
        return new(card, lightnessLevel, lightness.Lab, chips);
    }
}
