using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.Model;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private Control TextileMetamerismPage()
    {
        TextileSpectra? calculated = null;
        var standard = Input("MetamerismStandardInput", "380,18.00\n390,18.29\n…");
        var sample = Input("MetamerismSampleInput", "380,18.00\n390,18.29\n…");
        standard.Text = TextileMetamerismExamples.Standard;
        sample.Text = TextileMetamerismExamples.MetamericSample;
        var observer = Observer(); observer.Name = "MetamerismObserver";
        var units = Select(["百分数（18 = 18%）", "比例（0.18 = 18%）"]); units.Name = "MetamerismUnits";
        var start = SmallInput("380"); start.Name = "MetamerismStart";
        var end = SmallInput("780"); end.Name = "MetamerismEnd";
        var step = SmallInput("10"); step.Name = "MetamerismStep";
        static int Integer(TextBox box) => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : throw new ArgumentException("波长和间隔必须是整数。");

        var formula = Select(DifferenceFormulaChoices);
        formula.Name = "MetamerismFormula"; formula.SelectedIndex = (int)ComparisonFormula.Ciede2000;
        var cmcL = SmallInput("2"); cmcL.Name = "MetamerismCmcL";
        var cmcC = SmallInput("1"); cmcC.Name = "MetamerismCmcC";
        var kl = SmallInput("1"); kl.Name = "MetamerismKL";
        var kc = SmallInput("1"); kc.Name = "MetamerismKC";
        var kh = SmallInput("1"); kh.Name = "MetamerismKH";
        var parameterRow = Fields(("色差公式", formula), ("CMC l", cmcL), ("CMC c", cmcC),
            ("ΔE00 kL", kl), ("ΔE00 kC", kc), ("ΔE00 kH", kh));
        var cmcFields = parameterRow.Children.Skip(1).Take(2).ToArray();
        var e00Fields = parameterRow.Children.Skip(3).ToArray();
        var noWeights = Note("ΔE1976 无可调系数。");
        var settings = Stack(parameterRow, noWeights,
            Note("CWF 使用 FL2，U3000 使用 U30（FL12），TL84 使用 FL11。修改公式或系数后自动更新分析。"));
        var result = new ResultPanel(_downloader, title: "不同光源下的色差对比", settings: settings,
            renderResult: TextileMetamerismResults, successMessage: "已更新分析。无法按原条件计算的光源会单独标明原因。")
            { Name = "TextileMetamerismResult" };
        ColorInconstancySettings Settings()
        {
            var selected = (ComparisonFormula)formula.SelectedIndex;
            double Weight(TextBox box, string name) => ToolCalculations.Positive(box.Text ?? "", name);
            return selected switch
            {
                ComparisonFormula.Cmc => new(selected, Weight(cmcL, "CMC l"), Weight(cmcC, "CMC c")),
                ComparisonFormula.Ciede2000 => new(selected, KL: Weight(kl, "kL"), KC: Weight(kc, "kC"), KH: Weight(kh, "kH")),
                _ => new(selected)
            };
        }
        void UpdateAnalysis()
        {
            foreach (var field in cmcFields) field.IsVisible = formula.SelectedIndex == (int)ComparisonFormula.Cmc;
            foreach (var field in e00Fields) field.IsVisible = formula.SelectedIndex == (int)ComparisonFormula.Ciede2000;
            noWeights.IsVisible = formula.SelectedIndex == (int)ComparisonFormula.Cie76;
            if (calculated is { } spectra) result.Run(() => TextileMetamerismCalculations.Analyze(spectra, Settings()));
        }
        formula.SelectionChanged += (_, _) => UpdateAnalysis();
        foreach (var box in new[] { cmcL, cmcC, kl, kc, kh })
            box.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) UpdateAnalysis(); };
        UpdateAnalysis();

        var plot = DiagramControls.Localize(new SpectrumPlot
        {
            Name = "MetamerismSpectrumPlot", Height = 220, IsVisible = false,
            StandardCurveColor = Color.Parse("#888888"), CurveColor = Color.Parse("#222222")
        });
        var navigation = DiagramControls.Localize(new SpectrumNavigator { Name = "MetamerismSpectrumNavigator", Plot = plot, IsVisible = false });
        var plotNote = Note("反射光谱对比：灰线为标准品，黑线为样品；纵轴统一显示百分数。");
        plotNote.IsVisible = false;
        void LoadExample(Func<bool, string> example)
        {
            observer.SelectedIndex = 1;
            start.Text = "380"; end.Text = "780"; step.Text = "10";
            var fraction = units.SelectedIndex == 1;
            standard.Text = TextileMetamerismExamples.GetStandard(fraction); sample.Text = example(fraction);
            // Loading the same example must clear prior results as well.
            Invalidate();
        }
        void Invalidate()
        {
            calculated = null; plot.Clear(); plot.IsVisible = navigation.IsVisible = plotNote.IsVisible = false;
            result.Invalidate();
        }
        var columns = new ResponsiveColumns { Name = "MetamerismInputs" };
        foreach (var (label, input) in new[] { ("标准品相对反射光谱", standard), ("样品相对反射光谱", sample) })
            columns.Children.Add(Stack(Text(label, 16, true), input,
                Actions(TemplateButton(input, result, () => CsvDataFiles.SpectrumTemplate(Integer(start), Integer(end), Integer(step), units.SelectedIndex == 1)),
                    ImportButton(input, result, CsvInputKind.Spectrum))));
        var calculate = Primary("评价同色异谱", () =>
        {
            Invalidate();
            result.Run(() =>
            {
                var spectra = TextileMetamerismCalculations.Parse(standard.Text ?? "", sample.Text ?? "",
                    Integer(start), Integer(end), Integer(step), units.SelectedIndex == 1, SelectedObserver(observer));
                var table = TextileMetamerismCalculations.Analyze(spectra, Settings());
                calculated = spectra;
                plot.StandardData = SpectrumDiagramData.FromUniform(spectra.Standard.Spectrums!, Integer(start), Integer(step));
                plot.SetData(spectra.Sample.Spectrums!, Integer(start), Integer(step));
                plot.IsVisible = navigation.IsVisible = plotNote.IsVisible = true;
                return table;
            });
        });
        calculate.Name = "MetamerismCalculate";
        Watch(result, () =>
        {
            calculated = null; plot.Clear(); plot.IsVisible = navigation.IsVisible = plotNote.IsVisible = false;
        }, standard, sample, observer, units, start, end, step);
        return Stack(Card(Stack(Text("纺织品同色异谱评价", 24, true),
            Note("分别输入一条标准品和样品的相对反射光谱，每行一个采样点：一列反射率，或两列波长、反射率。两条光谱使用相同单位、波段和间隔；CSV 可带表头。"),
            Fields(("观察者", observer), ("输入单位", units)),
            Fields(("起始波长 / nm", start), ("结束波长 / nm", end), ("间隔 / nm", step)),
            Note("分析光源固定为 D65 / CWF / A / U3000 / TL84。超出某光源覆盖波段时，该行显示原因；保留其他光源结果。"),
            columns, Note(TextileMetamerismExamples.Notice),
            Actions(Button("载入同色异谱示例", () => LoadExample(TextileMetamerismExamples.GetMetamericSample)),
                Button("载入相同光谱示例", () => LoadExample(TextileMetamerismExamples.GetStandard)),
                Button("载入明显色差示例", () => LoadExample(TextileMetamerismExamples.GetDifferentSample)), calculate),
            plotNote, navigation, plot)), result);
    }

    private static Control TextileMetamerismResults(CalculationTable table)
    {
        var grid = ComparisonTable("MetamerismTable", 9, 70, 180, 140, 85, 85, 85, 85, 85, 85, ColorDifferenceColumnWidth, 90, 90, 110);
        var standardHex = Array.IndexOf(table.Headers, "标样 sRGB");
        var sampleHex = Array.IndexOf(table.Headers, "样品 sRGB");
        var rows = new[] { table.Headers }.Concat(table.Rows).ToArray();
        for (var row = 0; row < rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var column = 0; column < 13; column++)
            {
                var sourceColumn = column > 2 ? column - 1 : column;
                Control content = column == 2 && row > 0
                    ? PairedColorSwatches(rows[row][standardHex], rows[row][sampleHex], "Metamerism", rows[row][0])
                    : row == 0 && sourceColumn == 8 ? DifferenceHeader(rows[row][sourceColumn])
                    : Text(column == 2 ? "sRGB 对比\n左标样 / 右样品" : rows[row][sourceColumn],
                        row == 0 ? 12 : 14, row == 0 || sourceColumn == 8);
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
        }, Note(TextileMetamerismCalculations.PreviewNotice), Note(ColorInconstancyCalculations.IlluminantReferenceNotice));
    }
}
