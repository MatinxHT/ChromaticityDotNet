using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.OxyplotDiagram;
using global::OxyPlot;
using global::OxyPlot.Annotations;
using global::OxyPlot.Axes;
using global::OxyPlot.Legends;
using global::OxyPlot.Series;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests.OxyplotDiagram;

public class ChromaticityPlotTests
{
    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy, StandardObserver.Degree2)]
    [InlineData(ChromaticityDiagramSpace.Xy, StandardObserver.Degree10)]
    [InlineData(ChromaticityDiagramSpace.UvPrime, StandardObserver.Degree2)]
    [InlineData(ChromaticityDiagramSpace.UvPrime, StandardObserver.Degree10)]
    public void CurvesAndImageUseTheSameObserverAndCoordinateSpace(ChromaticityDiagramSpace space, StandardObserver observer)
    {
        var chart = new ChromaticityPlot(space, observer);
        var projection = new ChromaticityDiagramProjection(space);
        Assert.Equal(PlotType.Cartesian, chart.PlotModel.PlotType);
        Assert.All(chart.PlotModel.Axes, axis => { Assert.False(axis.IsZoomEnabled); Assert.False(axis.IsPanEnabled); });
        var expected = projection.GetPlanckianLocus(observer)[0];
        var locus = Assert.IsType<LineSeries>(chart.PlotModel.Series[0]);
        Assert.Equal(expected.X, locus.Points[0].X); Assert.Equal(expected.Y, locus.Points[0].Y);
        var boundary = Assert.IsType<LineSeries>(chart.PlotModel.Series[1]);
        Assert.Equal(boundary.Points[0], boundary.Points[^1]);
        var image = Assert.Single(chart.PlotModel.Annotations.OfType<ImageAnnotation>());
        Assert.Equal(6, chart.PlotModel.Annotations.OfType<TextAnnotation>().Count());
        Assert.Equal(400, image.ImageSource!.Width); Assert.Equal(400, image.ImageSource.Height);
        var bytes = ChromaticityDiagramBackground.Create(observer, space);
        var pixels = image.ImageSource.GetPixels();
        Assert.Equal(OxyColor.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2]), pixels[0, 0]);
        int i = (150 * 400 + 160) * 4;
        Assert.Equal(OxyColor.FromArgb(bytes[i + 3], bytes[i], bytes[i + 1], bytes[i + 2]), pixels[160, 150]);
    }

    [Fact]
    public void UpdatingPointsReplacesDynamicDataAndChangingObserverClearsIt()
    {
        var chart = new Cie1931Plot();
        var model = chart.PlotModel;
        chart.UpdatePoint(new CIExyY { CIEx = 0.3, CIEy = 0.4 });
        chart.UpdatePoints(new[] { new CIExyY { CIEx = 0.4, CIEy = 0.2 }, new CIExyY { CIEx = 0.2, CIEy = 0.3 } });
        Assert.Equal(4, model.Series.Count);
        Assert.Equal(0.4, Assert.IsType<ScatterSeries>(model.Series[2]).Points[0].X);
        chart.SetObserver(StandardObserver.Degree10);
        Assert.Same(model, chart.PlotModel); Assert.Equal(2, model.Series.Count);
        Assert.NotEmpty(Assert.IsType<LineSeries>(model.Series[0]).Points);
        Assert.Contains("10°", model.Series[1].Title);
    }

    [Fact]
    public void UvInputsAre1976ChromaticityAndInvalidUpdateDoesNotDestroyPreviousMeasurements()
    {
        var chart = new Cie1976Plot();
        chart.UpdatePoint(new CIEuv { CIEu = 0.20123456789, CIEv = 0.47123456789 });
        var scatter = Assert.IsType<ScatterSeries>(chart.PlotModel.Series[2]);
        Assert.Equal(0.20123456789, scatter.Points[0].X);
        Assert.Throws<ArgumentException>(() => chart.UpdatePoint(new CIEuv { CIEu = double.NaN, CIEv = 0.47 }));
        Assert.Same(scatter, chart.PlotModel.Series[2]);
        Assert.Contains("u′", chart.PlotModel.Axes.Single(axis => axis.Position == AxisPosition.Bottom).Title);
    }

    [Fact]
    public void BothPlotsShareTheSameWavelengthAndCctDataAndClearOldAnnotations()
    {
        var xy = new Cie1931Plot(); var uv = new Cie1976Plot();
        var data = WavelengthDiagramData.Create(new CIExyY { CIEx = 0.4, CIEy = 0.2, CIEY = 100 });
        xy.SetData(data); uv.SetData(data);
        var sample = Assert.IsType<ScatterSeries>(uv.PlotModel.Series.Single(series => series.Title == "Sample")).Points[0];
        var projected = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime).Project(data.Sample.X, data.Sample.Y);
        Assert.Equal(projected.X, sample.X); Assert.Equal(projected.Y, sample.Y);
        Assert.Contains(uv.PlotModel.Annotations.OfType<TextAnnotation>(), label => label.Text.Contains("Duv"));
        uv.UpdatePoints(Array.Empty<CIEuv>());
        Assert.Equal(2, uv.PlotModel.Series.Count);
        Assert.Single(uv.PlotModel.Annotations.OfType<ImageAnnotation>());
        Assert.All(uv.PlotModel.Annotations.OfType<TextAnnotation>(), label => Assert.EndsWith(" nm", label.Text));
        Assert.NotSame(xy.PlotModel, new Cie1931Plot().PlotModel);
    }

    [Fact]
    public void BothModelsCanExportWithOnlyOxyPlotCore()
    {
        var data = WavelengthDiagramData.Create(new CIExyY { CIEx = 0.4, CIEy = 0.2, CIEY = 100 });
        foreach (var chart in new ChromaticityPlot[] { new Cie1931Plot(), new Cie1976Plot() })
        {
            chart.SetData(data);
            string svg = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
            Assert.Contains("<svg", svg);
            Assert.Contains("image/png", svg);
            string? directory = Environment.GetEnvironmentVariable("CHROMATICITY_DIAGRAM_ARTIFACTS");
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"oxyplot-{chart.Space}.svg"), svg);
            }
        }
    }

    [Fact]
    public void OxyPlotAssemblyDoesNotReferenceWpfOrAvalonia()
    {
        Assert.DoesNotContain(typeof(Cie1931Plot).Assembly.GetReferencedAssemblies(), assembly =>
            assembly.Name!.Contains("Wpf") || assembly.Name.StartsWith("Avalonia") || assembly.Name.StartsWith("Presentation") || assembly.Name.Contains("CommunityToolkit"));
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void NamedSamplesKeepTheirNamesColorsAndPrecisionInBothSpaces(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space) { ShowWavelengthLabels = false, ShowPlanckianLocus = false };
        var samples = new[]
        {
            new ChromaticityDiagramSample("Reference A", new ChromaticityCoordinates(0.3127123456789, 0.329), 0xFF2563EB),
            new ChromaticityDiagramSample("Measurement B", new ChromaticityCoordinates(0.4, 0.2), 0xFFE11D48)
        };
        chart.SetSamples(samples);
        var markers = chart.PlotModel.Series.OfType<ScatterSeries>().ToArray();
        Assert.Equal(2, markers.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            var expected = new ChromaticityDiagramProjection(space).Project(samples[i].Coordinates.X, samples[i].Coordinates.Y);
            Assert.Equal(samples[i].Name, markers[i].Title);
            Assert.Equal(expected.X, markers[i].Points[0].X); Assert.Equal(expected.Y, markers[i].Points[0].Y);
            Assert.True(markers[i].RenderInLegend);
        }
        Assert.Equal(OxyColor.FromRgb(37, 99, 235), markers[0].MarkerFill);
        Assert.Equal(OxyColor.FromRgb(225, 29, 72), markers[1].MarkerFill);
        string svg = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
        Assert.Contains("Reference A", svg); Assert.Contains("Measurement B", svg);
        Assert.DoesNotContain("Planckian locus", svg); Assert.DoesNotContain("CIE 1931 2°", svg);
        chart.ShowLegend = false;
        Assert.DoesNotContain("Reference A", SvgExporter.ExportToString(chart.PlotModel, 600, 600, true));
        Assert.Throws<ArgumentException>(() => chart.SetSamples(new[] { samples[0], null! }));
        Assert.Same(markers[0], chart.PlotModel.Series[2]);
        chart.SetSamples(samples.Take(1));
        Assert.Single(chart.PlotModel.Series.OfType<ScatterSeries>());
        chart.SetObserver(StandardObserver.Degree10);
        Assert.Empty(chart.PlotModel.Series.OfType<ScatterSeries>());
        Assert.False(chart.ShowWavelengthLabels); Assert.False(chart.ShowPlanckianLocus); Assert.False(chart.ShowLegend);
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void ReferenceOptionsAreIndependentAndDoNotClearMeasurements(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space);
        chart.SetLightData(PlanckianDiagramData.Create(new ChromaticityCoordinates(0.3127, 0.329), StandardObserver.Degree2), "D65");
        var sample = chart.PlotModel.Series.Single(series => series.Title == "D65");
        var measuredLabel = chart.PlotModel.Annotations.OfType<TextAnnotation>().Single(label => label.Text.Contains("Duv"));
        Assert.DoesNotContain(" K", measuredLabel.Text);
        chart.ShowTemperatureLabels = true;
        Assert.Contains(" K", measuredLabel.Text);
        chart.ShowTemperatureLabels = false;
        Assert.DoesNotContain(" K", measuredLabel.Text);
        chart.ShowWavelengthLabels = false;
        Assert.DoesNotContain(chart.PlotModel.Annotations.OfType<TextAnnotation>(), label => label.Text.EndsWith(" nm"));
        chart.ShowPlanckianLocus = false;
        Assert.False(chart.PlotModel.Series[0].IsVisible);
        Assert.False(chart.PlotModel.Series.Single(series => series.Title == "Nearest Planckian point").IsVisible);
        Assert.DoesNotContain(chart.PlotModel.Annotations.OfType<TextAnnotation>(), label => label.Text.Contains("Duv"));
        chart.ShowTemperatureLabels = true;
        Assert.Equal(6, chart.PlotModel.Annotations.OfType<TextAnnotation>().Count(label => label.Text.EndsWith(" K")));
        Assert.Same(sample, chart.PlotModel.Series.Single(series => series.Title == "D65")); Assert.True(sample.IsVisible);
        chart.ShowPlanckianLocus = true;
        Assert.Contains(chart.PlotModel.Annotations.OfType<TextAnnotation>(), label => label.Text.Contains("Duv"));
        chart.SetObserver(StandardObserver.Degree10);
        var point = ChromaticityDotNet.Controller.ChromaticityConversion.GetPlanckianChromaticity(2000, StandardObserver.Degree10);
        var expected = new ChromaticityDiagramProjection(space).Project(point.X, point.Y);
        var label = chart.PlotModel.Annotations.OfType<TextAnnotation>().Single(annotation => annotation.Text == "2000 K");
        Assert.Equal(expected.X, label.TextPosition.X); Assert.Equal(expected.Y, label.TextPosition.Y);
        Assert.DoesNotContain(chart.PlotModel.Annotations.OfType<TextAnnotation>(), annotation => annotation.Text.Contains("Duv"));
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void LegendIsInsideTheRequestedCornerAndAxesCanExpandWithoutStretchingTheBackground(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space);
        chart.SetSamples(new[] { new ChromaticityDiagramSample("Sample A", new ChromaticityCoordinates(0.4, 0.2)) });
        _ = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
        var legend = Assert.Single(chart.PlotModel.Legends);
        var viewport = new ChromaticityDiagramViewport(space);
        Assert.Equal(LegendPlacement.Inside, legend.LegendPlacement);
        Assert.Equal(viewport.LegendAtTop ? LegendPosition.TopRight : LegendPosition.BottomRight, legend.LegendPosition);
        var plot = chart.PlotModel.PlotArea;
        Assert.True(legend.LegendArea.Left >= plot.Left); Assert.True(legend.LegendArea.Right <= plot.Right);
        Assert.True(legend.LegendArea.Top >= plot.Top); Assert.True(legend.LegendArea.Bottom <= plot.Bottom);
        Assert.True(legend.LegendArea.Center.X > plot.Center.X);
        Assert.True(viewport.LegendAtTop ? legend.LegendArea.Center.Y < plot.Center.Y : legend.LegendArea.Center.Y > plot.Center.Y);
        Assert.Equal(viewport.MaximumX, chart.PlotModel.Axes[0].Maximum);
        Assert.Equal(viewport.MaximumY, chart.PlotModel.Axes[1].Maximum);
        var background = Assert.Single(chart.PlotModel.Annotations.OfType<ImageAnnotation>());
        var projection = new ChromaticityDiagramProjection(space);
        chart.AxisMaximumX = 1.2; chart.AxisMaximumY = 1.1;
        _ = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
        Assert.Equal(1.2, chart.PlotModel.Axes[0].Maximum); Assert.Equal(1.1, chart.PlotModel.Axes[1].Maximum);
        // Cartesian plots may extend the displayed range to keep equal x/y scaling.
        Assert.True(chart.PlotModel.Axes[0].ActualMaximum >= 1.2 - 1e-12);
        Assert.True(chart.PlotModel.Axes[1].ActualMaximum >= 1.1 - 1e-12);
        Assert.Equal(projection.MaxX, background.Width.Value); Assert.Equal(projection.MaxY, background.Height.Value);
        Assert.Single(chart.PlotModel.Series.OfType<ScatterSeries>());
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.AxisMaximumX = double.NaN);
        Assert.Equal(1.2, chart.AxisMaximumX);
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void LeftMouseControllerTracksNamedSamplesAndCurveMetadata(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space);
        chart.SetSamples(new[] { new ChromaticityDiagramSample("Sample {A}", new ChromaticityCoordinates(0.40123456789, 0.2)) });
        _ = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
        var view = new TestPlotView(chart);
        var marker = chart.PlotModel.Series.OfType<ScatterSeries>().Single();
        var position = marker.Transform(marker.Points[0].X, marker.Points[0].Y);
        Assert.True(chart.Controller.HandleMouseDown(view, new OxyMouseDownEventArgs
        {
            Position = position, ChangedButton = OxyMouseButton.Left, ClickCount = 1
        }));
        var hit = Assert.IsType<TrackerHitResult>(view.LastTracker);
        var target = Assert.IsType<ChromaticityDiagramTarget>(hit.Item);
        Assert.Equal("Sample {A}", target.Name); Assert.Equal(target.TrackerText, hit.Text);
        Assert.Equal(marker.Points[0].X, hit.DataPoint.X); Assert.Equal(marker.Points[0].Y, hit.DataPoint.Y);
        Assert.Contains(space == ChromaticityDiagramSpace.Xy ? "CIE x:" : "CIE u′:", hit.Text);
        chart.Controller.HandleMouseUp(view, new OxyMouseEventArgs { Position = position });
        var planckian = Assert.IsType<LineSeries>(chart.PlotModel.Series[0]);
        hit = planckian.GetNearestPoint(planckian.Transform(planckian.Points[0].X, planckian.Points[0].Y), false);
        target = Assert.IsType<ChromaticityDiagramTarget>(hit!.Item);
        Assert.NotNull(target.Temperature); Assert.Contains(" K", hit.Text);
        var boundary = Assert.IsType<LineSeries>(chart.PlotModel.Series[1]);
        hit = boundary.GetNearestPoint(boundary.Transform(boundary.Points[0].X, boundary.Points[0].Y), false);
        target = Assert.IsType<ChromaticityDiagramTarget>(hit!.Item);
        Assert.NotNull(target.Wavelength); Assert.Contains(" nm", hit.Text);
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void LegendClicksTemporarilyHideAndRestoreCurvesAndSamples(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space) { ShowTemperatureLabels = true };
        chart.SetLightData(PlanckianDiagramData.Create(new ChromaticityCoordinates(0.3127, 0.329), StandardObserver.Degree2), "D65");
        var view = new TestPlotView(chart);
        var curve = chart.PlotModel.Series[0];
        void ClickLegend(global::OxyPlot.Series.Series series)
        {
            _ = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
            var legend = chart.PlotModel.Legends.Single();
            ScreenPoint? position = null;
            for (double y = legend.LegendArea.Top + 1; y < legend.LegendArea.Bottom && position is null; y += 2)
                for (double x = legend.LegendArea.Left + 1; x < legend.LegendArea.Right; x += 4)
                {
                    var point = new ScreenPoint(x, y);
                    if (!ReferenceEquals(legend.HitTest(new HitTestArguments(point, 0))?.Item, series)) continue;
                    position = point; break;
                }
            Assert.NotNull(position);
            Assert.True(chart.Controller.HandleMouseDown(view, new OxyMouseDownEventArgs
            {
                Position = position.Value, ChangedButton = OxyMouseButton.Left, ClickCount = 1
            }));
            chart.Controller.HandleMouseUp(view, new OxyMouseEventArgs { Position = position.Value });
        }
        ClickLegend(curve);
        Assert.False(curve.IsVisible); Assert.True(chart.ShowPlanckianLocus); Assert.True(chart.ShowTemperatureLabels);
        Assert.False(chart.PlotModel.Series.Single(series => series.Title == "Nearest Planckian point").IsVisible);
        Assert.DoesNotContain(chart.PlotModel.Annotations.OfType<TextAnnotation>(), label => label.Text.Contains("Duv") || label.Text.EndsWith(" K"));
        Assert.True(chart.PlotModel.Series.Single(series => series.Title == "D65").IsVisible);
        ClickLegend(curve);
        Assert.True(curve.IsVisible);
        Assert.Contains(chart.PlotModel.Annotations.OfType<TextAnnotation>(), label => label.Text.Contains("Duv"));
        chart.SetSamples(new[] { new ChromaticityDiagramSample("Sample A", new ChromaticityCoordinates(0.4, 0.2)) });
        var sample = chart.PlotModel.Series.OfType<ScatterSeries>().Single();
        ClickLegend(sample);
        Assert.False(sample.IsVisible); Assert.Single(sample.Points);
        string svg = SvgExporter.ExportToString(chart.PlotModel, 600, 600, true);
        Assert.Contains("Sample A", svg); // The disabled item remains available to restore.
        var samplePosition = sample.Transform(sample.Points[0].X, sample.Points[0].Y);
        chart.Controller.HandleMouseDown(view, new OxyMouseDownEventArgs
        {
            Position = samplePosition, ChangedButton = OxyMouseButton.Left, ClickCount = 1
        });
        Assert.NotEqual("Sample A", (view.LastTracker?.Item as ChromaticityDiagramTarget)?.Name);
        chart.Controller.HandleMouseUp(view, new OxyMouseEventArgs { Position = samplePosition });
        ClickLegend(sample);
        Assert.True(sample.IsVisible);
        Assert.Equal(new ChromaticityDiagramProjection(space).Project(0.4, 0.2).X, sample.Points[0].X);
        ClickLegend(curve); ClickLegend(sample);
        chart.SetSamples(new[] { new ChromaticityDiagramSample("Sample B", new ChromaticityCoordinates(0.3, 0.3)) });
        Assert.False(curve.IsVisible); Assert.True(chart.ShowPlanckianLocus);
        Assert.True(chart.PlotModel.Series.OfType<ScatterSeries>().Single().IsVisible);
        chart.ShowPlanckianLocus = true;
        Assert.True(curve.IsVisible);
    }

    private sealed class TestPlotView(ChromaticityPlot chart) : IPlotView
    {
        public PlotModel ActualModel => chart.PlotModel;
        global::OxyPlot.Model IView.ActualModel => ActualModel;
        public IController ActualController => chart.Controller;
        public OxyRect ClientArea => new(0, 0, 600, 600);
        public TrackerHitResult? LastTracker { get; private set; }
        public void ShowTracker(TrackerHitResult trackerHitResult) => LastTracker = trackerHitResult;
        public void HideTracker() => LastTracker = null;
        public void InvalidatePlot(bool updateData) { }
        public void SetClipboardText(string text) { }
        public void SetCursorType(CursorType cursorType) { }
        public void HideZoomRectangle() { }
        public void ShowZoomRectangle(OxyRect rectangle) { }
    }
}
