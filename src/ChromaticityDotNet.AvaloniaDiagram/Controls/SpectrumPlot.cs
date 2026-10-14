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
    public static readonly StyledProperty<string> AxisLabelProperty = AvaloniaProperty.Register<SpectrumPlot, string>(nameof(AxisLabel), "%");
    public static readonly StyledProperty<double> MinimumMaximumProperty =
        AvaloniaProperty.Register<SpectrumPlot, double>(nameof(MinimumMaximum), 100, validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<SpectrumPlot, int>(
        nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay, validate: value => value >= -1);
    public static readonly DirectProperty<SpectrumPlot, double?> SelectedWavelengthProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double?>(nameof(SelectedWavelength), plot => plot.SelectedWavelength);
    public static readonly DirectProperty<SpectrumPlot, double?> SelectedValueProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double?>(nameof(SelectedValue), plot => plot.SelectedValue);
    public static readonly DirectProperty<SpectrumPlot, double> YAxisMinimumProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double>(nameof(YAxisMinimum), plot => plot.YAxisMinimum);
    public static readonly DirectProperty<SpectrumPlot, double> YAxisMaximumProperty =
        AvaloniaProperty.RegisterDirect<SpectrumPlot, double>(nameof(YAxisMaximum), plot => plot.YAxisMaximum);
    private double? _selectedWavelength, _selectedValue;
    private double _yAxisMinimum, _yAxisMaximum = 100;
    private static readonly IBrush[] VisibleBackground = Enumerable.Range(WavelengthColors.Start, WavelengthColors.End - WavelengthColors.Start + 1)
        .Select(wavelength =>
        {
            var color = WavelengthColors.At(wavelength);
            return (IBrush)new SolidColorBrush(Color.FromArgb(32, color.R, color.G, color.B));
        }).ToArray();

    static SpectrumPlot() => AffectsRender<SpectrumPlot>(DataProperty, AxisLabelProperty, MinimumMaximumProperty, SelectedIndexProperty);
    public SpectrumPlot() { Focusable = true; ClipToBounds = true; }
    public SpectrumDiagramData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string AxisLabel { get => GetValue(AxisLabelProperty); set => SetValue(AxisLabelProperty, value); }
    public double MinimumMaximum { get => GetValue(MinimumMaximumProperty); set => SetValue(MinimumMaximumProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double? SelectedWavelength => _selectedWavelength;
    public double? SelectedValue => _selectedValue;
    public double YAxisMinimum => _yAxisMinimum;
    public double YAxisMaximum => _yAxisMaximum;
    public event EventHandler? SelectionChanged;
    public Rect PlotArea => new(55, 8, Math.Max(0, Bounds.Width - 75), Math.Max(0, Bounds.Height - 43));

    public void SetData(double[] values, int start, int step) => SetData(SpectrumDiagramData.FromUniform(values, start, step));
    public void SetData(SpectrumDiagramData data) => SetCurrentValue(DataProperty, data ?? throw new ArgumentNullException(nameof(data)));
    public void Clear() => SetCurrentValue(DataProperty, null);

    /// <summary>Select the nearest finite sample; values remain in the source units.</summary>
    public bool SelectWavelength(double wavelength)
    {
        int index = Data?.FindNearestIndex(wavelength) ?? -1;
        SetCurrentValue(SelectedIndexProperty, index);
        return index >= 0;
    }

    /// <summary>Move one measured point to the left/right, skipping gaps. Optional wrap enables cyclic button navigation.</summary>
    public bool MoveSelection(int direction, bool wrap = false)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (Data is not { } data) return false;
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
        if (Data is not { } data || PlotArea.Width <= 0 || PlotArea.Height <= 0 || !PlotArea.Contains(position)) return false;
        return SelectWavelength(data.Start + (position.X - PlotArea.Left) / PlotArea.Width * (data.End - data.Start));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
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
        if (Data is { } data)
            foreach (double value in data.Values)
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
        bool valid = Data is { } data && SelectedIndex >= 0 && SelectedIndex < data.Count && double.IsFinite(data.Values[SelectedIndex]);
        double? wavelength = valid ? Data!.Wavelengths[SelectedIndex] : null;
        double? value = valid ? Data!.Values[SelectedIndex] : null;
        if (_selectedWavelength == wavelength && _selectedValue == value) return;
        SetAndRaise(SelectedWavelengthProperty, ref _selectedWavelength, wavelength);
        SetAndRaise(SelectedValueProperty, ref _selectedValue, value);
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
        if (Data is not { } data) return;
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
        if (Data is not { } data || PlotArea.Width <= 0 || PlotArea.Height <= 0) return;
        var plot = PlotArea;
        double span = data.End - data.Start;
        double X(double wavelength) => span == 0 ? plot.Center.X : plot.Left + (wavelength - data.Start) * plot.Width / span;
        double minimum = YAxisMinimum, maximum = YAxisMaximum;
        Point At(int index) => new(X(data.Wavelengths[index]),
            plot.Bottom - plot.Height * Math.Clamp((data.Values[index] - minimum) / (maximum - minimum), 0, 1));
        FormattedText Caption(string text) => new(LabelText(text), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 12, Brush.Parse("#666666"));
        void Label(string text, double x, double y) => context.DrawText(Caption(text), new Point(x, y));
        // Give the whole control a hit-test surface, including the otherwise transparent plot.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        for (int wavelength = Math.Max((int)Math.Ceiling(data.Start), WavelengthColors.Start);
            wavelength < Math.Min(data.End, WavelengthColors.End); wavelength++)
        {
            double x = X(wavelength);
            context.DrawRectangle(VisibleBackground[wavelength - WavelengthColors.Start], null,
                new Rect(x, plot.Top, X(Math.Min(wavelength + 1, data.End)) - x, plot.Height));
        }
        for (int i = 0; i <= 4; i++)
        {
            double y = plot.Top + plot.Height * i / 4;
            context.DrawLine(new Pen(Brush.Parse("#E5E5E5")), new Point(plot.Left, y), new Point(plot.Right, y));
            Label((minimum + (maximum - minimum) * (4 - i) / 4).ToString("0.#", CultureInfo.InvariantCulture), 0, y - 7);
        }
        using (context.PushClip(plot))
        {
            var pen = new Pen(Brush.Parse("#333333"), 2.5);
            Point? previous = null;
            for (int i = 0; i < data.Count; i++)
            {
                if (!double.IsFinite(data.Values[i])) { previous = null; continue; }
                var point = At(i);
                if (previous is { } start) context.DrawLine(pen, start, point);
                previous = point;
            }
            if (SelectedWavelength is { } wavelength)
            {
                double x = X(wavelength);
                context.DrawLine(new Pen(Brush.Parse("#555555"), 1, new DashStyle(new double[] { 4, 3 }, 0)),
                    new Point(x, plot.Top), new Point(x, plot.Bottom));
                context.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 2), At(SelectedIndex), 4, 4);
            }
        }
        Label($"{data.Start.ToString("0.##", CultureInfo.InvariantCulture)} nm", plot.Left, plot.Bottom + 12);
        Label($"{data.End.ToString("0.##", CultureInfo.InvariantCulture)} nm", plot.Right - 55, plot.Bottom + 12);
        double tickStep = span <= 200 ? 50 : 100;
        for (double wavelength = (Math.Floor(data.Start / tickStep) + 1) * tickStep; wavelength < data.End; wavelength += tickStep)
            if (X(wavelength) - plot.Left >= 65 && plot.Right - X(wavelength) >= 65)
                Label(wavelength.ToString("0.##", CultureInfo.InvariantCulture), X(wavelength) - 12, plot.Bottom + 12);
    }
}
