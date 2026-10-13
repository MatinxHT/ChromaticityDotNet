using System.Globalization;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.Controller;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class PlanckianDiagramTests
{
    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void LightDiagramsUseUnroundedIntegratedCoordinatesAndTheDisplayedCct(StandardObserver observer)
    {
        foreach (var interval in ToolCalculations.IlluminantIntervals)
        {
            var query = ToolCalculations.QueryIlluminant("D65", interval, 400, 700, observer);
            var diagram = Assert.IsType<PlanckianDiagramData>(query.Diagram);
            var white = ChromaticityMatch.GetStandardWhitePoint("D65", observer, 400, 700);
            double sum = white.CIEX + white.CIEY + white.CIEZ;
            Assert.Equal(white.CIEX / sum, diagram.Sample.X);
            Assert.Equal(white.CIEY / sum, diagram.Sample.Y);
            Assert.Equal(observer, diagram.Observer);
            var row = query.WhitePointTable.Rows[0];
            Assert.Equal(ToolCalculations.F(diagram.Cct.Temperature!.Value), row[Array.IndexOf(query.WhitePointTable.Headers, "CCT (CIE 1960 UCS) / K")]);
            Assert.Equal(diagram.Cct.Duv.ToString("F6", CultureInfo.InvariantCulture), row[Array.IndexOf(query.WhitePointTable.Headers, "Duv (1960)")]);
        }
    }

    [Fact]
    public void UndefinedLightWhiteHasNoStaleMarkerAndRangeAndObserverChangesUpdateTheDiagram()
    {
        Assert.Null(ToolCalculations.QueryIlluminant("D65", 10, 300, 350).Diagram);
        var full = ToolCalculations.QueryIlluminant("D65", 10, 380, 780, StandardObserver.Degree2).Diagram!;
        var cropped = ToolCalculations.QueryIlluminant("D65", 10, 400, 600, StandardObserver.Degree2).Diagram!;
        var ten = ToolCalculations.QueryIlluminant("D65", 10, 380, 780, StandardObserver.Degree10).Diagram!;
        Assert.NotEqual(full.Sample, cropped.Sample);
        Assert.NotEqual(full.Cct.NearestPoint.X, cropped.Cct.NearestPoint.X);
        Assert.NotEqual(full.Sample, ten.Sample);
        Assert.Equal(StandardObserver.Degree10, ten.Observer);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, 0.3127, 0.329)]
    [InlineData(StandardObserver.Degree10, 0.3127, 0.329)]
    [InlineData(StandardObserver.Degree2, 0.3, 0.6)]
    [InlineData(StandardObserver.Degree10, 0.4, 0.2)]
    public void BothInputSpacesUseTheSame1960NearestPointAndProjectItTo1976(StandardObserver observer, double x, double y)
    {
        var xy = WavelengthCalculations.Calculate(new CIExyY { CIEx = x, CIEy = y, CIEY = 100 }, observer, "D65");
        var projection = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime);
        var input = projection.Project(x, y);
        var uv = WavelengthCalculations.CalculateUv(new CIEuv { CIEu = input.X, CIEv = input.Y }, observer, "D65");
        Assert.Equal(xy.Planckian.Cct.NearestPoint.U, uv.Planckian.Cct.NearestPoint.U, 10);
        Assert.Equal(xy.Planckian.Cct.NearestPoint.V, uv.Planckian.Cct.NearestPoint.V, 10);
        var nearest = xy.Planckian.Cct.NearestPoint;
        var projected = projection.Project(nearest.X, nearest.Y);
        Assert.Equal(nearest.U, projected.X, 14);
        Assert.Equal(1.5 * nearest.V, projected.Y, 14);
        var sample1960 = ChromaticityConversion.xyToLuv1960(xy.Sample.ToXyY());
        double distance = Math.Sqrt(Math.Pow(sample1960.CIEu - nearest.U, 2) + Math.Pow(sample1960.CIEv - nearest.V, 2));
        Assert.Equal(distance, Math.Abs(xy.Planckian.Cct.Duv), 14);
    }

    [Fact]
    public void SampleConnectionIsIndependentOfTheWavelengthReferenceWhiteAndRetainsInapplicableCct()
    {
        var sample = new CIExyY { CIEx = 0.3, CIEy = 0.6, CIEY = 100 };
        var d65 = WavelengthCalculations.Calculate(sample, StandardObserver.Degree2, "D65");
        var a = WavelengthCalculations.Calculate(sample, StandardObserver.Degree2, "A");
        Assert.Null(d65.Planckian.Cct.Temperature);
        Assert.Equal(d65.Planckian.Cct.NearestPoint.X, a.Planckian.Cct.NearestPoint.X);
        Assert.Equal(d65.Planckian.Cct.Duv, a.Planckian.Cct.Duv);
        Assert.NotEqual(d65.White, a.White);
        sample.CIEx = 0.1;
        Assert.Equal(0.3, d65.Planckian.Sample.X);
    }

    [Theory]
    [InlineData("T CCT 不适用")]
    [InlineData("T 轨迹端点")]
    [InlineData("S 为所选光源的积分白点，实线为普朗克轨迹，标记温度单位为 K。")]
    [InlineData("当前波段的光源色度未定义，仅显示普朗克轨迹。")]
    [InlineData(PlanckianDiagramData.Explanation)]
    [InlineData(PlanckianDiagramData.UnavailableExplanation)]
    public void DiagramLegendsTranslateCompletely(string note) =>
        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", UiLanguage.Translate(note, UiLanguage.English));
}
