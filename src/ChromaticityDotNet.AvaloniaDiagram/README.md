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
// Add both spectrum and navigation to the host's layout.
spectrum.SetData(new[] { 18.0, 20.0, 22.0 }, 400, 10);
spectrum.SelectWavelength(413); // Selects the measured 410 nm point: 20%.
```

Use `ChromaticityDiagram.Space` to select xy / u′v′, `WavelengthData` / `SetData`
for sample/white rays, or `LightData` / `SetLightData` for a light-source marker.
These are bindable styled properties. `Clear(observer)` clears old markers.

`SpectrumPlot.Data` accepts immutable `SpectrumDiagramData`. `SelectedIndex`
supports two-way binding; `SelectedWavelength` and `SelectedValue` are observable
read-only properties. `SelectionChanged` reports changes. Click, touch or drag
inside the plot to select a measured point, or use Left/Right/Home/End while
focused. Selection uses the actual sampling grid; it does not interpolate values.
Changing data clears selection. Missing/nonfinite values are gaps and are skipped.

When attached, `SpectrumNavigator` selects the first valid point on load and when
data changes. Its buttons move one actual sample at a time, wrap at either end,
and skip gaps. The readout stays in the source units (reflectance % or relative
spectral power) and follows pointer/keyboard selections too. `Plot` is bindable;
buttons are disabled without valid data. For custom buttons, call
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
