using System.Globalization;
using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

public enum ColorGradeFormula { Cie76, Cmc, Ciede2000 }
public enum ColorGradeAxis { Lightness, Chroma, Hue }

public readonly record struct ColorGradeLab(double L, double A, double B)
{
    // Keep the path unrounded; the library's derived C/H properties are rounded to four decimals.
    public double Chroma => Math.Sqrt(A * A + B * B);
    public double Hue => (Math.Atan2(B, A) * 180 / Math.PI + 360) % 360;
    public CIELABCH ToLab() => new(L, A, B);
}

public sealed record ColorGradeChip(int Level, ColorGradeLab? Lab, string? Hex,
    double? StepDeltaE, double? StandardDeltaE, string? UnavailableReason);
public sealed record ColorGradeScale(ColorGradeAxis Axis, string Title, string NegativeLabel,
    string PositiveLabel, IReadOnlyList<ColorGradeChip> Chips);
public sealed record ColorGradeSettings(ColorGradeFormula Formula, double StepDeltaE = 1.5, double CmcL = 2, double CmcC = 1)
{
    public string FormulaName => Formula switch
    {
        ColorGradeFormula.Cie76 => "ΔE1976 / CIE76",
        ColorGradeFormula.Cmc => FormattableString.Invariant($"CMC l:c={CmcL}:{CmcC}"),
        _ => "ΔE00 / CIEDE2000 kL:kC:kH=1:1:1"
    };
}
public sealed record ColorGradeResult(string Formula, IReadOnlyList<ColorGradeScale> Scales, CalculationTable Table,
    ColorGradeLab Standard, ColorGradeSettings Settings, int Levels);

/// <summary>
/// .NET/WASM implementation of JQDigitalColorCard's successive L/C/h paths.
/// Each outward step uses the previous chip as reference (significant for asymmetric CMC).
/// Color difference and screen conversions reuse ChromaticityDotNet.
/// </summary>
public static partial class ColorGradeCalculations
{
    public const int MaxLevels = 10;
    public const double DefaultStepDeltaE = 1.5;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ColorGradeResult Generate(string standardLab, ColorGradeFormula formula,
        int levels = 4, double cmcL = 2, double cmcC = 1, double stepDeltaE = DefaultStepDeltaE)
    {
        var settings = new ColorGradeSettings(formula, stepDeltaE, cmcL, cmcC);
        ValidateSettings(settings);
        if (levels is < 1 or > MaxLevels) throw new ArgumentException($"每侧级数必须为 1–{MaxLevels} 的整数。");
        var standard = ParseLab(standardLab, "标样");
        var standardHex = Hex(standard);
        var formulaName = settings.FormulaName;
        var stepText = stepDeltaE.ToString("0.####", Invariant);
        var conditions = $"{formulaName}; outward adjacent ΔE={stepText}; reference: previous chip; cumulative reference: original standard; preview: D65 / 2°, clipped sRGB; levels per side: {levels}";
        double Delta(ColorGradeLab reference, ColorGradeLab sample) => Difference(reference, sample, settings);
        var scales = new List<ColorGradeScale>();
        foreach (var axis in Enum.GetValues<ColorGradeAxis>())
        {
            var chips = new List<ColorGradeChip>();
            foreach (var direction in new[] { -1, 1 })
            {
                var previous = standard;
                string? blocked = null;
                for (var level = 1; level <= levels; level++)
                {
                    var next = blocked is null ? Next(standard, previous, axis, direction, Delta, stepDeltaE) : null;
                    if (next is null)
                    {
                        blocked ??= axis switch
                        {
                            ColorGradeAxis.Lightness => $"明度已到 0–100 边界，无法再达到 ΔE={stepText}。",
                            ColorGradeAxis.Chroma when standard.Chroma == 0 => "中性色无确定色相，无法沿固定色相展开彩度。",
                            ColorGradeAxis.Chroma when direction < 0 => $"彩度已接近 0，无法再达到 ΔE={stepText}。",
                            ColorGradeAxis.Hue when standard.Chroma == 0 => "中性色无确定色相，无法展开色相。",
                            ColorGradeAxis.Hue => $"当前彩度或半周色相范围内无法再达到 ΔE={stepText}。",
                            _ => $"当前参数下无法再达到 ΔE={stepText}。"
                        };
                        chips.Add(new(direction * level, null, null, null, null, blocked));
                        continue;
                    }
                    chips.Add(new(direction * level, next, Hex(next.Value), Delta(previous, next.Value),
                        Delta(standard, next.Value), null));
                    previous = next.Value;
                }
            }
            chips.Add(new(0, standard, standardHex, 0, 0, null));
            var (title, negative, positive) = axis switch
            {
                ColorGradeAxis.Lightness => ("明度 L*", "更暗", "更亮"),
                ColorGradeAxis.Chroma => ("彩度 C*", "更灰", "更艳"),
                _ => ("色相 h°", "h° 减小", "h° 增大")
            };
            scales.Add(new(axis, title, negative, positive, chips.OrderBy(chip => chip.Level).ToArray()));
        }
        static string F(double? value) => value?.ToString("F4", Invariant) ?? "—";
        var export = scales.SelectMany(scale => scale.Chips.Select(chip => new[]
        {
            scale.Title, chip.Level.ToString(Invariant), F(chip.Lab?.L), F(chip.Lab?.A), F(chip.Lab?.B),
            F(chip.Lab?.Chroma), F(chip.Lab?.Hue), chip.Hex ?? "—", F(chip.StepDeltaE), F(chip.StandardDeltaE),
            formulaName, F(stepDeltaE), chip.UnavailableReason ?? "有效", conditions
        })).ToArray();
        return new(formulaName, scales, new(["方向", "级别", "L*", "a*", "b*", "C*", "h°", "HEX",
            "向外相邻 ΔE", "相对标样 ΔE", "色差公式及参数", "目标级间 ΔE", "状态", "计算条件"], export, conditions),
            standard, settings, levels);
    }

