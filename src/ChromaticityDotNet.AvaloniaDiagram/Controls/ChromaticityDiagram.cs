using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.AvaloniaDiagram.Controls;

/// <summary>Observer-specific xy or u'v' diagram, with a Planckian locus, UCS nearest point and optional wavelength rays.</summary>
public sealed class ChromaticityDiagram : DiagramControl
{
    public static readonly StyledProperty<ChromaticityDiagramSpace> SpaceProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, ChromaticityDiagramSpace>(nameof(Space), validate: value =>
            value is ChromaticityDiagramSpace.Xy or ChromaticityDiagramSpace.UvPrime);
    public static readonly StyledProperty<StandardObserver> ObserverProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, StandardObserver>(nameof(Observer), StandardObserver.Degree2,
            validate: value => value is StandardObserver.Degree2 or StandardObserver.Degree10);
    public static readonly StyledProperty<bool> ShowTemperatureLabelsProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, bool>(nameof(ShowTemperatureLabels));
    public static readonly StyledProperty<bool> ShowWavelengthLabelsProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, bool>(nameof(ShowWavelengthLabels), true);
    public static readonly StyledProperty<bool> ShowPlanckianLocusProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, bool>(nameof(ShowPlanckianLocus), true);
    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, bool>(nameof(ShowLegend), true);
    public static readonly StyledProperty<bool> ShowObserverLabelProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, bool>(nameof(ShowObserverLabel), true);
    public static readonly StyledProperty<double?> AxisMaximumXProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, double?>(nameof(AxisMaximumX), validate: ValidMaximum);
    public static readonly StyledProperty<double?> AxisMaximumYProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, double?>(nameof(AxisMaximumY), validate: ValidMaximum);
    public static readonly DirectProperty<ChromaticityDiagram, ChromaticityDiagramTarget?> SelectedTargetProperty =
        AvaloniaProperty.RegisterDirect<ChromaticityDiagram, ChromaticityDiagramTarget?>(nameof(SelectedTarget), diagram => diagram.SelectedTarget);
    public static readonly StyledProperty<IReadOnlyList<ChromaticityDiagramSample>> SamplesProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, IReadOnlyList<ChromaticityDiagramSample>>(nameof(Samples),
            Array.Empty<ChromaticityDiagramSample>(), validate: samples => samples is not null && samples.All(sample => sample is not null));
    public static readonly StyledProperty<WavelengthDiagramData?> WavelengthDataProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, WavelengthDiagramData?>(nameof(WavelengthData));
    public static readonly StyledProperty<PlanckianDiagramData?> LightDataProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, PlanckianDiagramData?>(nameof(LightData));
    public static readonly StyledProperty<string?> LightNameProperty =
        AvaloniaProperty.Register<ChromaticityDiagram, string?>(nameof(LightName));

