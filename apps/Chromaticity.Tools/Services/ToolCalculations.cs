using System.Globalization;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

public sealed record CalculationTable(string[] Headers, string[][] Rows, string Conditions)
{
    public string ToTsv()
    {
        var headers = Headers.Select(header => header.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ')).ToArray();
        return string.Join('\n', new[] { headers }.Concat(Rows).Select(row => string.Join('\t', row)));
    }

    public string ToCsv()
    {
        static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        return string.Join("\r\n", new[] { Headers }.Concat(Rows)
            .Select(row => string.Join(",", row.Select(Escape)))) + "\r\n";
    }
}

public sealed record SpectrumResult(CalculationTable Table, double[] Values, int Start, int Step, string? Hex);
public sealed record IlluminantQueryResult(CalculationTable Table, double[] Values, int Start, int Step);
public enum InputSpace { XYZ, Lab, Luv, xyY, sRGB, HEX }

/// <summary>Input validation and presentation only; all color calculations call the existing library.</summary>
public static class ToolCalculations
{
    public const int MaxRows = 10000;
    public const int MaxTextLength = 2_000_000;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public static readonly Standardilluminant[] Illuminants = Enum.GetValues<Standardilluminant>();
    public static readonly int[] IlluminantIntervals = [1, 5, 10, 20];

    public static IlluminantQueryResult QueryIlluminant(Standardilluminant illuminant, int interval) =>
        QueryIlluminant(illuminant switch
        {
            Standardilluminant.D65 => "D65", Standardilluminant.A => "A",
            Standardilluminant.CWF => "FL2", Standardilluminant.F7 => "FL7",
            Standardilluminant.TL84 => "FL11", Standardilluminant.U30 => "FL12",
            _ => throw new ArgumentOutOfRangeException(nameof(illuminant))
        }, interval);

    public static IlluminantQueryResult QueryIlluminant(string illuminantId, int interval, int? start = null, int? end = null)
    {
        if (!IlluminantIntervals.Contains(interval)) throw new ArgumentException("波长间隔请选择 1、5、10 或 20 nm。");
        var info = CieSpectralData.Illuminants.FirstOrDefault(item => string.Equals(item.Id, illuminantId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("请选择有效的标准光源。", nameof(illuminantId));
        var spectrum = CieSpectralData.GetIlluminantSpectrum(info.Id);
        var rangeStart = start ?? spectrum.StartingWavelength;
        var rangeEnd = end ?? spectrum.EndingWavelength;
        if (rangeStart < spectrum.StartingWavelength || rangeEnd > spectrum.EndingWavelength || rangeStart >= rangeEnd)
            throw new ArgumentException($"波段必须位于 {spectrum.StartingWavelength}–{spectrum.EndingWavelength} nm 内，且起始波长小于结束波长。");
        var count = (rangeEnd - rangeStart) / interval + 1;
        if (count < 2) throw new ArgumentException("所选波段和间隔至少需要包含两个采样点。");
        var nativeStep = spectrum.WavelengthInterval;
        double Sample(int wavelength)
        {
            var offset = wavelength - spectrum.StartingWavelength;
            var index = offset / nativeStep;
            var remainder = offset % nativeStep;
            var value = spectrum.Spectrums![index];
            return remainder == 0 ? value : value + (spectrum.Spectrums[index + 1] - value) * remainder / nativeStep;
        }
        var values = Enumerable.Range(0, count).Select(i => Sample(rangeStart + i * interval)).ToArray();
        var sampledEnd = rangeStart + (count - 1) * interval;
        var conditions = $"{info.DisplayName} · {rangeStart}–{sampledEnd} nm · 间隔 {interval} nm · {count} 个采样点。" +
            $"源数据覆盖 {spectrum.StartingWavelength}–{spectrum.EndingWavelength} nm / {nativeStep} nm（{info.SourceFile}）。";
        if (Enumerable.Range(0, count).Any(i => (rangeStart + i * interval - spectrum.StartingWavelength) % nativeStep != 0))
            conditions += "非原始采样点使用线性插值，不代表新增实测精度。";
        if (info.IsApproximated) conditions += "CIE 数据质量标注：approximated。";
        if (sampledEnd != rangeEnd) conditions += $"指定结束波长为 {rangeEnd} nm，不额外补齐末点。";
        var rows = values.Select((value, i) => new[]
        {
            (rangeStart + i * interval).ToString(Invariant), value.ToString("G", Invariant)
        }).ToArray();
        return new(new(["波长 / nm", $"{info.DisplayName} 相对光谱功率"], rows, conditions), values, rangeStart, interval);
    }

    public static string F(double value) => double.IsFinite(value)
        ? value.ToString("F4", Invariant) : throw new ArgumentException("结果超出有效数值范围，请检查输入。");

    public static double Number(string text, string name)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, Invariant, out var value) || !double.IsFinite(value))
            throw new ArgumentException($"{name} 必须是有限数字（小数点使用 .）。");
        return value;
    }

