using Avalonia;
using Avalonia.Controls;
using Xunit;

namespace Chromaticity.Tools.Tests;

public class LayoutTests
{
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
