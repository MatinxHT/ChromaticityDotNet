using System.Globalization;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

public sealed record ColorInconstancySettings(ComparisonFormula Formula = ComparisonFormula.Ciede2000,
    double CmcL = 2, double CmcC = 1, double KL = 1, double KC = 1, double KH = 1)
{
    public string FormulaName => Formula switch
    {
        ComparisonFormula.Cie76 => "ΔE1976 / CIE76",
        ComparisonFormula.Cmc => FormattableString.Invariant($"CMC l:c = {CmcL}:{CmcC}"),
        _ => FormattableString.Invariant($"ΔE00 / CIEDE2000 kL:kC:kH = {KL}:{KC}:{KH}")
    };
}

/// <summary>Compares one reflectance spectrum under five illuminants to its original Lab reference.</summary>
public static class ColorInconstancyCalculations
{
    public const string IlluminantReferenceNotice = "光源以实测SPD数据为准，以上仅供参考";
    private static readonly (string Name, string Id, string Reference)[] Lights =
    [
        ("D65", "D65", "全球通用平均日光"),
        ("CWF", "FL2", "北美商业冷白荧光灯"),
        ("A", "A", "全球通用钨丝白炽灯"),
        ("U3000", "FL12", "美国暖白商业荧光灯"),
        ("TL84", "FL11", "欧洲商业荧光灯")
    ];

    public static CalculationTable Analyze(SpectrumResult reference, ColorInconstancySettings settings)
    {
        if (!Enum.IsDefined(settings.Formula)) throw new ArgumentException("请选择有效的色差公式。");
        var weights = settings.Formula switch
        {
            ComparisonFormula.Cmc => new[] { settings.CmcL, settings.CmcC },
            ComparisonFormula.Ciede2000 => new[] { settings.KL, settings.KC, settings.KH },
            _ => Array.Empty<double>()
        };
        if (weights.Any(value => !double.IsFinite(value) || value <= 0))
            throw new ArgumentException("色差权重必须是有限正数。");
        var comparisonOptions = new ColorComparisonOptions
        {
            Cmc = settings.Formula == ComparisonFormula.Cmc ? new(settings.CmcL, settings.CmcC) : new(),
            Ciede2000 = settings.Formula == ComparisonFormula.Ciede2000 ? new(settings.KL, settings.KC, settings.KH) : new(),
            Evaluation = new() { Formula = settings.Formula }
        };

        var spectrum = new Spectrum
        {
            StartingWavelength = reference.Start, EndingWavelength = reference.End,
            WavelengthInterval = reference.Step, Spectrums = reference.Values
        };
        var observer = reference.Observer == StandardObserver.Degree2 ? "2° · CIE 1931" : "10° · CIE 1964";
        var standard = reference.Lab;
        var conditions = $"标样参考：{reference.Illuminant} / {observer} · L* {F(standard.CIEL)} · a* {F(standard.CIEA)} · b* {F(standard.CIEB)}\n" +
            $"同一反射光谱 · {reference.Start}–{reference.End} nm / {reference.Step} nm · {settings.FormulaName}\n" +
            "各光源使用原波段的积分白点（Y = 100），与原照明条件的 Lab 直接计算色差。";
        var unavailable = new List<string>();
        var rows = Lights.Select(light =>
        {
            CIELAB? lab = null;
            double? delta = null;
            string[] comments = ["—", "—", "—"];
            string? reason = null;
            var illuminant = CieSpectralData.GetIlluminantSpectrum(light.Id);
            if (reference.Start < illuminant.StartingWavelength || reference.End > illuminant.EndingWavelength)
                reason = $"光源仅覆盖 {illuminant.StartingWavelength}–{illuminant.EndingWavelength} nm，无法按原波段计算。";
            else
            {
                try
                {
                    var xyz = ChromaticityConversion.REFToXYZ(spectrum, illuminant, reference.Observer);
                    var white = ChromaticityMatch.GetStandardWhitePoint(illuminant, reference.Observer, reference.Start, reference.End);
                    var candidate = ChromaticityConversion.XYZToLab(xyz, white);
                    var comparison = ChromaticityMatch.CompareColors(standard, candidate, comparisonOptions);
                    var difference = settings.Formula switch
                    {
                        ComparisonFormula.Cie76 => comparison.DeltaE1976,
                        ComparisonFormula.Cmc => comparison.DeltaECmc,
                        _ => comparison.DeltaE2000
                    };
                    // Reject overflow for this illuminant without discarding the other results.
                    _ = F(candidate.CIEL); _ = F(candidate.CIEA); _ = F(candidate.CIEB); _ = F(difference);
                    lab = candidate; delta = difference;
                    comments = ColorEvaluationPresentation.Comments(comparison)[..3];
                }
                catch (Exception ex) when (ex is ArgumentException or ArithmeticException)
                { reason = $"无法计算：{ex.Message}"; }
            }
            var rowConditions = conditions.Replace("\n", "; ") + "; achromatic: L* < 10 OR C* < 5; " + IlluminantReferenceNotice;
            if (reason is not null)
            {
                var explanation = $"光源 {light.Name}：{reason}";
                unavailable.Add(explanation); rowConditions += "。" + explanation;
            }
            string[] row =
            [
                light.Name, light.Reference, lab is null ? "—" : F(lab.CIEL), lab is null ? "—" : F(lab.CIEA), lab is null ? "—" : F(lab.CIEB),
                delta.HasValue ? F(delta.Value) : "—", ..comments, light.Id,
                reference.Illuminant, F(standard.CIEL), F(standard.CIEA), F(standard.CIEB), observer,
                reference.Start.ToString(CultureInfo.InvariantCulture), reference.End.ToString(CultureInfo.InvariantCulture),
                reference.Step.ToString(CultureInfo.InvariantCulture), settings.FormulaName,
                rowConditions
            ];
            return row;
        }).ToArray();
        if (unavailable.Count > 0) conditions += "\n" + string.Join('\n', unavailable);
        return new(["照明体", "照明体参考", "L*", "a*", "b*", settings.FormulaName, ..ColorEvaluationPresentation.Headers[..3], "光源数据 ID", "标样照明体",
            "标样 L*", "标样 a*", "标样 b*", "观察者", "起始波长 / nm", "结束波长 / nm", "间隔 / nm", "色差公式", "计算条件"], rows, conditions);
    }

    private static string F(double value) => ToolCalculations.F(value);
}
