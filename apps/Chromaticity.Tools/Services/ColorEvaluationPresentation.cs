using ChromaticityDotNet.Model;

namespace Chromaticity.Tools.Services;

/// <summary>Short application labels; the library's English fields remain language-independent.</summary>
public static class ColorEvaluationPresentation
{
    public static readonly string[] Headers = ["明度评价", "彩度评价", "色相偏色", "无彩色判定"];

    public static string[] Comments(ColorComparisonResult comparison) =>
    [
        Label(comparison.Evaluation.LightnessCommentsEnglish),
        Label(comparison.Evaluation.ChromaCommentsEnglish),
        Label(comparison.Evaluation.HueCommentsEnglish),
        Label(comparison.Evaluation.AchromaticCommentsEnglish)
    ];

    public static string Label(string english) => english switch
    {
        "Darker" => "更暗", "Lighter" => "更亮",
        "Lower chroma" => "更灰", "Higher chroma" => "更艳",
        "No change" => "无变化", "Not applicable" => "不适用",
        "More reddish" => "偏红", "More yellowish" => "偏黄", "More greenish" => "偏绿",
        "More bluish" => "偏蓝", "More purplish" => "偏紫",
        "Both achromatic" => "均为无彩色", "Reference achromatic" => "标样无彩色",
        "Sample achromatic" => "样品无彩色", "Both chromatic" => "均为有彩色",
        _ => english // Custom hue-axis phrases are supplied by the developer.
    };
}