    private static readonly Lazy<byte[]> Background2 = new(() => ChromaticityDiagramBackground.Create(StandardObserver.Degree2));
    private static readonly Lazy<byte[]> Background10 = new(() => ChromaticityDiagramBackground.Create(StandardObserver.Degree10));
    private static readonly Lazy<byte[]> UvBackground2 = new(() => ChromaticityDiagramBackground.Create(StandardObserver.Degree2, ChromaticityDiagramSpace.UvPrime));
    private static readonly Lazy<byte[]> UvBackground10 = new(() => ChromaticityDiagramBackground.Create(StandardObserver.Degree10, ChromaticityDiagramSpace.UvPrime));
    private WriteableBitmap? _background;
    private StandardObserver _backgroundObserver;
    private ChromaticityDiagramSpace _backgroundSpace;
    private ChromaticityDiagramTarget? _selectedTarget;
    private const string PlanckianKey = "planckian", WhiteKey = "white", SampleKey = "sample",
        DominantKey = "dominant", ComplementaryKey = "complementary", LightKey = "light", NearestKey = "nearest";
    private readonly HashSet<string> _hiddenLegendItems = new();
    private bool PlanckianVisible => ShowPlanckianLocus && !_hiddenLegendItems.Contains(PlanckianKey);
    private bool Visible(string key) => !_hiddenLegendItems.Contains(key) && (key != NearestKey || PlanckianVisible);
    private static string NamedSampleKey(int index) => $"named:{index}";
    public ChromaticityDiagramSpace Space { get => GetValue(SpaceProperty); set => SetValue(SpaceProperty, value); }
    public StandardObserver Observer { get => GetValue(ObserverProperty); set => SetValue(ObserverProperty, value); }
    public bool ShowTemperatureLabels { get => GetValue(ShowTemperatureLabelsProperty); set => SetValue(ShowTemperatureLabelsProperty, value); }
    public bool ShowWavelengthLabels { get => GetValue(ShowWavelengthLabelsProperty); set => SetValue(ShowWavelengthLabelsProperty, value); }
    public bool ShowPlanckianLocus
    {
        get => GetValue(ShowPlanckianLocusProperty);
        set { _hiddenLegendItems.Remove(PlanckianKey); SetValue(ShowPlanckianLocusProperty, value); InvalidateVisual(); }
    }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    /// <summary>Show the observer/standard caption above the axes. Hiding it reclaims its margin without changing Observer.</summary>
    public bool ShowObserverLabel { get => GetValue(ShowObserverLabelProperty); set => SetValue(ShowObserverLabelProperty, value); }
    /// <summary>Null uses the shared default: xy 1.0 × 1.0, u′v′ 0.8 × 0.7.</summary>
    public double? AxisMaximumX { get => GetValue(AxisMaximumXProperty); set => SetValue(AxisMaximumXProperty, value); }
    public double? AxisMaximumY { get => GetValue(AxisMaximumYProperty); set => SetValue(AxisMaximumYProperty, value); }
    public ChromaticityDiagramTarget? SelectedTarget => _selectedTarget;
    public event EventHandler? SelectionChanged;
    /// <summary>Named xy samples projected into the selected space. Replace the list to refresh a binding.</summary>
    public IReadOnlyList<ChromaticityDiagramSample> Samples { get => GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public WavelengthDiagramData? WavelengthData { get => GetValue(WavelengthDataProperty); set => SetValue(WavelengthDataProperty, value); }
    public PlanckianDiagramData? LightData { get => GetValue(LightDataProperty); set => SetValue(LightDataProperty, value); }
    public string? LightName { get => GetValue(LightNameProperty); set => SetValue(LightNameProperty, value); }
    private StandardObserver EffectiveObserver => WavelengthData?.Observer ?? LightData?.Observer ?? Observer;
    private ChromaticityDiagramProjection Projection => new(Space);
    private ChromaticityDiagramViewport Viewport => new(Space, AxisMaximumX, AxisMaximumY);
    private static bool ValidMaximum(double? value) => value is null || double.IsFinite(value.Value) && value > 0;

    public Rect PlotArea
    {
        get
        {
            var viewport = Viewport;
            double top = ShowObserverLabel ? 30 : 20;
            double bottom = ShowObserverLabel ? 54 : 44;
            double scale = Math.Max(0, Math.Min((Bounds.Width - 72) / viewport.MaximumX, (Bounds.Height - top - bottom) / viewport.MaximumY));
            return new Rect((Bounds.Width - viewport.MaximumX * scale + 40) / 2, top,
                viewport.MaximumX * scale, viewport.MaximumY * scale);
        }
    }
    public Rect LegendArea => LayoutLegend(PlotArea).Area;

    /// <summary>Convert a point in the diagram's selected coordinate space to a control-relative position.</summary>
    public Point PlotPoint(ChromaticityDiagramPoint point)
    {
        var plot = PlotArea; var viewport = Viewport;
        return new Point(plot.Left + point.X / viewport.MaximumX * plot.Width,
            plot.Bottom - point.Y / viewport.MaximumY * plot.Height);
    }

    static ChromaticityDiagram()
    {
        AffectsRender<ChromaticityDiagram>(SpaceProperty, ObserverProperty,
            ShowTemperatureLabelsProperty, ShowWavelengthLabelsProperty, ShowPlanckianLocusProperty, ShowLegendProperty, ShowObserverLabelProperty,
            AxisMaximumXProperty, AxisMaximumYProperty, SelectedTargetProperty,
            SamplesProperty, WavelengthDataProperty, LightDataProperty, LightNameProperty);
    }

    public ChromaticityDiagram()
    {
        MaxWidth = 600;
        HorizontalAlignment = HorizontalAlignment.Center;
        Focusable = true; ClipToBounds = true;
    }

    public void Clear(StandardObserver observer)
    {
        SetCurrentValue(ObserverProperty, observer);
        SetCurrentValue(WavelengthDataProperty, null);
        SetCurrentValue(LightDataProperty, null);
        SetCurrentValue(LightNameProperty, null);
        SetCurrentValue(SamplesProperty, Array.Empty<ChromaticityDiagramSample>());
    }

    /// <summary>Replace measurements with a snapshot of named samples, preserving display options and observer.</summary>
    public void SetSamples(IEnumerable<ChromaticityDiagramSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var snapshot = samples.ToArray();
        if (snapshot.Any(sample => sample is null)) throw new ArgumentException("A sample cannot be null.", nameof(samples));
        foreach (var sample in snapshot) Projection.Project(sample.Coordinates.X, sample.Coordinates.Y);
        Clear(Observer);
        SetCurrentValue(SamplesProperty, Array.AsReadOnly(snapshot));
    }

    public void SetData(WavelengthDiagramData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Clear(data.Observer); SetCurrentValue(WavelengthDataProperty, data);
    }

    public void SetLightData(PlanckianDiagramData data, string lightName)
    {
        ArgumentNullException.ThrowIfNull(data);
        Clear(data.Observer); SetCurrentValue(LightDataProperty, data); SetCurrentValue(LightNameProperty, lightName);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _background?.Dispose(); _background = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double side = double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, 600) : 600;
        return new Size(side, side);
    }

