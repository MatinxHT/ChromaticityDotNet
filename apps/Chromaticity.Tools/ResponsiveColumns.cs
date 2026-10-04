using Avalonia;
using Avalonia.Controls;

namespace Chromaticity.Tools;

/// <summary>Equal-width columns measured against the actual container, stacking on narrow screens.</summary>
public sealed class ResponsiveColumns : Panel
{
    private const double Gap = 18;
    private static int Columns(double width) => double.IsFinite(width) && width >= 720 ? 2 : 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns(availableSize.Width);
        var width = Math.Max(0, (availableSize.Width - Gap * (columns - 1)) / columns);
        double height = 0, desiredWidth = 0;
        for (var i = 0; i < Children.Count; i += columns)
        {
            double rowHeight = 0, rowWidth = 0;
            for (var j = i; j < Math.Min(i + columns, Children.Count); j++)
            {
                Children[j].Measure(new Size(width, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, Children[j].DesiredSize.Height);
                rowWidth += Children[j].DesiredSize.Width + (j == i ? 0 : Gap);
            }
            height += rowHeight + (i == 0 ? 0 : Gap);
            desiredWidth = Math.Max(desiredWidth, rowWidth);
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var width = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        double y = 0;
        for (var i = 0; i < Children.Count; i += columns)
        {
            var rowHeight = Children.Skip(i).Take(columns).Max(child => child.DesiredSize.Height);
            for (var j = i; j < Math.Min(i + columns, Children.Count); j++)
                Children[j].Arrange(new Rect((j - i) * (width + Gap), y, width, rowHeight));
            y += rowHeight + Gap;
        }
        return finalSize;
    }
}
