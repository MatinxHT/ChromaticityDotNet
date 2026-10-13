using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Services;

/// <summary>Full-precision sample and library CCT result shared by both diagram projections.</summary>
public sealed record PlanckianDiagramData(ChromaticityCoordinates Sample, StandardObserver Observer, Cct1960Result Cct)
{
    public static PlanckianDiagramData Create(ChromaticityCoordinates sample, StandardObserver observer) =>
        new(sample, observer, ChromaticityConversion.luv1960ToCCTResult(
            ChromaticityConversion.xyToLuv1960(sample.ToXyY()), observer));

    public const string Explanation = "普朗克轨迹覆盖 1000–25000 K。T 为 CIE 1960 uv 中的最近轨迹点；点线连接光源或样品与 T。Duv 按 1960 uv 计算，图中投影连线的长度不代表 Duv。";
    public const string UnavailableExplanation = "CCT 不适用时仍显示搜索范围内的最近点；若求解超出温度范围，连线指向轨迹端点。10° 轨迹与 CCT 为观察者相关估算，标准 CCT 使用 1931 2°。";
}
