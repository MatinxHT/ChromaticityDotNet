using System.Globalization;
using global::Avalonia;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Media;
using ChromaticityDotNet.Diagrams;

namespace ChromaticityDotNet.AvaloniaDiagram.Controls;

/// <summary>Spectral curve with the nearest measured wavelength selected by click, touch, drag or keyboard.
/// Adapted from Chromaticity.Tools and Colorimeter.Embedded's reflectance selection.</summary>
public sealed class SpectrumPlot : DiagramControl
{
    public static readonly StyledProperty<SpectrumDiagramData?> DataProperty =
        AvaloniaProperty.Register<SpectrumPlot, SpectrumDiagramData?>(nameof(Data));
    public static readonly StyledProperty<SpectrumDiagramData?> StandardDataProperty =
        AvaloniaProperty.Register<SpectrumPlot, SpectrumDiagramData?>(nameof(StandardData));
    public static readonly StyledProperty<Color> CurveColorProperty =
        AvaloniaProperty.Register<SpectrumPlot, Color>(nameof(CurveColor), Color.Parse("#333333"));
    public static readonly StyledProperty<Color> StandardCurveColorProperty =
        AvaloniaProperty.Register<SpectrumPlot, Color>(nameof(StandardCurveColor), Color.Parse("#E07B39"));
    public static readonly StyledProperty<string> AxisLabelProperty = AvaloniaProperty.Register<SpectrumPlot, string>(nameof(AxisLabel), "%");
    public static readonly StyledProperty<double> MinimumMaximumProperty =
        AvaloniaProperty.Register<SpectrumPlot, double>(nameof(MinimumMaximum), 100, validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<SpectrumPlot, int>(
        nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay, validate: value => value >= -1);
    public static readonly DirectProperty<SpectrumPlot, double?> SelectedWavelengthProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double?>(nameof(SelectedWavelength), plot => plot.SelectedWavelength);
    public static readonly DirectProperty<SpectrumPlot, double?> SelectedValueProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double?>(nameof(SelectedValue), plot => plot.SelectedValue);
    public static readonly DirectProperty<SpectrumPlot, double?> SelectedStandardValueProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double?>(nameof(SelectedStandardValue), plot => plot.SelectedStandardValue);
    public static readonly DirectProperty<SpectrumPlot, double> YAxisMinimumProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double>(nameof(YAxisMinimum), plot => plot.YAxisMinimum);
    public static readonly DirectProperty<SpectrumPlot, double> YAxisMaximumProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double>(nameof(YAxisMaximum), plot => plot.YAxisMaximum);
    private double? _selectedWavelength, _selectedValue, _selectedStandardValue;
    private double _yAxisMinimum, _yAxisMaximum = 100;
    private static readonly IBrush[] VisibleBackground = Enumerable.Range(WavelengthColors.Start, WavelengthColors.End - WavelengthColors.Start + 1)
        .Select(wavelength =>
        {
            var color = WavelengthColors.At(wavelength);
            return (IBrush)new SolidColorBrush(Color.FromArgb(32, color.R, color.G, color.B));
        }).ToArray();

    static SpectrumPlot() => AffectsRender<SpectrumPlot>(DataProperty, StandardDataProperty, CurveColorProperty,
        StandardCurveColorProperty, AxisLabelProperty, MinimumMaximumProperty, SelectedIndexProperty);
    public SpectrumPlot() { Focusable = true; ClipToBounds = true; }
    public SpectrumDiagramData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>Optional comparison curve. Both curves share axes; selection uses Data, or this curve when Data is null.</summary>
    public SpectrumDiagramData? StandardData { get => GetValue(StandardDataProperty); set => SetValue(StandardDataProperty, value); }
    public Color CurveColor { get => GetValue(CurveColorProperty); set => SetValue(CurveColorProperty, value); }
    public Color StandardCurveColor { get => GetValue(StandardCurveColorProperty); set => SetValue(StandardCurveColorProperty, value); }
    public SpectrumDiagramData? SelectionData => Data ?? StandardData;
    public string AxisLabel { get => GetValue(AxisLabelProperty); set => SetValue(AxisLabelProperty, value); }
    public double MinimumMaximum { get => GetValue(MinimumMaximumProperty); set => SetValue(MinimumMaximumProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double? SelectedWavelength => _selectedWavelength;
    public double? SelectedValue => _selectedValue;
    /// <summary>The standard's actual value at SelectedWavelength, or null for a missing/different-grid sample.</summary>
    public double? SelectedStandardValue => _selectedStandardValue;
    public double XAxisMinimum => Math.Min(Data?.Start ?? StandardData?.Start ?? 400, StandardData?.Start ?? Data?.Start ?? 400);
    public double XAxisMaximum => Math.Max(Data?.End ?? StandardData?.End ?? 700, StandardData?.End ?? Data?.End ?? 700);
    public double YAxisMinimum => _yAxisMinimum;
    public double YAxisMaximum => _yAxisMaximum;
    public event EventHandler? SelectionChanged;
    public Rect PlotArea => new(55, 8, Math.Max(0, Bounds.Width - 75), Math.Max(0, Bounds.Height - 43));

    public void SetData(double[] values, int start, int step) => SetData(SpectrumDiagramData.FromUniform(values, start, step));
    public void SetData(SpectrumDiagramData data) => SetCurrentValue(DataProperty, data ?? throw new ArgumentNullException(nameof(data)));
    public void Clear()
    {
        SetCurrentValue(DataProperty, null);
        SetCurrentValue(StandardDataProperty, null);
    }

    /// <summary>Select the nearest finite sample; values remain in the source units.</summary>
    public bool SelectWavelength(double wavelength)
    {
        int index = SelectionData?.FindNearestIndex(wavelength) ?? -1;
        SetCurrentValue(SelectedIndexProperty, index);
        return index >= 0;
    }

    /// <summary>Move one measured point to the left/right, skipping gaps. Optional wrap enables cyclic button navigation.</summary>
    public bool MoveSelection(int direction, bool wrap = false)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (SelectionData is not { } data) return false;
        bool selected = SelectedIndex >= 0 && SelectedIndex < data.Count;
        int index = selected ? SelectedIndex + direction : direction > 0 ? 0 : data.Count - 1;
        for (int visited = 0; visited < data.Count; visited++, index += direction)
        {
            if (index < 0 || index >= data.Count)
            {
                if (!wrap) return false;
                index = index < 0 ? data.Count - 1 : 0;
            }
            if (!double.IsFinite(data.Values[index])) continue;
            SetCurrentValue(SelectedIndexProperty, index);
            return true;
        }
        return false;
    }

    /// <summary>Select using a control-relative position. Axis labels and margins do not select data.</summary>
    public bool SelectAt(Point position)
    {
        if (SelectionData is null || PlotArea.Width <= 0 || PlotArea.Height <= 0 || !PlotArea.Contains(position)) return false;
        return SelectWavelength(XAxisMinimum + (position.X - PlotArea.Left) / PlotArea.Width * (XAxisMaximum - XAxisMinimum));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == StandardDataProperty)
        {
            UpdateAxisRange();
            SetCurrentValue(SelectedIndexProperty, -1);
            UpdateSelection();
        }
        else if (change.Property == MinimumMaximumProperty) UpdateAxisRange();
        else if (change.Property == SelectedIndexProperty) UpdateSelection();
    }

    private void UpdateAxisRange()
    {
        double minimum = 0, maximum = MinimumMaximum;
        foreach (var data in new[] { Data, StandardData })
            foreach (double value in data?.Values ?? Array.Empty<double>())
            {
                if (!double.IsFinite(value)) continue;
                minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
            }
        double lower = Math.Floor(minimum / 10) * 10, upper = Math.Ceiling(maximum / 10) * 10;
        SetAndRaise(YAxisMinimumProperty, ref _yAxisMinimum, double.IsFinite(lower) ? lower : minimum);
        SetAndRaise(YAxisMaximumProperty, ref _yAxisMaximum, double.IsFinite(upper) ? upper : maximum);
    }

    private void UpdateSelection()
    {
        var data = SelectionData;
        bool valid = data is not null && SelectedIndex >= 0 && SelectedIndex < data.Count && double.IsFinite(data.Values[SelectedIndex]);
        double? wavelength = valid ? data!.Wavelengths[SelectedIndex] : null;
        double? value = valid ? data!.Values[SelectedIndex] : null;
        double? standardValue = wavelength is { } selected ? StandardData?.ValueAt(selected) : null;
        if (_selectedWavelength == wavelength && _selectedValue == value && _selectedStandardValue == standardValue) return;
        SetAndRaise(SelectedWavelengthProperty, ref _selectedWavelength, wavelength);
        SetAndRaise(SelectedValueProperty, ref _selectedValue, value);
        SetAndRaise(SelectedStandardValueProperty, ref _selectedStandardValue, standardValue);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!SelectAt(e.GetPosition(this))) return;
        Focus(); e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured == this && SelectAt(e.GetPosition(this))) e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this) { e.Pointer.Capture(null); e.Handled = true; }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (SelectionData is not { } data) return;
        if (e.Key is not (Key.Left or Key.Right or Key.Home or Key.End)) return;
        if (e.Key is Key.Left or Key.Right)
        {
            MoveSelection(e.Key == Key.Left ? -1 : 1);
            e.Handled = true; return;
        }
        int direction = e.Key == Key.End ? -1 : 1;
        int index = e.Key == Key.Home ? 0 : data.Count - 1;
        while (index >= 0 && index < data.Count && !double.IsFinite(data.Values[index])) index += direction;
        if (index >= 0 && index < data.Count) SetCurrentValue(SelectedIndexProperty, index);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (PlotArea.Width <= 0 || PlotArea.Height <= 0) return;
        var plot = PlotArea;
        double startWavelength = XAxisMinimum, endWavelength = XAxisMaximum;
        double span = endWavelength - startWavelength;
        double X(double wavelength) => span == 0 ? plot.Center.X : plot.Left + (wavelength - startWavelength) * plot.Width / span;
        double minimum = YAxisMinimum, maximum = YAxisMaximum;
        Point At(double wavelength, double value) => new(X(wavelength),
            plot.Bottom - plot.Height * Math.Clamp((value - minimum) / (maximum - minimum), 0, 1));
        FormattedText Caption(string text) => new(LabelText(text), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 12, Brush.Parse("#666666"));
        void Label(string text, double x, double y) => context.DrawText(Caption(text), new Point(x, y));
        // Give the whole control a hit-test surface, including the otherwise transparent plot.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        for (int wavelength = (int)Math.Max(Math.Ceiling(startWavelength), WavelengthColors.Start);
            wavelength < Math.Min(endWavelength, WavelengthColors.End); wavelength++)
        {
            double x = X(wavelength);
            context.DrawRectangle(VisibleBackground[wavelength - WavelengthColors.Start], null,
                new Rect(x, plot.Top, X(Math.Min(wavelength + 1, endWavelength)) - x, plot.Height));
        }
        for (int i = 0; i <= 4; i++)
        {
            double y = plot.Top + plot.Height * i / 4;
            context.DrawLine(new Pen(Brush.Parse("#E5E5E5")), new Point(plot.Left, y), new Point(plot.Right, y));
            Label((minimum + (maximum - minimum) * (4 - i) / 4).ToString("0.#", CultureInfo.InvariantCulture), 0, y - 7);
        }
        using (context.PushClip(plot))
        {
            void DrawCurve(SpectrumDiagramData? data, Color color, double thickness)
            {
                if (data is null) return;
                var pen = new Pen(new SolidColorBrush(color), thickness);
                Point? previous = null;
                for (int i = 0; i < data.Count; i++)
                {
                    if (!double.IsFinite(data.Values[i])) { previous = null; continue; }
                    var point = At(data.Wavelengths[i], data.Values[i]);
                    if (previous is { } start) context.DrawLine(pen, start, point);
                    previous = point;
                }
            }
            DrawCurve(StandardData, StandardCurveColor, 5);
            DrawCurve(Data, CurveColor, 2.5);
            if (SelectedWavelength is { } wavelength)
            {
                double x = X(wavelength);
                context.DrawLine(new Pen(Brush.Parse("#555555"), 1, new DashStyle(new double[] { 4, 3 }, 0)),
                    new Point(x, plot.Top), new Point(x, plot.Bottom));
                if (SelectedStandardValue is { } standardValue)
                    context.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(StandardCurveColor), 2.5), At(wavelength, standardValue), 7, 7);
                if (Data is not null && SelectedValue is { } sampleValue)
                    context.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(CurveColor), 2), At(wavelength, sampleValue), 4, 4);
            }
        }
        Label($"{startWavelength.ToString("0.##", CultureInfo.InvariantCulture)} nm", plot.Left, plot.Bottom + 12);
        Label($"{endWavelength.ToString("0.##", CultureInfo.InvariantCulture)} nm", plot.Right - 55, plot.Bottom + 12);
        double tickStep = span <= 200 ? 50 : 100;
        for (double wavelength = (Math.Floor(startWavelength / tickStep) + 1) * tickStep; wavelength < endWavelength; wavelength += tickStep)
            if (X(wavelength) - plot.Left >= 65 && plot.Right - X(wavelength) >= 65)
                Label(wavelength.ToString("0.##", CultureInfo.InvariantCulture), X(wavelength) - 12, plot.Bottom + 12);
    }
}
