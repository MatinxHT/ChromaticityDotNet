using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ChromaticityDotNet.Diagrams;
using static ChromaticityDotNet.Model.DataModel;

namespace ChromaticityDotNet.AvaloniaDiagram.Controls;

/// <summary>Separates L* from the polar C*/h° plane, comparing standard and sample.</summary>
public sealed class LchPlot : DiagramControl
{
    public static readonly StyledProperty<CIELAB?> SampleLabProperty =
        AvaloniaProperty.Register<LchPlot, CIELAB?>(nameof(SampleLab));
    public static readonly StyledProperty<CIELAB?> StandardLabProperty =
        AvaloniaProperty.Register<LchPlot, CIELAB?>(nameof(StandardLab));

    // A fixed lightness keeps the CH background comparable between measurements.
    public const double BackgroundLightness = LchDiagramBackground.DefaultLightness;
    private const int BackgroundPixels = 256;
    private static readonly IBrush StandardBrush = new SolidColorBrush(Color.Parse("#E07B39"));
    private static readonly IBrush SampleBrush = new SolidColorBrush(Color.Parse("#1778B5"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#526373"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#9064748B")), 1);
    private static readonly IBrush LightnessBackground = CreateLightnessBackground();
    private WriteableBitmap? _chBackground;
    private double _backgroundMaximum;

    static LchPlot() => AffectsRender<LchPlot>(SampleLabProperty, StandardLabProperty);

    public CIELAB? SampleLab
    {
        get => GetValue(SampleLabProperty);
        set => SetValue(SampleLabProperty, value);
    }
    public CIELAB? StandardLab
    {
        get => GetValue(StandardLabProperty);
        set => SetValue(StandardLabProperty, value);
    }

    public double ChromaMaximum => LchDiagramProjection.ChromaMaximum(StandardLab, SampleLab);

    public Rect LightnessArea => new(36, 18, 32, Math.Max(0, Bounds.Height - 36));
    public Rect ChromaHueArea
    {
        get
        {
            const double left = 108;
            double availableWidth = Math.Max(0, Bounds.Width - left - 40);
            double availableHeight = Math.Max(0, Bounds.Height - 36);
            double side = Math.Min(availableWidth, availableHeight);
            return new Rect(left + (availableWidth - side) / 2, 18 + (availableHeight - side) / 2, side, side);
        }
    }

    public Point? LightnessPoint(CIELAB? lab, bool standard)
    {
        if (lab is null || !double.IsFinite(lab.CIEL)) return null;
        var area = LightnessArea;
        return new Point(standard ? area.Left : area.Right,
            area.Bottom - area.Height * Math.Clamp(lab.CIEL / 100, 0, 1));
    }

    public Point? ChromaHuePoint(CIELAB? lab)
    {
        if (lab is null || !double.IsFinite(lab.CIEC) || !double.IsFinite(lab.CIEH)) return null;
        var area = ChromaHueArea;
        var point = LchDiagramProjection.Project(lab, ChromaMaximum);
        return new Point(area.Center.X + area.Width / 2 * point.X, area.Center.Y - area.Width / 2 * point.Y);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 160 || Bounds.Height < 64) return;
        DrawLightness(context);
        var area = ChromaHueArea;
        if (area.Width <= 0) return;
        EnsureChromaBackground();
        context.DrawImage(_chBackground!, area);
        double radius = area.Width / 2;
        for (int ring = 1; ring <= 4; ring++)
            context.DrawEllipse(null, GridPen, area.Center, radius * ring / 4, radius * ring / 4);
        context.DrawLine(GridPen, new Point(area.Left, area.Center.Y), new Point(area.Right, area.Center.Y));
        context.DrawLine(GridPen, new Point(area.Center.X, area.Top), new Point(area.Center.X, area.Bottom));
        DrawLabel(context, "90°", new Point(area.Center.X, area.Top - 18), centered: true);
        DrawLabel(context, "270°", new Point(area.Center.X, area.Bottom + 2), centered: true);
        DrawLabel(context, "180°", new Point(area.Left - 34, area.Center.Y - 8));
        DrawLabel(context, "0°", new Point(area.Right + 4, area.Center.Y - 8));
        // Label a diagonal radius so neither the hue labels nor the markers are obscured.
        for (int ring = 1; ring <= 4; ring++)
        {
            if (area.Width < 160 && ring != 4) continue;
            double offset = radius * ring / 4 / Math.Sqrt(2);
            string caption = (ring == 4 ? "C* " : "") + (ChromaMaximum * ring / 4).ToString("0", CultureInfo.InvariantCulture);
            DrawLabel(context, caption,
                new Point(area.Center.X + offset, area.Center.Y + offset), centered: true, backdrop: true);
        }
        DrawMarker(context, ChromaHuePoint(StandardLab), standard: true);
        DrawMarker(context, ChromaHuePoint(SampleLab), standard: false);
    }

    private void DrawLightness(DrawingContext context)
    {
        var area = LightnessArea;
        context.DrawRectangle(LightnessBackground, GridPen, area);
        DrawLabel(context, "L*", new Point(area.Center.X, 0), centered: true);
        for (int tick = 0; tick <= 4; tick++)
        {
            double y = area.Bottom - area.Height * tick / 4;
            context.DrawLine(GridPen, new Point(area.Left - 3, y), new Point(area.Right + 3, y));
            DrawLabel(context, (tick * 25).ToString(CultureInfo.InvariantCulture), new Point(2, y - 8));
        }
        DrawMarker(context, LightnessPoint(StandardLab, standard: true), standard: true);
        DrawMarker(context, LightnessPoint(SampleLab, standard: false), standard: false);
    }

    private static IBrush CreateLightnessBackground()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative)
        };
        for (int tick = 0; tick <= 10; tick++)
        {
            double lightness = tick * 10;
            gradient.GradientStops.Add(new GradientStop(NeutralColor(lightness), lightness / 100));
        }
        return gradient;
    }

    private void EnsureChromaBackground()
    {
        double maximum = ChromaMaximum;
        if (_chBackground is not null && _backgroundMaximum == maximum) return;
        _chBackground?.Dispose();
        _chBackground = new WriteableBitmap(new PixelSize(BackgroundPixels, BackgroundPixels),
            new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var buffer = _chBackground.Lock();
        var pixels = LchDiagramBackground.Create(maximum, BackgroundPixels, BackgroundLightness);
        int rowBytes = BackgroundPixels * 4;
        for (int row = 0; row < BackgroundPixels; row++)
            Marshal.Copy(pixels, row * rowBytes, IntPtr.Add(buffer.Address, row * buffer.RowBytes), rowBytes);
        _backgroundMaximum = maximum;
    }

    public static Color NeutralColor(double lightness)
    {
        var color = LchDiagramBackground.NeutralColor(lightness);
        return Color.FromRgb(color.R, color.G, color.B);
    }

    private static void DrawMarker(DrawingContext context, Point? point, bool standard)
    {
        if (point is not { } center) return;
        var brush = standard ? StandardBrush : SampleBrush;
        // White and dark outlines keep both symbols visible against any background.
        if (standard)
        {
            context.DrawEllipse(Brushes.White, new Pen(Brushes.White, 4.5), center, 8, 8);
            context.DrawEllipse(null, new Pen(brush, 2.5), center, 8, 8);
        }
        else
        {
            var diamond = new StreamGeometry();
            using (var path = diamond.Open())
            {
                path.BeginFigure(new Point(center.X, center.Y - 5), true);
                path.LineTo(new Point(center.X + 5, center.Y));
                path.LineTo(new Point(center.X, center.Y + 5));
                path.LineTo(new Point(center.X - 5, center.Y));
                path.EndFigure(true);
            }
            context.DrawGeometry(Brushes.White, new Pen(Brushes.White, 3), diamond);
            context.DrawGeometry(brush, new Pen(brush, 1.5), diamond);
        }
    }

    private void DrawLabel(DrawingContext context, string text, Point position, bool centered = false, bool backdrop = false)
    {
        var label = new FormattedText(LabelText(text), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            LabelTypeface, 11, LabelBrush);
        if (centered) position = new Point(position.X - label.Width / 2, position.Y);
        if (backdrop)
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#D9FFFFFF")), null,
                new Rect(position.X - 2, position.Y, label.Width + 4, label.Height), 2, 2);
        context.DrawText(label, position);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _chBackground?.Dispose();
        _chBackground = null;
    }
}
