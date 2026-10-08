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
            _ => SpectrumPage()
        });
        content.Children.Add(Note($"ChromaticityDotNet v{LibraryInfo.Version} · 代码 MIT · CIE 数据 CC BY-SA 4.0\n计算保留库的四位小数与既有白点约定。色块仅为屏幕近似，不能代替仪器测量。"));
        Content = new ScrollViewer { Content = content, Background = Brush.Parse("#F5F5F5"), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private Control SpectrumPage()
    {
        var result = new ResultPanel(_downloader, sampleCards: true);
        var plot = new SpectrumPlot { Height = 200, IsVisible = false };
        var input = Input("SpectrumInput", "380,18\n390,18\n…");
        var light = Light(); var observer = Observer();
        var start = SmallInput("380"); var end = SmallInput("780"); var step = SmallInput("10");
        var units = Select(["百分数（18 = 18%）", "比例（0.18 = 18%）"]);
        var form = Stack(
            Text("光谱计算", 24, true),
            Note("使用库内 CIE 光谱数据。输入一列反射率，或两列“波长、反射率”；每行一个采样点，无需表头。"),
            Fields(("照明体", light), ("观察者", observer), ("输入单位", units)),
            Fields(("起始波长 / nm", start), ("结束波长 / nm", end), ("间隔 / nm", step)),
            input,
            Actions(Button("载入 18% 灰示例", () =>
            {
                start.Text = "380"; end.Text = "780"; step.Text = "10"; units.SelectedIndex = 0;
                input.Text = string.Join('\n', Enumerable.Range(0, 41).Select(i => $"{380 + i * 10},18"));
            }), ImportButton(input, result), Primary("计算颜色值", () => result.Run(() =>
            {
                static int Integer(TextBox box) => int.TryParse(box.Text, out var n) ? n : throw new ArgumentException("波长和间隔必须是整数。");
                var data = ToolCalculations.Reflectance(input.Text ?? "", Integer(start), Integer(end), Integer(step), units.SelectedIndex == 1,
                    SelectedLight(light), SelectedObserver(observer));
                plot.SetData(data.Values, data.Start, data.Step); plot.IsVisible = true;
                return data.Table;
            }))),
            Note("D65 / A：360–830 nm；CWF / F7 / TL84 / U30：380–780 nm。\nLab / Luv 使用库的固定白点，可能与所选波段的积分白点略有不同。sRGB 预览仅在 D65 / 2° 下显示，会裁剪超色域颜色。\n曲线淡色背景为 380–780 nm 可见光的屏幕近似，仅用于辨识波长位置。"), plot);
        Watch(result, () => plot.IsVisible = false, input, start, end, step, light, observer, units);
        return Stack(Card(form), result);
    }

    private Control DifferencePage()
    {
        var result = new ResultPanel(_downloader, differencePreviews: true);
        var standard = Input("StandardInput", "每行 L*, a*, b*\n例如：50,20,-30");
        var sample = Input("SampleInput", "每行 L*, a*, b*\n例如：52,18,-28");
        var mode = Select(["一个标准 → 多个样品", "标准与样品逐行配对"]);
        var kl = SmallInput("1"); var kc = SmallInput("1"); var kh = SmallInput("1");
        var cmc = Select(["1:1", "2:1"]);
        var columns = new ResponsiveColumns { Name = "DifferenceInputs" };
        foreach (var item in new[] { ("标准 Lab", standard), ("样品 Lab", sample) })
        {
            var column = Stack(Text(item.Item1, 16, true), item.Item2, ImportButton(item.Item2, result));
            columns.Children.Add(column);
        }
        var form = Stack(Text("色差计算", 24, true),
            Note("粘贴 Excel 数据，或导入 CSV / TSV。每行三列 L*、a*、b*，支持中英文逗号、制表符或空格分列，无需表头；标准与样品必须使用相同白点和观察者条件。"),
            Fields(("比较方式", mode), ("CMC l:c", cmc)),
            Fields(("ΔE00 kL", kl), ("ΔE00 kC", kc), ("ΔE00 kH", kh)),
            columns,
            Actions(Button("载入示例", () => { mode.SelectedIndex = 0; standard.Text = "50,20,-30"; sample.Text = "50,20,-30\n52,18,-28\n60,10,-20"; }),
                Primary("计算色差", () => result.Run(() => ToolCalculations.Difference(standard.Text ?? "", sample.Text ?? "", mode.SelectedIndex == 1,
                    ToolCalculations.Positive(kl.Text ?? "", "kL"), ToolCalculations.Positive(kc.Text ?? "", "kC"), ToolCalculations.Positive(kh.Text ?? "", "kH"),
                    cmc.SelectedIndex == 0 ? 1 : 2, 1)))),
            Note("先显示标样与样品的 sRGB 屏幕预览，再显示 ΔL*、Δa*、Δb*、ΔC*、Δh° 及四种色差值。\n差值方向为样品减标样，Δh° 为 Lab 色相角的最短有符号差；预览按 D65 / 2° 计算并裁剪超色域值。公式参数显示在列名中；一次最多 10,000 行。"));
        Watch(result, null, standard, sample, mode, kl, kc, kh, cmc);
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
        var start = SmallInput(catalog[light.SelectedIndex].StartingWavelength.ToString(CultureInfo.InvariantCulture)); start.Name = "IlluminantStart";
        var end = SmallInput(catalog[light.SelectedIndex].EndingWavelength.ToString(CultureInfo.InvariantCulture)); end.Name = "IlluminantEnd";
        var conditions = Note("");
        var plot = new SpectrumPlot { Height = 260, AxisLabel = "相对功率", MinimumMaximum = 1 };
        var result = new ResultPanel(_downloader, title: "光谱数据");
        void InvalidateRange()
        {
            result.Invalidate(); plot.IsVisible = false; conditions.Text = "";
        }
        void Update()
        {
            if (light.SelectedIndex < 0 || interval.SelectedIndex < 0) return;
            InvalidateRange();
            result.Run(() =>
            {
                static int Wavelength(TextBox box) => int.TryParse(box.Text, out var value) ? value : throw new ArgumentException("起始和结束波长必须是整数。");
                var query = ToolCalculations.QueryIlluminant(catalog[light.SelectedIndex].Id,
                    ToolCalculations.IlluminantIntervals[interval.SelectedIndex], Wavelength(start), Wavelength(end));
                conditions.Text = query.Table.Conditions;
                plot.SetData(query.Values, query.Start, query.Step); plot.IsVisible = true;
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
        Update();
        return Stack(Card(Stack(Text("标准光源查询", 24, true),
            Note($"共 {catalog.Length} 条 CIE 光源光谱。选择光源后自动更新；修改波段后点击“更新波段”，数据与曲线仅显示所选范围。"),
            Fields(("标准光源", light), ("波长间隔", interval)),
            Fields(("起始波长 / nm", start), ("结束波长 / nm", end)), Actions(Primary("更新波段", Update)), conditions, plot,
            Note("保持原始相对光谱功率尺度；必要时线性插值，不向源数据范围外推。淡色背景为 380–780 nm 可见光的屏幕近似，仅用于辨识波长位置。"))), result);
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
            if (isRgb) { light.SelectedIndex = 0; observer.SelectedIndex = 0; }
            light.IsEnabled = observer.IsEnabled = !isRgb;
            help.Text = current switch
            {
                InputSpace.XYZ => "输入顺序：X, Y, Z。参考白亮度 Y = 100。",
                InputSpace.Lab => "输入顺序：L*, a*, b*。L* ≥ 0。",
                InputSpace.Luv => "输入顺序：L*, u*, v*。L* ≥ 0；黑色为 0,0,0。",
                InputSpace.xyY => "输入顺序：x, y, Y。x ≥ 0，y > 0，x + y ≤ 1，Y ≥ 0。",
                InputSpace.sRGB => "输入顺序：R, G, B。每个通道为 0–255 整数，固定 D65 / 2°。",
                _ => "每行一个 #RRGGBB，例如 #20A090。固定 D65 / 2°。"
            };
        }
        space.SelectionChanged += (_, _) => UpdateHelp(); UpdateHelp();
        var form = Stack(Text("颜色转换", 24, true),
            Note("支持单个或批量输入，每个颜色以卡片展示 XYZ、Lab、LCh、Luv、xyY，以及 sRGB 屏幕预览和 HEX。"),
            Fields(("输入空间", space), ("参考照明体", light), ("观察者", observer)), help,
            Note("每行一个颜色；数值支持中英文逗号、制表符或空格分列。"), input,
            Actions(Button("载入示例", () => input.Text = (InputSpace)space.SelectedIndex switch
            {
                InputSpace.XYZ => "21.4643,18.4187,40.4654\n0,0,0",
                InputSpace.Lab => "50,20,-30\n75,-15,25",
                InputSpace.Luv => "50,10,-20\n0,0,0",
                InputSpace.xyY => "0.3127,0.3290,50\n0.3,0.3,20",
                InputSpace.sRGB => "255,0,0\n32,160,144",
                _ => "#FF0000\n#20A090"
            }), ImportButton(input, result), Primary("转换颜色", () => result.Run(() => ToolCalculations.ConvertColors(input.Text ?? "", (InputSpace)space.SelectedIndex, SelectedLight(light), SelectedObserver(observer))))),
            Note("sRGB 预览由 XYZ 直接映射并裁剪超色域值；其他照明体或观察者条件下为未经色适应的屏幕近似。\n黑色的 x、y 未定义，显示为 —。LCh 为库内 Lab 的派生输出。"));
        Watch(result, null, input, space, light, observer);
        return Stack(Card(form), result);
    }

    private static void Watch(ResultPanel result, Action? extra, params Control[] controls)
    {
        void Invalidate() { result.Invalidate(); extra?.Invoke(); }
        foreach (var control in controls)
        {
            if (control is TextBox text) text.TextChanged += (_, _) => Invalidate();
            if (control is ComboBox combo) combo.SelectionChanged += (_, _) => Invalidate();
        }
    }

    private static Button ImportButton(TextBox target, ResultPanel result)
    {
        var button = Button("导入 CSV / TSV", () => { });
        button.Click += async (_, _) =>
        {
            try
            {
                var top = TopLevel.GetTopLevel(button) ?? throw new InvalidOperationException("浏览器尚未就绪。");
                var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "导入无表头的数值数据",
                    AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("文本数据") { Patterns = ["*.csv", "*.tsv", "*.txt"], MimeTypes = ["text/csv", "text/tab-separated-values", "text/plain"] }]
                });
                if (files.Count == 0) return;
                using var file = files[0];
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                var buffer = new char[ToolCalculations.MaxTextLength + 1];
                var length = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                if (length > ToolCalculations.MaxTextLength) throw new ArgumentException("文件过大，请拆分为小于 2 MB 的 UTF-8 文本。");
                target.Text = new string(buffer, 0, length);
            }
            catch (Exception ex) { result.ShowError($"导入失败：{ex.Message}"); }
        };
        return button;
    }

    private static Standardilluminant SelectedLight(ComboBox combo) => ToolCalculations.Illuminants[combo.SelectedIndex];
    private static StandardObserver SelectedObserver(ComboBox combo) => combo.SelectedIndex == 0 ? StandardObserver.Degree2 : StandardObserver.Degree10;
    private static ComboBox Light() => Select(ToolCalculations.Illuminants.Select(x => x.ToString()).ToArray());
    private static ComboBox Observer() => Select(["2° · CIE 1931", "10° · CIE 1964"]);
    private static ComboBox Select(string[] items) => new() { ItemsSource = items, SelectedIndex = 0, MinWidth = 155 };
    private static TextBox SmallInput(string value) => new() { Text = value, Width = 155 };
    private static TextBox Input(string name, string placeholder) => new()
    {
        Name = name,
        PlaceholderText = placeholder,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        Height = 200,
        MaxLength = ToolCalculations.MaxTextLength,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private static TextBlock Text(string text, double size = 14, bool bold = false) => new()
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
        var button = new Button { Content = label }; button.Click += (_, _) => action(); return button;
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
        return Card(Stack(Text($"颜色 {Value("行")}", 20, true), Text("sRGB 屏幕预览", 16, true), preview,
            Note(hex == "—" ? "当前条件不提供 sRGB 预览（仅支持 D65 / 2°）。"
                : $"{hex}    R {Value("R")} · G {Value("G")} · B {Value("B")}"),
            values, Note(Value("计算条件"))));
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
            Func<CalculationTable, Control>? renderResult = null, string? successMessage = null, bool allowExport = true)
        {
            _sampleCards = sampleCards;
            _differencePreviews = differencePreviews;
            _renderResult = renderResult;
            _successMessage = successMessage;
            if (differencePreviews) { _grid.RowHeight = 56; _grid.FrozenColumnCount = 1; }
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
                        await clipboard.SetTextAsync(table.ToTsv());
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
                            await downloader.DownloadAsync("chromaticity-results.csv", table.ToCsv());
                            _status.Text = $"已开始下载 {table.Rows.Length} 行结果，请查看浏览器下载列表。";
                            return;
                        }
                        var top = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException("浏览器尚未就绪。");
                        using var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                        {
                            Title = "导出计算结果",
                            SuggestedFileName = "chromaticity-results.csv",
                            DefaultExtension = "csv",
                            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] }]
                        });
                        if (file is null) return;
                        await using var stream = await file.OpenWriteAsync();
                        await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                        await writer.WriteAsync(table.ToCsv());
                    }
                    catch (Exception ex) { _status.Text = $"导出失败：{ex.Message}。也可使用复制结果。"; }
                };
            }
            var content = Stack(Text(title, 20, true), _status, _grid, _samples);
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
                        var column = i;
                        _grid.Columns.Add(new DataGridTemplateColumn
                        {
                            Header = new TextBlock { Text = table.Headers[i], FontSize = 12, FontWeight = FontWeight.SemiBold },
                            Width = new DataGridLength(_differencePreviews
                                ? i switch { 0 => 64, 1 or 2 => 116, 9 => 178, 10 => 160, 11 => 108, _ => 82 }
                                : i == 0 ? 160 : 260),
                            CellTemplate = new FuncDataTemplate<string[]>((row, _) =>
                            {
                                var value = row?[column] ?? "";
                                if (_differencePreviews && column is 1 or 2)
                                {
                                    var preview = new StackPanel
                                    {
                                        Orientation = Orientation.Horizontal,
                                        Spacing = 6,
                                        Margin = new Thickness(6),
                                        VerticalAlignment = VerticalAlignment.Center
                                    };
                                    preview.Children.Add(new Border
                                    {
                                        Width = 30,
                                        Height = 30,
                                        CornerRadius = new CornerRadius(5),
                                        Background = Brush.Parse(value),
                                        BorderBrush = Brush.Parse("#DDDDDD"),
                                        BorderThickness = new Thickness(1)
                                    });
                                    var hex = Text(value, 11); hex.VerticalAlignment = VerticalAlignment.Center;
                                    preview.Children.Add(hex);
                                    return preview;
                                }
                                return new TextBlock { Text = value, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
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

public sealed class SpectrumPlot : Control
{
    private static readonly IBrush[] VisibleBackground = Enumerable.Range(WavelengthColors.Start, WavelengthColors.End - WavelengthColors.Start + 1)
        .Select(wavelength =>
        {
            var color = WavelengthColors.At(wavelength);
            return (IBrush)new SolidColorBrush(Color.FromArgb(32, color.R, color.G, color.B));
        }).ToArray();
    public string AxisLabel { get; init; } = "%";
    public double MinimumMaximum { get; init; } = 100;
    private double[] _values = [];
    private int _start, _step;
    public void SetData(double[] values, int start, int step) { _values = values; _start = start; _step = step; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_values.Length < 2 || Bounds.Width < 100) return;
        var left = 55.0; var top = 30.0; var width = Bounds.Width - 75; var height = Bounds.Height - 65;
        var end = _start + (_values.Length - 1) * _step;
        var span = end - _start;
        double X(double wavelength) => left + (wavelength - _start) * width / span;
        for (var wavelength = Math.Max(_start, WavelengthColors.Start); wavelength < Math.Min(end, WavelengthColors.End); wavelength++)
        {
            var x = X(wavelength);
            context.DrawRectangle(VisibleBackground[wavelength - WavelengthColors.Start], null,
                new Rect(x, top, X(wavelength + 1) - x, height));
        }
        var max = Math.Max(MinimumMaximum, _values.Max());
        var font = new Typeface("avares://Chromaticity.Tools/Assets#Chromaticity UI");
        void Label(string text, double x, double y) => context.DrawText(new FormattedText(text, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, font, 12, Brush.Parse("#666666")), new Point(x, y));
        for (var i = 0; i <= 4; i++)
        {
            var y = top + height * i / 4;
            context.DrawLine(new Pen(Brush.Parse("#E5E5E5")), new Point(left, y), new Point(left + width, y));
            Label((max * (4 - i) / 4).ToString("0.#", CultureInfo.InvariantCulture), 0, y - 7);
        }
        var pen = new Pen(Brush.Parse("#333333"), 2.5);
        Point At(int i) => new(left + i * width / (_values.Length - 1), top + height * (1 - _values[i] / max));
        for (var i = 1; i < _values.Length; i++) context.DrawLine(pen, At(i - 1), At(i));
        Label(AxisLabel, 0, 0); Label($"{_start} nm", left, top + height + 12);
        Label($"{end} nm", left + width - 55, top + height + 12);
        var tickStep = span <= 200 ? 50 : 100;
        for (var wavelength = (_start / tickStep + 1) * tickStep; wavelength < end; wavelength += tickStep)
            if (X(wavelength) - left >= 65 && left + width - X(wavelength) >= 65)
                Label(wavelength.ToString(CultureInfo.InvariantCulture), X(wavelength) - 12, top + height + 12);
    }
}
