# ChromaticityDotNet.AvaloniaDiagram

Reusable Avalonia controls depending only on Avalonia and `ChromaticityDotNet`:

- `ChromaticityDiagram`: xy / CIE 1976 u′v′ plots from Chromaticity.Tools,
  including observer-specific backgrounds, Planckian locus, CCT / Duv and wavelength rays.
- `LchPlot`: Colorimeter.Embedded's separate L* axis and C*/h° plane,
  comparing standard and sample with distinct circle / diamond markers.
- `SpectrumPlot`: Chromaticity.Tools' spectrum curve with Colorimeter.Embedded's
  nearest-sample selection, extended to arbitrary wavelength ranges and irregular grids.
- `SpectrumNavigator`: reusable ◀ / ▶ buttons and a wavelength/value readout
  associated with a `SpectrumPlot`, matching Colorimeter.Embedded's cyclic navigation.

All controls are in `ChromaticityDotNet.AvaloniaDiagram.Controls`. Shared data and
geometry are in the main package's `ChromaticityDotNet.Diagrams` namespace.

```csharp
using ChromaticityDotNet.AvaloniaDiagram.Controls;
using ChromaticityDotNet.Diagrams;
using static ChromaticityDotNet.Model.DataModel;

var lch = new LchPlot
{
    StandardLab = new CIELAB(50, 30, 20),
    SampleLab = new CIELAB(53, 32, 18)
};
var spectrum = new SpectrumPlot { AxisLabel = "%", MinimumMaximum = 100 };
var navigation = new SpectrumNavigator { Plot = spectrum };
// Place navigation above spectrum; navigation aligns to the right by default.
spectrum.SetData(new[] { 18.0, 20.0, 22.0 }, 400, 10);
spectrum.SelectWavelength(413); // Selects the measured 410 nm point: 20%.
```

Use `ChromaticityDiagram.Space` to select xy / u′v′, `WavelengthData` / `SetData`
for sample/white rays, or `LightData` / `SetLightData` for a light-source marker.
These are bindable styled properties. `Clear(observer)` clears old markers.

`ShowWavelengthLabels` controls the edge wavelength markers and captions;
`ShowPlanckianLocus` controls the blackbody curve and nearest-point / Duv overlay;
`ShowTemperatureLabels` controls reference temperatures and the measured CCT label.
Reference temperatures can be displayed independently of the curve. Wavelengths
and the curve default to visible; temperatures default to hidden.
`ShowLegend` defaults to true and lists the blackbody curve and current measurement
markers inside the plot: xy at the top right, u′v′ at the bottom right. Many entries use additional columns;
long names are shortened with an ellipsis. The control keeps its square layout.
Click a legend item to temporarily hide or restore its curve / marker. Hidden
items stay in the legend in gray and are excluded from data selection. Hiding the
blackbody curve also hides its reference temperatures and nearest-point overlay,
without changing `ShowPlanckianLocus` or `ShowTemperatureLabels`. Sample visibility
resets when measurements are replaced; the curve stays hidden until restored.
Explicitly assigning `ShowPlanckianLocus` restores its configured visibility.
`ToggleLegendItem`, `IsLegendItemVisible` and `GetLegendItemArea` support host control.

The shared default axes are xy 0–1.0 on both axes, and u′v′ 0–0.8 / 0–0.7,
leaving room in these corners. Set `AxisMaximumX` / `AxisMaximumY` to expand the
view for larger legends; null restores the defaults. Changing the display range
does not stretch the background in coordinate space or change sample coordinates.

Left-click or touch a sample, white point, wavelength intersection, boundary or
blackbody point to show its name and coordinates, plus wavelength / temperature /
Duv where applicable. Drag while pressed to inspect nearby targets. Click empty
space or press Escape to dismiss the readout. `SelectedTarget` and
`SelectionChanged` expose full-precision data to the host. `HitTestTarget`,
`PlotPoint`, `PlotArea` and `LegendArea` are available for host interaction or tests;
the legend itself does not select targets. Replacing data clears selection.

For a background with named sample points, use the same xy samples in either space:

