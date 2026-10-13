using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.VisualTree;
using Chromaticity.Tools;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests.AvaloniaDiagram;

public class DiagramControlTests
{
    [AvaloniaFact]
    public void SpectrumSupportsPointerDraggingKeyboardAndSourceValues()
    {
        var plot = new SpectrumPlot();
        plot.SetData(new[] { 18.123456789, 22.0, 120.5 }, 400, 10);
        var window = new Window { Width = 600, Height = 260, Content = plot };
        window.Show();
        try
        {
            Point Position(double fraction) => plot.TranslatePoint(
                new Point(plot.PlotArea.Left + plot.PlotArea.Width * fraction, plot.PlotArea.Center.Y), window)!.Value;
            window.MouseDown(Position(0.65), MouseButton.Left);
            Assert.Equal(410, plot.SelectedWavelength); Assert.Equal(22, plot.SelectedValue);
            window.MouseMove(Position(0.99));
            window.MouseUp(Position(0.99), MouseButton.Left);
            Assert.Equal(420, plot.SelectedWavelength); Assert.Equal(120.5, plot.SelectedValue);
            window.KeyPress(Key.Left, RawInputModifiers.None, default, null); window.KeyRelease(Key.Left, RawInputModifiers.None, default, null);
            Assert.Equal(410, plot.SelectedWavelength);
            window.KeyPress(Key.Home, RawInputModifiers.None, default, null); window.KeyRelease(Key.Home, RawInputModifiers.None, default, null);
            Assert.Equal(400, plot.SelectedWavelength); Assert.Equal(18.123456789, plot.SelectedValue);
            Assert.False(plot.SelectAt(new Point(5, 5)));
            Assert.Equal(0, plot.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void IrregularGridAndDataChangesDoNotLeaveStaleSelection()
    {
        var plot = new SpectrumPlot { Data = new SpectrumDiagramData(new[] { 380.0, 405, 500, 830 }, new[] { 1.0, double.NaN, 3, 4 }) };
        int notifications = 0;
        plot.SelectionChanged += (_, _) => notifications++;
        Assert.True(plot.SelectWavelength(490));
        Assert.Equal(500, plot.SelectedWavelength); Assert.Equal(3, plot.SelectedValue);
        plot.Data = SpectrumDiagramData.FromUniform(new[] { 2.0, 5 }, 600, 5);
        Assert.Equal(-1, plot.SelectedIndex); Assert.Null(plot.SelectedWavelength); Assert.Null(plot.SelectedValue);
        Assert.Equal(2, notifications);
        plot.SelectWavelength(605); plot.Clear();
        Assert.Null(plot.Data); Assert.Null(plot.SelectedValue);
    }

    [AvaloniaFact]
    public void SpectrumButtonsSelectMeasuredPointsWrapAndFollowDataReplacement()
    {
        var plot = new SpectrumPlot
        {
            Data = new SpectrumDiagramData(new[] { 380.0, 405, 500, 830 }, new[] { double.NaN, 2.123456789, double.NaN, 4 }),
            AxisLabel = "relative power"
        };
        var navigator = new SpectrumNavigator { Plot = plot };
        var panel = new StackPanel(); panel.Children.Add(plot); panel.Children.Add(navigator);
        var window = new Window { Width = 600, Height = 300, Content = panel };
        window.Show();
        try
        {
            Button Button(string name) => navigator.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
            var previous = Button("PreviousWavelengthButton"); var next = Button("NextWavelengthButton");
            var readout = navigator.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SpectrumPointReadout");
            Assert.Equal(405, plot.SelectedWavelength); Assert.Equal(2.123456789, plot.SelectedValue);
            Assert.Contains("405 nm · 2.1235 relative power", readout.Text);
            next.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(830, plot.SelectedWavelength); Assert.Equal(4, plot.SelectedValue);
            next.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(405, plot.SelectedWavelength);
            previous.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(830, plot.SelectedWavelength);
            plot.SetData(new[] { 7.0, 9 }, 600, 5);
            Assert.Equal(600, plot.SelectedWavelength); Assert.Contains("600 nm · 7.0000", readout.Text);
            var replacement = new SpectrumPlot(); replacement.SetData(new[] { 11.0, 13 }, 700, 10);
            navigator.Plot = replacement;
            Assert.Equal(700, replacement.SelectedWavelength);
            next.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(710, replacement.SelectedWavelength); Assert.Equal(600, plot.SelectedWavelength);
            plot.Clear(); // The old plot must no longer drive the readout.
            Assert.Contains("710 nm · 13.0000", readout.Text);
            replacement.Clear();
            Assert.False(previous.IsEnabled); Assert.False(next.IsEnabled);
            Assert.Equal("未选择波长", readout.Text);
            replacement.SetData(new[] { double.NaN, double.NaN }, 400, 10);
            Assert.Null(replacement.SelectedWavelength); Assert.False(next.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LchLayoutSeparatesLightnessAndMaintainsTheChromaHueDirections()
    {
        var plot = new LchPlot { StandardLab = new CIELAB(50, 0, 0), SampleLab = new CIELAB(50, 160, 120) };
        plot.Measure(new Size(600, 300)); plot.Arrange(new Rect(0, 0, 600, 300));
        Assert.Equal(220, plot.ChromaMaximum);
        Assert.Equal(plot.ChromaHueArea.Width, plot.ChromaHueArea.Height);
        Assert.Equal(plot.ChromaHueArea.Center, plot.ChromaHuePoint(plot.StandardLab));
        Assert.True(plot.ChromaHuePoint(new CIELAB(50, 50, 0))!.Value.X > plot.ChromaHueArea.Center.X);
        Assert.True(plot.ChromaHuePoint(new CIELAB(50, 0, 50))!.Value.Y < plot.ChromaHueArea.Center.Y);
        Assert.Equal(plot.LightnessArea.Bottom, plot.LightnessPoint(new CIELAB(0, 0, 0), true)!.Value.Y);
        Assert.Equal(plot.LightnessArea.Top, plot.LightnessPoint(new CIELAB(100, 0, 0), false)!.Value.Y);
    }

    [AvaloniaFact]
    public void IlluminantPageUsesLibraryControlsAndClearsTheSpectrumWhenRangeChanges()
    {
        var page = new MainView(tool: "illuminant");
        var window = new Window { Width = 1200, Height = 1100, Content = page };
        window.Show();
        try
        {
            var plot = page.GetVisualDescendants().OfType<SpectrumPlot>().Single();
            Assert.Equal("IlluminantSpectrumPlot", plot.Name);
            Assert.Equal("ChromaticityDotNet.AvaloniaDiagram", plot.GetType().Assembly.GetName().Name);
            var navigator = page.GetVisualDescendants().OfType<SpectrumNavigator>().Single();
            Assert.Same(plot, navigator.Plot); Assert.True(navigator.IsVisible);
            var previous = navigator.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PreviousWavelengthButton");
            var next = navigator.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "NextWavelengthButton");
            var readout = navigator.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SpectrumPointReadout");
            Assert.NotNull(plot.Data); Assert.Equal(plot.Data.Start, plot.SelectedWavelength);
            previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(plot.Data.End, plot.SelectedWavelength);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(plot.Data.Start, plot.SelectedWavelength);
            plot.SelectWavelength(520);
            Assert.Equal(520, plot.SelectedWavelength);
            var source = CieSpectralData.GetIlluminantSpectrum("D65");
            Assert.Equal(source.Spectrums![(520 - source.StartingWavelength) / source.WavelengthInterval], plot.SelectedValue);
            Assert.Contains("520 nm · 104.7900 相对功率", readout.Text);
            var diagrams = page.GetVisualDescendants().OfType<ChromaticityDiagram>().ToArray();
            Assert.Equal(2, diagrams.Length);
            Assert.Equal(ChromaticityDiagramSpace.Xy, diagrams.Single(diagram => diagram.Name == "IlluminantXyDiagram").Space);
            Assert.Equal(ChromaticityDiagramSpace.UvPrime, diagrams.Single(diagram => diagram.Name == "IlluminantUvDiagram").Space);
            Assert.All(diagrams, diagram =>
            {
                Assert.Equal("ChromaticityDotNet.AvaloniaDiagram", diagram.GetType().Assembly.GetName().Name);
                Assert.Equal(StandardObserver.Degree10, diagram.Observer);
                Assert.NotNull(diagram.LightData); Assert.Equal(StandardObserver.Degree10, diagram.LightData.Observer);
            });
            Assert.Same(diagrams[0].LightData, diagrams[1].LightData);
            string? directory = Environment.GetEnvironmentVariable("CHROMATICITY_DIAGRAM_ARTIFACTS");
            if (directory is not null)
            {
                var scroll = (ScrollViewer)page.Content!;
                scroll.Offset = new Vector(0, plot.TranslatePoint(new Point(0, 0), scroll)!.Value.Y - 15);
                using var frame = window.CaptureRenderedFrame();
                Directory.CreateDirectory(directory); frame!.Save(Path.Combine(directory, "app04-spectrum-buttons-xy-uv.png"), PngBitmapEncoderOptions.Default);
            }
            var previousWhite = diagrams[0].LightData!.Sample;
            page.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == "IlluminantObserver").SelectedIndex = 0;
            Assert.All(diagrams, diagram =>
            {
                Assert.Equal(StandardObserver.Degree2, diagram.Observer);
                Assert.Equal(StandardObserver.Degree2, diagram.LightData!.Observer);
            });
            Assert.NotEqual(previousWhite, diagrams[0].LightData!.Sample);
            Assert.Same(diagrams[0].LightData, diagrams[1].LightData);
            page.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == "IlluminantInterval").SelectedIndex = 1;
            Assert.Equal(plot.Data.Start, plot.SelectedWavelength);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(plot.Data.Start + 5, plot.SelectedWavelength); Assert.Equal(plot.Data.Values[1], plot.SelectedValue);
            page.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "IlluminantEnd").Text = "700";
            Assert.Null(plot.Data); Assert.Null(plot.SelectedValue); Assert.False(plot.IsVisible);
            Assert.False(navigator.IsVisible); Assert.False(previous.IsEnabled); Assert.False(next.IsEnabled);
            Assert.All(diagrams, diagram => Assert.Null(diagram.LightData));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReflectancePageCalculatesAndClearsSelectedDataWhenInputChanges()
    {
        var page = new MainView(tool: "spectrum");
        var window = new Window { Width = 1000, Height = 900, Content = page };
        window.Show();
        try
        {
            Button FindButton(string caption) => page.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Content is TextBlock text && text.Text == caption);
            FindButton("载入 18% 灰示例").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FindButton("计算颜色值").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            using var updated = window.CaptureRenderedFrame();
            var plot = page.GetVisualDescendants().OfType<SpectrumPlot>().Single();
            Assert.NotNull(plot.Data); Assert.True(plot.IsVisible);
            var navigator = page.GetVisualDescendants().OfType<SpectrumNavigator>().Single();
            Assert.True(navigator.IsVisible); Assert.Same(plot, navigator.Plot); Assert.Equal(380, plot.SelectedWavelength);
            navigator.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "NextWavelengthButton")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(390, plot.SelectedWavelength); Assert.Equal(18, plot.SelectedValue);
            plot.SelectWavelength(521);
            Assert.Equal(520, plot.SelectedWavelength); Assert.Equal(18, plot.SelectedValue);
            page.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "SpectrumInput").Text = "18";
            Assert.Null(plot.Data); Assert.Null(plot.SelectedValue); Assert.False(plot.IsVisible);
            Assert.False(navigator.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WavelengthPageUsesBothLibraryCoordinateSpacesWithTheSameSampleAndWhite()
    {
        var page = new MainView(tool: "wavelength");
        var window = new Window { Width = 1200, Height = 900, Content = page };
        window.Show();
        try
        {
            var diagrams = page.GetVisualDescendants().OfType<ChromaticityDiagram>().ToArray();
            Assert.Equal(2, diagrams.Length);
            Assert.All(diagrams, diagram => Assert.Equal("ChromaticityDotNet.AvaloniaDiagram", diagram.GetType().Assembly.GetName().Name));
            Assert.Equal(ChromaticityDiagramSpace.Xy, diagrams.Single(diagram => diagram.Name == "WavelengthDiagram").Space);
            Assert.Equal(ChromaticityDiagramSpace.UvPrime, diagrams.Single(diagram => diagram.Name == "WavelengthUvDiagram").Space);
            var calculate = page.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "WavelengthCalculate");
            calculate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.NotNull(diagrams[0].WavelengthData); Assert.Same(diagrams[0].WavelengthData, diagrams[1].WavelengthData);
            Assert.All(diagrams, diagram => Assert.Equal(StandardObserver.Degree10, diagram.Observer));
            page.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == "WavelengthObserver").SelectedIndex = 0;
            Assert.All(diagrams, diagram => { Assert.Null(diagram.WavelengthData); Assert.Equal(StandardObserver.Degree2, diagram.Observer); });
            calculate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.NotNull(diagrams[0].WavelengthData); Assert.Same(diagrams[0].WavelengthData, diagrams[1].WavelengthData);
            Assert.All(diagrams, diagram => Assert.Equal(StandardObserver.Degree2, diagram.WavelengthData!.Observer));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StandaloneControlsRenderAndCoordinateSpaceCanChangeAtRuntime()
    {
        var data = WavelengthDiagramData.Create(new CIExyY { CIEx = 0.4, CIEy = 0.2, CIEY = 100 });
        var xy = new ChromaticityDiagram { Width = 400, Height = 400, ShowTemperatureLabels = true, WavelengthData = data };
        var uv = new ChromaticityDiagram { Width = 400, Height = 400, Space = ChromaticityDiagramSpace.UvPrime, WavelengthData = data };
        var lch = new LchPlot { Width = 400, Height = 360, StandardLab = new CIELAB(50, 30, 20), SampleLab = new CIELAB(63, -12, 42) };
        var spectrum = new SpectrumPlot { Width = 1200, Height = 210 };
        spectrum.SetData(Enumerable.Range(0, 41).Select(i => 30.0 + 12 * Math.Sin(i / 5.0)).ToArray(), 380, 10);
        spectrum.SelectWavelength(520);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(xy); row.Children.Add(uv); row.Children.Add(lch);
        var panel = new StackPanel(); panel.Children.Add(row); panel.Children.Add(spectrum);
        var window = new Window { Width = 1200, Height = 610, Background = Brushes.White, Content = panel };
        window.Show();
        try
        {
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using (var buffer = frame.Lock())
            {
                var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
                Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
                Assert.Contains(pixels, channel => channel != 0 && channel != 255);
            }
            string? directory = Environment.GetEnvironmentVariable("CHROMATICITY_DIAGRAM_ARTIFACTS");
            if (directory is not null)
            {
                Directory.CreateDirectory(directory); frame.Save(Path.Combine(directory, "avalonia-diagrams.png"), PngBitmapEncoderOptions.Default);
            }
            xy.Space = ChromaticityDiagramSpace.UvPrime;
            using var changed = window.CaptureRenderedFrame();
            Assert.NotNull(changed);
            xy.Clear(StandardObserver.Degree10);
            Assert.Null(xy.WavelengthData); Assert.Equal(StandardObserver.Degree10, xy.Observer);
        }
        finally { window.Close(); }
    }
}