    public static double Positive(string text, string name)
    {
        var value = Number(text, name);
        return value > 0 ? value : throw new ArgumentException($"{name} 必须大于 0。");
    }

    public static string[][] ParseRows(string text)
    {
        if (text.Length > MaxTextLength) throw new ArgumentException("输入过大，请拆分为小于 2 MB 的文本。");
        var lines = text.Trim('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n')
            .Replace('\u2028', '\n').Replace('\u2029', '\n').Replace('，', ',').Split('\n');
        var rows = new List<string[]>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim(' ');
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Preserve empty CSV/TSV fields so missing coordinates cannot silently shift columns.
            var cells = line.Contains('\t') ? line.Split('\t') : line.Contains(',') ? line.Split(',')
                : line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException($"第 {i + 1} 行有空单元格。");
            rows.Add(cells.Select(c => c.Trim()).ToArray());
            if (rows.Count > MaxRows) throw new ArgumentException($"一次最多处理 {MaxRows} 行。");
        }
        if (rows.Count == 0) throw new ArgumentException("请先输入数据，或载入示例。");
        return rows.ToArray();
    }

    private static double[] Triple(string[] cells, int row)
    {
        if (cells.Length != 3) throw new ArgumentException($"第 {row} 行需要恰好 3 列数字。");
        return cells.Select((cell, i) => Number(cell, $"第 {row} 行第 {i + 1} 列")).ToArray();
    }

    private static CIELABCH Lab(double[] v)
    {
        if (v[0] < 0) throw new ArgumentException("L* 不能小于 0。");
        return new CIELABCH(v[0], v[1], v[2]);
    }

    public static CalculationTable DifferenceAuto(string standards, string samples,
        double kl, double kc, double kh, double cmcL, double cmcC) =>
        Difference(standards, samples, ParseRows(standards).Length > 1, kl, kc, kh, cmcL, cmcC);

    public static CalculationTable Difference(string standards, string samples, bool paired,
        double kl, double kc, double kh, double cmcL, double cmcC)
    {
        if (new[] { kl, kc, kh, cmcL, cmcC }.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ArgumentException("色差权重必须是有限正数。");
        var reference = ParseRows(standards).Select((r, i) => Lab(Triple(r, i + 1))).ToArray();
        var test = ParseRows(samples).Select((r, i) => Lab(Triple(r, i + 1))).ToArray();
        if (paired ? reference.Length != test.Length : reference.Length != 1)
            throw new ArgumentException(paired ? "逐行配对时，标准和样品的行数必须相同。" : "一对多模式只允许一行标准色。");
        var conditions = FormattableString.Invariant($"CMC l:c={cmcL}:{cmcC}; CIEDE2000 kL:kC:kH={kl}:{kc}:{kh}; preview: D65 / 2°; differences: sample minus standard; hue: signed shortest Lab hue angle");
        var result = test.Select((sample, i) =>
        {
            var standard = reference[paired ? i : 0];
            var de00 = ChromaticityDeltaEFormulations.DeltaE2000(standard, sample, kl, kc, kh);
            var hue = sample.CIEH - standard.CIEH;
            if (hue > 180) hue -= 360;
            if (hue < -180) hue += 360;
            string Swatch(CIELABCH lab) => Hex(ChromaticityConversion.XYZ2RGB(
                ChromaticityConversion.Labch2XYZ(lab, Standardilluminant.D65, StandardObserver.Degree2)));
            return new[] { (i + 1).ToString(Invariant), Swatch(standard), Swatch(sample),
                F(standard.CIEL), F(standard.CIEA), F(standard.CIEB),
                F(sample.CIEL), F(sample.CIEA), F(sample.CIEB),
                F(de00.DL), F(de00.DA), F(de00.DB), F(de00.DC), F(hue),
                F(ChromaticityDeltaEFormulations.DeltaE1976(standard, sample)),
                F(ChromaticityDeltaEFormulations.DeltaEcmc(standard, sample, cmcL, cmcC)), F(de00.DeltaE) };
        }).ToArray();
        return new(["序号", "标样 sRGB", "样品 sRGB", "标样 L*", "标样 a*", "标样 b*", "样品 L*", "样品 a*", "样品 b*",
            "ΔL*", "Δa*", "Δb*", "ΔC*", "Δh°", "ΔE76",
            FormattableString.Invariant($"CMC\nl:c = {cmcL}:{cmcC}"),
            FormattableString.Invariant($"ΔE00\nkL:kC:kH = {kl}:{kc}:{kh}")], result, conditions);
    }

    public static SpectrumResult Reflectance(string text, int start, int end, int step, bool fraction,
        Standardilluminant illuminant, StandardObserver observer)
    {
        if (step <= 0 || start < 360 || end > 830 || end <= start || (end - start) % step != 0)
            throw new ArgumentException("波长需在 360–830 nm 内，结束大于起始，且范围可被正整数间隔整除。荧光光源限 380–780 nm。");
        var rows = ParseRows(text);
        if (rows.Length != (end - start) / step + 1) throw new ArgumentException($"当前波长设置需要 {(end - start) / step + 1} 个采样点，实际为 {rows.Length} 个。");
        var columns = rows[0].Length;
        if (columns is not (1 or 2)) throw new ArgumentException("光谱输入应为一列反射率，或两列：波长、反射率。无需表头。");
        var values = rows.Select((row, i) =>
        {
            if (row.Length != columns) throw new ArgumentException($"第 {i + 1} 行的列数不一致。");
            if (columns == 2 && Number(row[0], $"第 {i + 1} 行波长") != start + i * step)
                throw new ArgumentException($"第 {i + 1} 行波长应为 {start + i * step} nm。");
            var value = Number(row[^1], $"第 {i + 1} 行反射率") * (fraction ? 100 : 1);
            if (!double.IsFinite(value) || value < 0) throw new ArgumentException($"第 {i + 1} 行反射率必须有限且非负。");
            return value;
        }).ToArray();
        var xyz = ChromaticityConversion.REFtoXYZ(new Spectrum { StartingWavelength = start, EndingWavelength = end,
            WavelengthInterval = step, Spectrums = values }, illuminant, observer);
        var conditions = $"Reflectance; {start}–{end} nm / {step} nm; {Condition(illuminant, observer)}; Lab/Luv: library fixed white";
        var table = new CalculationTable(ColorHeaders, [ColorRow(1, xyz, illuminant, observer, conditions)], conditions);
        return new(table, values, start, step, Preview(xyz, illuminant, observer));
    }

    public static CalculationTable ConvertColors(string text, InputSpace space,
        Standardilluminant illuminant, StandardObserver observer)
    {
        if (space is InputSpace.sRGB or InputSpace.HEX && !CanPreview(illuminant, observer))
            throw new ArgumentException("sRGB / HEX 输入使用 D65 / 2°。库尚未执行色适应，请选择对应条件。");
        var conditions = $"{space}; {Condition(illuminant, observer)}; Lab/Luv: library fixed white";
        if (!CanPreview(illuminant, observer)) conditions += "; sRGB: screen approximation, no chromatic adaptation";
        var rows = ParseRows(text).Select((row, i) =>
        {
            CIEXYZ xyz;
            if (space == InputSpace.HEX)
            {
                if (row.Length != 1 || row[0].Length != 7 || row[0][0] != '#' ||
                    !int.TryParse(row[0][1..], NumberStyles.HexNumber, Invariant, out var hex))
                    throw new ArgumentException($"第 {i + 1} 行需要 #RRGGBB 格式。");
                xyz = ChromaticityConversion.RGB2XYZ(new CIERGB { redValue = (byte)(hex >> 16), greenValue = (byte)(hex >> 8), blueValue = (byte)hex });
            }
            else
            {
                var v = Triple(row, i + 1);
                xyz = space switch
                {
                    InputSpace.XYZ => v.Any(n => n < 0) ? throw new ArgumentException("XYZ 输入不能为负数。") : new CIEXYZ { CIEX = v[0], CIEY = v[1], CIEZ = v[2] },
                    InputSpace.Lab => ChromaticityConversion.Labch2XYZ(Lab(v), illuminant, observer),
                    InputSpace.Luv => ChromaticityConversion.Luv2XYZ(new CIELuv { CIEL = v[0], CIEu = v[1], CIEv = v[2] }, illuminant, observer),
                    InputSpace.xyY => v[0] < 0 || v[1] <= 0 || v[0] + v[1] > 1 || v[2] < 0
                        ? throw new ArgumentException("xyY 需要 x≥0、y>0、x+y≤1、Y≥0。")
                        : ChromaticityConversion.xy2XYZ(new CIExyY { CIEx = v[0], CIEy = v[1], CIEY = v[2] }),
                    InputSpace.sRGB => v.Any(n => n < 0 || n > 255 || n != Math.Truncate(n))
                        ? throw new ArgumentException("sRGB 每个通道必须是 0–255 的整数。")
                        : ChromaticityConversion.RGB2XYZ(new CIERGB { redValue = (byte)v[0], greenValue = (byte)v[1], blueValue = (byte)v[2] }),
                    _ => throw new ArgumentException("不支持的输入空间。")
                };
            }
            return ColorRow(i + 1, xyz, illuminant, observer, conditions, alwaysPreview: true);
        }).ToArray();
        return new(ColorHeaders, rows, conditions);
    }

    private static string Condition(Standardilluminant light, StandardObserver observer) =>
        $"{light} / {(observer == StandardObserver.Degree2 ? "2°" : "10°")}";

    public static bool CanPreview(Standardilluminant light, StandardObserver observer) =>
        light == Standardilluminant.D65 && observer == StandardObserver.Degree2;

    public static string? Preview(CIEXYZ xyz, Standardilluminant light, StandardObserver observer)
    {
        if (!CanPreview(light, observer)) return null;
        var rgb = ChromaticityConversion.XYZ2RGB(xyz);
        return Hex(rgb);
    }

    private static string Hex(CIERGB rgb) => $"#{rgb.redValue:X2}{rgb.greenValue:X2}{rgb.blueValue:X2}";

    private static readonly string[] ColorHeaders = ["行", "X", "Y", "Z", "L*", "a*", "b*", "C*", "h°", "L* (Luv)", "u*", "v*", "x", "y", "R", "G", "B", "HEX", "计算条件"];

    private static string[] ColorRow(int index, CIEXYZ xyz, Standardilluminant light, StandardObserver observer, string conditions, bool alwaysPreview = false)
    {
        // Format first to reject overflow before passing results to the byte-based RGB API.
        var coordinates = new[] { F(xyz.CIEX), F(xyz.CIEY), F(xyz.CIEZ) };
        var lab = ChromaticityConversion.XYZ2Labch(xyz, light, observer);
        var luv = ChromaticityConversion.XYZ2Luv(xyz, light, observer);
        var sum = xyz.CIEX + xyz.CIEY + xyz.CIEZ;
        var xy = sum == 0 ? null : ChromaticityConversion.XYZ2xyY(xyz);
        var rgb = alwaysPreview || CanPreview(light, observer) ? ChromaticityConversion.XYZ2RGB(xyz) : null;
        return [index.ToString(Invariant), ..coordinates, F(lab.CIEL), F(lab.CIEA), F(lab.CIEB), F(lab.CIEC), F(lab.CIEH),
            F(luv.CIEL), F(luv.CIEu), F(luv.CIEv), xy is null ? "—" : F(xy.CIEx), xy is null ? "—" : F(xy.CIEy),
            rgb?.redValue.ToString(Invariant) ?? "—", rgb?.greenValue.ToString(Invariant) ?? "—", rgb?.blueValue.ToString(Invariant) ?? "—",
            rgb is null ? "—" : Hex(rgb), conditions];
    }
}