    private sealed record LegendItem(string Key, string Name, IBrush Fill, bool Square = false, bool Line = false);

    private List<LegendItem> LegendItems()
    {
        var legend = new List<LegendItem>();
        if (ShowLegend)
        {
            if (ShowPlanckianLocus) legend.Add(new(PlanckianKey, "黑体辐射", Brushes.Black, Line: true));
            if (WavelengthData is { } calculation)
            {
                legend.Add(new(WhiteKey, "W 白点", Brushes.White));
                legend.Add(new(SampleKey, "P 样品", Brushes.Black));
                if (!calculation.Wavelengths.IsAchromatic)
                {
                    legend.Add(new(DominantKey, "D 主波长", Brushes.White));
                    legend.Add(new(ComplementaryKey, "C 补色波长", Brushes.White));
                }
            }
            else if (LightData is not null && LightName is not null) legend.Add(new(LightKey, $"S {LightName}", Brushes.Black));
            if ((WavelengthData?.Planckian ?? LightData) is not null && ShowPlanckianLocus)
                legend.Add(new(NearestKey, "T 黑体轨迹最近点", Brushes.White, Square: true));
            for (int i = 0; i < Samples.Count; i++) legend.Add(new(NamedSampleKey(i), Samples[i].Name, SampleBrush(Samples[i])));
        }
        return legend;
    }

    private (List<(FormattedText Caption, LegendItem Item, Point Offset, Rect HitArea)> Items, Rect Area) LayoutLegend(Rect plot)
    {
        var legend = LegendItems();
        var positions = new List<(FormattedText, LegendItem, Point, Rect)>();
        if (legend.Count == 0 || plot.Width <= 16 || plot.Height <= 16) return (positions, default);
        const double rowHeight = 20, padding = 8;
        int rows = Math.Max(1, (int)((plot.Height - padding * 2 - 16) / rowHeight));
        int columns = (legend.Count + rows - 1) / rows;
        double width = Math.Min(200, (plot.Width - padding * 2 - 16) / Math.Max(2.5, columns));
        double height = Math.Min(rows, legend.Count) * rowHeight + padding * 2;
        double totalWidth = columns * width + padding * 2;
        var area = new Rect(plot.Right - totalWidth - 8,
            Viewport.LegendAtTop ? plot.Top + 8 : plot.Bottom - height - 8, totalWidth, height);
        for (int i = 0; i < legend.Count; i++)
        {
            var item = legend[i];
            var caption = new FormattedText(LabelText(item.Name), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 11, Brush.Parse(Visible(item.Key) ? "#555555" : "#AAAAAA"))
            {
                MaxTextWidth = Math.Max(1, width - 28), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis
            };
            var offset = new Point(padding + i / rows * width, padding + i % rows * rowHeight);
            positions.Add((caption, item, offset, new Rect(area.Left + offset.X - 2, area.Top + offset.Y - 2, width, rowHeight)));
        }
        return (positions, area);
    }