```csharp
var diagram = new ChromaticityDiagram
{
    Space = ChromaticityDiagramSpace.UvPrime,
    ShowWavelengthLabels = false,
    ShowPlanckianLocus = false,
    ShowTemperatureLabels = false
};
diagram.SetSamples(new[]
{
    new ChromaticityDiagramSample("Reference", new ChromaticityCoordinates(0.3127, 0.3290), 0xFF2563EB),
    new ChromaticityDiagramSample("Sample 1", new ChromaticityCoordinates(0.4, 0.2), 0xFFE11D48)
});
```

`ChromaticityDiagramSample` keeps its name, full-precision xy coordinates and ARGB
marker color. Its legend uses the same color. `SetSamples` takes an immutable
snapshot and replaces wavelength/light measurements. `Samples` is also bindable;
replace the list when changing points. `Clear` and `SetData` / `SetLightData` clear
named samples; changing display options preserves them.

`SpectrumPlot.Data` accepts immutable `SpectrumDiagramData`. `SelectedIndex`
supports two-way binding; `SelectedWavelength` and `SelectedValue` are observable
read-only properties. `SelectionChanged` reports changes. Click, touch or drag
inside the plot to select a measured point, or use Left/Right/Home/End while
focused. Selection uses the actual sampling grid; it does not interpolate values.
Changing data clears selection. Missing/nonfinite values are gaps and are skipped.
Y-axis limits include zero and round outward to multiples of 10: a peak of 117
uses 0–120, and a minimum of −13 uses −20. `MinimumMaximum` remains a configurable
floor for the upper limit (default 100 for percentage plots). `YAxisMinimum` /
`YAxisMaximum` expose the displayed limits; source values are unchanged.

For standard/sample comparison, bind `StandardData` alongside sample `Data`.
Both curves use a shared wavelength range and Y-axis range, including negative
values and peaks from either curve. `XAxisMinimum` / `XAxisMaximum` expose the
combined wavelength range. `CurveColor` defaults to dark gray;
`StandardCurveColor` defaults to orange. The thicker standard curve and larger
selection ring remain visible when curves or selected points coincide.
Selection follows the sample's actual grid, falling back to the standard's grid
when `Data` is null. `SelectedValue` remains the selected curve's source value;
`SelectedStandardValue` is the standard's measured value at that wavelength,
or null for a missing/different-grid point. No comparison values are interpolated.
Replacing either curve clears selection; `Clear()` clears both curves.
`SpectrumDiagramData.ValueAt(wavelength)` exposes the same measured-value lookup.
An attached `SpectrumNavigator` selects the first valid point after either curve
changes and labels both values in its comparison readout.

```csharp
var comparison = new SpectrumPlot
{
    Data = SpectrumDiagramData.FromUniform(new[] { 12.0, 18, 22 }, 400, 10),
    StandardData = SpectrumDiagramData.FromUniform(new[] { 15.0, 20, 25 }, 400, 10),
    CurveColor = Avalonia.Media.Color.Parse("#1778B5")
};
```

When attached, `SpectrumNavigator` selects the first valid point on load and when
data changes. Its buttons move one actual sample at a time, wrap at either end,
and skip gaps. The readout stays in the source units (reflectance % or relative
spectral power) and follows pointer/keyboard selections too. `Plot` is bindable;
buttons are disabled without valid data. The plot itself no longer repeats this
readout in its upper-left corner. For custom buttons, call
`spectrum.MoveSelection(-1, wrap: true)` for the previous sample or
`spectrum.MoveSelection(1, wrap: true)` for the next sample.

The LCh background uses L*=70 and the library's integrated D65 / 10° white.
It is a clipped sRGB screen preview, independent of measurement calculations.
The C*/h° plane preserves unrounded a*/b* and scales to include both colors.

`DiagramControl.LabelTypeface` and `Translate` let the host provide its own font
and label translation. The library has no Chromaticity.Tools resources, global
language events or Colorimeter services. Hosts should invalidate controls when
their language changes.
Call `SpectrumNavigator.RefreshLabels()` when the host language changes; it
uses its plot's translation and label font.

Targets: `net8.0`, `net10.0`; Avalonia 12.1.2 or compatible newer version.
Avalonia 12 requires .NET 8 or later and does not provide .NET Standard runtime
assemblies. The framework-independent geometry and data remain in the main
`ChromaticityDotNet` package, which supports `netstandard2.0`.
Code: MIT; CIE data: CC BY-SA 4.0.
