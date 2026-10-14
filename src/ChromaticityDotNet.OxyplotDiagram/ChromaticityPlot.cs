using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using global::OxyPlot;
using global::OxyPlot.Annotations;
using global::OxyPlot.Axes;
using global::OxyPlot.Legends;
using global::OxyPlot.Series;
using System.Globalization;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

namespace ChromaticityDotNet.OxyplotDiagram;

/// <summary>Framework-independent chromaticity plot extracted from QuantumSpect's two chart view models.
/// Each instance owns one PlotModel; create separate instances for separate PlotViews.</summary>
public class ChromaticityPlot
{
    private static readonly Lazy<OxyImage> Xy2 = new(() => CreateBackground(StandardObserver.Degree2, ChromaticityDiagramSpace.Xy));
    private static readonly Lazy<OxyImage> Xy10 = new(() => CreateBackground(StandardObserver.Degree10, ChromaticityDiagramSpace.Xy));
    private static readonly Lazy<OxyImage> Uv2 = new(() => CreateBackground(StandardObserver.Degree2, ChromaticityDiagramSpace.UvPrime));
    private static readonly Lazy<OxyImage> Uv10 = new(() => CreateBackground(StandardObserver.Degree10, ChromaticityDiagramSpace.UvPrime));
    private readonly LineSeries _planckian = new() { Title = "Planckian locus", Color = OxyColors.DarkGray, StrokeThickness = 2, RenderInLegend = false };
    private readonly LineSeries _boundary = new() { Color = OxyColors.Gray, StrokeThickness = 1, RenderInLegend = false };
    private readonly ImageAnnotation _background;
    private readonly List<global::OxyPlot.Series.Series> _measurements = new();
    private readonly List<Annotation> _labels = new();
    private readonly List<Annotation> _referenceLabels = new();
    private readonly List<global::OxyPlot.Series.Series> _planckianMeasurements = new();
    private readonly List<Annotation> _planckianLabels = new();
    private readonly HashSet<global::OxyPlot.Series.Series> _hiddenLegendItems = new();
    private readonly Dictionary<global::OxyPlot.Series.Series, List<Annotation>> _markerLabels = new();
    private readonly ChromaticityDiagramProjection _projection;
    private PlanckianDiagramData? _measurementPlanckian;
    private bool _showWavelengthLabels = true, _showTemperatureLabels, _showPlanckianLocus = true;
    private double? _axisMaximumX, _axisMaximumY;

    public PlotModel PlotModel { get; }
    /// <summary>Bind this controller to the host PlotView.Controller for left-button snap tracking.</summary>
    public PlotController Controller { get; }
    public ChromaticityDiagramSpace Space => _projection.Space;
    public StandardObserver Observer { get; private set; }

    public double? AxisMaximumX
    {
        get => _axisMaximumX;
        set { ApplyAxisRange(value, _axisMaximumY); _axisMaximumX = value; }
    }
    public double? AxisMaximumY
    {
        get => _axisMaximumY;
        set { ApplyAxisRange(_axisMaximumX, value); _axisMaximumY = value; }
    }

    private void ApplyAxisRange(double? maximumX, double? maximumY)
    {
        var viewport = new ChromaticityDiagramViewport(Space, maximumX, maximumY);
        PlotModel.Axes[0].Maximum = viewport.MaximumX; PlotModel.Axes[0].Reset();
        PlotModel.Axes[1].Maximum = viewport.MaximumY; PlotModel.Axes[1].Reset();
        PlotModel.InvalidatePlot(false);
    }

    public bool ShowWavelengthLabels
    {
        get => _showWavelengthLabels;
        set { if (_showWavelengthLabels == value) return; _showWavelengthLabels = value; UpdateReferenceLabels(); }
    }

    /// <summary>Show the blackbody curve and any measurement connection to its nearest point.</summary>
    public bool ShowPlanckianLocus
    {
        get => _showPlanckianLocus;
        set
        {
            _showPlanckianLocus = value; _hiddenLegendItems.Remove(_planckian);
            _planckian.RenderInLegend = value;
            RefreshLegendVisibility(); UpdateReferenceLabels();
        }
    }

    private bool PlanckianVisible => ShowPlanckianLocus && !_hiddenLegendItems.Contains(_planckian);

    /// <summary>Temporarily toggle an item in displayed legend order, without changing measurement data or display options.</summary>
    public void ToggleLegendItem(int index)
    {
        var items = PlotModel.Series.Where(series => series.RenderInLegend && !string.IsNullOrEmpty(series.Title)).ToArray();
        if (index < 0 || index >= items.Length) throw new ArgumentOutOfRangeException(nameof(index));
        ToggleLegendSeries(items[index]);
    }

