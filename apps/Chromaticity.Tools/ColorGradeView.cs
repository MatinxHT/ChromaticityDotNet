using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chromaticity.Tools.Services;

namespace Chromaticity.Tools;

public sealed partial class MainView
{
    private Control ColorGradePage()
    {
        var result = new ResultPanel(_downloader, title: "色差分级色卡", renderResult: ColorGradeCards,
            successMessage: "已生成色卡。各方向按所选公式和阈值逐级展开。", allowExport: false);
        result.Name = "GradeResult";
        ColorGradeResult? generated = null;
        ColorGradeComparison? composed = null;
        ColorGradeComparison? analyzed = null;
        var composedResult = new ResultPanel(_downloader, title: "目标色与标样比较",
            renderResult: _ => ColorGradeComparisonView(composed!), successMessage: "已推算目标色，下方显示目标颜色、等级与色差。", allowExport: false);
        var analyzedResult = new ResultPanel(_downloader, title: "样本色差与等级分析",
            renderResult: _ => ColorGradeComparisonView(analyzed!), successMessage: "已分析样本，下方显示整数等级与色差。", allowExport: false);
        composedResult.IsVisible = analyzedResult.IsVisible = false;
        var l = SmallInput("50"); l.Name = "GradeL";
        var a = SmallInput("30"); a.Name = "GradeA";
        var b = SmallInput("20"); b.Name = "GradeB";
        var formula = Select(["ΔE1976 / CIE76", "CMC (l:c)", "ΔE00 / CIEDE2000"]);
        formula.SelectedIndex = (int)ColorGradeFormula.Cmc;
        formula.Name = "GradeFormula";
        var cmcRatio = Select(["2:1", "1:1", "自定义"]); cmcRatio.Name = "GradeCmcRatio";
        var cmcL = SmallInput("2"); cmcL.Name = "GradeCmcL";
        var cmcC = SmallInput("1"); cmcC.Name = "GradeCmcC";
        var levels = Select(Enumerable.Range(1, ColorGradeCalculations.MaxLevels).Select(n => $"每侧 {n} 级").ToArray());
        levels.SelectedIndex = 3; levels.Name = "GradeLevels";
        var thresholdSelect = Select(["0.5", "1", "1.5", "2", "3", "自定义"]);
        thresholdSelect.SelectedIndex = 2; thresholdSelect.Name = "GradeThresholdSelect";
        var threshold = SmallInput("1.5"); threshold.Name = "GradeThresholdCustom";
        var thresholdFields = Fields(("自定义阈值 ΔE", threshold));
        var cmcFields = Fields(("CMC l:c", cmcRatio));
        var customFields = Fields(("CMC l", cmcL), ("CMC c", cmcC));
        var parameterFields = Fields(("色差公式", formula), ("一级阈值 ΔE", thresholdSelect), ("展开级数", levels));
        parameterFields.Children.Add(cmcFields);
        parameterFields.Children.Add(customFields);
        parameterFields.Children.Add(thresholdFields);
        var lGrade = Select(["0 级"]); lGrade.Name = "ComposeLGrade";
        var cGrade = Select(["0 级"]); cGrade.Name = "ComposeCGrade";
        var hGrade = Select(["0 级"]); hGrade.Name = "ComposeHGrade";
        var analysisL = SmallInput("50"); analysisL.Name = "AnalysisStandardL";
        var analysisA = SmallInput("30"); analysisA.Name = "AnalysisStandardA";
        var analysisB = SmallInput("20"); analysisB.Name = "AnalysisStandardB";
        var sampleL = SmallInput("53"); sampleL.Name = "AnalysisSampleL";
        var sampleA = SmallInput("32"); sampleA.Name = "AnalysisSampleA";
        var sampleB = SmallInput("18"); sampleB.Name = "AnalysisSampleB";
        var rounding = Select(["四舍五入", "向上取整"]); rounding.Name = "GradeRounding";
        var analysisInputs = new ResponsiveColumns { Name = "GradeLabInputs" };
        analysisInputs.Children.Add(Stack(Text("标样", 16, true),
            Fields(("L*（0–100）", analysisL), ("a*", analysisA), ("b*", analysisB))));
        analysisInputs.Children.Add(Stack(Text("样本", 16, true),
            Fields(("L*（0–100）", sampleL), ("a*", sampleA), ("b*", sampleB))));
        var context = Note("");
        var generatedTools = new StackPanel { Spacing = 22, IsVisible = false, Name = "GradeAnalysisTools" };
        var useTarget = Button("使用刚推算的目标色", () =>
        {
            if (composed is null) return;
            analysisL.Text = ToolCalculations.F(composed.Standard.L);
            analysisA.Text = ToolCalculations.F(composed.Standard.A);
            analysisB.Text = ToolCalculations.F(composed.Standard.B);
            sampleL.Text = ToolCalculations.F(composed.Sample.L);
            sampleA.Text = ToolCalculations.F(composed.Sample.A);
            sampleB.Text = ToolCalculations.F(composed.Sample.B);
        });
        useTarget.IsEnabled = false;
        void InvalidateComposition()
        {
            composed = null; composedResult.Invalidate(); composedResult.IsVisible = false; useTarget.IsEnabled = false;
        }
        void InvalidateAnalysis() { analyzed = null; analyzedResult.Invalidate(); analyzedResult.IsVisible = false; }
        void Invalidate()
        {
            generated = null; generatedTools.IsVisible = false;
            result.Invalidate(); InvalidateComposition(); InvalidateAnalysis();
        }
        void UpdateParameters()
        {
            cmcFields.IsVisible = formula.SelectedIndex == (int)ColorGradeFormula.Cmc;
            customFields.IsVisible = cmcFields.IsVisible && cmcRatio.SelectedIndex == 2;
            thresholdFields.IsVisible = thresholdSelect.SelectedIndex == 5;
        }
        // Property notifications are synchronous; queued TextChanged must not erase a just-generated card.
        static void Observe(Action invalidate, params Control[] controls)
        {
            foreach (var control in controls)
            {
                if (control is TextBox box)
                    box.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) invalidate(); };
                if (control is ComboBox combo) combo.SelectionChanged += (_, _) => invalidate();
            }
        }
        Observe(Invalidate, l, a, b, cmcL, cmcC, threshold);
        Observe(InvalidateComposition, lGrade, cGrade, hGrade);
        Observe(InvalidateAnalysis, analysisL, analysisA, analysisB, sampleL, sampleA, sampleB, rounding);
        foreach (var combo in new[] { formula, cmcRatio, levels, thresholdSelect })
            combo.SelectionChanged += (_, _) => { UpdateParameters(); Invalidate(); };
        UpdateParameters();
        var compose = Primary("推算目标色", () =>
        {
            composedResult.IsVisible = true;
            composedResult.Run(() =>
            {
                var card = generated ?? throw new InvalidOperationException("请先生成色卡。");
                composed = ColorGradeCalculations.Compose(card, lGrade.SelectedIndex - card.Levels,
                    cGrade.SelectedIndex - card.Levels, hGrade.SelectedIndex - card.Levels);
                useTarget.IsEnabled = true;
                return composed.Table;
            });
        });
        compose.Name = "ComposeGrades";
        generatedTools.Children.Add(context);
        generatedTools.Children.Add(Card(Stack(Text("按等级推算目标色", 20, true),
            Note("按 L* → C* → h° 顺序执行。每一步以前一步颜色为标样，保持其他坐标分量，按当前公式达到一个等级阈值。"),
            Fields(("明度 L* 等级", lGrade), ("彩度 C* 等级", cGrade), ("色相 h° 等级", hGrade)),
            Actions(Button("载入 +2 / +1 / -3 示例", () =>
            {
                if (generated is null) return;
                var n = generated.Levels;
                lGrade.SelectedIndex = n + Math.Min(2, n);
                cGrade.SelectedIndex = n + 1;
                hGrade.SelectedIndex = n - Math.Min(3, n);
            }), compose))));
        generatedTools.Children.Add(composedResult);
        var analyze = Primary("分析样本等级", () =>
        {
            analyzedResult.IsVisible = true;
            analyzedResult.Run(() =>
            {
                var card = generated ?? throw new InvalidOperationException("请先生成色卡。");
                analyzed = ColorGradeCalculations.Analyze($"{analysisL.Text}\t{analysisA.Text}\t{analysisB.Text}",
                    $"{sampleL.Text}\t{sampleA.Text}\t{sampleB.Text}", card.Settings, (ColorGradeRounding)rounding.SelectedIndex);
                return analyzed.Table;
            });
        });
        analyze.Name = "AnalyzeGrades";
        generatedTools.Children.Add(Card(Stack(Text("标样与样本分级分析", 20, true),
            Note("输入一个标样和一个样本的 Lab，使用已生成色卡的公式与等级阈值。"),
            analysisInputs, Fields(("等级计算方式", rounding)),
            Actions(useTarget, analyze),
            Note("按 L* → C* → h° 拆分偏差，逐级累计后按所选方式显示整数等级。先对等级幅度四舍五入或向上取整，再保留正负方向；四舍五入遇到半级时进一。色相取最短角度路径，中性色未定义的色相显示为 —。"))));
        generatedTools.Children.Add(analyzedResult);
        var generate = Primary("生成色卡", () =>
        {
            Invalidate();
            result.Run(() =>
            {
                var currentFormula = (ColorGradeFormula)formula.SelectedIndex;
                var weightL = 2.0; var weightC = 1.0;
                if (currentFormula == ColorGradeFormula.Cmc)
                {
                    weightL = cmcRatio.SelectedIndex switch
                    {
                        0 => 2, 1 => 1, _ => ToolCalculations.Positive(cmcL.Text ?? "", "CMC l")
                    };
                    weightC = cmcRatio.SelectedIndex == 2 ? ToolCalculations.Positive(cmcC.Text ?? "", "CMC c") : 1;
                }
                var step = ToolCalculations.Number(thresholdSelect.SelectedIndex == 5 ? threshold.Text ?? "" :
                    thresholdSelect.SelectedItem?.ToString() ?? "", "等级阈值 ΔE");
                var card = ColorGradeCalculations.Generate($"{l.Text}\t{a.Text}\t{b.Text}", currentFormula,
                    levels.SelectedIndex + 1, weightL, weightC, step);
                foreach (var combo in new[] { lGrade, cGrade, hGrade })
                {
                    combo.ItemsSource = Enumerable.Range(-card.Levels, card.Levels * 2 + 1)
                        .Select(n => $"{n.ToString("+0;-0;0", CultureInfo.InvariantCulture)} 级").ToArray();
                    combo.SelectedIndex = card.Levels;
                }
                analysisL.Text = l.Text; analysisA.Text = a.Text; analysisB.Text = b.Text;
                context.Text = $"当前分析条件：{card.Formula} · 每一级 ΔE = {step.ToString("0.####", CultureInfo.InvariantCulture)} · 顺序 L* → C* → h°。";
                generated = card; generatedTools.IsVisible = true;
                return card.Table;
            });
        });
        generate.Name = "GenerateGrades";
        return Stack(Card(Stack(Text("色差分级色卡", 24, true),
            Note("以一个标样为中心，沿明度、彩度、色相分别向两侧展开。每向外一级，按所选公式与前一级保持所选等级阈值，默认 ΔE = 1.5。"),
            Fields(("标样 L*（0–100）", l), ("标样 a*", a), ("标样 b*", b)),
            parameterFields,
            Actions(Button("载入示例", () =>
            {
                l.Text = "50"; a.Text = "30"; b.Text = "20"; levels.SelectedIndex = 3;
            }), generate),
            Note("ΔE00 固定 kL:kC:kH = 1:1:1；CMC 可选择或自定义 l:c。阈值支持最多四位小数，最小 0.0001。\n级别是沿该方向的步数；相对标样的累计 ΔE 不一定等于级数 × 阈值。CMC 每步以前一级作标样，向外计算。\n预览使用 D65 / 2°，超出 sRGB 色域会裁剪。达到坐标边界或无法满足阈值的位置显示为不可生成。"))), result, generatedTools);
    }

    private static Control ColorGradeCards(CalculationTable table)
    {
        static TextBlock CenteredText(string value, double size = 14, bool bold = false)
        {
            var text = Text(value, size, bold);
            text.TextAlignment = TextAlignment.Center;
            return text;
        }
        var content = new StackPanel { Spacing = 24 };
        content.Children.Add(CenteredText(table.Rows[0][Array.IndexOf(table.Headers, "色差公式及参数")] +
            " · 一级 ΔE = " + table.Rows[0][Array.IndexOf(table.Headers, "目标级间 ΔE")], 16, true));
        foreach (var group in table.Rows.GroupBy(row => row[0]))
        {
            var ends = group.Key switch
            {
                "明度 L*" => "更暗 ← 标样 → 更亮",
                "彩度 C*" => "更灰 ← 标样 → 更艳",
                _ => "h° 减小 ← 标样 → h° 增大"
            };
            var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0,
                HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var row in group)
            {
                string Value(string header) => row[Array.IndexOf(table.Headers, header)];
                var standard = Value("级别") == "0";
                var available = Value("状态") == "有效";
                var label = standard ? "标样 · 0 级" : $"{(Value("级别").StartsWith('-') ? "" : "+")}{Value("级别")} 级";
                var column = new StackPanel { Width = 120, Spacing = 0 };
                var heading = Text(label, 13, standard);
                heading.Height = 28;
                heading.HorizontalAlignment = HorizontalAlignment.Center;
                heading.Foreground = standard ? Brushes.Black : Muted;
                column.Children.Add(heading);
                column.Children.Add(new Border
                {
                    Name = "GradeSwatch", Height = 112,
                    Background = available ? Brush.Parse(Value("HEX")) : Brush.Parse("#EEEEEE"),
                    Child = available ? null : new LocalizedTextBlock { Text = "不可生成", FontSize = 12, Foreground = Muted,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                });
                var details = new StackPanel { Spacing = 8 };
                if (available)
                {
                    details.Children.Add(CenteredText(Value("HEX"), 12));
                    details.Children.Add(CenteredText($"L* {Value("L*")}\na* {Value("a*")}\nb* {Value("b*")}", 11));
                    details.Children.Add(CenteredText($"C* {Value("C*")}\nh° {Value("h°")}", 11));
                    details.Children.Add(CenteredText($"标样 ΔE {Value("相对标样 ΔE")}", 11));
                }
                else
                {
                    var reason = Note(Value("状态")); reason.TextAlignment = TextAlignment.Center;
                    details.Children.Add(reason);
                }
                column.Children.Add(new Border
                {
                    Padding = new Thickness(9, 12, 9, 8), Child = details,
                    Background = standard ? Brush.Parse("#F5F5F5") : Brushes.White
                });
                strip.Children.Add(column);
            }
            var direction = Note(ends); direction.TextAlignment = TextAlignment.Center;
            content.Children.Add(Stack(CenteredText(group.Key, 18, true), direction, new ScrollViewer
            {
                Content = strip, HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            }));
        }
        return content;
    }
}
