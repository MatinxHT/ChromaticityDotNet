using Avalonia;
using Avalonia.Controls;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class LayoutTests
{
    [Theory]
    [InlineData(1190, true, true)]
    [InlineData(900, false, true)]
    [InlineData(720, false, true)]
    [InlineData(390, false, false)]
    public void WavelengthDiagramsStayPairedToTheRightOrBelowTheSettings(double width, bool sidebar, bool paired)
    {
        var panel = new WavelengthLayout();
        var settings = new Border { Height = 250 };
        var diagrams = new ResponsiveColumns();
        var xy = new Border { Height = 300 };
        var uv = new Border { Height = 300 };
        diagrams.Children.Add(xy); diagrams.Children.Add(uv);
        panel.Children.Add(settings); panel.Children.Add(diagrams);
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
        Assert.Equal(sidebar ? 378 : 0, diagrams.Bounds.X);
        Assert.Equal(sidebar ? 0 : 268, diagrams.Bounds.Y);
        Assert.Equal(paired ? 0 : 318, uv.Bounds.Y);
        Assert.Equal(paired ? xy.Bounds.Right + 18 : 0, uv.Bounds.X);
        Assert.Equal(diagrams.Bounds.Width, uv.Bounds.Right);
    }

    [Theory]
    [InlineData(1100, 3)]
    [InlineData(900, 2)]
    [InlineData(390, 1)]
    public void ThreeGradeDirectionsAdaptToTheAvailableWidth(double width, int columns)
    {
        var panel = new ResponsiveColumns { MaximumColumns = 3 };
        foreach (var i in Enumerable.Range(0, 3)) panel.Children.Add(new Border { Height = 100 });
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
        var columnWidth = (width - 18 * (columns - 1)) / columns;
        for (var i = 0; i < 3; i++)
        {
            Assert.InRange(Math.Abs(columnWidth - panel.Children[i].Bounds.Width), 0, 1);
            Assert.InRange(Math.Abs((i % columns) * (columnWidth + 18) - panel.Children[i].Bounds.X), 0, 1);
            Assert.Equal((i / columns) * 118, panel.Children[i].Bounds.Y);
        }
    }

    [Theory]
    [InlineData(1100, true)]
    [InlineData(720, true)]
    [InlineData(719, false)]
    [InlineData(280, false)]
    public void InputsUseAvailableWidthAndStackOnlyOnNarrowScreens(double width, bool sideBySide)
    {
        var panel = new ResponsiveColumns();
        var standard = new Border { Height = 250 };
        var sample = new Border { Height = 250 };
        panel.Children.Add(standard);
        panel.Children.Add(sample);
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
        Assert.Equal(sideBySide ? 250 : 518, panel.DesiredSize.Height);
        Assert.Equal(sideBySide ? (width - 18) / 2 : width, standard.Bounds.Width);
        Assert.Equal(standard.Bounds.Width, sample.Bounds.Width);
        Assert.Equal(sideBySide ? 0 : 268, sample.Bounds.Y);
        Assert.Equal(width, sample.Bounds.Right);
    }
}
