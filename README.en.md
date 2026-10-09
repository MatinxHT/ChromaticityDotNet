# ChromaticityDotNet

[中文（主文档）](README.md) | English

**The Chinese README is authoritative.**

[![Code: MIT](https://img.shields.io/badge/Code-MIT-yellow.svg)](LICENSE)
[![.NET C#](https://img.shields.io/badge/.NET-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
![NuGet](https://img.shields.io/nuget/vpre/ChromaticityDotNet)

A .NET library for converting relative spectra into color-space values and calculating color differences, targeting `netstandard2.0`.

## Installation

```sh
dotnet add package ChromaticityDotNet
```

## Online evaluation tools

[`apps/`](apps/README.md) provides browser-based tools built with Avalonia Browser for reflectance-spectrum calculations, batch color-difference calculations, color-space conversions, and standard illuminant queries. These tools reuse this library's existing data and algorithms. The website uses a separate solution and is built and deployed by Cloudflare Pages through Git integration, without adding Avalonia dependencies to the NuGet package. Try [ChromaticityDotNet online](https://chromaticitydotnet.martinphysics.club/?utm_source=GithubREADME).

## Data sources

The wavelength-aware spectral API uses [official CIE datasets](https://www.cie.co.at/data-tables):

| Data | Wavelength range / interval | Source |
| --- | --- | --- |
| CIE 1931 2° / 1964 10° color-matching functions | 360–830 nm / 1 nm | [1931](https://doi.org/10.25039/CIE.DS.xvudnb9b), [1964](https://doi.org/10.25039/CIE.DS.sqksu2n5) |
| Illuminants A / D65 | 300–830 nm / 1 nm | [A](https://doi.org/10.25039/CIE.DS.8jsxjrsn), [D65](https://doi.org/10.25039/CIE.DS.hjfjmt59) |
| FL1–FL12 / FL3.1–FL3.15 | 380–780 nm / 1 nm | [Fluorescent illuminants](https://doi.org/10.25039/CIE.DS.54hy6srn) |

Library aliases: **CWF → FL2, F7 → FL7, TL84 → FL11, U30 → FL12**.
CIE marks the official FL 1 nm data as `approximated`; undefined z̄ tail values in the 1964 matching functions are treated as zero in calculations.

[`reference/`](reference/README.md) stores official CSVs and JSON records for online update checks, organized by category.
The CSV files serve as references for Codex and maintainers when verifying data and incorporating them into code; runtime calculations use compiled constants. The complete catalog of 50 illuminants has been incorporated into the code, including D50, C, D55/D75, ID50/65, L41, and the full FL, HP, and LED series. The source tables use sampling intervals of 1 or 5 nm; see `CieSpectralData.Illuminants` for metadata.
See [CIE data attribution](CIE-DATA-NOTICE.md) for data licensing and credits.

## Spectral conversion to XYZ

Conversion methods belong to `ChromaticityConversion`; `Spectrum` belongs to `DataModel`.

| Method | Purpose |
| --- | --- |
| `REFtoXYZ(Spectrum, Standardilluminant, StandardObserver)` | Primary entry point: reflectance percentages → XYZ, normalized to Y = 100 for a perfect reflector |
| `REFtoXYZ(Spectrum, Spectrum, StandardObserver)` | Use a custom illuminant spectrum; the first two arguments are reflectance and illuminant, respectively |
| `SPDtoXYZ(Spectrum, StandardObserver)` | Unnormalized XYZ weighted sums for self-luminous SPD; scale conventions remain a [TODO](TODO.md) |
| `CieSpectralData.Illuminants` | IDs, source files, native wavelength ranges, sampling intervals, and quality metadata for 50 illuminants |
| `CieSpectralData.GetIlluminantSpectrum(illuminant)` / `GetIlluminantSpectrum("D50")` | Get an independent copy of an illuminant spectrum using the legacy enum or a catalog ID, preserving its native sampling grid |
| `CieSpectralData.GetColorMatchingFunctions(observer)` | Get independent copies of the matching-function spectra `(X, Y, Z)` |

```csharp
using System.Linq;
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

var reflectance = new Spectrum
{
    StartingWavelength = 380,
    EndingWavelength = 780,
    WavelengthInterval = 1,
    Spectrums = Enumerable.Repeat(18.0, 401).ToArray() // 18% reflectance
};
var xyz = ChromaticityConversion.REFtoXYZ(
    reflectance, Standardilluminant.D65, StandardObserver.Degree2); // Y = 18

// Pass an illuminant Spectrum directly, or replace it with a custom spectrum.
var light = CieSpectralData.GetIlluminantSpectrum("D50"); // Also accepts catalog IDs such as "LED-B1", "FL3.1", and "HP1".
var customXyz = ChromaticityConversion.REFtoXYZ(reflectance, light, StandardObserver.Degree2);
var (xBar, yBar, zBar) = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);

// Legacy fast calculation: exactly 31 points, 400–700 nm at 10 nm intervals.
var fastXyz = ChromaticityConversion.REFtoXYZ(
    Enumerable.Repeat(18.0, 31).ToArray(), Standardilluminant.D65, StandardObserver.Degree2);
```

- Wavelength endpoints are inclusive; the sample count must equal `(end - start) / interval + 1`. The interval must be a positive integer in nm, and sample values must be finite and nonnegative.
- Reflectance is supplied as percentages; values above 100 are allowed. Samples are linearly interpolated to 1 nm and summed over the input range, which must lie within both observer and illuminant coverage. No extrapolation or automatic clipping is applied.
- The new entry points round XYZ results to four decimal places, with midpoints rounded away from zero. The illuminant's reference luminance over the calculation range must be positive; invalid inputs and numeric overflow throw exceptions.
- Both `double[]` entry points retain the original tables and algorithms for **fast 31-point calculations over 400–700 nm at 10 nm intervals**.
- `SPDtoXYZ` currently does not resample, multiply by the wavelength interval, or normalize Y. Results depend on the sampling interval and do not represent absolute photometric XYZ.

## Color-space conversions

All methods below are static members of `ChromaticityConversion`; color models belong to `DataModel`.

| Method | Input → output |
| --- | --- |
| `XYZ2Labch(xyz, illuminant, observer)` / `Labch2XYZ(lab, illuminant, observer)` | XYZ ↔ Lab; `CIELABCH` automatically calculates C* and h° |
| `LabToLch(lab)` | Lab → read-only `CIELCH`; derives C* and h° directly from a*/b* without intermediate rounding |
| `XYZ2Luv(xyz, illuminant, observer)` / `Luv2XYZ(luv, illuminant, observer)` | XYZ ↔ L*u*v* |
| `XYZ2RGB(xyz)` / `RGB2XYZ(rgb)` | XYZ ↔ sRGB, with RGB components stored as bytes from 0 to 255 |
| `RGBToHSL(rgb)` / `RGBToHSV(rgb)` | sRGB → `DataModel.CIEHSL` / `DataModel.CIEHSV`; H in [0,360) degrees, S/L/V in [0,1] |
| `RGBToHex(rgb)` | sRGB → uppercase `#RRGGBB` string |
| `XYZ2xyY(xyz)` / `xy2XYZ(xyY)` | XYZ ↔ xyY |
| `xy2uv(xyY)` | xyY → CIE 1976 u′v′ chromaticity coordinates |
| `xy2CCT(xyY)` | Approximate correlated color temperature from xy chromaticity |

HSL/HSV use gamma-encoded sRGB channels without linearization and retain intermediate precision. Multiply S/L/V by 100 for percentage display. Black and gray return valid coordinates with S = 0 and H = 0 as an undefined-hue placeholder; white also returns finite values. HSL/HSV are not CIE color spaces; their model names follow the library's naming convention.

```csharp
var rgb = new CIERGB { redValue = 32, greenValue = 160, blueValue = 144 };
var hsl = ChromaticityConversion.RGBToHSL(rgb); // H=172.5°, S≈0.6667, L≈0.3765
var hsv = ChromaticityConversion.RGBToHSV(rgb); // H=172.5°, S=0.8, V≈0.6275
var hex = ChromaticityConversion.RGBToHex(rgb); // #20A090
```

The following example uses the same `using` directives as above:

```csharp
var lab = new CIELABCH(50.0, 20.0, -30.0);
var fromLab = ChromaticityConversion.Labch2XYZ(
    lab, Standardilluminant.D65, StandardObserver.Degree2);
// XYZ = (21.4643, 18.4187, 40.4654)

var fromRgb = ChromaticityConversion.RGB2XYZ(
    new CIERGB { redValue = 255, greenValue = 0, blueValue = 0 });
// XYZ = (41.2391, 21.2639, 1.9331)
var red = ChromaticityConversion.XYZ2RGB(fromRgb); // RGB = (255, 0, 0)

var white = ChromaticityMatch.GetStandardWhitePoint(
    Standardilluminant.D65, StandardObserver.Degree2);
```

### White points and precision

- Lab/Luv conversions use an XYZ reference white with Y = 100. Use the same illuminant and observer in both directions. These conversions still use legacy fixed white points, which may differ from whites obtained by the new spectral integration.
- Inverse conversions retain `double` precision internally and round XYZ outputs to four decimal places, with midpoints rounded away from zero. Reference tests allow an error of at most 0.00005 per component; XYZ → Lab/Luv → XYZ round-trip tests use a tolerance of 0.0003.
- Lab/Luv inverse conversions require finite coordinates and nonnegative L*; L* above 100 is allowed. Luv(0,0,0) returns black. Zero L* with nonzero u*/v*, nonpositive reconstructed v′, or numeric overflow throws an exception.
- sRGB uses D65 chromaticity `(0.3127, 0.3290)` and an XYZ white point of `(95.0456, 100, 108.9058)`, which differs slightly from the library's fixed D65/2° white point `(95.047, 100, 108.883)`. No chromatic adaptation is performed.
- `XYZ2RGB` clips colors outside the sRGB gamut and rounds to bytes, so arbitrary XYZ values cannot be round-tripped losslessly.

The sRGB conversion matrices follow [W3C CSS Color 4](https://www.w3.org/TR/css-color-4/#color-conversion-code); the Luv inverse formula was checked against the [Colour documentation](https://colour.readthedocs.io/en/develop/_modules/colour/models/cie_luv.html#Luv_to_XYZ).

## Color differences

All methods below are static members of `ChromaticityDeltaEFormulations`. `standard` is the reference color and `sample` is the test color.
Both use `CIELABCH` and should share the same reference white and observer conditions.

| Formula / method | Parameters and calculation conventions | Return value |
| --- | --- | --- |
| CIE76: `DeltaE1976(standard, sample)` | Euclidean distance in Lab; no additional weighting parameters | `double` |
| CIE94: `DeltaE1994(standard, sample)` | Fixed graphic-arts parameters: kL = kC = kH = 1, K1 = 0.045, K2 = 0.015; weights use the standard color's chroma | `double` |
| CIEDE2000: `DeltaE2000(standard, sample, kL, kC, kH)` | Explicit lightness, chroma, and hue weights, usually 1, 1, 1 | `ColorDifferenceEquationResults`; total color difference is `.DeltaE` |
| CMC(l:c): `DeltaEcmc(standard, sample, pl, pc)` | `pl` and `pc` are lightness and chroma weights, commonly 1:1 or 2:1; weights use the standard color | `double` |

```csharp
using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;

var standard = new CIELABCH(50, 20, 0);
var sample = new CIELABCH(50, 0, 20);

double de76 = ChromaticityDeltaEFormulations.DeltaE1976(standard, sample); // 28.2843
double de94 = ChromaticityDeltaEFormulations.DeltaE1994(standard, sample); // 21.7571
var result00 = ChromaticityDeltaEFormulations.DeltaE2000(standard, sample, 1, 1, 1);
double de00 = result00.DeltaE;
double cmc11 = ChromaticityDeltaEFormulations.DeltaEcmc(standard, sample, 1, 1); // 24.8752
double cmc21 = ChromaticityDeltaEFormulations.DeltaEcmc(standard, sample, 2, 1);
```

### CIEDE2000 result fields

| Field | Meaning |
| --- | --- |
| `DeltaE` | Total color difference ΔE₀₀ |
| `DeltaLonly`, `DeltaConly`, `DeltaHonly` | Absolute values of the weighted, normalized lightness, chroma, and hue components |
| `DL`, `DA`, `DB` | Raw L*, a*, b* differences: sample minus standard |
| `DC` | Raw C* chroma difference: sample minus standard, without the CIEDE2000 chroma correction |
| `DH` | The current implementation returns the direct difference between corrected hue angles h′, in degrees; it is neither wrapped to the shortest angular difference nor the formula's ΔH′ term |

- CIE94 and CMC are asymmetric; swapping standard and sample may change the result.
- Weights should be finite and positive. CIE94 currently has no option to switch to textile parameters.
- CIEDE2000 includes a chroma–hue cross term; its total cannot be reconstructed solely as the square root of the sum of squares of the three `Delta*only` components.
- Intermediate calculations retain `double` precision. Returned values are rounded to four decimal places, with midpoints rounded away from zero; use `F4` to display exactly four decimal places.

## Color comparison and English evaluation

`ChromaticityMatch.CompareColors(CIELABCH reference, CIELABCH sample, ColorComparisonOptions? options = null)`
calculates CIE76, CMC and CIEDE2000 together. It returns numeric results, structured enum evaluations,
short independent English comments, and immutable input/parameter snapshots. Types are in `ChromaticityDotNet.Model`.
Every direction and difference describes **sample relative to reference**. Inputs must share the same
white point and observer; no chromatic adaptation is performed.

```csharp
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;

var reference = new CIELABCH(50, 20, 20);
var sample = new CIELABCH(52, 18, 24);
var result = ChromaticityMatch.CompareColors(reference, sample);
// Defaults: CMC 1:1, CIEDE2000 1:1:1; achromatic when L* < 10 OR C* < 5.
double de76 = result.DeltaE1976;
double cmc = result.DeltaECmc;
double de00 = result.DeltaE2000;
string lightness = result.Evaluation.LightnessCommentsEnglish; // Lighter
string chroma = result.Evaluation.ChromaCommentsEnglish;       // Higher chroma
string hue = result.Evaluation.HueCommentsEnglish;             // More yellowish
bool achromatic = result.Evaluation.IsAchromatic;
string neutrality = result.Evaluation.AchromaticCommentsEnglish; // Both chromatic
string overall = result.Evaluation.TotalCommentsEnglish;         // Not evaluated

var options = new ColorComparisonOptions
{
    Cmc = new CmcParameters(l: 2, c: 1), // Explicitly choose 2:1 for a textile application, for example.
    Ciede2000 = new Ciede2000Parameters(kL: 1, kC: 1, kH: 1),
    AchromaticLightnessThreshold = 10,
    AchromaticChromaThreshold = 5,
    Evaluation = new ColorEvaluationOptions
    {
        Formula = ComparisonFormula.Ciede2000,
        AcceptanceTolerance = 1.0, // Application example, not a universal acceptance standard.
        PerceptibilityThreshold = 0.5,
        LightnessTolerance = 0.0001,
        ChromaTolerance = 0.0001,
        HueAngleToleranceDegrees = 0.0001
    }
};
options.HueAxes[0] = new HueAxis("Red", 20, "More reddish");
var configured = ChromaticityMatch.CompareColors(reference, sample, options);
```

| Result property | Purpose |
| --- | --- |
| `Reference` / `Sample` | Read-only Lab, unrounded C*/h°, and `IsAchromatic`; `ToLab()` returns an independent copy |
| `DeltaE1976` / `DeltaECmc` / `DeltaE2000` | Total differences, using the existing formulas' four-decimal outputs |
| `Differences` | `DeltaL`, `DeltaA`, `DeltaB`, `DeltaChroma`, `HueAngleDifferenceDegrees`, rounded to four decimals |
| `Evaluation.Lightness` / `Chroma` | `Lower` / `Unchanged` / `Higher` and English text; decisions use unrounded differences |
| `Evaluation.Hue` | `NotApplicable` / `Unchanged` / `Shifted`, increasing/decreasing direction, nearest sample axis, and one target axis |
| `Evaluation.Neutrality` | Separate reference/sample achromatic flags and English text |
| `Evaluation.Overall` | Selected formula and its difference, acceptance and perceptibility statuses |
| `AppliedParameters` | Actual weights, thresholds, tolerances, and read-only axes sorted by h° |
| `Evaluation.IsAchromatic` | True when either input falls in the configured achromatic range |
| `Evaluation.LightnessCommentsEnglish` / `ChromaCommentsEnglish` / `HueCommentsEnglish` | Independent short comments, such as `Lighter`, `Higher chroma`, `More yellowish`; unchanged is `No change`, inapplicable hue is `Not applicable` |
| `Evaluation.AchromaticCommentsEnglish` / `TotalCommentsEnglish` | Achromatic classification and overall threshold assessment; `Not evaluated` when no overall thresholds were supplied |

The API does not assemble a paragraph. Consumers choose numeric, enum or comment fields and compose or translate them as needed.

### Achromatic and single hue-bias rules

- A color is achromatic when **L* is strictly below its threshold OR C* is strictly below its threshold**.
  With the defaults, L*=10 and C*=5 is chromatic. Classification uses unrounded C*.
- If either input is achromatic, hue bias is skipped; `HueAngleDifferenceDegrees` and `TargetAxis` are null.
  Zero chroma also skips hue bias even with both thresholds set to zero. All three differences and L/C evaluations still run.
- Chromatic colors use h° from `ChromaticityConversion.LabToLch`. The difference is sample.h minus reference.h,
  wrapped to `(-180, 180]`: 359° → 1° is +2°; a 180° tie uses increasing hue.
  This differs from the legacy E00 `DH` field's direct corrected-hue difference.
- Default axes: Red 22°, Yellow 85°, Green 158°, Blue 263°, Purple 310°.
  Increasing h selects the first axis ahead of the sample hue; decreasing h selects the first axis behind it.
  An axis exactly at the sample hue is skipped. For example, 40° → 50° yields `More yellowish`,
  while 60° → 50° yields `More reddish`. Exactly one bias phrase is emitted; chroma-only changes do not produce hue bias.
- `HueAxes` requires at least two axes, unique names (case-insensitive), and distinct angles in `[0, 360)`.
  Input order is unrestricted. `SampleMainAxis` reports the nearest axis by circular distance,
  with ties resolved by the lower angle. Axis coordinates and labels are configurable library rules.

Weights must be finite and positive and are used without normalization. Thresholds/tolerances must be finite and nonnegative;
hue tolerance must not exceed 180°. Lab coordinates must be finite with nonnegative L*; L* above 100 is allowed.
Calculation overflow throws an exception. Acceptance and perceptibility are `NotEvaluated` by default.
When configured, acceptance uses `DeltaE <= AcceptanceTolerance`; perceptibility uses `DeltaE >= PerceptibilityThreshold`.
Weights do not change Lab direction decisions. Input/options mutations after the call cannot change the returned result.
Consumers should use enums and numeric fields rather than parse the English text.

## License

The software is licensed under [MIT](LICENSE); CIE data are licensed under [CC BY-SA 4.0](CIE-DATA-NOTICE.md).
