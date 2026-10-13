using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
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
    public ChromaticityDiagramSpace Space { get => GetValue(SpaceProperty); set => SetValue(SpaceProperty, value); }
    public StandardObserver Observer { get => GetValue(ObserverProperty); set => SetValue(ObserverProperty, value); }
    public bool ShowTemperatureLabels { get => GetValue(ShowTemperatureLabelsProperty); set => SetValue(ShowTemperatureLabelsProperty, value); }
    public WavelengthDiagramData? WavelengthData { get => GetValue(WavelengthDataProperty); set => SetValue(WavelengthDataProperty, value); }
    public PlanckianDiagramData? LightData { get => GetValue(LightDataProperty); set => SetValue(LightDataProperty, value); }
    public string? LightName { get => GetValue(LightNameProperty); set => SetValue(LightNameProperty, value); }
    private StandardObserver EffectiveObserver => WavelengthData?.Observer ?? LightData?.Observer ?? Observer;
    private ChromaticityDiagramProjection Projection => new(Space);

    static ChromaticityDiagram() => AffectsRender<ChromaticityDiagram>(SpaceProperty, ObserverProperty,
        ShowTemperatureLabelsProperty, WavelengthDataProperty, LightDataProperty, LightNameProperty);

    public ChromaticityDiagram()
    {
        MaxWidth = 600;
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    public void Clear(StandardObserver observer)
    {
        SetCurrentValue(ObserverProperty, observer);
        SetCurrentValue(WavelengthDataProperty, null);
        SetCurrentValue(LightDataProperty, null);
        SetCurrentValue(LightNameProperty, null);
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
        double maxX = projection.MaxX, maxY = projection.MaxY;
        double scale = Math.Min((Bounds.Width - 72) / maxX, (Bounds.Height - 84) / maxY);
        double left = (Bounds.Width - maxX * scale + 40) / 2, top = 30;
        var plot = new Rect(left, top, maxX * scale, maxY * scale);
        Point At(double x, double y) => new(left + x * scale, top + (maxY - y) * scale);
        Point FromXy(double x, double y)
        {
            var point = projection.Project(x, y);
            return At(point.X, point.Y);
        }
        FormattedText Caption(string text, IBrush? brush = null) => new(LabelText(text), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 11, brush ?? Brush.Parse("#555555"));
        void Label(string text, double x, double y) => context.DrawText(Caption(text), new Point(x, y));
        var annotations = new List<Rect>();
        if (planckian is { } markerData)
        {
            foreach (var point in new[] { FromXy(markerData.Sample.X, markerData.Sample.Y), FromXy(markerData.Cct.NearestPoint.X, markerData.Cct.NearestPoint.Y) })
                annotations.Add(new Rect(point.X - 8, point.Y - 8, 16, 16));
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
        context.DrawImage(_background!, plot);
        var grid = new Pen(Brush.Parse("#80909090"), 0.6);
        for (int i = 0; i <= (int)Math.Round(maxX * 10); i++)
        {
            double x = i / 10.0;
            context.DrawLine(grid, At(x, 0), At(x, maxY));
            Label(x.ToString("0.0", CultureInfo.InvariantCulture), At(x, 0).X - 8, plot.Bottom + 8);
        }
        for (int i = 0; i <= (int)Math.Round(maxY * 10); i++)
        {
            double y = i / 10.0;
            context.DrawLine(grid, At(0, y), At(maxX, y));
            Label(y.ToString("0.0", CultureInfo.InvariantCulture), plot.Left - 28, At(0, y).Y - 7);
        }
        context.DrawRectangle(null, new Pen(Brush.Parse("#999999")), plot);
        Label(Space == ChromaticityDiagramSpace.Xy ? "x" : "u′", plot.Right - 12, plot.Bottom + 28);
        Label(Space == ChromaticityDiagramSpace.Xy ? "y" : "v′", plot.Left - 28, top - 19);
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
        void TemperatureLabels()
        {
            if (!ShowTemperatureLabels) return;
            int[] temperatures = Bounds.Width < 400 ? [2000, 4000, 6500, 25000] : [2000, 3000, 4000, 6500, 10000, 25000];
            foreach (int temperature in temperatures)
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
            Tag(nearestLabel, new Point(nearest.X - 12, nearest.Y + 12), nearest);
            var middle = new Point((sample.X + nearest.X) / 2, (sample.Y + nearest.Y) / 2);
            Tag($"Duv {data.Cct.Duv.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture)}",
                new Point(middle.X + 12, middle.Y + 34), middle);
            if (LightName is not null)
            {
                context.DrawEllipse(Brushes.Black, new Pen(Brushes.White, 2), sample, 5, 5);
                Tag($"S {LightName}", new Point(sample.X + 10, sample.Y - 24), sample);
            }
        }
        var locus = CieSpectralData.GetSpectralLocus(observer);
        foreach (int wavelength in new[] { 400, 450, 500, 550, 600, 650 })
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
        if (calculation is null)
        {
            PlanckianConnection(); TemperatureLabels(); return;
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
                context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1.5), at, 4, 4);
                Tag(wavelength.HasValue ? $"{name} {wavelength.Value.ToString("F2", CultureInfo.InvariantCulture)} nm" : $"{name} 紫边",
                    new Point(at.X + 8, at.Y + offsetY), at);
            }
            Intersection("D", forward, calculation.Wavelengths.DominantWavelength, -25);
            Intersection("C", reverse, calculation.Wavelengths.ComplementaryWavelength, 8);
        }
        PlanckianConnection();
        context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 2), w, 6, 6);
        context.DrawEllipse(Brushes.Black, new Pen(Brushes.White, 2), p, 5, 5);
        Tag("W 白点", new Point(w.X + 10, w.Y + 7), w);
        Tag("P 样品", new Point(p.X + 10, p.Y - 24), p);
        TemperatureLabels();
    }
}
