using System.Globalization;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

public sealed record TextileSpectra(Spectrum Standard, Spectrum Sample, StandardObserver Observer, bool Fraction);

/// <summary>Compares two relative reflectance spectra under each textile viewing illuminant.</summary>
public static class TextileMetamerismCalculations
{
    public const string Explanation = "每行分别计算同一光源下标准品和样品的 Lab，再以该标准品计算色差。结合五种光源下的色差变化评价同色异谱现象；所有差值方向为样品减标样。";
    public const string PreviewNotice = "色块左侧为该行光源下的标准品，右侧为样品；按 D65 / 10° 将 Lab 转为 sRGB，超出色域会裁剪。";

    public static TextileSpectra Parse(string standard, string sample, int start, int end, int step,
        bool fraction, StandardObserver observer)
    {
        if (observer is not (StandardObserver.Degree2 or StandardObserver.Degree10))
            throw new ArgumentException("请选择有效的观察者。");
        Spectrum Read(string text, string label)
        {
            try { return ToolCalculations.ParseReflectance(text, start, end, step, fraction); }
            catch (ArgumentException ex) { throw new ArgumentException($"{label}：{ex.Message}"); }
        }
        return new(Read(standard, "标准品光谱"), Read(sample, "样品光谱"), observer, fraction);
    }

    public static CalculationTable Analyze(TextileSpectra input, ColorInconstancySettings settings)
    {
        var options = ColorInconstancyCalculations.ComparisonOptions(settings);
        var standard = input.Standard;
        var sample = input.Sample;
        if (standard.StartingWavelength != sample.StartingWavelength || standard.EndingWavelength != sample.EndingWavelength ||
            standard.WavelengthInterval != sample.WavelengthInterval)
            throw new ArgumentException("标准品和样品必须使用相同波段和采样间隔。");
        var start = standard.StartingWavelength;
        var end = standard.EndingWavelength;
        var step = standard.WavelengthInterval;
        var observer = input.Observer == StandardObserver.Degree2 ? "2° · CIE 1931" : "10° · CIE 1964";
        var units = input.Fraction ? "比例（0.18 = 18%）" : "百分数（18 = 18%）";
        var conditions = $"两条相对反射光谱 · {start}–{end} nm / {step} nm · {observer} · {settings.FormulaName}\n" +
            "各光源使用相同波段的积分白点（Y = 100），标准品和样品保留输入反射率尺度。\n" + Explanation;
        var unavailable = new List<string>();
        var rows = ColorInconstancyCalculations.Lights.Select(light =>
        {
            // Lab, selected delta-E, three comments, and sample-minus-standard components.
            var values = Enumerable.Repeat("—", 13).ToArray();
            var standardHex = "—";
            var sampleHex = "—";
            string? reason = null;
            var illuminant = CieSpectralData.GetIlluminantSpectrum(light.Id);
            if (start < illuminant.StartingWavelength || end > illuminant.EndingWavelength)
                reason = $"光源仅覆盖 {illuminant.StartingWavelength}–{illuminant.EndingWavelength} nm，无法按原波段计算。";
            else
            {
                try
                {
                    var white = ChromaticityMatch.GetStandardWhitePoint(illuminant, input.Observer, start, end);
                    CIELAB Lab(Spectrum spectrum) => ChromaticityConversion.XYZToLab(
                        ChromaticityConversion.REFToXYZ(spectrum, illuminant, input.Observer), white);
                    var standardLab = Lab(standard);
                    var sampleLab = Lab(sample);
                    var comparison = ChromaticityMatch.CompareColors(standardLab, sampleLab, options);
                    var deltaE = settings.Formula switch
                    {
                        ComparisonFormula.Cie76 => comparison.DeltaE1976,
                        ComparisonFormula.Cmc => comparison.DeltaECmc,
                        _ => comparison.DeltaE2000
                    };
                    var differences = comparison.Differences;
                    values = [F(standardLab.CIEL), F(standardLab.CIEA), F(standardLab.CIEB),
                        F(sampleLab.CIEL), F(sampleLab.CIEA), F(sampleLab.CIEB), F(deltaE),
                        ..ColorEvaluationPresentation.Comments(comparison)[..3], F(differences.DeltaL), F(differences.DeltaChroma),
                        differences.HueAngleDifferenceDegrees is { } hue ? F(hue) : "—"];
                    standardHex = ColorInconstancyCalculations.ScreenPreview(standardLab) ?? "—";
                    sampleHex = ColorInconstancyCalculations.ScreenPreview(sampleLab) ?? "—";
                }
                catch (Exception ex) when (ex is ArgumentException or ArithmeticException)
                { reason = $"无法计算：{ex.Message}"; }
            }
            var rowConditions = conditions.Replace("\n", "; ") + "; achromatic: L* < 10 OR C* < 5; " +
                "preview: D65 / 10°, clipped sRGB; standard on left, sample on right; " +
                ColorInconstancyCalculations.IlluminantReferenceNotice;
            if (reason is not null)
            {
                var explanation = $"光源 {light.Name}：{reason}";
                unavailable.Add(explanation);
                rowConditions += "。" + explanation;
            }
            return new[] { light.Name, light.Reference }.Concat(values).Concat([
                light.Id, observer, start.ToString(CultureInfo.InvariantCulture), end.ToString(CultureInfo.InvariantCulture),
                step.ToString(CultureInfo.InvariantCulture), units, settings.FormulaName, standardHex, sampleHex, rowConditions]).ToArray();
        }).ToArray();
        if (unavailable.Count > 0) conditions += "\n" + string.Join('\n', unavailable);
        return new(["照明体", "照明体参考", "标样 L*", "标样 a*", "标样 b*", "样品 L*", "样品 a*", "样品 b*",
            settings.FormulaName, ..ColorEvaluationPresentation.Headers[..3], "ΔL*", "ΔC*", "Δh°", "光源数据 ID", "观察者",
            "起始波长 / nm", "结束波长 / nm", "间隔 / nm", "输入单位", "色差公式", "标样 sRGB", "样品 sRGB", "计算条件"], rows, conditions);
    }

    private static string F(double value) => ToolCalculations.F(value);
}
