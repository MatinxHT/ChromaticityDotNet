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
// Bind xy.PlotModel and uv.PlotModel to platform-specific PlotViews.
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

The 10° Planckian locus is an observer-specific estimate. CCT / Duv are calculated
in CIE 1960 UCS, then projected into the diagram; the drawn line length is not Duv.

Targets: `netstandard2.0`, `net10.0`. Code: MIT; CIE data: CC BY-SA 4.0.