    private static ColorGradeLab? Next(ColorGradeLab standard, ColorGradeLab previous, ColorGradeAxis axis,
        int direction, Func<ColorGradeLab, ColorGradeLab, double> difference, double stepDeltaE,
        double distanceLimit = double.PositiveInfinity)
    {
        if (axis != ColorGradeAxis.Lightness && previous.Chroma == 0) return null;
        ColorGradeLab At(double distance)
        {
            if (axis == ColorGradeAxis.Lightness) return new(previous.L + direction * distance, previous.A, previous.B);
            if (axis == ColorGradeAxis.Chroma)
            {
                var chroma = Math.Max(0, previous.Chroma + direction * distance);
                var ratio = chroma / previous.Chroma;
                return new(previous.L, previous.A * ratio, previous.B * ratio);
            }
            var angle = direction * distance * Math.PI / 180;
            return new(previous.L, previous.A * Math.Cos(angle) - previous.B * Math.Sin(angle),
                previous.A * Math.Sin(angle) + previous.B * Math.Cos(angle));
        }
        var limit = axis switch
        {
            ColorGradeAxis.Lightness => direction < 0 ? previous.L : 100 - previous.L,
            ColorGradeAxis.Chroma when direction < 0 => previous.Chroma,
            // Never circle past the opposite hue and generate repeated colors.
            ColorGradeAxis.Hue => Math.Max(0, 180 - Math.Abs(Math.IEEERemainder(previous.Hue - standard.Hue, 360))),
            _ => double.PositiveInfinity
        };
        limit = Math.Min(limit, distanceLimit);
        if (limit <= 1e-10) return null;
        var low = 0.0;
        var high = Math.Min(axis == ColorGradeAxis.Hue ? 1 : 0.25, limit);
        var bracketed = false;
        // Hue difference need not be monotonic over a half circle; scan for the first crossing.
        for (var i = 0; i < (axis == ColorGradeAxis.Hue ? 180 : 40); i++)
        {
            var delta = difference(previous, At(high));
            if (!double.IsFinite(delta)) return null;
            if (delta >= stepDeltaE)
            {
                // In reverse analysis, an exact-threshold endpoint is already a complete step.
                // Avoid leaving a rounded ΔE plateau's tiny residual to be counted as an extra grade.
                if (high == distanceLimit && delta == stepDeltaE) return At(high);
                bracketed = true; break;
            }
            if (high >= limit) return null;
            low = high;
            high = Math.Min(axis == ColorGradeAxis.Hue ? high + 1 : high * 2, limit);
        }
        if (!bracketed) return null;
        // The library returns four decimals. Solve within that precision without fixed-increment overshoot.
        for (var i = 0; i < 64; i++)
        {
            var mid = (low + high) / 2;
            var candidate = At(mid);
            var delta = difference(previous, candidate);
            if (delta == stepDeltaE) return candidate;
            if (delta < stepDeltaE) low = mid; else high = mid;
        }
        var result = At((low + high) / 2);
        return Math.Abs(difference(previous, result) - stepDeltaE) <= 0.0001 ? result : null;
    }

