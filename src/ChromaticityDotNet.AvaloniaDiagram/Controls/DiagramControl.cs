using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ChromaticityDotNet.AvaloniaDiagram.Controls;

/// <summary>Application-provided label presentation shared by the diagram controls.</summary>
public abstract class DiagramControl : Control
{
    public static readonly StyledProperty<Func<string, string>?> TranslateProperty =
        AvaloniaProperty.Register<DiagramControl, Func<string, string>?>(nameof(Translate));
    public static readonly StyledProperty<Typeface> LabelTypefaceProperty =
        AvaloniaProperty.Register<DiagramControl, Typeface>(nameof(LabelTypeface),
            new Typeface("Microsoft YaHei, Noto Sans CJK SC, Segoe UI"));

    static DiagramControl() => AffectsRender<DiagramControl>(TranslateProperty, LabelTypefaceProperty);

    /// <summary>Optional label translation. The control does not subscribe to application language events.</summary>
    public Func<string, string>? Translate { get => GetValue(TranslateProperty); set => SetValue(TranslateProperty, value); }
    public Typeface LabelTypeface { get => GetValue(LabelTypefaceProperty); set => SetValue(LabelTypefaceProperty, value); }
    protected string LabelText(string text) => Translate?.Invoke(text) ?? text;
}
