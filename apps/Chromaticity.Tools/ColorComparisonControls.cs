using Avalonia.Controls;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private sealed class ColorComparisonInputs
    {
        public TextBox Lightness { get; } = SmallInput("10");
        public TextBox Chroma { get; } = SmallInput("5");
        public Control View { get; }

        public ColorComparisonInputs(string namePrefix)
        {
            Lightness.Name = namePrefix + "AchromaticL";
            Chroma.Name = namePrefix + "AchromaticC";
            View = Stack(Fields(("无彩色 L* 阈值", Lightness), ("无彩色 C* 阈值", Chroma)),
                Note("L* 低于阈值或 C* 低于阈值，满足任一项即按无彩色评价；标样或样品为无彩色时不评价色相偏色。阈值允许设为 0。"));
        }

        public ColorComparisonOptions Options()
        {
            static double Threshold(TextBox input, string name)
            {
                var value = ToolCalculations.Number(input.Text ?? "", name);
                if (value < 0) throw new ArgumentException("无彩色阈值必须是有限非负数。");
                return value;
            }
            return new()
            {
                AchromaticLightnessThreshold = Threshold(Lightness, "L*"),
                AchromaticChromaThreshold = Threshold(Chroma, "C*")
            };
        }
    }
}