    private static void ValidateSettings(ColorGradeSettings settings)
    {
        if (!Enum.IsDefined(settings.Formula)) throw new ArgumentException("请选择有效的色差公式。");
        if (!double.IsFinite(settings.StepDeltaE) || settings.StepDeltaE < 0.0001 ||
            Math.Abs(settings.StepDeltaE - Math.Round(settings.StepDeltaE, 4)) > 1e-10)
            throw new ArgumentException("等级阈值 ΔE 必须为不小于 0.0001 的有限数字，最多四位小数。");
        if (settings.Formula == ColorGradeFormula.Cmc &&
            (!double.IsFinite(settings.CmcL) || !double.IsFinite(settings.CmcC) || settings.CmcL <= 0 || settings.CmcC <= 0))
            throw new ArgumentException("CMC 的 l、c 必须是有限正数。");
    }

    private static ColorGradeLab ParseLab(string text, string name)
    {
        var rows = ToolCalculations.ParseRows(text);
        if (rows.Length != 1 || rows[0].Length != 3) throw new ArgumentException($"请输入一个{name}，恰好三列 L*、a*、b*。");
        var lab = new ColorGradeLab(ToolCalculations.Number(rows[0][0], $"{name} L*"),
            ToolCalculations.Number(rows[0][1], $"{name} a*"), ToolCalculations.Number(rows[0][2], $"{name} b*"));
        if (lab.L is < 0 or > 100) throw new ArgumentException($"{name} L* 必须在 0–100 之间。");
        if (!double.IsFinite(lab.Chroma)) throw new ArgumentException($"{name}彩度过大，无法计算。");
        return lab;
    }

    private static double Difference(ColorGradeLab reference, ColorGradeLab sample, ColorGradeSettings settings)
    {
        var delta = settings.Formula switch
        {
            ColorGradeFormula.Cie76 => ChromaticityDeltaEFormulations.DeltaE1976(reference.ToLab(), sample.ToLab()),
            ColorGradeFormula.Cmc => ChromaticityDeltaEFormulations.DeltaEcmc(reference.ToLab(), sample.ToLab(), settings.CmcL, settings.CmcC),
            _ => ChromaticityDeltaEFormulations.DeltaE2000(reference.ToLab(), sample.ToLab(), 1, 1, 1).DeltaE
        };
        return double.IsFinite(delta) ? delta : throw new ArgumentException("颜色或公式参数超出有效计算范围。");
    }

    private static string Hex(ColorGradeLab lab)
    {
        var xyz = ChromaticityConversion.Labch2XYZ(lab.ToLab(), Standardilluminant.D65, StandardObserver.Degree2);
        if (!double.IsFinite(xyz.CIEX) || !double.IsFinite(xyz.CIEY) || !double.IsFinite(xyz.CIEZ))
            throw new ArgumentException("颜色坐标过大，无法生成屏幕预览。");
        var rgb = ChromaticityConversion.XYZ2RGB(xyz);
        return $"#{rgb.redValue:X2}{rgb.greenValue:X2}{rgb.blueValue:X2}";
    }
}