    private void ToggleLegendSeries(global::OxyPlot.Series.Series series)
    {
        if (!_hiddenLegendItems.Remove(series)) _hiddenLegendItems.Add(series);
        RefreshLegendVisibility();
        if (series == _planckian) UpdateReferenceLabels();
    }

    private void RefreshLegendVisibility()
    {
        _planckian.IsVisible = PlanckianVisible;
        bool nearestHidden = _planckianMeasurements.OfType<ScatterSeries>().Any(series => _hiddenLegendItems.Contains(series));
        foreach (var series in _measurements)
        {
            series.IsVisible = !_hiddenLegendItems.Contains(series) && (!_planckianMeasurements.Contains(series) || PlanckianVisible && !nearestHidden);
            if (_planckianMeasurements.Contains(series)) series.RenderInLegend = ShowPlanckianLocus;
        }
        foreach (var pair in _markerLabels)
            foreach (var label in pair.Value)
            {
                PlotModel.Annotations.Remove(label);
                if (pair.Key.IsVisible) PlotModel.Annotations.Add(label);
            }
        PlotModel.InvalidatePlot(false);
    }

    /// <summary>Show reference temperatures and the measured CCT. Reference temperatures are independent of the curve.</summary>
    public bool ShowTemperatureLabels
    {
        get => _showTemperatureLabels;
        set
        {
            if (_showTemperatureLabels == value) return;
            _showTemperatureLabels = value;
            if (_measurementPlanckian is { } data)
                foreach (var label in _planckianLabels.OfType<TextAnnotation>()) label.Text = PlanckianMeasurementText(data);
            UpdateReferenceLabels();
        }
    }

    public bool ShowLegend
    {
        get => PlotModel.IsLegendVisible;
        set { PlotModel.IsLegendVisible = value; PlotModel.InvalidatePlot(false); }
    }

    public double BackgroundOpacity
    {
        get => _background.Opacity;
        set
        {
            if (double.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            _background.Opacity = value; PlotModel.InvalidatePlot(false);
        }
    }

    public ChromaticityPlot(ChromaticityDiagramSpace space, StandardObserver observer = StandardObserver.Degree2)
    {
        _projection = new(space);
        var viewport = new ChromaticityDiagramViewport(space);
        Controller = new PlotController();
        Controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.SnapTrack);
        PlotModel = new PlotModel { PlotType = PlotType.Cartesian, PlotAreaBackground = OxyColors.White };
        PlotModel.Axes.Add(Axis(AxisPosition.Bottom, space == ChromaticityDiagramSpace.Xy ? "CIE x" : "CIE u′",
            viewport.MaximumX));
        PlotModel.Axes.Add(Axis(AxisPosition.Left, space == ChromaticityDiagramSpace.Xy ? "CIE y" : "CIE v′",
            viewport.MaximumY));
        var legend = new InteractiveLegend(ToggleLegendSeries)
        {
            LegendBackground = OxyColor.FromAColor(242, OxyColors.White), LegendBorder = OxyColors.Gray,
            LegendBorderThickness = 1, LegendPlacement = LegendPlacement.Inside,
            LegendPosition = viewport.LegendAtTop ? LegendPosition.TopRight : LegendPosition.BottomRight, LegendItemSpacing = 3,
            LegendSymbolMargin = 3, LegendSymbolLength = 18, LegendMaxWidth = 200, FontSize = 10, ShowInvisibleSeries = true
        };
        PlotModel.Legends.Add(legend);
        _planckian.RenderInLegend = true;
        _background = new ImageAnnotation
        {
            X = new PlotLength(0, PlotLengthUnit.Data), Y = new PlotLength(_projection.MaxY, PlotLengthUnit.Data),
            Width = new PlotLength(_projection.MaxX, PlotLengthUnit.Data), Height = new PlotLength(_projection.MaxY, PlotLengthUnit.Data),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Layer = AnnotationLayer.BelowAxes, Interpolate = true
        };
        PlotModel.Annotations.Add(_background);
        // Preserve QuantumSpect's static series order: Planckian locus, then CIE boundary.
        PlotModel.Series.Add(_planckian); PlotModel.Series.Add(_boundary);
        SetObserver(observer);
    }

    private static LinearAxis Axis(AxisPosition position, string title, double maximum) => new()
    {
        Position = position, Title = title, TitleFontSize = 10,
        MajorGridlineStyle = LineStyle.Solid, MinorGridlineStyle = LineStyle.Dot,
        Minimum = 0, Maximum = maximum, IsZoomEnabled = false, IsPanEnabled = false
    };

