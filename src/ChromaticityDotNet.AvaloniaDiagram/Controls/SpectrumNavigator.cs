using System.Globalization;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Layout;
using global::Avalonia.LogicalTree;
using global::Avalonia.Media;

namespace ChromaticityDotNet.AvaloniaDiagram.Controls;

/// <summary>Reusable left/right buttons and a measured-value readout for SpectrumPlot.
/// Like Colorimeter.Embedded's page navigation, data selects its first valid point and buttons wrap at either end.</summary>
public sealed class SpectrumNavigator : UserControl
{
    public static readonly StyledProperty<SpectrumPlot?> PlotProperty =
        AvaloniaProperty.Register<SpectrumNavigator, SpectrumPlot?>(nameof(Plot));
    private readonly Button _previous = new()
    {
        Name = "PreviousWavelengthButton", Width = 44, MinHeight = 32, Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly Button _next = new()
    {
        Name = "NextWavelengthButton", Width = 44, MinHeight = 32, Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _readout = new()
    {
        Name = "SpectrumPointReadout", FontSize = 14, FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };
    private SpectrumPlot? _subscribedPlot;
    private bool _attached;

    public SpectrumPlot? Plot { get => GetValue(PlotProperty); set => SetValue(PlotProperty, value); }

    public SpectrumNavigator()
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        SetArrow(_previous, forward: false); SetArrow(_next, forward: true);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 8 };
        Grid.SetColumn(_next, 1); Grid.SetColumn(_readout, 2);
        row.Children.Add(_previous); row.Children.Add(_next); row.Children.Add(_readout);
        Content = row;
        _previous.Click += (_, _) => Plot?.MoveSelection(-1, wrap: true);
        _next.Click += (_, _) => Plot?.MoveSelection(1, wrap: true);
        RefreshLabels();
    }

    private static void SetArrow(Button button, bool forward)
    {
        // Vector icons also render on Browser when the host's bundled font has no triangle glyphs.
        var icon = new global::Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(forward ? "M 2,0 L 10,6 L 2,12 Z" : "M 10,0 L 2,6 L 10,12 Z"),
            Width = 12, Height = 12, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        icon.Bind(Shape.FillProperty, button.GetObservable(Button.ForegroundProperty));
        button.Content = icon;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PlotProperty) return;
        Unsubscribe();
        if (_attached) Subscribe();
        RefreshLabels();
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _attached = true; Subscribe(); RefreshLabels();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _attached = false; Unsubscribe();
        base.OnDetachedFromLogicalTree(e);
    }

    private void Subscribe()
    {
        if (Plot is not { } plot || _subscribedPlot == plot) return;
        _subscribedPlot = plot;
        plot.SelectionChanged += OnSelectionChanged;
        plot.PropertyChanged += OnPlotChanged;
        SelectInitialPoint();
    }

    private void Unsubscribe()
    {
        if (_subscribedPlot is not { } plot) return;
        plot.SelectionChanged -= OnSelectionChanged;
        plot.PropertyChanged -= OnPlotChanged;
        _subscribedPlot = null;
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => RefreshLabels();
    private void OnPlotChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SpectrumPlot.DataProperty || e.Property == SpectrumPlot.StandardDataProperty) SelectInitialPoint();
        if (e.Property == SpectrumPlot.DataProperty || e.Property == SpectrumPlot.StandardDataProperty || e.Property == SpectrumPlot.AxisLabelProperty ||
            e.Property == DiagramControl.TranslateProperty || e.Property == DiagramControl.LabelTypefaceProperty)
            RefreshLabels();
    }

    private void SelectInitialPoint()
    {
        if (Plot is { SelectionData: { } data, SelectedWavelength: null } plot) plot.SelectWavelength(data.Start);
    }

    /// <summary>Call when the host language changes. Font and translation are taken from the associated plot.</summary>
    public void RefreshLabels()
    {
        string Translate(string text) => Plot?.Translate?.Invoke(text) ?? text;
        string previous = Translate("上一个波长"), next = Translate("下一个波长");
        ToolTip.SetTip(_previous, previous); ToolTip.SetTip(_next, next);
        AutomationProperties.SetName(_previous, previous); AutomationProperties.SetName(_next, next);
        if (Plot is { } plot) _readout.FontFamily = plot.LabelTypeface.FontFamily;
        bool selected = Plot?.SelectedWavelength is not null;
        _previous.IsEnabled = selected; _next.IsEnabled = selected;
        _readout.Text = Plot is { SelectedWavelength: { } wavelength, SelectedValue: { } value } selectedPlot
            ? $"{wavelength.ToString("0.##", CultureInfo.InvariantCulture)} nm · {value.ToString("F4", CultureInfo.InvariantCulture)} {Translate(selectedPlot.AxisLabel)}"
            : Translate("未选择波长");
        if (Plot is { StandardData: not null, SelectedWavelength: { } selectedWavelength } comparison)
        {
            var values = new List<string>(2);
            string UnitValue(double source) => $"{source.ToString("F4", CultureInfo.InvariantCulture)} {Translate(comparison.AxisLabel)}";
            if (comparison.SelectedStandardValue is { } standard) values.Add($"{Translate("标样")} {UnitValue(standard)}");
            if (comparison.Data is not null && comparison.SelectedValue is { } sample) values.Add($"{Translate("样本")} {UnitValue(sample)}");
            _readout.Text = $"{selectedWavelength.ToString("0.##", CultureInfo.InvariantCulture)} nm · {string.Join(" · ", values)}";
        }
    }
}
