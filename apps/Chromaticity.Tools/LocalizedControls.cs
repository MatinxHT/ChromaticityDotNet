using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Chromaticity.Tools.Services;

namespace Chromaticity.Tools;

// Keep the source text so switching languages is lossless, including dynamic results.
// Subscribe only while attached so replaced result cards do not remain in memory.
internal sealed class LocalizedTextBlock : TextBlock
{
    private string _source = "";
    private bool _translating;
    protected override Type StyleKeyOverride => typeof(TextBlock);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty || _translating) return;
        _source = Text ?? "";
        Translate();
    }
    private void Translate()
    {
        _translating = true;
        try { Text = UiLanguage.Translate(_source); }
        finally { _translating = false; }
    }
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        UiLanguage.Changed += Translate;
        Translate();
    }
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        UiLanguage.Changed -= Translate;
        base.OnDetachedFromLogicalTree(e);
    }
}

internal sealed class LocalizedTextBox : TextBox
{
    private string _source = "";
    private bool _translating;
    protected override Type StyleKeyOverride => typeof(TextBox);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PlaceholderTextProperty || _translating) return;
        _source = PlaceholderText ?? "";
        Translate();
    }
    private void Translate()
    {
        _translating = true;
        try { PlaceholderText = UiLanguage.Translate(_source); }
        finally { _translating = false; }
    }
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        UiLanguage.Changed += Translate;
        Translate();
    }
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        UiLanguage.Changed -= Translate;
        base.OnDetachedFromLogicalTree(e);
    }
}
