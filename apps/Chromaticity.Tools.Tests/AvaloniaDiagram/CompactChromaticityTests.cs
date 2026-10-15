using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using ChromaticityDotNet.Diagrams;
using Xunit;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests.AvaloniaDiagram;

public class CompactChromaticityTests
{
    [AvaloniaTheory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void ObserverCaptionCanBeHiddenWithoutChangingMeasurementCoordinates(ChromaticityDiagramSpace space)
    {
        var captions = new List<string>();
        var sample = new ChromaticityDiagramSample("Sample", new ChromaticityCoordinates(0.3127, 0.3290));
        var diagram = new ChromaticityDiagram
        {
            Space = space, Observer = StandardObserver.Degree10,
            ShowLegend = false, ShowPlanckianLocus = false, ShowWavelengthLabels = false,
            Samples = new[] { sample }, Translate = text => { captions.Add(text); return text; }
        };
        var window = new Window { Width = 300, Height = 300, Background = Brushes.White, Content = diagram };
        window.Show();
        try
        {
            using var before = window.CaptureRenderedFrame(); Assert.NotNull(before);
            Assert.Contains("10° · CIE 1964", captions);
            var previousArea = diagram.PlotArea;
            captions.Clear(); diagram.ShowObserverLabel = false;
            using var after = window.CaptureRenderedFrame(); Assert.NotNull(after);
            Assert.DoesNotContain("10° · CIE 1964", captions);
            Assert.DoesNotContain(captions, text => text.Contains('°'));
            Assert.Equal(StandardObserver.Degree10, diagram.Observer); Assert.Same(sample, diagram.Samples[0]);
            Assert.True(diagram.PlotArea.Top < previousArea.Top);
            Assert.True(diagram.PlotArea.Height >= previousArea.Height);
            var point = new ChromaticityDiagramProjection(space).Project(sample.Coordinates.X, sample.Coordinates.Y);
            Assert.Equal("Sample", diagram.HitTestTarget(diagram.PlotPoint(point))!.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(ChromaticityDiagramSpace.Xy, 150)]
    [InlineData(ChromaticityDiagramSpace.UvPrime, 150)]
    [InlineData(ChromaticityDiagramSpace.Xy, 240)]
    [InlineData(ChromaticityDiagramSpace.UvPrime, 240)]
    public void CompactPointsOnlyDiagramsKeepBothCoincidentMeasurementsVisible(ChromaticityDiagramSpace space, int size)
    {
        var coordinates = new ChromaticityCoordinates(0.3127, 0.3290);
        var diagram = new ChromaticityDiagram
        {
            Space = space, ShowLegend = false, ShowPlanckianLocus = false,
            ShowTemperatureLabels = false, ShowWavelengthLabels = false,
            Samples = new[]
            {
                new ChromaticityDiagramSample("Standard", coordinates, 0xFFE07B39, 8),
                new ChromaticityDiagramSample("Sample", coordinates, 0xFF1778B5, 4)
            }
        };
        var window = new Window { Width = size, Height = size, Background = Brushes.White, Content = diagram };
        window.Show();
        try
        {
            Assert.Equal(0, diagram.LegendArea.Width);
            var point = diagram.PlotPoint(new ChromaticityDiagramProjection(space).Project(coordinates.X, coordinates.Y));
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            using var png = new MemoryStream(); frame.Save(png, PngBitmapEncoderOptions.Default);
            using var bitmap = SkiaSharp.SKBitmap.Decode(png.ToArray());
            var sample = bitmap.GetPixel((int)Math.Round(point.X), (int)Math.Round(point.Y));
            var standard = bitmap.GetPixel((int)Math.Round(point.X), (int)Math.Round(point.Y - 6));
            Assert.True(sample.Blue > sample.Red); Assert.True(standard.Red > standard.Blue);
            Assert.NotNull(diagram.HitTestTarget(point));
        }
        finally { window.Close(); }
    }

    [Fact]
    public void MarkerRadiusDefaultsRemainCompatibleAndInvalidSizesAreRejected()
    {
        var xy = new ChromaticityCoordinates(0.3, 0.3);
        Assert.Equal(5, new ChromaticityDiagramSample("Sample", xy).MarkerRadius);
        foreach (double radius in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChromaticityDiagramSample("Sample", xy, 0xFF1778B5, radius));
    }
}
