using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.OxyplotDiagram;
using global::OxyPlot;
using global::OxyPlot.Annotations;
using global::OxyPlot.Axes;
using global::OxyPlot.Series;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.OxyplotDiagram.Tests;

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
        var image = Assert.IsType<ImageAnnotation>(Assert.Single(chart.PlotModel.Annotations));
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
        Assert.IsType<ImageAnnotation>(Assert.Single(uv.PlotModel.Annotations));
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
}
