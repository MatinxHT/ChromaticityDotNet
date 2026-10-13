using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Diagrams;
using Xunit;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.Tests;

public class DiagramsTests
{
    [Fact]
    public void DiagramSnapshotsKeepFullPrecisionAndDoNotTrackMutableInputs()
    {
        var sample = new CIExyY { CIEx = 0.400012345, CIEy = 0.200067891, CIEY = 80 };
        var white = new CIExyY { CIEx = 0.312712345, CIEy = 0.329067891, CIEY = 100 };
        var data = WavelengthDiagramData.Create(sample, white);
        sample.CIEx = 0; white.CIEy = 0;
        Assert.Equal(0.400012345, data.Sample.X);
        Assert.Equal(0.329067891, data.White.Y);
        Assert.Equal(data.Sample, data.Planckian.Sample);
        var copy = data.Sample.ToXyY(); copy.CIEx = 0;
        Assert.Equal(0.400012345, data.Sample.X);
    }

    [Theory]
    [InlineData(StandardObserver.Degree2, Standardilluminant.D65)]
    [InlineData(StandardObserver.Degree10, Standardilluminant.CWF)]
    public void DiagramWhiteUsesTheIntegratedLibraryWhiteWithoutPresentationRounding(StandardObserver observer, Standardilluminant illuminant)
    {
        var data = WavelengthDiagramData.Create(new CIExyY { CIEx = 0.4, CIEy = 0.2, CIEY = 100 }, observer, illuminant);
        var white = ChromaticityMatch.GetStandardWhitePoint(illuminant, observer);
        double sum = white.CIEX + white.CIEY + white.CIEZ;
        Assert.Equal(white.CIEX / sum, data.White.X);
        Assert.Equal(white.CIEY / sum, data.White.Y);
        Assert.Equal(observer, data.Planckian.Observer);
    }

    [Fact]
    public void IrregularSpectrumSelectionUsesMeasuredSamplesSkipsGapsAndKeepsASnapshot()
    {
        var wavelengths = new[] { 380.0, 391.0, 423.0, 700.0 };
        var values = new[] { 18.1256789, double.NaN, 37.25, 118.0 };
        var data = new SpectrumDiagramData(wavelengths, values);
        wavelengths[0] = 0; values[0] = 0;
        Assert.Equal(380, data.Start);
        Assert.Equal(18.1256789, data.Values[0]);
        Assert.Equal(0, data.FindNearestIndex(391));
        Assert.Equal(0, data.FindNearestIndex(401.5)); // Midpoint tie selects lower wavelength.
        Assert.Equal(2, data.FindNearestIndex(402));
        Assert.Equal(0, data.FindNearestIndex(100));
        Assert.Equal(3, data.FindNearestIndex(900));
        Assert.Equal(-1, new SpectrumDiagramData(new[] { 400.0 }, new[] { double.PositiveInfinity }).FindNearestIndex(400));
    }

    [Fact]
    public void SpectrumRequiresAValidIncreasingGridAndConsistentRange()
    {
        Assert.Throws<ArgumentException>(() => new SpectrumDiagramData(new[] { 400.0, 400 }, new[] { 1.0, 2 }));
        Assert.Throws<ArgumentException>(() => new SpectrumDiagramData(new[] { 400.0 }, new[] { 1.0, 2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpectrumDiagramData.FromUniform(new[] { 1.0 }, 400, 0));
        Assert.Throws<ArgumentException>(() => SpectrumDiagramData.FromSpectrum(new Spectrum
            { StartingWavelength = 400, EndingWavelength = 430, WavelengthInterval = 10, Spectrums = new[] { 1.0, 2, 3 } }));
    }

    [Theory]
    [InlineData(50, 0, 0.5, 0)]
    [InlineData(0, 50, 0, 0.5)]
    [InlineData(-50, 0, -0.5, 0)]
    [InlineData(0, -50, 0, -0.5)]
    public void LchProjectionKeepsThePolarDirectionsAndUnroundedCoordinates(double a, double b, double x, double y)
    {
        Assert.Equal(new ChromaticityDiagramPoint(x, y), LchDiagramProjection.Project(new CIELAB(50, a, b), 100));
        Assert.Equal(0.123456789, LchDiagramProjection.Project(new CIELAB(50, 12.3456789, 0), 100).X, 14);
        Assert.Equal(220, LchDiagramProjection.ChromaMaximum(null, new CIELAB(50, 160, 120)));
    }

    [Fact]
    public void LchPreviewHasATransparentOutsideAndANeutralLightnessAxis()
    {
        var pixels = LchDiagramBackground.Create(100, width: 32);
        Assert.Equal(32 * 32 * 4, pixels.Length);
        Assert.Equal(0, pixels[3]);
        Assert.Equal(255, pixels[(16 * 32 + 16) * 4 + 3]);
        Assert.Equal(0, LchDiagramBackground.NeutralColor(0).R);
        Assert.Equal(255, LchDiagramBackground.NeutralColor(100).R);
        var mid = LchDiagramBackground.NeutralColor(50);
        Assert.Equal(mid.R, mid.G); Assert.Equal(mid.R, mid.B);
    }

    [Fact]
    public void MainAssemblyHasNoUiOrPlottingDependency()
    {
        Assert.DoesNotContain(typeof(ChromaticityDiagramProjection).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name!.StartsWith("Avalonia") || assembly.Name.StartsWith("OxyPlot") || assembly.Name.StartsWith("Presentation"));
    }
}