    /// <summary>Hit area of an item in displayed legend order.</summary>
    public Rect GetLegendItemArea(int index)
    {
        var items = LayoutLegend(PlotArea).Items;
        if (index < 0 || index >= items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return items[index].HitArea;
    }

    public bool IsLegendItemVisible(int index)
    {
        var items = LegendItems();
        if (index < 0 || index >= items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return Visible(items[index].Key);
    }

    /// <summary>Temporarily toggle an item without changing measurements or configured display options.</summary>
    public void ToggleLegendItem(int index)
    {
        var items = LegendItems();
        if (index < 0 || index >= items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!_hiddenLegendItems.Remove(items[index].Key)) _hiddenLegendItems.Add(items[index].Key);
        ClearSelection(); InvalidateVisual();
    }

    private void EnsureBackground()
    {
        var observer = EffectiveObserver;
        if (_background is not null && _backgroundObserver == observer && _backgroundSpace == Space) return;
        _background?.Dispose();
        _background = new WriteableBitmap(new PixelSize(ChromaticityDiagramBackground.Width, Projection.Height),
            new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        var bytes = Space == ChromaticityDiagramSpace.Xy
            ? (observer == StandardObserver.Degree2 ? Background2.Value : Background10.Value)
            : (observer == StandardObserver.Degree2 ? UvBackground2.Value : UvBackground10.Value);
        using var frame = _background.Lock();
        int rowBytes = ChromaticityDiagramBackground.Width * 4;
        for (int row = 0; row < Projection.Height; row++)
            Marshal.Copy(bytes, row * rowBytes, IntPtr.Add(frame.Address, row * frame.RowBytes), rowBytes);
        _backgroundObserver = observer; _backgroundSpace = Space;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 120 || Bounds.Height < 120) return;
        EnsureBackground();
        var calculation = WavelengthData;
        var planckian = calculation?.Planckian ?? LightData;
        var observer = EffectiveObserver;
        var projection = Projection;
        FormattedText Caption(string text, IBrush? brush = null) => new(LabelText(text), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 11, brush ?? Brush.Parse("#555555"));
        var plot = PlotArea;
        if (plot.Width <= 0 || plot.Height <= 0) return;
        var (legendPositions, legendArea) = LayoutLegend(plot);
        double maxX = Viewport.MaximumX, maxY = Viewport.MaximumY, top = plot.Top;
        Point At(double x, double y) => PlotPoint(new ChromaticityDiagramPoint(x, y));
        Point FromXy(double x, double y)
        {
            var point = projection.Project(x, y);
            return At(point.X, point.Y);
        }
        void Label(string text, double x, double y) => context.DrawText(Caption(text), new Point(x, y));
        var annotations = new List<Rect>();
        if (legendPositions.Count > 0) annotations.Add(legendArea.Inflate(4));
        foreach (var sample in Samples)
        {
            var point = FromXy(sample.Coordinates.X, sample.Coordinates.Y);
            double radius = Math.Max(8, sample.MarkerRadius + 2);
            annotations.Add(new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2));
        }
        if (planckian is { } markerData)
        {
            var point = FromXy(markerData.Sample.X, markerData.Sample.Y);
            annotations.Add(new Rect(point.X - 8, point.Y - 8, 16, 16));
            if (ShowPlanckianLocus)
            {
                var nearest = FromXy(markerData.Cct.NearestPoint.X, markerData.Cct.NearestPoint.Y);
                annotations.Add(new Rect(nearest.X - 8, nearest.Y - 8, 16, 16));
            }
        }
        void Tag(string text, Point at, Point anchor)
        {
            var caption = Caption(text, Brushes.Black);
            Rect Position(double dx, double dy) => new(
                Math.Clamp(at.X + dx, 1, Math.Max(1, Bounds.Width - caption.Width - 9)),
                Math.Clamp(at.Y + dy, 1, Math.Max(1, Bounds.Height - caption.Height - 6)),
                caption.Width + 8, caption.Height + 4);
            var rectangle = Position(0, 0);
            var offsets = new[] { (0.0, 0.0), (0.0, -24.0), (0.0, 24.0),
                (-caption.Width - 16, 0.0), (0.0, -48.0), (0.0, 48.0), (-caption.Width - 16, -24.0), (-caption.Width - 16, 24.0) }
                .Concat(from dy in new[] { -72.0, 72.0, -96.0, 96.0, -120.0, 120.0, -144.0, 144.0 }
                    from dx in new[] { 0.0, -caption.Width - 16, caption.Width + 16 }
                    select (dx, dy));
            foreach (var offset in offsets)
            {
                var candidate = Position(offset.Item1, offset.Item2);
                if (annotations.Any(existing => existing.Intersects(candidate))) continue;
                rectangle = candidate; break;
            }
            annotations.Add(new Rect(rectangle.X - 2, rectangle.Y - 2, rectangle.Width + 4, rectangle.Height + 4));
            var edge = new Point(Math.Clamp(anchor.X, rectangle.Left, rectangle.Right), Math.Clamp(anchor.Y, rectangle.Top, rectangle.Bottom));
            context.DrawLine(new Pen(Brush.Parse("#666666"), 0.8), anchor, edge);
            context.DrawRectangle(Brush.Parse("#EFFFFFFF"), null, rectangle, 3, 3);
            context.DrawText(caption, new Point(rectangle.X + 4, rectangle.Y + 2));
        }
        context.DrawRectangle(Brushes.White, null, plot);
        using (context.PushClip(plot))
            context.DrawImage(_background!, new Rect(At(0, projection.MaxY), At(projection.MaxX, 0)));
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var grid = new Pen(Brush.Parse("#80909090"), 0.6);
        double TickStep(double maximum, double pixels)
        {
            double minimumStep = Math.Max(0.1, maximum / pixels * 32);
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(minimumStep)));
            return new[] { 1.0, 2, 5, 10 }.First(factor => factor * magnitude >= minimumStep) * magnitude;
        }
        double xStep = TickStep(maxX, plot.Width), yStep = TickStep(maxY, plot.Height);
        for (int i = 0; i <= (int)Math.Floor(maxX / xStep + 1e-9); i++)
        {
            double x = i * xStep;
            context.DrawLine(grid, At(x, 0), At(x, maxY));
            Label(x.ToString("0.0", CultureInfo.InvariantCulture), At(x, 0).X - 8, plot.Bottom + 8);
        }
        for (int i = 0; i <= (int)Math.Floor(maxY / yStep + 1e-9); i++)
        {
            double y = i * yStep;
            context.DrawLine(grid, At(0, y), At(maxX, y));
            Label(y.ToString("0.0", CultureInfo.InvariantCulture), plot.Left - 28, At(0, y).Y - 7);
        }
        context.DrawRectangle(null, new Pen(Brush.Parse("#999999")), plot);
        Label(Space == ChromaticityDiagramSpace.Xy ? "x" : "u′", plot.Right - 12, plot.Bottom + 28);
        Label(Space == ChromaticityDiagramSpace.Xy ? "y" : "v′", plot.Left - 28, top - 19);
        if (ShowObserverLabel)
            Label(observer == StandardObserver.Degree2 ? "2° · CIE 1931" : "10° · CIE 1964", plot.Left + 4, 4);
        var boundary = CieSpectralData.GetChromaticityBoundary(observer);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(FromXy(boundary[0].X, boundary[0].Y), true);
            foreach (var point in boundary.Skip(1)) path.LineTo(FromXy(point.X, point.Y));
            path.EndFigure(true);
        }
        context.DrawGeometry(null, new Pen(Brush.Parse("#333333"), 1.7), geometry);
        if (PlanckianVisible)
        {
            var planckLocus = ChromaticityConversion.GetPlanckianLocus(observer);
            var planckGeometry = new StreamGeometry();
            using (var path = planckGeometry.Open())
            {
                path.BeginFigure(FromXy(planckLocus[0].X, planckLocus[0].Y), false);
                foreach (var point in planckLocus.Skip(1)) path.LineTo(FromXy(point.X, point.Y));
                path.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(Brushes.White, 3.5), planckGeometry);
            context.DrawGeometry(null, new Pen(Brushes.Black, 1.5), planckGeometry);
        }
        void TemperatureLabels()
        {
            if (!ShowTemperatureLabels || _hiddenLegendItems.Contains(PlanckianKey)) return;
            foreach (int temperature in ReferenceTemperatures())
            {
                var point = ChromaticityConversion.GetPlanckianChromaticity(temperature, observer);
                var position = FromXy(point.X, point.Y);
                context.DrawEllipse(Brushes.White, new Pen(Brushes.Black), position, 2.5, 2.5);
                Tag($"{temperature} K", new Point(position.X - 12, position.Y + (temperature >= 6500 ? 20 : -28)), position);
            }
        }
        void PlanckianConnection()
        {
            if (planckian is not { } data) return;
            var sample = FromXy(data.Sample.X, data.Sample.Y);
            if (LightName is not null && calculation is null && Visible(LightKey))
            {
                context.DrawEllipse(Brushes.Black, new Pen(Brushes.White, 2), sample, 5, 5);
                Tag($"S {LightName}", new Point(sample.X + 10, sample.Y - 24), sample);
            }
            if (!PlanckianVisible || !Visible(NearestKey)) return;
            var nearest = FromXy(data.Cct.NearestPoint.X, data.Cct.NearestPoint.Y);
            using (context.PushGeometryClip(geometry))
            {
                context.DrawLine(new Pen(Brushes.White, 3.5), sample, nearest);
                context.DrawLine(new Pen(Brushes.Black, 1.7, new DashStyle([2, 3], 0)), sample, nearest);
            }
            context.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1.5), new Rect(nearest.X - 3.5, nearest.Y - 3.5, 7, 7));
            string nearestLabel = data.Cct.Temperature is { } temperature
                ? $"T {temperature.ToString("0", CultureInfo.InvariantCulture)} K"
                : data.Cct.IsInTemperatureRange ? "T CCT 不适用" : "T 轨迹端点";
            if (ShowTemperatureLabels) Tag(nearestLabel, new Point(nearest.X - 12, nearest.Y + 12), nearest);
            var middle = new Point((sample.X + nearest.X) / 2, (sample.Y + nearest.Y) / 2);
            Tag($"Duv {data.Cct.Duv.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture)}",
                new Point(middle.X + 12, middle.Y + 34), middle);
        }
        var locus = CieSpectralData.GetSpectralLocus(observer);
        foreach (int wavelength in ShowWavelengthLabels ? new[] { 400, 450, 500, 550, 600, 650 } : Array.Empty<int>())
        {
            var point = locus[wavelength - 360];
            var position = FromXy(point.X, point.Y);
            context.DrawEllipse(Brushes.White, new Pen(Brush.Parse("#555555")), position, 2, 2);
            // Violet wavelengths cluster near the bottom of the diagram; offset their
            // captions separately so 400 and 450 nm remain readable on narrow screens.
            var label = wavelength switch
            {
                400 => new Point(position.X + 50, position.Y - 14),
                450 => new Point(position.X + 5, position.Y - 32),
                _ => new Point(position.X + 5, position.Y - 16)
            };
            Tag($"{wavelength} nm", label, position);
        }
        void SamplesAndLegend()
        {
            for (int i = 0; i < Samples.Count; i++)
                if (Visible(NamedSampleKey(i)))
                    context.DrawEllipse(SampleBrush(Samples[i]), new Pen(Brushes.White, 2),
                        FromXy(Samples[i].Coordinates.X, Samples[i].Coordinates.Y), Samples[i].MarkerRadius, Samples[i].MarkerRadius);
            if (legendPositions.Count > 0)
                context.DrawRectangle(Brush.Parse("#F2FFFFFF"), new Pen(Brush.Parse("#999999")), legendArea, 3, 3);
            foreach (var item in legendPositions)
            {
                var position = new Point(legendArea.Left + item.Offset.X, legendArea.Top + item.Offset.Y);
                var center = new Point(position.X + 5, position.Y + item.Caption.Height / 2);
                var pen = new Pen(Visible(item.Item.Key) ? Brushes.Black : Brush.Parse("#AAAAAA"));
                var fill = Visible(item.Item.Key) ? item.Item.Fill : Brush.Parse("#DDDDDD");
                if (item.Item.Line) context.DrawLine(pen, new Point(center.X - 4, center.Y), new Point(center.X + 6, center.Y));
                else if (item.Item.Square) context.DrawRectangle(fill, pen, new Rect(center.X - 4, center.Y - 4, 8, 8));
                else context.DrawEllipse(fill, pen, center, 4, 4);
                context.DrawText(item.Caption, new Point(position.X + 16, position.Y));
            }
            DrawTracker(context);
        }
        if (calculation is null)
        {
            PlanckianConnection(); TemperatureLabels(); SamplesAndLegend(); return;
        }
        var w = FromXy(calculation.White.X, calculation.White.Y);
        var p = FromXy(calculation.Sample.X, calculation.Sample.Y);
        annotations.Add(new Rect(w.X - 8, w.Y - 8, 16, 16));
        annotations.Add(new Rect(p.X - 7, p.Y - 7, 14, 14));
        if (projection.Intersections(calculation) is { } intersections)
        {
            var forward = At(intersections.Forward.X, intersections.Forward.Y);
            var reverse = At(intersections.Reverse.X, intersections.Reverse.Y);
            annotations.Add(new Rect(forward.X - 6, forward.Y - 6, 12, 12));
            annotations.Add(new Rect(reverse.X - 6, reverse.Y - 6, 12, 12));
            using (context.PushGeometryClip(geometry))
            {
                context.DrawLine(new Pen(Brushes.White, 3), reverse, forward);
                context.DrawLine(new Pen(Brush.Parse("#444444"), 1.3, new DashStyle([5, 4], 0)), reverse, forward);
                context.DrawLine(new Pen(Brushes.Black, 2), w, p);
            }
            void Intersection(string name, Point at, double? wavelength, double offsetY)
            {
                if (!Visible(name == "D" ? DominantKey : ComplementaryKey)) return;
                context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1.5), at, 4, 4);
                Tag(wavelength.HasValue ? $"{name} {wavelength.Value.ToString("F2", CultureInfo.InvariantCulture)} nm" : $"{name} 紫边",
                    new Point(at.X + 8, at.Y + offsetY), at);
            }
            Intersection("D", forward, calculation.Wavelengths.DominantWavelength, -25);
            Intersection("C", reverse, calculation.Wavelengths.ComplementaryWavelength, 8);
        }
        PlanckianConnection();
        if (Visible(WhiteKey))
        {
            context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 2), w, 6, 6);
            Tag("W 白点", new Point(w.X + 10, w.Y + 7), w);
        }
        if (Visible(SampleKey))
        {
            context.DrawEllipse(Brushes.Black, new Pen(Brushes.White, 2), p, 5, 5);
            Tag("P 样品", new Point(p.X + 10, p.Y - 24), p);
        }
        TemperatureLabels();
        SamplesAndLegend();
    }

    private static IBrush SampleBrush(ChromaticityDiagramSample sample) => new SolidColorBrush(Color.FromUInt32(sample.MarkerArgb));

    private IEnumerable<ChromaticityDiagramTarget> MarkerTargets()
    {
        var projection = Projection;
        ChromaticityDiagramTarget Target(string name, double x, double y, double? wavelength = null,
            double? temperature = null, double? duv = null) => new(name, Space, projection.Project(x, y), wavelength, temperature, duv);
        for (int i = 0; i < Samples.Count; i++)
            if (Visible(NamedSampleKey(i))) yield return Target(Samples[i].Name, Samples[i].Coordinates.X, Samples[i].Coordinates.Y);
        var calculation = WavelengthData;
        var planckian = calculation?.Planckian ?? LightData;
        if (calculation is not null)
        {
            if (Visible(SampleKey)) yield return Target("P 样品", calculation.Sample.X, calculation.Sample.Y);
            if (Visible(WhiteKey)) yield return Target("W 白点", calculation.White.X, calculation.White.Y);
            if (projection.Intersections(calculation) is { } intersections)
            {
                if (Visible(DominantKey)) yield return new("D 主波长", Space, intersections.Forward, calculation.Wavelengths.DominantWavelength);
                if (Visible(ComplementaryKey)) yield return new("C 补色波长", Space, intersections.Reverse, calculation.Wavelengths.ComplementaryWavelength);
            }
        }
        else if (LightData is { } light && LightName is not null && Visible(LightKey))
            yield return Target($"S {LightName}", light.Sample.X, light.Sample.Y, temperature: light.Cct.Temperature, duv: light.Cct.Duv);
        if (PlanckianVisible && Visible(NearestKey) && planckian is { } data)
            yield return Target("T 黑体轨迹最近点", data.Cct.NearestPoint.X, data.Cct.NearestPoint.Y,
                temperature: data.Cct.Temperature, duv: data.Cct.Duv);
        if (ShowTemperatureLabels && !_hiddenLegendItems.Contains(PlanckianKey))
            foreach (int temperature in ReferenceTemperatures())
            {
                var point = ChromaticityConversion.GetPlanckianChromaticity(temperature, EffectiveObserver);
                yield return Target("黑体辐射", point.X, point.Y, temperature: temperature);
            }
    }

    private int[] ReferenceTemperatures() => Bounds.Width < 400 ? [2000, 4000, 6500, 25000] : [2000, 3000, 4000, 6500, 10000, 25000];

    /// <summary>Hit-test visible markers, then curve vertices. Empty areas and the legend do not select data.</summary>
    public ChromaticityDiagramTarget? HitTestTarget(Point position, double tolerance = 10)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (!PlotArea.Contains(position) || LegendArea.Contains(position)) return null;
        ChromaticityDiagramTarget? Nearest(IEnumerable<ChromaticityDiagramTarget> targets, double radius)
        {
            double best = radius * radius;
            ChromaticityDiagramTarget? result = null;
            foreach (var target in targets)
            {
                var pixel = PlotPoint(target.Point);
                if (!PlotArea.Contains(pixel)) continue;
                double distance = Math.Pow(pixel.X - position.X, 2) + Math.Pow(pixel.Y - position.Y, 2);
                if (distance > best || result is not null && distance == best) continue;
                best = distance; result = target;
            }
            return result;
        }
        if (Nearest(MarkerTargets(), tolerance) is { } marker) return marker;
        var projection = Projection;
        var boundary = CieSpectralData.GetChromaticityBoundary(EffectiveObserver).Select(point =>
            new ChromaticityDiagramTarget("色度边界", Space, projection.Project(point.X, point.Y), point.Wavelength));
        var curves = PlanckianVisible
            ? boundary.Concat(ChromaticityConversion.GetPlanckianLocus(EffectiveObserver).Select(point =>
                new ChromaticityDiagramTarget("黑体辐射", Space, projection.Project(point.X, point.Y), temperature: point.Temperature)))
            : boundary;
        return Nearest(curves, Math.Min(tolerance, 6));
    }

    public void ClearSelection() => SetSelectedTarget(null);

    private void SetSelectedTarget(ChromaticityDiagramTarget? target)
    {
        if (_selectedTarget is null && target is null) return;
        if (_selectedTarget is { } current && target is not null && current.Name == target.Name && current.Space == target.Space &&
            current.Point == target.Point && current.Wavelength == target.Wavelength &&
            current.Temperature == target.Temperature && current.Duv == target.Duv) return;
        SetAndRaise(SelectedTargetProperty, ref _selectedTarget, target);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SamplesProperty || change.Property == WavelengthDataProperty || change.Property == LightDataProperty)
            _hiddenLegendItems.RemoveWhere(key => key != PlanckianKey);
        if (change.Property == ShowPlanckianLocusProperty) _hiddenLegendItems.Remove(PlanckianKey);
        if (change.Property == SpaceProperty || change.Property == ObserverProperty || change.Property == SamplesProperty ||
            change.Property == WavelengthDataProperty || change.Property == LightDataProperty || change.Property == LightNameProperty ||
            change.Property == ShowPlanckianLocusProperty || change.Property == ShowTemperatureLabelsProperty)
            ClearSelection();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var items = LayoutLegend(PlotArea).Items;
        for (int i = 0; i < items.Count; i++)
            if (items[i].HitArea.Contains(e.GetPosition(this)))
            {
                ToggleLegendItem(i); Focus(); e.Handled = true; return;
            }
        SetSelectedTarget(HitTestTarget(e.GetPosition(this)));
        Focus(); e.Pointer.Capture(this); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured != this) return;
        SetSelectedTarget(HitTestTarget(e.GetPosition(this))); e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this) { e.Pointer.Capture(null); e.Handled = true; }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { ClearSelection(); e.Handled = true; }
    }

    private void DrawTracker(DrawingContext context)
    {
        if (SelectedTarget is not { } target) return;
        var point = PlotPoint(target.Point);
        var caption = new FormattedText(LabelText(target.TrackerText), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 12, Brushes.Black)
        {
            MaxTextWidth = Math.Max(1, Bounds.Width - 24)
        };
        double width = caption.Width + 16, height = caption.Height + 12;
        double x = Math.Clamp(point.X + 12, 2, Math.Max(2, Bounds.Width - width - 2));
        double y = Math.Clamp(point.Y - height - 12, 2, Math.Max(2, Bounds.Height - height - 2));
        var rectangle = new Rect(x, y, width, height);
        context.DrawLine(new Pen(Brushes.Black), point, new Point(Math.Clamp(point.X, rectangle.Left, rectangle.Right), rectangle.Bottom));
        context.DrawRectangle(Brush.Parse("#FFFDEDB0"), new Pen(Brushes.Black), rectangle, 3, 3);
        context.DrawText(caption, new Point(x + 8, y + 6));
        context.DrawEllipse(null, new Pen(Brushes.Black, 2), point, 7, 7);
    }
}
