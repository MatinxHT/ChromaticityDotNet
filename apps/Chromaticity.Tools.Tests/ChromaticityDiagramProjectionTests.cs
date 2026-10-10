using Chromaticity.Tools.Services;
using ChromaticityDotNet.Model;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace Chromaticity.Tools.Tests;

public class ChromaticityDiagramProjectionTests
{
    private static CIExyY Xy(double x, double y) => new() { CIEx = x, CIEy = y, CIEY = 100 };

    [Fact]
    public void EqualEnergyWhiteUses1976CoordinatesWithoutFourDecimalRounding()
    {
        var projection = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime);
        var uv = projection.Project(1.0 / 3, 1.0 / 3);
        Assert.Equal(4.0 / 19, uv.X, 14);
        Assert.Equal(9.0 / 19, uv.Y, 14);
        var xy = projection.ToXy(uv.X, uv.Y);
        Assert.Equal(1.0 / 3, xy.X, 14);
        Assert.Equal(1.0 / 3, xy.Y, 14);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, 0.5253, 0.3485)]
    [InlineData(StandardObserver.Degree10, 0.5253, 0.3485)]
    [InlineData(StandardObserver.Degree2, 0.4, 0.2)]
    [InlineData(StandardObserver.Degree10, 0.4, 0.2)]
    public void BothDiagramsShareBoundaryIntersectionsAndPreserveTheWhiteSampleLine(StandardObserver observer, double x, double y)
    {
        var result = WavelengthCalculations.Calculate(Xy(x, y), observer, "D65");
        var xyProjection = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.Xy);
        var uvProjection = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime);
        var xyHits = xyProjection.Intersections(result)!.Value;
        var uvHits = uvProjection.Intersections(result)!.Value;
        Assert.Equal(uvProjection.Project(xyHits.Forward.X, xyHits.Forward.Y), uvHits.Forward);
        Assert.Equal(uvProjection.Project(xyHits.Reverse.X, xyHits.Reverse.Y), uvHits.Reverse);
        var white = uvProjection.Project(result.White.X, result.White.Y);
        var sample = uvProjection.Project(x, y);
        foreach (var hit in new[] { uvHits.Forward, uvHits.Reverse })
        {
            Assert.True(double.IsFinite(hit.X) && double.IsFinite(hit.Y));
            Assert.InRange(Math.Abs((sample.X - white.X) * (hit.Y - white.Y) - (sample.Y - white.Y) * (hit.X - white.X)), 0, 1e-14);
        }
        Assert.True((sample.X - white.X) * (uvHits.Forward.X - white.X) + (sample.Y - white.Y) * (uvHits.Forward.Y - white.Y) > 0);
        Assert.True((sample.X - white.X) * (uvHits.Reverse.X - white.X) + (sample.Y - white.Y) * (uvHits.Reverse.Y - white.Y) < 0);
    }

    [Fact]
    public void NeutralColorsHaveNoIntersectionMarkers()
    {
        var result = WavelengthCalculations.Calculate(Xy(0.3127, 0.329), StandardObserver.Degree2, "D65", Xy(0.3127, 0.329));
        Assert.Null(new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime).Intersections(result));
    }

    [Theory]
    [InlineData(StandardObserver.Degree2)]
    [InlineData(StandardObserver.Degree10)]
    public void UvBackgroundUsesTheTransformedGamutAndCorrectColorDirection(StandardObserver observer)
    {
        var projection = new ChromaticityDiagramProjection(ChromaticityDiagramSpace.UvPrime);
        var pixels = ChromaticityDiagramBackground.Create(observer, ChromaticityDiagramSpace.UvPrime);
        int At(double u, double v)
        {
            int column = (int)(u / projection.MaxX * ChromaticityDiagramBackground.Width);
            int row = (int)((1 - v / projection.MaxY) * projection.Height);
            return (row * ChromaticityDiagramBackground.Width + column) * 4;
        }
        var green = projection.Project(0.3, 0.6);
        int offset = At(green.X, green.Y);
        Assert.Equal(255, pixels[offset + 3]);
        Assert.True(pixels[offset + 1] > pixels[offset]);
        var white = projection.Project(0.3127, 0.329);
        Assert.Equal(255, pixels[At(white.X, white.Y) + 3]);
        Assert.Equal(0, pixels[At(0.05, 0.05) + 3]);
        Assert.Equal(0, pixels[At(0.65, 0.65) + 3]);
        Assert.Equal(400 * 400 * 4, pixels.Length);
    }
}
