using Avalonia;
using Avalonia.Controls;

namespace Chromaticity.Tools;

/// <summary>Compact settings beside the two diagrams on wide screens; diagrams remain paired
/// below settings on medium screens, and stack in xy/u'v' order on narrow screens.</summary>
public sealed class WavelengthLayout : Panel
{
    private const double SidebarWidth = 360;
    private const double Gap = 18;
    private static bool SideBySide(double width) => double.IsFinite(width) && width >= SidebarWidth + Gap + 720;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2) return default;
        bool beside = SideBySide(availableSize.Width);
        Children[0].Measure(new Size(beside ? SidebarWidth : availableSize.Width, double.PositiveInfinity));
        Children[1].Measure(new Size(beside ? availableSize.Width - SidebarWidth - Gap : availableSize.Width, double.PositiveInfinity));
        double height = beside ? Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height)
            : Children[0].DesiredSize.Height + Gap + Children[1].DesiredSize.Height;
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : Math.Max(Children[0].DesiredSize.Width, Children[1].DesiredSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 2) return finalSize;
        if (SideBySide(finalSize.Width))
        {
            Children[0].Arrange(new Rect(0, 0, SidebarWidth, finalSize.Height));
            Children[1].Arrange(new Rect(SidebarWidth + Gap, 0, finalSize.Width - SidebarWidth - Gap, finalSize.Height));
        }
        else
        {
            Children[0].Arrange(new Rect(0, 0, finalSize.Width, Children[0].DesiredSize.Height));
            Children[1].Arrange(new Rect(0, Children[0].DesiredSize.Height + Gap, finalSize.Width, Children[1].DesiredSize.Height));
        }
        return finalSize;
    }
}
