using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools;

/// <summary>Observer-specific xy horseshoe, with an illustrative sRGB background and input markers.</summary>
public sealed class ChromaticityDiagram : Control
{
    private static readonly Lazy<byte[]> Background2 = new(() => ChromaticityDiagramBackground.Create(StandardObserver.Degree2));
    private static readonly Lazy<byte[]> Background10 = new(() => ChromaticityDiagramBackground.Create(StandardObserver.Degree10));
    private StandardObserver _observer = StandardObserver.Degree2;
    private WavelengthCalculation? _calculation;
    private WriteableBitmap? _background;

    public void Clear(StandardObserver observer)
    {
        if (_observer != observer) { _background?.Dispose(); _background = null; }
        _observer = observer; _calculation = null; InvalidateVisual();
    }

    public void SetData(WavelengthCalculation calculation)
    {
        Clear(calculation.Observer); _calculation = calculation; InvalidateVisual();
    }

    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e); UiLanguage.Changed += InvalidateVisual;
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        UiLanguage.Changed -= InvalidateVisual;
        _background?.Dispose(); _background = null;
        base.OnDetachedFromLogicalTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, 760) : 600;
        return new Size(width, Math.Clamp((width - 72) * 1.125 + 84, 280, 600));
    }

    private void EnsureBackground()
    {
        if (_background is not null) return;
        _background = new WriteableBitmap(new PixelSize(ChromaticityDiagramBackground.Width, ChromaticityDiagramBackground.Height),
            new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        var bytes = _observer == StandardObserver.Degree2 ? Background2.Value : Background10.Value;
        using var frame = _background.Lock();
        int rowBytes = ChromaticityDiagramBackground.Width * 4;
        for (int row = 0; row < ChromaticityDiagramBackground.Height; row++)
            Marshal.Copy(bytes, row * rowBytes, IntPtr.Add(frame.Address, row * frame.RowBytes), rowBytes);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 120 || Bounds.Height < 120) return;
        EnsureBackground();
        const double maxX = ChromaticityDiagramBackground.MaxX, maxY = ChromaticityDiagramBackground.MaxY;
        double scale = Math.Min((Bounds.Width - 72) / maxX, (Bounds.Height - 84) / maxY);
        double left = (Bounds.Width - maxX * scale + 40) / 2, top = 30;
        var plot = new Rect(left, top, maxX * scale, maxY * scale);
        Point At(double x, double y) => new(left + x * scale, top + (maxY - y) * scale);
        var font = new Typeface("avares://Chromaticity.Tools/Assets#Chromaticity UI");
        FormattedText Caption(string text, IBrush? brush = null) => new(UiLanguage.Translate(text), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, font, 11, brush ?? Brush.Parse("#555555"));
        void Label(string text, double x, double y) => context.DrawText(Caption(text), new Point(x, y));
        void Tag(string text, Point at)
        {
            var caption = Caption(text, Brushes.Black);
            double x = Math.Clamp(at.X, 1, Math.Max(1, Bounds.Width - caption.Width - 9));
            double y = Math.Clamp(at.Y, 1, Math.Max(1, Bounds.Height - caption.Height - 6));
            context.DrawRectangle(Brush.Parse("#EFFFFFFF"), null, new Rect(x, y, caption.Width + 8, caption.Height + 4), 3, 3);
            context.DrawText(caption, new Point(x + 4, y + 2));
        }
        context.DrawRectangle(Brushes.White, null, plot);
        context.DrawImage(_background!, plot);
        var grid = new Pen(Brush.Parse("#80909090"), 0.6);
        for (int i = 0; i <= 8; i++)
        {
            double x = i / 10.0;
            context.DrawLine(grid, At(x, 0), At(x, maxY));
            Label(x.ToString("0.0", CultureInfo.InvariantCulture), At(x, 0).X - 8, plot.Bottom + 8);
        }
        for (int i = 0; i <= 9; i++)
        {
            double y = i / 10.0;
            context.DrawLine(grid, At(0, y), At(maxX, y));
            Label(y.ToString("0.0", CultureInfo.InvariantCulture), plot.Left - 28, At(0, y).Y - 7);
        }
        context.DrawRectangle(null, new Pen(Brush.Parse("#999999")), plot);
        Label("x", plot.Right - 5, plot.Bottom + 28); Label("y", plot.Left - 28, top - 19);
        Label(_observer == StandardObserver.Degree2 ? "2° · CIE 1931" : "10° · CIE 1964", plot.Left + 4, 4);
        var boundary = CieSpectralData.GetChromaticityBoundary(_observer);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(At(boundary[0].X, boundary[0].Y), true);
            foreach (var point in boundary.Skip(1)) path.LineTo(At(point.X, point.Y));
            path.EndFigure(true);
        }
        context.DrawGeometry(null, new Pen(Brush.Parse("#333333"), 1.7), geometry);
        var locus = CieSpectralData.GetSpectralLocus(_observer);
        foreach (int wavelength in new[] { 400, 450, 500, 550, 600, 650 })
        {
            var point = locus[wavelength - 360];
            var position = At(point.X, point.Y);
            context.DrawEllipse(Brushes.White, new Pen(Brush.Parse("#555555")), position, 2, 2);
            // Violet wavelengths cluster near the bottom of the diagram; offset their
            // captions separately so 400 and 450 nm remain readable on narrow screens.
            var label = wavelength switch
            {
                400 => new Point(position.X + 50, position.Y - 14),
                450 => new Point(position.X + 5, position.Y - 32),
                _ => new Point(position.X + 5, position.Y - 16)
            };
            Tag($"{wavelength} nm", label);
        }
        if (_calculation is null) return;
        var w = At(_calculation.White.X, _calculation.White.Y);
        var p = At(_calculation.Sample.X, _calculation.Sample.Y);
        var delta = p - w;
        double length = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (length > 1e-9)
        {
            var unit = delta / length;
            using (context.PushGeometryClip(geometry))
            {
                context.DrawLine(new Pen(Brushes.White, 3), w - unit * 2000, w + unit * 2000);
                context.DrawLine(new Pen(Brush.Parse("#444444"), 1.3, new DashStyle([5, 4], 0)), w - unit * 2000, w + unit * 2000);
                context.DrawLine(new Pen(Brushes.Black, 2), w, p);
            }
        }
        context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 2), w, 6, 6);
        context.DrawEllipse(Brushes.Black, new Pen(Brushes.White, 2), p, 5, 5);
        Tag("W 白点", new Point(w.X + 10, w.Y + 7));
        Tag("P 样品", new Point(p.X + 10, p.Y - 24));
    }
}
