using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

/// <summary>Formats library CCT/CRI results and their applicability; no colour algorithms.</summary>
public static class LightQualityPresentation
{
    public static readonly string[] CctHeaders = ["CCT (McCamy) / K", "CCT (CIE 1960 UCS) / K", "Duv (1960)", "u (1960)", "v (1960)", "CCT 说明"];
    public static readonly string[] CriHeaders = ["CRI Ra", ..Enumerable.Range(1, 14).Select(i => $"CRI R{i}"), "CRI 说明"];

    public static string[] Cct(CIExyY? xy, StandardObserver observer, Cct1960Result? result = null)
    {
        if (xy is null || xy.CIEy <= 0)
            return ["—", "—", "—", "—", "—", "色度未定义，无法计算 CCT。"];
        var uv = ChromaticityConversion.xyToLuv1960(xy);
        result ??= ChromaticityConversion.luv1960ToCCTResult(uv, observer);
        double approximation = ChromaticityConversion.xyToCCT(xy);
        string description = "McCamy 为 xy 多项式近似；1960 UCS 为到普朗克轨迹的最短距离求解。";
        if (!result.IsInTemperatureRange) description += "1960 UCS 求解超出 1000–25000 K 范围。";
        else if (!result.Temperature.HasValue) description += "远离普朗克轨迹（|Duv| > 0.05），CCT 不适用。";
        if (observer == StandardObserver.Degree10)
            description += "10° 数据的 CCT 为观察者相关估算；标准 CCT 使用 1931 2°。";
        return [double.IsFinite(approximation) && approximation > 0 ? ToolCalculations.F(approximation) : "—",
            result.Temperature.HasValue ? ToolCalculations.F(result.Temperature.Value) : "—",
            result.Duv.ToString("F6", System.Globalization.CultureInfo.InvariantCulture), ToolCalculations.F(uv.CIEu), ToolCalculations.F(uv.CIEv), description];
    }

    public static string[] Cri(Spectrum spectrum, bool hasFullRange)
    {
        if (!hasFullRange) return UnavailableCri("CRI 需要覆盖 380–780 nm 的光源光谱；当前波段不足。");
        var result = ChromaticityConversion.SPDToCRI(spectrum);
        if (!result.Ra.HasValue) return UnavailableCri(result.Cct.Temperature is null
            ? "光源的 CCT 不适用，CRI 无法计算。"
            : "光源与参考光源的 1960 uv 距离超过 0.0054，CRI 不适用。");
        return [ToolCalculations.F(result.Ra.Value), ..result.SpecialIndices.Select(ToolCalculations.F),
            $"CIE 13.3-1995 / 1931 2° · 380–780 nm / 1 nm · {result.ReferenceType} · 参考 CCT {ToolCalculations.F(result.Cct.Temperature!.Value)} K · Ra 为 R1–R8 平均值。"];
    }

    public static string[] UnavailableCri(string explanation) => [..Enumerable.Repeat("—", 15), explanation];
}
