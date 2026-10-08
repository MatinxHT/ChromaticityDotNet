namespace Chromaticity.Tools.Services;

public sealed record ColorGradePathStep(ColorGradeLab Reference, ColorGradeLab Sample, double DeltaE);
public enum ColorGradeRounding { Nearest, Ceiling }
public sealed record ColorAxisGrade(ColorGradeAxis Axis, ColorGradeLab Start, ColorGradeLab End,
    double? RawGrade, double DeltaE, string Status, IReadOnlyList<ColorGradePathStep> Steps)
{
    public int? Grade { get; init; }
}
public sealed record ColorGradeComparison(ColorGradeLab Standard, ColorGradeLab Sample,
    string StandardHex, string SampleHex, ColorGradeSettings Settings, double DeltaE,
    IReadOnlyList<ColorAxisGrade> Components, CalculationTable Table, ColorGradeRounding? Rounding = null)
{
    public double DeltaL => Sample.L - Standard.L;
    public double DeltaA => Sample.A - Standard.A;
    public double DeltaB => Sample.B - Standard.B;
    public double DeltaC => Sample.Chroma - Standard.Chroma;
    public double? DeltaHue => Standard.Chroma == 0 || Sample.Chroma == 0
        ? null : ColorGradeCalculations.SignedHueDifference(Standard.Hue, Sample.Hue);
}

public static partial class ColorGradeCalculations
{
    public const int MaxAnalysisLevels = 1000;

    /// <summary>Apply signed integer grades in L, C, h order, rebasing each step on its actual predecessor.</summary>
    public static ColorGradeComparison Compose(ColorGradeResult card, int lightness, int chroma, int hue)
    {
        ValidateSettings(card.Settings);
        var requested = new[] { lightness, chroma, hue };
        if (requested.Any(level => level < -card.Levels || level > card.Levels))
            throw new ArgumentException($"所选等级必须在已生成色卡的 -{card.Levels} 至 +{card.Levels} 级内。");
        var current = card.Standard;
        var components = new List<ColorAxisGrade>();
        foreach (var axis in Enum.GetValues<ColorGradeAxis>())
        {
            var origin = current;
            var level = requested[(int)axis];
            var steps = new List<ColorGradePathStep>();
            for (var i = 1; i <= Math.Abs(level); i++)
            {
                var next = Next(origin, current, axis, Math.Sign(level),
                    (a, b) => Difference(a, b, card.Settings), card.Settings.StepDeltaE);
                if (next is null)
                    throw new ArgumentException($"{AxisTitle(axis)}方向第 {Math.Sign(level) * i:+0;-0;0} 级无法达到阈值；已到坐标边界、色相未定义或超出半周。请调整等级。");
                steps.Add(new(current, next.Value, Difference(current, next.Value, card.Settings)));
                current = next.Value;
            }
            components.Add(new(axis, origin, current, level, Difference(origin, current, card.Settings), "有效", steps) { Grade = level });
        }
        return Comparison(card.Standard, current, card.Settings, components, "按等级推算目标色");
    }

    /// <summary>Reverse the same sequential L/C/h path. Fractional grade is the final partial-step ΔE / threshold.</summary>
    public static ColorGradeComparison Analyze(string standardLab, string sampleLab, ColorGradeSettings settings,
        ColorGradeRounding rounding = ColorGradeRounding.Nearest)
    {
        ValidateSettings(settings);
        if (!Enum.IsDefined(rounding)) throw new ArgumentException("请选择有效的等级计算方式。");
        var standard = ParseLab(standardLab, "标样");
        var sample = ParseLab(sampleLab, "样本");
        var afterL = new ColorGradeLab(sample.L, standard.A, standard.B);
        var afterC = standard.Chroma == 0 ? sample :
            new ColorGradeLab(sample.L, standard.A * (sample.Chroma / standard.Chroma),
                standard.B * (sample.Chroma / standard.Chroma));
        // The neutral reference has no radial direction. Quantify ΔE but do not invent its C/h grade.
        var c = standard.Chroma == 0 && sample.Chroma != 0
            ? new ColorAxisGrade(ColorGradeAxis.Chroma, afterL, afterC, null,
                Difference(afterL, afterC, settings), "标样为中性色，固定色相的彩度等级未定义。", [])
            : Measure(ColorGradeAxis.Chroma, afterL, afterC, settings);
        var h = standard.Chroma == 0 || sample.Chroma == 0
            ? new ColorAxisGrade(ColorGradeAxis.Hue, afterC, sample, null,
                Difference(afterC, sample, settings), "标样或样本为中性色，色相等级未定义。", [])
            : Measure(ColorGradeAxis.Hue, afterC, sample, settings);
        var components = new[] { Measure(ColorGradeAxis.Lightness, standard, afterL, settings), c, h }
            .Select(component => component with { Grade = component.RawGrade.HasValue
                ? RoundGrade(component.RawGrade.Value, rounding) : null }).ToArray();
        return Comparison(standard, sample, settings, components, "标样与样本分级分析", rounding);
    }

