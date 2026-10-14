# ChromaticityDotNet.OxyplotDiagram

Framework-independent CIE xy and CIE 1976 u′v′ plots extracted from QuantumSpect.
Depends on `ChromaticityDotNet` and **OxyPlot.Core 2.2.0**. No WPF, Avalonia,
CommunityToolkit or application assembly is required.

```csharp
using ChromaticityDotNet.Diagrams;
using ChromaticityDotNet.OxyplotDiagram;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

var xy = new Cie1931Plot(StandardObserver.Degree10);
var uv = new Cie1976Plot(StandardObserver.Degree10);
var sample = new CIExyY { CIEx = 0.4, CIEy = 0.2, CIEY = 100 };
var data = WavelengthDiagramData.Create(sample, StandardObserver.Degree10, "D65");
xy.SetData(data);
uv.SetData(data);
// Bind each PlotModel and Controller to its platform-specific PlotView.
```

`Cie1931Plot.UpdatePoint` / `UpdatePoints` accept xyY. `Cie1976Plot.UpdatePoint`
/ `UpdatePoints` accept `CIEuv` (u′v′), **not** L*u*v* components. Both classes
support `UpdateXyPoints`, `SetObserver`, `ClearMeasurements` and `SetLightData`.
Create one instance per PlotView; models and mutable series are not shared.

The static series remain in QuantumSpect's original order: Planckian locus,
then CIE boundary. Observer changes update both curves and the sRGB background
and clear old measurements. Backgrounds are generated from the shared library
data, without WPF pack URIs or application bitmap resources. `BackgroundOpacity`
controls the background independently of curves and markers.

`ShowWavelengthLabels` controls edge wavelength captions (visible by default).
`ShowPlanckianLocus` controls the curve and measurement connection to its nearest
point (visible by default). `ShowTemperatureLabels` controls reference temperatures
and the measured CCT label (hidden by default). Reference temperatures can appear
independently of the curve. These settings survive observer and measurement updates.
`ShowLegend` defaults to true; the blackbody curve and current measurement markers
appear in the legend. Click an item to temporarily hide or restore it; hidden
entries stay gray and selectable in the legend. Hidden targets are excluded from
tracking. Hiding the blackbody curve also hides reference temperature labels and
its nearest-point overlay, while the configured display options remain unchanged.
Sample visibility resets when measurements are replaced; curve visibility persists
until toggled back or explicitly assigned through `ShowPlanckianLocus`.
`ToggleLegendItem(index)` also toggles an item in displayed legend order.
Both plotting libraries place the legend inside the plot: xy at the top right,
u′v′ at the bottom right. The shared default axes are xy 0–1.0 / 0–1.0 and u′v′
0–0.8 / 0–0.7. `AxisMaximumX` / `AxisMaximumY` can expand these ranges for larger
legends; null restores the defaults. Background image extents remain fixed in
coordinate space. OxyPlot's Cartesian layout may extend the displayed ranges
slightly to keep equal x/y scaling.

Bind `Controller` as well as `PlotModel` to the host PlotView. It explicitly maps
the left mouse button to snap tracking; press and drag to inspect samples or
curve vertices. Trackers display the name, xy / u′v′ coordinates, and wavelength /
temperature / Duv where available. `TrackerHitResult.Item` contains the shared
`ChromaticityDiagramTarget` with full-precision values. Measurement connectors
are excluded from tracking so they do not hide marker data.

For example, a WPF view model can expose both properties from its chart instance:

```xml
<oxy:PlotView Model="{Binding PlotModel}" Controller="{Binding Controller}" />
```

Use `SetSamples` for named points on the background. Both plots accept the same xy
samples and project them into their coordinate space; marker and legend colors match:

```csharp
xy.ShowWavelengthLabels = false;
xy.ShowPlanckianLocus = false;
xy.ShowTemperatureLabels = false;
xy.SetSamples(new[]
{
    new ChromaticityDiagramSample("Reference", new ChromaticityCoordinates(0.3127, 0.3290), 0xFF2563EB),
    new ChromaticityDiagramSample("Sample 1", new ChromaticityCoordinates(0.4, 0.2), 0xFFE11D48)
});
```

`SetSamples` replaces previous measurement markers and labels. `ClearMeasurements`
and observer changes clear the sample legend too. Existing unnamed `UpdatePoints`
calls use `Sample 1`, `Sample 2`, etc. for multiple points.

The 10° Planckian locus is an observer-specific estimate. CCT / Duv are calculated
in CIE 1960 UCS, then projected into the diagram; the drawn line length is not Duv.

Targets: `netstandard2.0`, `net10.0`. Code: MIT; CIE data: CC BY-SA 4.0.