    /// <summary>Replace the observer-specific background and curves, clearing old measurement markers.</summary>
    public void SetObserver(StandardObserver observer)
    {
        var boundary = CieSpectralData.GetChromaticityBoundary(observer).Select(point =>
            new ChromaticityDiagramTarget("Chromaticity boundary", Space, _projection.Project(point.X, point.Y), point.Wavelength)).ToArray();
        var planckian = ChromaticityConversion.GetPlanckianLocus(observer).Select(point =>
            new ChromaticityDiagramTarget("Planckian locus", Space, _projection.Project(point.X, point.Y), temperature: point.Temperature)).ToArray();
        Observer = observer; ClearMeasurements();
        _planckian.Points.Clear(); _boundary.Points.Clear();
        foreach (var point in planckian) _planckian.Points.Add(new DataPoint(point.X, point.Y));
        foreach (var point in boundary) _boundary.Points.Add(new DataPoint(point.X, point.Y));
        _boundary.Points.Add(new DataPoint(boundary[0].X, boundary[0].Y));
        _planckian.ItemsSource = planckian;
        _boundary.ItemsSource = boundary.Concat(new[] { boundary[0] }).ToArray();
        _planckian.Mapping = _boundary.Mapping = item => new DataPoint(((ChromaticityDiagramTarget)item).X, ((ChromaticityDiagramTarget)item).Y);
        _planckian.TrackerFormatString = "{0}\n{1}: {2:F6}\n{3}: {4:F6}\nT: {Temperature:0} K";
        _boundary.TrackerFormatString = "{0}\n{1}: {2:F6}\n{3}: {4:F6}\nλ: {Wavelength:F2} nm";
        _planckian.CanTrackerInterpolatePoints = _boundary.CanTrackerInterpolatePoints = false;
        _boundary.Title = observer == StandardObserver.Degree2 ? "CIE 1931 2°" : "CIE 1964 10°";
        _background.ImageSource = Space == ChromaticityDiagramSpace.Xy
            ? (observer == StandardObserver.Degree2 ? Xy2.Value : Xy10.Value)
            : (observer == StandardObserver.Degree2 ? Uv2.Value : Uv10.Value);
        UpdateReferenceLabels();
        PlotModel.InvalidatePlot(false);
    }

    private void UpdateReferenceLabels()
    {
        foreach (var label in _referenceLabels) PlotModel.Annotations.Remove(label);
        _referenceLabels.Clear();
        void Reference(double x, double y, string text, ScreenVector offset)
        {
            var point = _projection.Project(x, y);
            var label = new TextAnnotation
            {
                TextPosition = new DataPoint(point.X, point.Y), Text = text, Offset = offset,
                TextColor = OxyColors.Black, Stroke = OxyColors.Undefined, FontSize = 10
            };
            _referenceLabels.Add(label); PlotModel.Annotations.Add(label);
        }
        if (ShowWavelengthLabels)
        {
            var locus = CieSpectralData.GetSpectralLocus(Observer);
            foreach (int wavelength in new[] { 400, 450, 500, 550, 600, 650 })
            {
                var point = locus[wavelength - 360];
                Reference(point.X, point.Y, $"{wavelength} nm", wavelength == 400 ? new ScreenVector(28, 12) : new ScreenVector(8, -12));
            }
        }
        if (ShowTemperatureLabels && !_hiddenLegendItems.Contains(_planckian))
            foreach (int temperature in new[] { 2000, 3000, 4000, 6500, 10000, 25000 })
            {
                var point = ChromaticityConversion.GetPlanckianChromaticity(temperature, Observer);
                var offset = temperature switch
                {
                    2000 => new ScreenVector(0, -18),
                    3000 => new ScreenVector(0, -36),
                    4000 => new ScreenVector(0, -54),
                    6500 => new ScreenVector(-12, 18),
                    10000 => new ScreenVector(-12, 36),
                    _ => new ScreenVector(-12, 54)
                };
                Reference(point.X, point.Y, $"{temperature} K", offset);
            }
        PlotModel.InvalidatePlot(false);
    }

    public void ClearMeasurements()
    {
        foreach (var series in _measurements) PlotModel.Series.Remove(series);
        foreach (var label in _labels) PlotModel.Annotations.Remove(label);
        _measurements.Clear(); _labels.Clear(); _measurementPlanckian = null;
        _planckianMeasurements.Clear(); _planckianLabels.Clear();
        _markerLabels.Clear();
        _hiddenLegendItems.RemoveWhere(series => series != _planckian);
        RefreshLegendVisibility();
    }

