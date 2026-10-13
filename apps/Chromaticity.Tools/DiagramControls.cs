using Avalonia.Media;
using Chromaticity.Tools.Services;
using ChromaticityDotNet.AvaloniaDiagram.Controls;

namespace Chromaticity.Tools;

internal static class DiagramControls
{
    internal static SpectrumNavigator Localize(SpectrumNavigator control)
    {
        control.AttachedToLogicalTree += (_, _) => UiLanguage.Changed += control.RefreshLabels;
        control.DetachedFromLogicalTree += (_, _) => UiLanguage.Changed -= control.RefreshLabels;
        return control;
    }

    internal static T Localize<T>(T control) where T : DiagramControl
    {
        control.LabelTypeface = new Typeface("avares://Chromaticity.Tools/Assets#Chromaticity UI");
        control.Translate = UiLanguage.Translate;
        control.AttachedToLogicalTree += (_, _) => UiLanguage.Changed += control.InvalidateVisual;
        control.DetachedFromLogicalTree += (_, _) => UiLanguage.Changed -= control.InvalidateVisual;
        return control;
    }
}
