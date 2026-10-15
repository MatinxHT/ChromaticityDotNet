using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.VisualTree;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using ChromaticityDotNet.Diagrams;
using Xunit;

namespace Chromaticity.Tools.Tests.AvaloniaDiagram;

public class SpectrumComparisonTests
{
    [AvaloniaFact]
    public void ComparisonSharesAxesAndSelectsOnlyActualSamplesOnDifferentGrids()
    {
        var plot = new SpectrumPlot
        {
            Data = new SpectrumDiagramData(new[] { 400.0, 450, 600 }, new[] { 12.123456789, 20, 75 }),
            StandardData = new SpectrumDiagramData(new[] { 380.0, 450, 700 }, new[] { -13.25, 117.5, 30 })
        };
        plot.Measure(new Size(600, 260)); plot.Arrange(new Rect(0, 0, 600, 260));
        Assert.Equal(380, plot.XAxisMinimum); Assert.Equal(700, plot.XAxisMaximum);
        Assert.Equal(-20, plot.YAxisMinimum); Assert.Equal(120, plot.YAxisMaximum);
        Assert.True(plot.SelectAt(new Point(plot.PlotArea.Left + plot.PlotArea.Width * 70 / 320, plot.PlotArea.Center.Y)));
        Assert.Equal(450, plot.SelectedWavelength); Assert.Equal(20, plot.SelectedValue); Assert.Equal(117.5, plot.SelectedStandardValue);
        plot.SelectWavelength(400);
        Assert.Equal(12.123456789, plot.SelectedValue); Assert.Null(plot.SelectedStandardValue);
        Assert.Null(plot.StandardData.ValueAt(400)); Assert.Null(plot.StandardData.ValueAt(451));
        Assert.True(plot.MoveSelection(-1, wrap: true)); Assert.Equal(600, plot.SelectedWavelength);
        Assert.Null(plot.SelectedStandardValue);
        plot.StandardData = SpectrumDiagramData.FromUniform(new[] { 4.0, 5 }, 400, 10);
        Assert.Null(plot.SelectedValue); Assert.Null(plot.SelectedStandardValue); Assert.Equal(-1, plot.SelectedIndex);
        Assert.Equal(0, plot.YAxisMinimum); Assert.Equal(100, plot.YAxisMaximum);
        plot.Clear(); Assert.Null(plot.Data); Assert.Null(plot.StandardData);
    }

    [AvaloniaFact]
    public void StandardOnlySupportsNavigatorPointerAndKeyboardThenResetsForSample()
    {
        var plot = new SpectrumPlot
        {
            Height = 200,
            StandardData = new SpectrumDiagramData(new[] { 400.0, 410, 590, 700 }, new[] { double.NaN, 65, double.NaN, 30 })
        };
        var navigator = new SpectrumNavigator { Plot = plot };
        var panel = new StackPanel(); panel.Children.Add(navigator); panel.Children.Add(plot);
        var window = new Window { Width = 600, Height = 300, Content = panel };
        window.Show();
        try
        {
            var readout = navigator.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SpectrumPointReadout");
            Assert.Equal(410, plot.SelectedWavelength); Assert.Equal(65, plot.SelectedStandardValue);
            Assert.Contains("标样 65.0000 %", readout.Text); Assert.DoesNotContain("样本", readout.Text);
            Point Position(double fraction) => plot.TranslatePoint(new Point(
                plot.PlotArea.Left + plot.PlotArea.Width * fraction, plot.PlotArea.Center.Y), window)!.Value;
            window.MouseDown(Position(0.99), MouseButton.Left); window.MouseUp(Position(0.99), MouseButton.Left);
            Assert.Equal(700, plot.SelectedWavelength);
            window.KeyPress(Key.Home, RawInputModifiers.None, default, null);
            window.KeyRelease(Key.Home, RawInputModifiers.None, default, null);
            Assert.Equal(410, plot.SelectedWavelength);
            plot.Data = SpectrumDiagramData.FromUniform(new[] { 12.0, 75 }, 400, 10);
            Assert.Equal(400, plot.SelectedWavelength); Assert.Contains("样本 12.0000 %", readout.Text);
            plot.SelectWavelength(410);
            Assert.Contains("标样 65.0000 % · 样本 75.0000 %", readout.Text);
            plot.Clear(); Assert.Equal("未选择波长", readout.Text);
            Assert.All(navigator.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEnabled));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BothCurvesAndOverlappingMarkersRenderInTheirConfiguredColors()
    {
        var plot = new SpectrumPlot
        {
            CurveColor = Color.Parse("#1778B5"),
            Data = SpectrumDiagramData.FromUniform(new[] { 50.0, 50, 50 }, 400, 150),
            StandardData = SpectrumDiagramData.FromUniform(new[] { 50.0, 50, 50 }, 400, 150)
        };
        var window = new Window { Width = 600, Height = 260, Background = Brushes.White, Content = plot };
        window.Show();
        try
        {
            plot.SelectWavelength(550);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            using var png = new MemoryStream(); frame.Save(png, PngBitmapEncoderOptions.Default);
            using var bitmap = SkiaSharp.SKBitmap.Decode(png.ToArray());
            SkiaSharp.SKColor At(double x, double y) => bitmap.GetPixel((int)Math.Round(x), (int)Math.Round(y));
            var center = plot.PlotArea.Center;
            // The thick standard line stays visible around the sample line, even when they coincide.
            var sampleLine = At(center.X - 50, center.Y);
            var standardLine = At(center.X - 50, center.Y + 2);
            var sampleRing = At(center.X, center.Y - 4);
            var standardRing = At(center.X, center.Y - 7);
            Assert.True(sampleLine.Blue > sampleLine.Red); Assert.True(standardLine.Red > standardLine.Blue);
            Assert.True(sampleRing.Blue > sampleRing.Red); Assert.True(standardRing.Red > standardRing.Blue);
            plot.Data = null;
            using var standardOnly = window.CaptureRenderedFrame(); Assert.NotNull(standardOnly);
        }
        finally { window.Close(); }
    }
}