    /// <summary>Replace measurements with named xy samples, projecting into this plot's coordinate space.</summary>
    public void SetSamples(IEnumerable<ChromaticityDiagramSample> samples)
    {
        if (samples is null) throw new ArgumentNullException(nameof(samples));
        var snapshot = samples.Select(sample =>
        {
            if (sample is null) throw new ArgumentException("A sample cannot be null.", nameof(samples));
            return (Sample: sample, Point: _projection.Project(sample.Coordinates.X, sample.Coordinates.Y));
        }).ToArray();
        ClearMeasurements();
        foreach (var item in snapshot)
        {
            uint argb = item.Sample.MarkerArgb;
            Marker(item.Sample.Name, item.Point, MarkerType.Circle, 4,
                OxyColor.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        }
        PlotModel.InvalidatePlot(false);
    }

    public void UpdateXyPoints(IEnumerable<CIExyY> points)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        var projected = points.Select(p => ChromaticityCoordinates.FromXyY(p))
            .Select(p => _projection.Project(p.X, p.Y)).ToArray();
        UpdateProjectedPoints(projected);
    }

    protected void UpdateProjectedPoints(IEnumerable<ChromaticityDiagramPoint> points)
    {
        var snapshot = points.ToArray();
        foreach (var point in snapshot)
            if (double.IsNaN(point.X) || double.IsInfinity(point.X) || double.IsNaN(point.Y) || double.IsInfinity(point.Y))
                throw new ArgumentException("Finite chromaticity coordinates are required.", nameof(points));
        ClearMeasurements();
        for (int i = 0; i < snapshot.Length; i++)
            Marker(snapshot.Length == 1 ? "Sample" : $"Sample {i + 1}", snapshot[i], MarkerType.Circle, 2);
        PlotModel.InvalidatePlot(false);
    }

    public void SetData(WavelengthDiagramData data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        var hits = _projection.Intersections(data);
        SetObserver(data.Observer);
        var white = _projection.Project(data.White.X, data.White.Y);
        var sample = _projection.Project(data.Sample.X, data.Sample.Y);
        if (hits is { } intersection)
        {
            Line(intersection.Reverse, intersection.Forward, LineStyle.Dash, 1);
            var dominant = Marker("D", intersection.Forward, MarkerType.Circle, 4, wavelength: data.Wavelengths.DominantWavelength);
            var complementary = Marker("C", intersection.Reverse, MarkerType.Circle, 4, wavelength: data.Wavelengths.ComplementaryWavelength);
            _markerLabels[dominant] = new() { Label(intersection.Forward, WavelengthLabel("D", data.Wavelengths.DominantWavelength)) };
            _markerLabels[complementary] = new() { Label(intersection.Reverse, WavelengthLabel("C", data.Wavelengths.ComplementaryWavelength)) };
        }
        Line(white, sample, LineStyle.Solid, 2);
        PlanckianConnection(data.Planckian, sample);
        Marker("White", white, MarkerType.Cross, 4);
        Marker("Sample", sample, MarkerType.Circle, 3);
        PlotModel.InvalidatePlot(false);
    }

