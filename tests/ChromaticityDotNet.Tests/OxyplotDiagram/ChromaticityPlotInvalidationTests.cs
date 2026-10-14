using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.OxyplotDiagram;
using OxyPlot;
using OxyPlot.Series;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests.OxyplotDiagram;

public class ChromaticityPlotInvalidationTests
{
    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void LiveUpdatesRenderOnlyTheCompleteLatestSample(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space);
        var view = new RecordingPlotView(chart);
        ((IPlotModel)chart.PlotModel).AttachPlotView(view);
        ((IPlotModel)chart.PlotModel).Update(true);
        var projection = new ChromaticityDiagramProjection(space);
        for (int i = 0; i < 20; i++)
        {
            double x = 0.3 + i * 0.005;
            view.Invalidations.Clear();
            view.RenderedSamples.Clear();
            chart.UpdateXyPoints(new[] { new CIExyY { CIEx = x, CIEy = 0.35 } });
            Assert.True(Assert.Single(view.Invalidations));
            var expected = projection.Project(x, 0.35);
            var sample = Assert.Single(Assert.Single(view.RenderedSamples));
            Assert.Equal(expected.X, sample.X);
            Assert.Equal(expected.Y, sample.Y);
            var series = Assert.Single(chart.PlotModel.Series.OfType<ScatterSeries>());
            Assert.Equal(expected.X, series.MaxX);
            Assert.Equal(expected.Y, series.MaxY);
        }
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void ObserverChangesReplaceTheRenderedCurveCacheOnce(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space);
        chart.UpdateXyPoints(new[] { new CIExyY { CIEx = 0.3, CIEy = 0.35 } });
        var view = new RecordingPlotView(chart);
        ((IPlotModel)chart.PlotModel).AttachPlotView(view);
        ((IPlotModel)chart.PlotModel).Update(true);
        chart.SetObserver(StandardObserver.Degree10);
        Assert.True(Assert.Single(view.Invalidations));
        Assert.Empty(Assert.Single(view.RenderedSamples));
        foreach (var curve in chart.PlotModel.Series.OfType<LineSeries>())
        {
            Assert.Equal(curve.Points.Max(point => point.X), curve.MaxX);
            Assert.Equal(curve.Points.Max(point => point.Y), curve.MaxY);
        }
    }

    [Theory]
    [InlineData(ChromaticityDiagramSpace.Xy)]
    [InlineData(ChromaticityDiagramSpace.UvPrime)]
    public void CompoundMeasurementsAndVisibilityChangesDoNotRenderIntermediateStates(ChromaticityDiagramSpace space)
    {
        var chart = new ChromaticityPlot(space);
        var view = new RecordingPlotView(chart);
        ((IPlotModel)chart.PlotModel).AttachPlotView(view);
        var wavelength = WavelengthDiagramData.Create(new CIExyY { CIEx = 0.4, CIEy = 0.2, CIEY = 100 });
        chart.SetData(wavelength);
        Assert.True(Assert.Single(view.Invalidations));
        Assert.NotEmpty(Assert.Single(view.RenderedSamples));
        view.Invalidations.Clear();
        chart.SetLightData(PlanckianDiagramData.Create(new ChromaticityCoordinates(0.3127, 0.329), StandardObserver.Degree2), "D65");
        Assert.True(Assert.Single(view.Invalidations));
        view.Invalidations.Clear();
        chart.ShowPlanckianLocus = false;
        Assert.False(Assert.Single(view.Invalidations));
        view.Invalidations.Clear();
        chart.ToggleLegendItem(0);
        Assert.Single(view.Invalidations);
        view.Invalidations.Clear();
        chart.ClearMeasurements();
        Assert.True(Assert.Single(view.Invalidations));
        Assert.Empty(chart.PlotModel.Series.OfType<ScatterSeries>());
    }

    private sealed class RecordingPlotView(ChromaticityPlot chart) : IPlotView
    {
        public PlotModel ActualModel => chart.PlotModel;
        global::OxyPlot.Model IView.ActualModel => ActualModel;
        public IController ActualController => chart.Controller;
        public OxyRect ClientArea => new(0, 0, 1000, 1000);
        public List<bool> Invalidations { get; } = new();
        public List<ScatterPoint[]> RenderedSamples { get; } = new();
        public void InvalidatePlot(bool updateData)
        {
            Invalidations.Add(updateData);
            ((IPlotModel)ActualModel).Update(updateData);
            RenderedSamples.Add(ActualModel.Series.OfType<ScatterSeries>().SelectMany(series => series.Points).ToArray());
        }
        public void ShowTracker(TrackerHitResult trackerHitResult) { }
        public void HideTracker() { }
        public void SetClipboardText(string text) { }
        public void SetCursorType(CursorType cursorType) { }
        public void HideZoomRectangle() { }
        public void ShowZoomRectangle(OxyRect rectangle) { }
    }
}
