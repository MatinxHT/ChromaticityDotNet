using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using ChromaticityDotNet.Diagrams;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools;

public sealed partial class MainView : UserControl
{
    private static readonly IBrush Muted = Brush.Parse("#666666");
    private readonly IResultDownloader? _downloader;

    public MainView(IResultDownloader? downloader = null, string tool = "spectrum")
    {
        _downloader = downloader;
        var content = new StackPanel { Spacing = 22, MaxWidth = 1240, Margin = new Thickness(28, 24), HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(tool switch
        {
            "difference" => DifferencePage(),
            "conversion" => ConversionPage(),
            "illuminant" => IlluminantPage(),
            "grades" => ColorGradePage(),
            "wavelength" => WavelengthPage(),
            _ => SpectrumPage()
        });
        content.Children.Add(Note($"ChromaticityDotNet v{LibraryInfo.Version} · 代码 MIT · CIE 数据 CC BY-SA 4.0\n波长保留两位小数，Duv 保留六位小数，其他计算保留四位小数。参考白点由光源光谱与所选观察者积分得到。色块仅为屏幕近似，不能代替仪器测量。"));
        Content = new ScrollViewer { Content = content, Background = Brush.Parse("#F5F5F5"), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private Control SpectrumPage()
    {
        SpectrumResult? calculated = null;
        var result = new ResultPanel(_downloader, sampleCards: true);
        result.Name = "SpectrumResult";
        var analysis = CreateInconstancyAnalysis(() => calculated);
        var plot = DiagramControls.Localize(new SpectrumPlot { Name = "ReflectanceSpectrumPlot", Height = 200, IsVisible = false });
        var navigation = DiagramControls.Localize(new SpectrumNavigator { Name = "ReflectanceSpectrumNavigator", Plot = plot, IsVisible = false });
        var example = string.Join('\n', Enumerable.Range(0, 41).Select(i =>
            $"{380 + i * 10},{ToolCalculations.F(41.5 - 23.5 * Math.Cos(Math.PI * i / 20))}"));
        var input = Input("SpectrumInput", "380,18\n390,18.2893\n…");
        input.Text = example;
        var light = Light(); var observer = Observer();
        light.Name = "SpectrumIlluminant"; observer.Name = "SpectrumObserver";
        var start = SmallInput("380"); var end = SmallInput("780"); var step = SmallInput("10");
        start.Name = "SpectrumStart"; end.Name = "SpectrumEnd"; step.Name = "SpectrumStep";
        var coverage = Note("");
        void UpdateCoverage()
        {
            var info = ToolCalculations.Illuminants[light.SelectedIndex];
            coverage.Text = $"所选光源 {info.Id} 可用波段：{Math.Max(360, info.StartingWavelength)}–{Math.Min(830, info.EndingWavelength)} nm。";
        }
        light.SelectionChanged += (_, _) => UpdateCoverage(); UpdateCoverage();
        var units = Select(["百分数（18 = 18%）", "比例（0.18 = 18%）"]);
        var form = Stack(
            Text("光谱颜色值计算及不恒常性分析", 24, true),
            Note("使用库内 CIE 光谱数据。输入一列反射率，或两列“波长、反射率”；每行一个采样点。仅导入 CSV，可有表头，列名不限。模板按当前波段和单位生成。"),
            Fields(("照明体", light), ("观察者", observer), ("输入单位", units)),
            Fields(("起始波长 / nm", start), ("结束波长 / nm", end), ("间隔 / nm", step)), coverage,
            input,
            Actions(Button("载入默认示例光谱", () =>
            {
                start.Text = "380"; end.Text = "780"; step.Text = "10"; units.SelectedIndex = 0;
                input.Text = example;
            }), TemplateButton(input, result, () => CsvDataFiles.SpectrumTemplate(
                int.Parse(start.Text ?? "", CultureInfo.InvariantCulture), int.Parse(end.Text ?? "", CultureInfo.InvariantCulture),
                int.Parse(step.Text ?? "", CultureInfo.InvariantCulture), units.SelectedIndex == 1)),
                ImportButton(input, result, CsvInputKind.Spectrum), Primary("计算颜色值", () => result.Run(() =>
            {
                static int Integer(TextBox box) => int.TryParse(box.Text, out var n) ? n : throw new ArgumentException("波长和间隔必须是整数。");
                var data = ToolCalculations.Reflectance(input.Text ?? "", Integer(start), Integer(end), Integer(step), units.SelectedIndex == 1,
                    SelectedLight(light), SelectedObserver(observer));
                plot.SetData(data.Values, data.Start, data.Step); plot.IsVisible = true; navigation.IsVisible = true;
                calculated = data; analysis.Update();
                return data.Table;
            }))),
            Note("参考照明体包含全部 50 条 CIE 光源，白点由光谱与所选观察者积分计算。\nLab / Luv 使用与反射光谱相同波段的积分白点。sRGB 预览在 D65 下显示，会裁剪超色域颜色。\n曲线淡色背景为 380–780 nm 可见光的屏幕近似，仅用于辨识波长位置。"), Stack(navigation, plot));
        form.Children.Add(Note("使用左右按钮逐点查看波长与数据，首尾循环；也可点击或拖动光谱图选点，选中图表后可用方向键查看。"));
        Watch(result, () =>
        {
            calculated = null; analysis.Panel.Invalidate();
            plot.Clear(); plot.IsVisible = false; navigation.IsVisible = false;
        }, input, start, end, step, light, observer, units);
        return Stack(Card(form), result, analysis.Panel);
    }

    private Control DifferencePage()
    {
        var result = new ResultPanel(_downloader, differencePreviews: true);
        var standard = Input("StandardInput", "每行 L*, a*, b*\n例如：50,20,-30");
        var sample = Input("SampleInput", "每行 L*, a*, b*\n例如：52,18,-28");
        var comparison = Note("");
        comparison.Name = "DifferenceComparison";
        void UpdateComparison()
        {
            try
            {
                var standardRows = ToolCalculations.ParseRows(standard.Text ?? "").Length;
                var sampleRows = ToolCalculations.ParseRows(sample.Text ?? "").Length;
                comparison.Text = standardRows == 1 && sampleRows > 1
                    ? $"自动比较：1 行标准 → {sampleRows} 行样品（一对多）。"
                    : standardRows == sampleRows
                        ? $"自动比较：{standardRows} 行标准与样品逐行配对（一对一）。"
                        : "多行标准需与样品行数相同，按顺序逐行配对。";
            }
            catch (ArgumentException) { comparison.Text = "比较方式自动判断：一行标准对应多个样品，多行标准与样品逐行配对。"; }
        }
        standard.TextChanged += (_, _) => UpdateComparison();
        sample.TextChanged += (_, _) => UpdateComparison();
        UpdateComparison();
        var kl = SmallInput("1"); var kc = SmallInput("1"); var kh = SmallInput("1");
        var cmc = Select(["1:1", "2:1"]);
        var evaluationInputs = new ColorComparisonInputs("Difference");
        var columns = new ResponsiveColumns { Name = "DifferenceInputs" };
        foreach (var item in new[] { ("标准 Lab", standard), ("样品 Lab", sample) })
        {
            var column = Stack(Text(item.Item1, 16, true), item.Item2);
            columns.Children.Add(column);
        }
        var form = Stack(Text("色差计算", 24, true),
            Note("导入一个六列 CSV：前三列标准 L*、a*、b*，后三列样品 L*、a*、b*。一对多时只填写一行标准，其余标准三列留空；一对一时每行填写对应的标准和样品。表头可选，列名不限；标准与样品必须使用相同白点和观察者条件。"),
            Fields(("CMC l:c", cmc)),
            Fields(("ΔE00 kL", kl), ("ΔE00 kC", kc), ("ΔE00 kH", kh)),
            evaluationInputs.View,
            columns,
            comparison,
            Note("也可在两个输入框分别粘贴或修改三列 Lab 数据，支持中英文逗号、制表符或空格分列。比较方式随标准和样品行数自动更新。"),
            Actions(Button("载入示例", () => { standard.Text = "50,20,-30"; sample.Text = "50,20,-30\n52,18,-28\n60,10,-20"; }),
                TemplateButton("DifferenceInput", result, CsvDataFiles.DifferenceTemplate),
                ImportButton("DifferenceInput", result, async (fileName, stream) =>
                {
                    var data = await CsvDataFiles.ReadDifferenceAsync(fileName, stream);
                    standard.Text = data.Standards; sample.Text = data.Samples;
                }),
                Primary("计算色差", () => result.Run(() =>
                {
                    var options = evaluationInputs.Options();
                    return ToolCalculations.DifferenceAuto(standard.Text ?? "", sample.Text ?? "",
                    ToolCalculations.Positive(kl.Text ?? "", "kL"), ToolCalculations.Positive(kc.Text ?? "", "kC"), ToolCalculations.Positive(kh.Text ?? "", "kH"),
                    cmc.SelectedIndex == 0 ? 1 : 2, 1, options.AchromaticLightnessThreshold, options.AchromaticChromaThreshold);
                }))),
            Note("先显示标样 HEX、相邻的对比色块和样品 HEX，色块左侧为标样、右侧为样品。随后显示标样 L*、a*、b* 和样品 L*、a*、b* 六列输入值，再显示 ΔL*、Δa*、Δb*、ΔC*、Δh° 及 ΔE76、CMC、ΔE00。\n差值方向为样品减标样，Δh° 为 Lab 色相角的最短有符号差；预览按 D65 / 10° 计算并裁剪超色域值。公式参数显示在列名中；一次最多 10,000 行。"));
        Watch(result, null, standard, sample, kl, kc, kh, cmc, evaluationInputs.Lightness, evaluationInputs.Chroma);
        return Stack(Card(form), result);
    }

    private Control IlluminantPage()
    {
        var catalog = ChromaticityDotNet.Model.CieSpectralData.Illuminants
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var light = Select(catalog.Select(item => item.DisplayName).ToArray());
        light.SelectedIndex = Array.FindIndex(catalog, item => item.Id == "D65");
        light.Name = "IlluminantSelect"; light.MinWidth = 240; light.MaxDropDownHeight = 320;
        var interval = Select(ToolCalculations.IlluminantIntervals.Select(value => $"{value} nm").ToArray());
        interval.Name = "IlluminantInterval"; interval.SelectedIndex = 2;
        var observer = Observer(); observer.Name = "IlluminantObserver";
        var start = SmallInput(catalog[light.SelectedIndex].StartingWavelength.ToString(CultureInfo.InvariantCulture)); start.Name = "IlluminantStart";
        var end = SmallInput(catalog[light.SelectedIndex].EndingWavelength.ToString(CultureInfo.InvariantCulture)); end.Name = "IlluminantEnd";
        var conditions = Note("");
        var plot = DiagramControls.Localize(new SpectrumPlot { Name = "IlluminantSpectrumPlot", Height = 260, AxisLabel = "相对功率", MinimumMaximum = 1 });
        var navigation = DiagramControls.Localize(new SpectrumNavigator { Name = "IlluminantSpectrumNavigator", Plot = plot });
        var xyPlot = DiagramControls.Localize(new ChromaticityDiagram { Name = "IlluminantXyDiagram", ShowTemperatureLabels = true });
        var uvPlot = DiagramControls.Localize(new ChromaticityDiagram { Name = "IlluminantUvDiagram", Space = ChromaticityDiagramSpace.UvPrime, ShowTemperatureLabels = true });
        var diagrams = new ResponsiveColumns { Name = "IlluminantDiagrams" };
        diagrams.Children.Add(Stack(Text("CIE 1931 xy 色度图", 20, true), xyPlot));
        diagrams.Children.Add(Stack(Text("CIE 1976 u′v′ 色度图", 20, true), uvPlot));
        var diagramNote = Note("");
        var diagramCard = Card(Stack(diagrams, Note("S 为所选光源的积分白点，实线为普朗克轨迹，标记温度单位为 K。"),
            Note(PlanckianDiagramNotes.Explanation), Note(PlanckianDiagramNotes.UnavailableExplanation), diagramNote));
        diagramCard.Name = "IlluminantChromaticity"; diagramCard.IsVisible = false;
        var result = new ResultPanel(_downloader, title: "光谱数据");
        var whiteResult = new ResultPanel(_downloader, title: "光谱白点", renderResult: IlluminantWhitePointResults,
            successMessage: "已更新光谱白点。复制及导出包含光源、观察者、积分波段和白点坐标。") { Name = "IlluminantWhitePoint", IsVisible = false };
        void InvalidateRange()
        {
            result.Invalidate(); whiteResult.Invalidate(); whiteResult.IsVisible = false;
            plot.Clear(); plot.IsVisible = false; navigation.IsVisible = false; conditions.Text = "";
            xyPlot.Clear(SelectedObserver(observer)); uvPlot.Clear(SelectedObserver(observer));
            diagramCard.IsVisible = false;
        }
        void Update()
        {
            if (light.SelectedIndex < 0 || interval.SelectedIndex < 0) return;
            InvalidateRange();
            result.Run(() =>
            {
                static int Wavelength(TextBox box) => int.TryParse(box.Text, out var value) ? value : throw new ArgumentException("起始和结束波长必须是整数。");
                var query = ToolCalculations.QueryIlluminant(catalog[light.SelectedIndex].Id,
                    ToolCalculations.IlluminantIntervals[interval.SelectedIndex], Wavelength(start), Wavelength(end), SelectedObserver(observer));
                conditions.Text = query.Table.Conditions;
                plot.SetData(query.Values, query.Start, query.Step); plot.IsVisible = true; navigation.IsVisible = true;
                if (query.Diagram is { } diagram)
                {
                    xyPlot.SetLightData(diagram, catalog[light.SelectedIndex].Id);
                    uvPlot.SetLightData(diagram, catalog[light.SelectedIndex].Id);
                    diagramNote.Text = query.WhitePointTable.Conditions;
                }
                else diagramNote.Text = "当前波段的光源色度未定义，仅显示普朗克轨迹。";
                diagramCard.IsVisible = true;
                whiteResult.Run(() => query.WhitePointTable); whiteResult.IsVisible = true;
                return query.Table;
            });
        }
        // TextChanged is queued by Avalonia; observing the property synchronously
        // prevents a delayed notification from clearing a newly queried result.
        start.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) InvalidateRange(); };
        end.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) InvalidateRange(); };
        light.SelectionChanged += (_, _) =>
        {
            if (light.SelectedIndex < 0) return;
            var info = catalog[light.SelectedIndex];
            var rangeStart = int.TryParse(start.Text, out var a) ? Math.Max(a, info.StartingWavelength) : info.StartingWavelength;
            var rangeEnd = int.TryParse(end.Text, out var b) ? Math.Min(b, info.EndingWavelength) : info.EndingWavelength;
            if (rangeStart >= rangeEnd) { rangeStart = info.StartingWavelength; rangeEnd = info.EndingWavelength; }
            start.Text = rangeStart.ToString(CultureInfo.InvariantCulture); end.Text = rangeEnd.ToString(CultureInfo.InvariantCulture);
            Update();
        };
        interval.SelectionChanged += (_, _) => Update();
        observer.SelectionChanged += (_, _) => Update();
        Update();
        return Stack(Card(Stack(Text("标准光源查询", 24, true),
            Note($"共 {catalog.Length} 条 CIE 光源光谱。选择光源、观察者或间隔后自动更新；修改波段后点击“更新波段”，同步更新光谱和白点。"),
            Fields(("标准光源", light), ("观察者", observer), ("波长间隔", interval)),
            Fields(("起始波长 / nm", start), ("结束波长 / nm", end)), Actions(Primary("更新波段", Update)), conditions, Stack(navigation, plot),
            Note("使用左右按钮逐点查看波长与数据，首尾循环；也可点击或拖动光谱图选点，选中图表后可用方向键查看。"),
            Note("保持原始相对光谱功率尺度；必要时线性插值，不向源数据范围外推。淡色背景为 380–780 nm 可见光的屏幕近似，仅用于辨识波长位置。"))), diagramCard, whiteResult, result);
    }

    private static Control IlluminantWhitePointResults(CalculationTable table)
    {
        var row = table.Rows[0];
        var coordinates = new ResponsiveColumns();
        coordinates.Children.Add(Stack(Text("XYZ (Y = 100)", 18, true),
            Fields(("X", Text(row[4], 24, true)), ("Y", Text(row[5], 24, true)), ("Z", Text(row[6], 24, true)))));
        coordinates.Children.Add(Stack(Text("xy", 18, true),
            Fields(("x", Text(row[7], 24, true)), ("y", Text(row[8], 24, true)))));
        return Stack(coordinates, LightQualityResults(table, includeCri: true), Note(table.Conditions));
    }

    private Control ConversionPage()
    {
        var result = new ResultPanel(_downloader, sampleCards: true);
        var input = Input("ConversionInput", "每行一个颜色，例如：50,20,-30");
        var space = Select(Enum.GetNames<InputSpace>()); space.SelectedIndex = 1;
        var light = Light(); var observer = Observer();
        var help = Note("");
        void UpdateHelp()
        {
            var current = (InputSpace)space.SelectedIndex;
            var isRgb = current is InputSpace.sRGB or InputSpace.HEX;
            if (isRgb) light.SelectedIndex = 0;
            light.IsEnabled = !isRgb;
            help.Text = current switch
            {
                InputSpace.XYZ => "输入顺序：X, Y, Z。参考白亮度 Y = 100。",
                InputSpace.Lab => "输入顺序：L*, a*, b*。L* ≥ 0。",
                InputSpace.Luv => "输入顺序：L*, u*, v*。L* ≥ 0；黑色为 0,0,0。",
                InputSpace.xyY => "输入顺序：x, y, Y。x ≥ 0，y > 0，x + y ≤ 1，Y ≥ 0。",
                InputSpace.sRGB => "输入顺序：R, G, B。每个通道为 0–255 整数，固定 D65；Lab/Luv 使用所选观察者。",
                _ => "每行一个 #RRGGBB，例如 #20A090。固定 D65；Lab/Luv 使用所选观察者。"
            };
        }
        space.SelectionChanged += (_, _) => UpdateHelp(); UpdateHelp();
        var form = Stack(Text("颜色转换", 24, true),
            Note("支持单个或批量输入，每个颜色以卡片展示 XYZ、Lab、LCh、Luv、xyY、sRGB、HSL、HSV 和 HEX。HSL/HSV 的 H 为角度，S/L/V 显示为百分数。"),
            Fields(("输入空间", space), ("参考照明体", light), ("观察者", observer)), help,
            Note("参考照明体包含全部 50 条 CIE 光源，白点由光谱与所选观察者积分计算。"),
            Note("每行一个颜色；仅导入 CSV，可有表头，列名不限。模板随输入空间切换。粘贴数值支持中英文逗号、制表符或空格分列。"), input,
            Actions(Button("载入示例", () => input.Text = (InputSpace)space.SelectedIndex switch
            {
                InputSpace.XYZ => "21.4643,18.4187,40.4654\n0,0,0",
                InputSpace.Lab => "50,20,-30\n75,-15,25",
                InputSpace.Luv => "50,10,-20\n0,0,0",
                InputSpace.xyY => "0.3127,0.3290,50\n0.3,0.3,20",
                InputSpace.sRGB => "255,0,0\n32,160,144",
                _ => "#FF0000\n#20A090"
            }), TemplateButton(input, result, () => CsvDataFiles.ColorTemplate((InputSpace)space.SelectedIndex)),
                ImportButton(input, result, CsvInputKind.Color, () => (InputSpace)space.SelectedIndex),
                Primary("转换颜色", () => result.Run(() => ToolCalculations.ConvertColors(input.Text ?? "", (InputSpace)space.SelectedIndex, SelectedLight(light), SelectedObserver(observer))))),
            Note("sRGB 预览由 XYZ 直接映射并裁剪超色域值；其他照明体或观察者条件下为未经色适应的屏幕近似。\n黑色的 x、y 未定义，显示为 —。LCh 为库内 Lab 的派生输出。\nHSL/HSV 基于显示的 sRGB 值；灰色的 H 使用 0 作为占位值。"));
        Watch(result, null, input, space, light, observer);
        return Stack(Card(form), result);
    }

    private static void Watch(ResultPanel result, Action? extra, params Control[] controls)
    {
        void Invalidate() { result.Invalidate(); extra?.Invoke(); }
        foreach (var control in controls)
        {
            // TextChanged is queued; clear old results and spectrum selection as soon as the input changes.
            if (control is TextBox text) text.PropertyChanged += (_, change) =>
            {
                if (change.Property == TextBox.TextProperty) Invalidate();
            };
            if (control is ComboBox combo) combo.SelectionChanged += (_, _) => Invalidate();
        }
    }

    private Button TemplateButton(TextBox target, ResultPanel result, Func<CsvTemplate> create) =>
        TemplateButton(target.Name ?? "CsvInput", result, create);

    private Button TemplateButton(string name, ResultPanel result, Func<CsvTemplate> create)
    {
        var button = Button("下载数据模板", () => { });
        button.Name = name + "Template";
        button.Click += async (_, _) =>
        {
            try
            {
                var template = create();
                if (_downloader is not null) await _downloader.DownloadAsync(template.FileName, template.Csv);
                else
                {
                    var top = TopLevel.GetTopLevel(button) ?? throw new InvalidOperationException("浏览器尚未就绪。");
                    using var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = UiLanguage.Translate("下载数据模板"), SuggestedFileName = template.FileName, DefaultExtension = "csv",
                        FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
                    });
                    if (file is null) return;
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                    await writer.WriteAsync(template.Csv);
                }
                result.ShowNotice("已开始下载数据模板，请查看浏览器下载列表。填写数据时请保持列的顺序，表头可保留、修改或删除。");
            }
            catch (Exception ex) { result.ShowError($"模板下载失败：{ex.Message}"); }
        };
        return button;
    }

    private static Button ImportButton(TextBox target, ResultPanel result, CsvInputKind kind, Func<InputSpace>? space = null) =>
        ImportButton(target.Name ?? "CsvInput", result, async (fileName, stream) =>
            target.Text = await CsvDataFiles.ReadAsync(fileName, stream, kind, space?.Invoke() ?? InputSpace.Lab));

    private static Button ImportButton(string name, ResultPanel result, Func<string, Stream, Task> import)
    {
        var button = Button("导入 CSV", () => { });
        button.Name = name + "Import";
        button.Click += async (_, _) =>
        {
            try
            {
                var top = TopLevel.GetTopLevel(button) ?? throw new InvalidOperationException("浏览器尚未就绪。");
                var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = UiLanguage.Translate("导入 CSV 数据（表头可选）"),
                    AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
                });
                if (files.Count == 0) return;
                using var file = files[0];
                await using var stream = await file.OpenReadAsync();
                await import(file.Name, stream);
            }
            catch (Exception ex) { result.ShowError($"导入失败：{ex.Message}"); }
        };
        return button;
    }

    private static string SelectedLight(ComboBox combo) => ToolCalculations.Illuminants[combo.SelectedIndex].Id;
    private static StandardObserver SelectedObserver(ComboBox combo) => combo.SelectedIndex == 0 ? StandardObserver.Degree2 : StandardObserver.Degree10;
    private static ComboBox Light() => Select(ToolCalculations.Illuminants.Select(x => x.DisplayName).ToArray());
    private static ComboBox Observer()
    {
        var observer = Select(["2° · CIE 1931", "10° · CIE 1964"]);
        observer.SelectedIndex = ToolCalculations.DefaultObserver == StandardObserver.Degree10 ? 1 : 0;
        return observer;
    }
    private static ComboBox Select(string[] items) => new()
    {
        ItemsSource = items, SelectedIndex = 0, MinWidth = 155, MaxDropDownHeight = 320,
        ItemTemplate = new FuncDataTemplate<string>((item, _) => Text(item ?? ""))
    };
    private static TextBox SmallInput(string value) => new() { Text = value, Width = 155 };
    private static TextBox Input(string name, string placeholder) => new LocalizedTextBox()
    {
        Name = name,
        PlaceholderText = placeholder,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        Height = 200,
        MaxLength = ToolCalculations.MaxTextLength,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private static TextBlock Text(string text, double size = 14, bool bold = false) => new LocalizedTextBlock()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap
    };
    private static TextBlock Note(string text) { var block = Text(text); block.Foreground = Muted; block.LineHeight = 23; return block; }
    private static StackPanel Stack(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 16 }; foreach (var child in children) stack.Children.Add(child); return stack;
    }
    private static Border Card(Control child) => new()
    {
        Child = child,
        Padding = new Thickness(24),
        Background = Brushes.White,
        BorderBrush = Brush.Parse("#DDDDDD"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8)
    };
    private static WrapPanel Fields(params (string Label, Control Input)[] fields)
    {
        var wrap = new WrapPanel();
        foreach (var (label, input) in fields)
        {
            var field = Stack(Note(label), input); field.Spacing = 6; field.Margin = new Thickness(0, 0, 18, 10); wrap.Children.Add(field);
        }
        return wrap;
    }
    private static WrapPanel Actions(params Button[] buttons)
    {
        var wrap = new WrapPanel(); foreach (var button in buttons) { button.Margin = new Thickness(0, 0, 10, 8); wrap.Children.Add(button); }
        return wrap;
    }
    private static Button Button(string label, Action action)
    {
        var button = new Button { Content = new LocalizedTextBlock { Text = label } }; button.Click += (_, _) => action(); return button;
    }
    private static Button Primary(string label, Action action) { var button = Button(label, action); button.Classes.Add("primary"); return button; }

    private static Control SampleCard(string[] headers, string[] row)
    {
        string Value(string name) => row[Array.IndexOf(headers, name)];
        Control Values(string title, params string[] names)
        {
            var metrics = new WrapPanel();
            foreach (var name in names)
            {
                var metric = Text($"{name}  {Value(name)}");
                metric.Margin = new Thickness(0, 0, 18, 4);
                metrics.Children.Add(metric);
            }
            var content = Stack(Text(title, 15, true), metrics); content.Spacing = 8;
            return new Border
            {
                Child = content,
                Padding = new Thickness(14, 12),
                Background = Brush.Parse("#F5F5F5"),
                CornerRadius = new CornerRadius(8)
            };
        }
        var hex = Value("HEX");
        var preview = new Border
        {
            Name = "SampleSwatch",
            Height = 72,
            CornerRadius = new CornerRadius(8),
            Background = hex == "—" ? Brush.Parse("#EEEEEE") : Brush.Parse(hex)
        };
        var values = new ResponsiveColumns();
        values.Children.Add(Values("XYZ", "X", "Y", "Z"));
        values.Children.Add(Values("Lab", "L*", "a*", "b*"));
        values.Children.Add(Values("LCh", "C*", "h°"));
        values.Children.Add(Values("Luv", "L* (Luv)", "u*", "v*"));
        values.Children.Add(Values("xyY", "x", "y", "Y"));
        values.Children.Add(Values("HSL", "H (HSL) / °", "S (HSL) / %", "L (HSL) / %"));
        values.Children.Add(Values("HSV", "H (HSV) / °", "S (HSV) / %", "V (HSV) / %"));
        values.Children.Add(Values("CCT · McCamy", "CCT (McCamy) / K"));
        values.Children.Add(Values("CCT · CIE 1960 UCS", "CCT (CIE 1960 UCS) / K", "Duv (1960)"));
        return Card(Stack(Text($"颜色 {Value("行")}", 20, true), Text("sRGB 屏幕预览", 16, true), preview,
            Note(hex == "—" ? "当前条件不提供 sRGB 预览（仅支持 D65）。"
                : $"{hex}    R {Value("R")} · G {Value("G")} · B {Value("B")}"),
            values, Note(Value("CCT 说明")), Note(Value("计算条件"))));
    }

    private static Control LightQualityResults(CalculationTable table, bool includeCri)
    {
        var row = table.Rows[0];
        string Value(string header) => row[Array.IndexOf(table.Headers, header)];
        var metrics = new ResponsiveColumns();
        foreach (var header in LightQualityPresentation.CctHeaders.Take(2))
            metrics.Children.Add(Stack(Text(header, 16, true), Text(Value(header), 26, true)));
        var content = Stack(metrics, Note($"Duv (1960): {Value("Duv (1960)")}"), Note(Value("CCT 说明")));
        if (includeCri)
        {
            content.Children.Add(Text("显色指数 · CIE 13.3-1995 / 1931 2°", 18, true));
            var indices = new ResponsiveColumns { MaximumColumns = 3 };
            foreach (var header in LightQualityPresentation.CriHeaders.Take(15))
                indices.Children.Add(Stack(Text(header, 15, true), Text(Value(header), header == "CRI Ra" ? 26 : 20, true)));
            content.Children.Add(indices);
            content.Children.Add(Note(Value("CRI 说明")));
        }
        return content;
    }

    private sealed class ResultPanel : Border
    {
        private CalculationTable? _table;
        private readonly TextBlock _status = Note("输入数据后，计算结果将在这里显示。");
        private readonly DataGrid _grid = new() { IsReadOnly = true, AutoGenerateColumns = false, Height = 350, RowHeight = 32, IsVisible = false, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        private readonly StackPanel _samples = new() { Spacing = 16, IsVisible = false };
        private readonly bool _sampleCards;
        private readonly bool _differencePreviews;
        private readonly Func<CalculationTable, Control>? _renderResult;
        private readonly string? _successMessage;
        private readonly Button? _copy;
        private readonly Button? _save;

        public ResultPanel(IResultDownloader? downloader, bool sampleCards = false, bool differencePreviews = false, string title = "计算结果",
            Func<CalculationTable, Control>? renderResult = null, string? successMessage = null, bool allowExport = true, Control? settings = null)
        {
            _sampleCards = sampleCards;
            _differencePreviews = differencePreviews;
            _renderResult = renderResult;
            _successMessage = successMessage;
            if (differencePreviews) { _grid.Name = "DifferenceResultsTable"; _grid.RowHeight = 56; _grid.FrozenColumnCount = 1; }
            if (allowExport)
            {
                _copy = Button("复制结果", () => { }); _save = Button("下载 CSV", () => { });
                _copy.IsEnabled = _save.IsEnabled = false;
                _copy.Click += async (_, _) =>
                {
                    try
                    {
                        var table = _table; if (table is null) return;
                        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("剪贴板不可用，请下载 CSV。");
                        await clipboard.SetTextAsync(UiLanguage.TranslateTable(table).ToTsv());
                        _status.Text = $"已复制 {table.Rows.Length} 行，可粘贴到 Excel。";
                    }
                    catch (Exception ex) { _status.Text = $"复制失败：{ex.Message}"; }
                };
                _save.Click += async (_, _) =>
                {
                    try
                    {
                        var table = _table; if (table is null) return;
                        if (downloader is not null)
                        {
                            await downloader.DownloadAsync("chromaticity-results.csv", UiLanguage.TranslateTable(table).ToCsv());
                            _status.Text = $"已开始下载 {table.Rows.Length} 行结果，请查看浏览器下载列表。";
                            return;
                        }
                        var top = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException("浏览器尚未就绪。");
                        using var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                        {
                            Title = UiLanguage.Translate("导出计算结果"),
                            SuggestedFileName = "chromaticity-results.csv",
                            DefaultExtension = "csv",
                            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] }]
                        });
                        if (file is null) return;
                        await using var stream = await file.OpenWriteAsync();
                        await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                        await writer.WriteAsync(UiLanguage.TranslateTable(table).ToCsv());
                    }
                    catch (Exception ex) { _status.Text = $"导出失败：{ex.Message}。也可使用复制结果。"; }
                };
            }
            var content = Stack(Text(title, 20, true));
            if (settings is not null) content.Children.Add(settings);
            content.Children.Add(_status); content.Children.Add(_grid); content.Children.Add(_samples);
            if (_copy is not null && _save is not null) content.Children.Add(Actions(_copy, _save));
            Child = content;
            Background = Brushes.White; Padding = new Thickness(24); CornerRadius = new CornerRadius(8);
            BorderBrush = Brush.Parse("#DDDDDD"); BorderThickness = new Thickness(1);
        }

        public void Invalidate()
        {
            _samples.Children.Clear(); _samples.IsVisible = false;
            _table = null; _grid.ItemsSource = null; _grid.IsVisible = false;
            if (_copy is not null) _copy.IsEnabled = false;
            if (_save is not null) _save.IsEnabled = false;
            _status.Foreground = Muted; _status.FontWeight = FontWeight.Normal; _status.Text = "数据或条件已更新，请点击计算。";
        }

        public void ShowError(string message) { Invalidate(); _status.Foreground = Brushes.Black; _status.FontWeight = FontWeight.SemiBold; _status.Text = $"错误：{message}"; }

        public void ShowNotice(string message) { _status.Foreground = Muted; _status.FontWeight = FontWeight.Normal; _status.Text = message; }

        public void Run(Func<CalculationTable> calculate)
        {
            Invalidate();
            try
            {
                var table = calculate();
                if (_renderResult is not null)
                {
                    _samples.Children.Add(_renderResult(table));
                    _samples.IsVisible = true;
                }
                else if (_sampleCards)
                {
                    foreach (var row in table.Rows) _samples.Children.Add(SampleCard(table.Headers, row));
                    _samples.IsVisible = true;
                }
                else
                {
                    _grid.Columns.Clear();
                    for (var i = 0; i < table.Headers.Length; i++)
                    {
                        if (_differencePreviews && i == 2)
                            _grid.Columns.Add(new DataGridTemplateColumn
                            {
                                Header = Text("对比色块", 12, true), Width = new DataGridLength(140),
                                CellTemplate = new FuncDataTemplate<string[]>((row, _) =>
                                {
                                    if (row is null) return Text("");
                                    var preview = PairedColorSwatches(row[1], row[2], "Difference", height: 40);
                                    preview.Margin = new Thickness(6); preview.VerticalAlignment = VerticalAlignment.Center;
                                    return preview;
                                })
                            });
                        var column = i;
                        var header = Text(table.Headers[i], 12, true);
                        header.TextWrapping = _differencePreviews ? TextWrapping.Wrap : TextWrapping.NoWrap;
                        _grid.Columns.Add(new DataGridTemplateColumn
                        {
                            Header = header,
                            Width = new DataGridLength(_differencePreviews
                                ? i switch { 0 => 80, 1 or 2 => 140, >= 3 and <= 8 => 100, 15 => 108, 16 => 160, 17 or 18 => 140, 19 => 130, 20 => 190, _ => 82 }
                                : i == 0 ? 160 : 260),
                            CellTemplate = new FuncDataTemplate<string[]>((row, _) =>
                            {
                                var value = row?[column] ?? "";
                                var cell = Text(value); cell.TextWrapping = TextWrapping.NoWrap;
                                cell.Margin = new Thickness(8); cell.VerticalAlignment = VerticalAlignment.Center;
                                return cell;
                            })
                        });
                    }
                    _grid.ItemsSource = table.Rows; _grid.IsVisible = true;
                }
                _table = _copy is not null ? table : null;
                if (_copy is not null) _copy.IsEnabled = true;
                if (_save is not null) _save.IsEnabled = true;
                _status.Text = _successMessage ?? (_renderResult is not null
                    ? "已生成色卡。复制及导出包含颜色坐标、实际色差、公式参数与不可生成原因。"
                    : _sampleCards
                    ? $"已完成 {table.Rows.Length} 个样品计算。导出包含全部颜色值与计算条件。"
                    : _differencePreviews
                        ? $"已完成 {table.Rows.Length} 行计算。公式参数见列名，复制及导出保留参数和预览 HEX。"
                        : $"已显示 {table.Rows.Length} 行数据，可复制或下载 CSV。");
            }
            catch (Exception ex) when (ex is ArgumentException or ArithmeticException or InvalidOperationException)
            { ShowError(ex.Message); }
        }
    }
}