    public void SetLightData(PlanckianDiagramData data, string name = "Light")
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        SetObserver(data.Observer);
        var sample = _projection.Project(data.Sample.X, data.Sample.Y);
        PlanckianConnection(data, sample); Marker(name, sample, MarkerType.Circle, 3, temperature: data.Cct.Temperature, duv: data.Cct.Duv);
        PlotModel.InvalidatePlot(false);
    }

    private void PlanckianConnection(PlanckianDiagramData data, ChromaticityDiagramPoint sample)
    {
        var nearest = _projection.Project(data.Cct.NearestPoint.X, data.Cct.NearestPoint.Y);
        var line = Line(sample, nearest, LineStyle.Dot, 1.5);
        var marker = Marker("Nearest Planckian point", nearest, MarkerType.Square, 3, temperature: data.Cct.Temperature, duv: data.Cct.Duv);
        _planckianMeasurements.Add(line); _planckianMeasurements.Add(marker);
        line.IsVisible = marker.IsVisible = PlanckianVisible;
        marker.RenderInLegend = ShowPlanckianLocus;
        _measurementPlanckian = data;
        var label = Label(nearest, PlanckianMeasurementText(data));
        _planckianLabels.Add(label);
        _markerLabels[marker] = new() { label };
        if (!PlanckianVisible) PlotModel.Annotations.Remove(label);
    }

    private string PlanckianMeasurementText(PlanckianDiagramData data)
    {
        string temperature = data.Cct.Temperature is { } t ? t.ToString("0", CultureInfo.InvariantCulture) + " K"
            : data.Cct.IsInTemperatureRange ? "CCT unavailable" : "Locus endpoint";
        string duv = $"Duv {data.Cct.Duv.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture)}";
        return ShowTemperatureLabels ? $"{temperature}\n{duv}" : duv;
    }

    private static string WavelengthLabel(string name, double? value) => value is { } wavelength
        ? $"{name} {wavelength.ToString("F2", CultureInfo.InvariantCulture)} nm" : $"{name} Purple boundary";

    private ScatterSeries Marker(string title, ChromaticityDiagramPoint point, MarkerType type, double size, OxyColor? color = null,
        double? wavelength = null, double? temperature = null, double? duv = null)
    {
        var series = new ScatterSeries { Title = title, MarkerType = type, MarkerSize = size,
            MarkerFill = color ?? OxyColors.Black, MarkerStroke = OxyColors.Gray };
        series.Points.Add(new ScatterPoint(point.X, point.Y));
        var target = new ChromaticityDiagramTarget(title, Space, point, wavelength, temperature, duv);
        series.ItemsSource = new[] { target };
        series.Mapping = item => new ScatterPoint(((ChromaticityDiagramTarget)item).X, ((ChromaticityDiagramTarget)item).Y);
        // Keep arbitrary sample names in a numeric format argument; named-property expansion
        // would interpret braces in a name as format placeholders.
        series.TrackerFormatString = "{0}" + target.TrackerText.Substring(title.Length);
        PlotModel.Series.Add(series); _measurements.Add(series);
        return series;
    }

    private LineSeries Line(ChromaticityDiagramPoint a, ChromaticityDiagramPoint b, LineStyle style, double thickness)
    {
        var series = new ConnectionSeries { Color = OxyColors.Black, LineStyle = style, StrokeThickness = thickness };
        series.Points.Add(new DataPoint(a.X, a.Y)); series.Points.Add(new DataPoint(b.X, b.Y));
        PlotModel.Series.Add(series); _measurements.Add(series);
        return series;
    }

    // Measurement connectors are geometry, not additional data targets.
    private sealed class ConnectionSeries : LineSeries
    {
        public override TrackerHitResult? GetNearestPoint(ScreenPoint point, bool interpolate) => null;
    }

    private sealed class InteractiveLegend : Legend
    {
        private readonly Action<global::OxyPlot.Series.Series> _toggle;
        public InteractiveLegend(Action<global::OxyPlot.Series.Series> toggle) => _toggle = toggle;

        protected override HitTestResult? LegendHitTest(HitTestArguments args)
        {
            if (!IsLegendVisible || !PlotModel.IsLegendVisible || !IsPointInLegend(args.Point)) return null;
            // OxyPlot 2.2 toggles a series inside LegendHitTest and returns null.
            // Restore it while probing, then route a real left click through our group visibility logic.
            var before = PlotModel.Series.Select(series => (Series: series, Visible: series.IsVisible)).ToArray();
            base.LegendHitTest(args);
            global::OxyPlot.Series.Series? target = null;
            foreach (var state in before)
            {
                if (state.Series.IsVisible == state.Visible) continue;
                target = state.Series; state.Series.IsVisible = state.Visible;
            }
            return new HitTestResult(this, args.Point, target, 0);
        }

        protected override void OnMouseDown(OxyMouseDownEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == OxyMouseButton.Left && e.HitTestResult?.Item is global::OxyPlot.Series.Series series) _toggle(series);
            e.Handled = true;
        }
    }

    private TextAnnotation Label(ChromaticityDiagramPoint point, string text)
    {
        var label = new TextAnnotation { TextPosition = new DataPoint(point.X, point.Y), Text = text,
            TextColor = OxyColors.Black, Stroke = OxyColors.Undefined, FontSize = 10 };
        PlotModel.Annotations.Add(label); _labels.Add(label);
        return label;
    }

    private static OxyImage CreateBackground(StandardObserver observer, ChromaticityDiagramSpace space)
    {
        const int width = ChromaticityDiagramBackground.Width;
        var bytes = ChromaticityDiagramBackground.Create(observer, space);
        var pixels = new OxyColor[width, width];
        for (int y = 0; y < width; y++)
        for (int x = 0; x < width; x++)
        {
            int i = (y * width + x) * 4;
            pixels[x, y] = OxyColor.FromArgb(bytes[i + 3], bytes[i], bytes[i + 1], bytes[i + 2]);
        }
        return OxyImage.Create(pixels, ImageFormat.Png);
    }
}
