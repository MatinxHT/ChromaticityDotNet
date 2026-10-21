using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private (ResultPanel Panel, Action Update) CreateInconstancyAnalysis(Func<SpectrumResult?> reference)
    {
        var formula = Select(DifferenceFormulaChoices);
        formula.Name = "InconstancyFormula"; formula.SelectedIndex = (int)ComparisonFormula.Ciede2000;
        var cmcL = SmallInput("2"); cmcL.Name = "InconstancyCmcL";
        var cmcC = SmallInput("1"); cmcC.Name = "InconstancyCmcC";
        var kl = SmallInput("1"); kl.Name = "InconstancyKL";
        var kc = SmallInput("1"); kc.Name = "InconstancyKC";
        var kh = SmallInput("1"); kh.Name = "InconstancyKH";
        var cmcFields = Fields(("CMC l", cmcL), ("CMC c", cmcC));
        var e00Fields = Fields(("ΔE00 kL", kl), ("ΔE00 kC", kc), ("ΔE00 kH", kh));
        var noWeights = Note("ΔE1976 无可调系数。");
        var fields = Fields(("色差公式", formula));
        fields.Children.Add(cmcFields); fields.Children.Add(e00Fields); fields.Children.Add(noWeights);
        var settings = Stack(
            Note("分析光源固定为 D65 / CWF / A / U3000 / TL84。始终以原照明条件下的 Lab 为标样，保持反射光谱、观察者、波段和采样间隔一致，分别计算五种光源下的颜色值与色差。"),
            fields, Note("CWF 使用 FL2，U3000 使用 U30（FL12），TL84 使用 FL11。修改公式或系数后自动更新分析。"));
        var result = new ResultPanel(_downloader, title: "颜色不恒常性分析", settings: settings,
            renderResult: InconstancyResults, successMessage: "已更新分析。无法按原条件计算的光源会单独标明原因。")
            { Name = "ColorInconstancyAnalysis" };
        result.ShowNotice("请先计算颜色值，分析将使用同一次计算的原照明条件和 Lab 标样。");
        void Update()
        {
            cmcFields.IsVisible = formula.SelectedIndex == (int)ComparisonFormula.Cmc;
            e00Fields.IsVisible = formula.SelectedIndex == (int)ComparisonFormula.Ciede2000;
            noWeights.IsVisible = formula.SelectedIndex == (int)ComparisonFormula.Cie76;
            if (reference() is not { } spectrum) return;
            result.Run(() =>
            {
                var selected = (ComparisonFormula)formula.SelectedIndex;
                double Weight(TextBox box, string name) => ToolCalculations.Positive(box.Text ?? "", name);
                var options = selected switch
                {
                    ComparisonFormula.Cmc => new ColorInconstancySettings(selected, Weight(cmcL, "CMC l"), Weight(cmcC, "CMC c")),
                    ComparisonFormula.Ciede2000 => new ColorInconstancySettings(selected,
                        KL: Weight(kl, "kL"), KC: Weight(kc, "kC"), KH: Weight(kh, "kH")),
                    _ => new ColorInconstancySettings(selected)
                };
                return ColorInconstancyCalculations.Analyze(spectrum, options);
            });
        }
        formula.SelectionChanged += (_, _) => Update();
        foreach (var box in new[] { cmcL, cmcC, kl, kc, kh })
            box.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) Update(); };
        Update();
        return (result, Update);
    }

    private static Control InconstancyResults(CalculationTable table)
    {
        var grid = ComparisonTable("InconstancyTable", 6, 70, 180, 140, 78, 78, 78, ColorDifferenceColumnWidth, 90, 90, 146);
        var standardHex = Array.IndexOf(table.Headers, "标样 sRGB");
        var sampleHex = Array.IndexOf(table.Headers, "样品 sRGB");
        var rows = new[] { table.Headers }.Concat(table.Rows).ToArray();
        for (var row = 0; row < rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var column = 0; column < 10; column++)
            {
                var sourceColumn = column > 2 ? column - 1 : column;
                Control content = column == 2 && row > 0
                    ? PairedColorSwatches(rows[row][standardHex], rows[row][sampleHex], "Inconstancy", rows[row][0])
                    : row == 0 && sourceColumn == 5 ? DifferenceHeader(rows[row][sourceColumn])
                    : Text(column == 2 ? "sRGB 对比\n左标样 / 右样品" : rows[row][sourceColumn],
                        row == 0 ? 12 : 14, row == 0 || sourceColumn == 5);
                content.VerticalAlignment = VerticalAlignment.Center;
                var cell = new Border
                {
                    Child = content, Padding = new Thickness(10, 12),
                    Background = row == 0 ? Brush.Parse("#F5F5F5") : Brushes.White,
                    BorderBrush = Brush.Parse("#EEEEEE"), BorderThickness = new Thickness(0, 0, 0, 1)
                };
                Grid.SetRow(cell, row); Grid.SetColumn(cell, column); grid.Children.Add(cell);
            }
        }
        return Stack(Note(table.Conditions), new ScrollViewer
        {
            Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        }, Note("色块左侧为原照明标样，右侧为该行光源样品；按 D65 / 10° 将 Lab 转为 sRGB，超出色域会裁剪。"),
            Note(ColorInconstancyCalculations.IlluminantReferenceNotice));
    }

}
