using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chromaticity.Tools.Services;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private static Control ColorGradeComparisonView(ColorGradeComparison comparison)
    {
        static TextBlock Center(string value, double size = 14, bool bold = false)
        {
            var text = Text(value, size, bold); text.TextAlignment = TextAlignment.Center; return text;
        }
        static string Signed(double value) => (Math.Abs(value) < 0.00005 ? 0 : value)
            .ToString("+0.0000;-0.0000;0.0000", System.Globalization.CultureInfo.InvariantCulture);
        static string Lab(ColorGradeLab color) => $"L* {ToolCalculations.F(color.L)} · a* {ToolCalculations.F(color.A)} · b* {ToolCalculations.F(color.B)}";
        var preview = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,150,Auto"),
            MaxWidth = 800, HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var colors = new[] { ("标样", comparison.Standard, comparison.StandardHex),
            ("目标 / 样本", comparison.Sample, comparison.SampleHex) };
        for (var i = 0; i < colors.Length; i++)
        {
            var (label, color, hex) = colors[i];
            var heading = Center(label, 16, true); heading.Margin = new Thickness(0, 0, 0, 8);
            Grid.SetColumn(heading, i); preview.Children.Add(heading);
            var swatch = new Border { Name = i == 0 ? "GradeReferenceSwatch" : "GradeComparedSwatch", Background = Brush.Parse(hex) };
            Grid.SetColumn(swatch, i); Grid.SetRow(swatch, 1); preview.Children.Add(swatch);
            var hue = color.Chroma == 0 ? "—" : ToolCalculations.F(color.Hue);
            var details = new Border { Padding = new Thickness(12), Background = Brush.Parse("#F5F5F5"),
                Child = Stack(Center(hex, 13, true), Center(Lab(color), 13),
                    Center($"C* {ToolCalculations.F(color.Chroma)} · h° {hue}", 13)) };
            Grid.SetColumn(details, i); Grid.SetRow(details, 2); preview.Children.Add(details);
        }
        var conditionText = $"{comparison.Settings.FormulaName} · 一级 ΔE = {ToolCalculations.F(comparison.Settings.StepDeltaE)}";
        if (comparison.Rounding.HasValue) conditionText += " · 等级计算：" + ColorGradeCalculations.RoundingTitle(comparison.Rounding.Value);
        var condition = Note(conditionText);
        condition.TextAlignment = TextAlignment.Center;
        var hueDelta = comparison.DeltaHue.HasValue ? Signed(comparison.DeltaHue.Value) + "°" : "—（不评价色相）";
        var quantified = Center($"ΔL* {Signed(comparison.DeltaL)} · Δa* {Signed(comparison.DeltaA)} · Δb* {Signed(comparison.DeltaB)}\n" +
            $"ΔC* {Signed(comparison.DeltaC)} · Δh° {hueDelta}");
        var grades = new ResponsiveColumns { MaximumColumns = 3 };
        var comments = ColorEvaluationPresentation.Comments(comparison.ColorComparison);
        foreach (var component in comparison.Components)
        {
            var grade = component.Grade.HasValue ? component.Grade.Value.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture) + " 级" : "—";
            var label = ColorGradeCalculations.AxisTitle(component.Axis);
            var direction = comments[(int)component.Axis];
            grades.Children.Add(new Border { Padding = new Thickness(16), Background = Brush.Parse("#F5F5F5"),
                Child = Stack(Center(label, 16, true), Center(grade, 21, true), Center(direction),
                    Center($"本方向总 ΔE {ToolCalculations.F(component.DeltaE)}"),
                    Note(component.Status == "有效" ? comparison.Rounding.HasValue
                        ? "逐级计算后，按所选方式取整。" : "每个完整等级按阈值逐级计算。" : component.Status)) });
        }
        return Stack(preview, condition, Center($"标样 → 目标 / 样本 ΔE = {ToolCalculations.F(comparison.DeltaE)}", 22, true),
            quantified, grades, Note($"无彩色判定：{comments[3]}"),
            Note("等级按 L* → C* → h° 路径拆分；各方向总 ΔE、级数 × 阈值和最终色差可能不同。色相角差 Δh° 单位为度。"));
    }
}
