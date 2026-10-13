using ChromaticityDotNet.Diagrams;
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
    private readonly LineSeries _planckian = new() { Title = "Planckian locus", Color = OxyColors.DarkGray, StrokeThickness = 2 };
    private readonly LineSeries _boundary = new() { Color = OxyColors.Gray, StrokeThickness = 1 };
    private readonly ImageAnnotation _background;
    private readonly List<global::OxyPlot.Series.Series> _measurements = new();
    private readonly List<Annotation> _labels = new();
    private readonly ChromaticityDiagramProjection _projection;

    public PlotModel PlotModel { get; }
    public ChromaticityDiagramSpace Space => _projection.Space;
    public StandardObserver Observer { get; private set; }

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
        PlotModel = new PlotModel { PlotType = PlotType.Cartesian, PlotAreaBackground = OxyColors.White };
        PlotModel.Axes.Add(Axis(AxisPosition.Bottom, space == ChromaticityDiagramSpace.Xy ? "CIE x" : "CIE u′",
            space == ChromaticityDiagramSpace.Xy ? 0.8 : 0.65));
        PlotModel.Axes.Add(Axis(AxisPosition.Left, space == ChromaticityDiagramSpace.Xy ? "CIE y" : "CIE v′",
            space == ChromaticityDiagramSpace.Xy ? 0.9 : 0.65));
        PlotModel.Legends.Add(new Legend
        {
            LegendBackground = OxyColor.FromAColor(100, OxyColors.White), LegendBorder = OxyColors.Black,
            LegendBorderThickness = 1, LegendPlacement = LegendPlacement.Outside,
            LegendPosition = LegendPosition.RightTop, LegendItemSpacing = 3,
            LegendSymbolMargin = 3, LegendSymbolLength = 18, FontSize = 10
        });
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
        var boundary = _projection.GetBoundary(observer);
        var planckian = _projection.GetPlanckianLocus(observer);
        Observer = observer; ClearMeasurements();
        _planckian.Points.Clear(); _boundary.Points.Clear();
        foreach (var point in planckian) _planckian.Points.Add(new DataPoint(point.X, point.Y));
        foreach (var point in boundary) _boundary.Points.Add(new DataPoint(point.X, point.Y));
        _boundary.Points.Add(new DataPoint(boundary[0].X, boundary[0].Y));
        _boundary.Title = observer == StandardObserver.Degree2 ? "CIE 1931 2°" : "CIE 1964 10°";
        _background.ImageSource = Space == ChromaticityDiagramSpace.Xy
            ? (observer == StandardObserver.Degree2 ? Xy2.Value : Xy10.Value)
            : (observer == StandardObserver.Degree2 ? Uv2.Value : Uv10.Value);
        PlotModel.InvalidatePlot(false);
    }

    public void ClearMeasurements()
    {
        foreach (var series in _measurements) PlotModel.Series.Remove(series);
        foreach (var label in _labels) PlotModel.Annotations.Remove(label);
        _measurements.Clear(); _labels.Clear(); PlotModel.InvalidatePlot(false);
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
        foreach (var point in snapshot) Marker("Sample", point, MarkerType.Circle, 2);
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
            Marker("D", intersection.Forward, MarkerType.Circle, 4);
            Marker("C", intersection.Reverse, MarkerType.Circle, 4);
            Label(intersection.Forward, WavelengthLabel("D", data.Wavelengths.DominantWavelength));
            Label(intersection.Reverse, WavelengthLabel("C", data.Wavelengths.ComplementaryWavelength));
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
        PlanckianConnection(data, sample); Marker(name, sample, MarkerType.Circle, 3);
        PlotModel.InvalidatePlot(false);
    }

    private void PlanckianConnection(PlanckianDiagramData data, ChromaticityDiagramPoint sample)
    {
        var nearest = _projection.Project(data.Cct.NearestPoint.X, data.Cct.NearestPoint.Y);
        Line(sample, nearest, LineStyle.Dot, 1.5); Marker("Nearest Planckian point", nearest, MarkerType.Square, 3);
        string temperature = data.Cct.Temperature is { } t ? t.ToString("0", CultureInfo.InvariantCulture) + " K"
            : data.Cct.IsInTemperatureRange ? "CCT unavailable" : "Locus endpoint";
        Label(nearest, $"{temperature}\nDuv {data.Cct.Duv.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture)}");
    }

    private static string WavelengthLabel(string name, double? value) => value is { } wavelength
        ? $"{name} {wavelength.ToString("F2", CultureInfo.InvariantCulture)} nm" : $"{name} Purple boundary";

    private void Marker(string title, ChromaticityDiagramPoint point, MarkerType type, double size)
    {
        var series = new ScatterSeries { Title = title, MarkerType = type, MarkerSize = size,
            MarkerFill = OxyColors.Black, MarkerStroke = OxyColors.Gray };
        series.Points.Add(new ScatterPoint(point.X, point.Y));
        PlotModel.Series.Add(series); _measurements.Add(series);
    }

    private void Line(ChromaticityDiagramPoint a, ChromaticityDiagramPoint b, LineStyle style, double thickness)
    {
        var series = new LineSeries { Color = OxyColors.Black, LineStyle = style, StrokeThickness = thickness };
        series.Points.Add(new DataPoint(a.X, a.Y)); series.Points.Add(new DataPoint(b.X, b.Y));
        PlotModel.Series.Add(series); _measurements.Add(series);
    }

    private void Label(ChromaticityDiagramPoint point, string text)
    {
        var label = new TextAnnotation { TextPosition = new DataPoint(point.X, point.Y), Text = text,
            TextColor = OxyColors.Black, Stroke = OxyColors.Undefined, FontSize = 10 };
        PlotModel.Annotations.Add(label); _labels.Add(label);
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
