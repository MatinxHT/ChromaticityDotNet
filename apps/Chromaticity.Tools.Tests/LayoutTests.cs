using Avalonia;
using Avalonia.Controls;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class LayoutTests
{
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