    // Grade magnitude is rounded independently of its direction; this is not signed mathematical ceiling.
    public static int RoundGrade(double rawGrade, ColorGradeRounding rounding)
    {
        if (!Enum.IsDefined(rounding)) throw new ArgumentException("请选择有效的等级计算方式。");
        if (!double.IsFinite(rawGrade) || Math.Abs(rawGrade) > MaxAnalysisLevels + 1)
            throw new ArgumentException("等级超出有效取整范围。");
        var magnitude = Math.Abs(rawGrade);
        // The solver can leave sub-nanograde floating-point residue at an exact integer boundary.
        var nearestInteger = Math.Round(magnitude);
        if (Math.Abs(magnitude - nearestInteger) < 1e-9) magnitude = nearestInteger;
        var integer = rounding == ColorGradeRounding.Nearest
            ? Math.Round(magnitude, MidpointRounding.AwayFromZero) : Math.Ceiling(magnitude);
        return Math.Sign(rawGrade) * (int)integer;
    }

    public static string RoundingTitle(ColorGradeRounding rounding) => rounding switch
    {
        ColorGradeRounding.Nearest => "四舍五入", ColorGradeRounding.Ceiling => "向上取整",
        _ => throw new ArgumentException("请选择有效的等级计算方式。")
    };

    private static ColorAxisGrade Measure(ColorGradeAxis axis, ColorGradeLab start, ColorGradeLab end, ColorGradeSettings settings)
    {
        double Distance(ColorGradeLab from) => axis switch
        {
            ColorGradeAxis.Lightness => end.L - from.L,
            ColorGradeAxis.Chroma => end.Chroma - from.Chroma,
            _ => SignedHueDifference(from.Hue, end.Hue)
        };
        var distance = Distance(start);
        var direction = Math.Sign(distance);
        var stageDelta = Difference(start, end, settings);
        if (Math.Abs(distance) <= 1e-9) return new(axis, start, end, 0, stageDelta, "有效", []);
        var previous = start;
        var steps = new List<ColorGradePathStep>();
        for (var count = 0; count <= MaxAnalysisLevels; count++)
        {
            var remaining = direction * Distance(previous);
            if (remaining <= 1e-9)
                return new(axis, start, end, direction * count, stageDelta, "有效", steps);
            var next = Next(start, previous, axis, direction, (a, b) => Difference(a, b, settings),
                settings.StepDeltaE, remaining);
            if (next is null)
            {
                var residual = Difference(previous, end, settings);
                // A partial last step is reported explicitly rather than rounded into a whole grade.
                if (residual > 0) steps.Add(new(previous, end, residual));
                return new(axis, start, end, direction * (count + residual / settings.StepDeltaE), stageDelta, "有效", steps);
            }
            if (count == MaxAnalysisLevels) break;
            steps.Add(new(previous, next.Value, Difference(previous, next.Value, settings)));
            previous = next.Value;
        }
        return new(axis, start, end, null, stageDelta, $"超过 {MaxAnalysisLevels} 级的分析范围，请提高等级阈值。", steps);
    }

    public static double SignedHueDifference(double reference, double sample)
    {
        var delta = Math.IEEERemainder(sample - reference, 360);
        return delta == -180 ? 180 : delta;
    }

    public static string AxisTitle(ColorGradeAxis axis) => axis switch
    {
        ColorGradeAxis.Lightness => "明度 L*", ColorGradeAxis.Chroma => "彩度 C*", _ => "色相 h°"
    };

    private static ColorGradeComparison Comparison(ColorGradeLab standard, ColorGradeLab sample,
        ColorGradeSettings settings, IReadOnlyList<ColorAxisGrade> components, string mode, ColorGradeRounding? rounding = null)
    {
        var conditions = FormattableString.Invariant($"{mode}; {settings.FormulaName}; grade threshold ΔE={settings.StepDeltaE}; order: L -> C -> h; each full step reference: previous color; fractional grade: final partial ΔE / threshold; overall reference: original standard; hue: shortest signed Lab angle, 180° tie positive; preview: D65 / 2°, clipped sRGB");
        if (rounding.HasValue) conditions += $"; integer grades: {RoundingTitle(rounding.Value)}, magnitude rounded before applying direction";
        static string F(double? value) => value.HasValue ? ToolCalculations.F(value.Value) : "—";
        string[] Row(string stage, ColorGradeLab lab, ColorAxisGrade? component) =>
        [
            stage, F(lab.L), F(lab.A), F(lab.B), F(lab.Chroma), lab.Chroma == 0 ? "—" : F(lab.Hue), Hex(lab),
            F(lab.L - standard.L), F(lab.A - standard.A), F(lab.B - standard.B), F(lab.Chroma - standard.Chroma),
            standard.Chroma == 0 || lab.Chroma == 0 ? "—" : F(SignedHueDifference(standard.Hue, lab.Hue)),
            F(Difference(standard, lab, settings)), F(component?.DeltaE), component?.Grade?.ToString(Invariant) ?? "—", component?.Status ?? "有效",
            settings.FormulaName, F(settings.StepDeltaE), conditions
        ];
        var rows = new[] { Row("原始标样", standard, null) }.Concat(components.Select(component =>
            Row($"{AxisTitle(component.Axis)}完成{(component.Axis == ColorGradeAxis.Hue ? "（目标/样本）" : "（中间值）")}", component.End, component))).ToArray();
        var table = new CalculationTable(["阶段", "L*", "a*", "b*", "C*", "h°", "HEX",
            "相对标样 ΔL*", "相对标样 Δa*", "相对标样 Δb*", "相对标样 ΔC*", "相对标样 Δh°",
            "相对标样 ΔE", "本方向总 ΔE", "本方向有符号等级", "等级状态", "色差公式及参数", "目标级间 ΔE", "计算条件"], rows, conditions);
        return new(standard, sample, Hex(standard), Hex(sample), settings, Difference(standard, sample, settings), components, table, rounding);
    }
}
