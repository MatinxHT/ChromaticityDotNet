# ChromaticityDotNet
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET C#](https://img.shields.io/badge/.NET-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/ChromaticityDotNet)

## About
ChromaticityDotNet is a .Net Core SDK for chromaticity programming. You can find some useful standard illuminant data in `ChromaticityDotNet.Model` and cover color space conversion and color difference calculations in `ChromaticityDotNet.Controller`. You can also perform any other tasks you need.

## Install [NuGet packages](https://www.nuget.org/packages/ChromaticityDotNet)
```bash
dotnet add package ChromaticityDotNet
```

## Now available
- ChromaticityConversion:CIE XYZ/Lab/LCH Cover
- ChromaticityDeltaEFormulations:CIE de formulations
- ChromaticityMatch:Get Get Standard illuminant WhitePoint data(color mathing is dev ing...)

## What can we find in this SDK?
- StandardilluminantClass:CIE standard illuminant data(D65/A/CWF)
- CIEConstant:Chromaticity(xy)/CCT/Standard Observer data

## Code Sample
- build up an Color obj
in `ChromaticityDotNet` Color Mdoels are fill in namespace `ChromaticityDotNet.Model.DataModel`.so we can use code as follow to build color obj:

```csharp
using static ChromaticityDotNet.Model.DataModel;
// add to the top

var lab = new CIELABCH()
{
CIEL = 95.2,
CIEA = 24.5,
CIEB = 12.34
};
```

## CMC color difference and precision

`ChromaticityDeltaEFormulations.DeltaEcmc(standard, sample, pl, pc)` uses the
standard color for all CMC weighting terms, including hue. `pl` and `pc` are
the lightness and chroma weights; common choices are 1:1 and 2:1. CMC is
asymmetric, so exchanging the standard and sample can change the result.

For standard `Lab(50, 20, 0)`, sample `Lab(50, 0, 20)`, and 1:1 weights,
the corrected result is **24.8752** (previously **28.9795**).

CMC keeps full `double` precision for intermediate calculations and rounds
only the final result to four decimal places using `MidpointRounding.AwayFromZero`.
Reference tests check both the rounded value and an absolute error of at most
0.00005 against independently computed values. This checks CMC accuracy for
the supplied Lab inputs; it does not recover precision already lost in earlier
color conversions. To display trailing zeros, format the returned value with `F4`.

## Inverse color conversions

All conversion methods are static members of `ChromaticityConversion` and use
the existing models in `DataModel`.

| Method | Input | Output and reference white |
| --- | --- | --- |
| `Labch2XYZ(labColor, illuminant, observer)` | `CIELABCH`: L*, a*, b*; derived C/h are ignored | XYZ under the selected illuminant and observer |
| `Luv2XYZ(luvColor, illuminant, observer)` | `CIELuv`: L*, u*, v* | XYZ under the selected illuminant and observer |
| `RGB2XYZ(rgbColor)` | `CIERGB`: gamma-encoded sRGB bytes, 0–255 | XYZ under sRGB D65 |

```csharp
using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;
using static ChromaticityDotNet.Model.StandardChromaticityModel.StandardilluminantClass;

CIEXYZ fromLab = ChromaticityConversion.Labch2XYZ(
    new CIELABCH(50.0, 20.0, -30.0),
    Standardilluminant.D65, StandardObserver.Degree2);
// XYZ: (21.4643, 18.4187, 40.4654)

CIEXYZ fromLuv = ChromaticityConversion.Luv2XYZ(
    new CIELuv { CIEL = 50.0, CIEu = 20.0, CIEv = -30.0 },
    Standardilluminant.D65, StandardObserver.Degree2);
// XYZ: (22.4406, 18.4187, 31.3083)

CIEXYZ fromRgb = ChromaticityConversion.RGB2XYZ(
    new CIERGB { redValue = 255, greenValue = 0, blueValue = 0 });
// XYZ: (41.2391, 21.2639, 1.9331)
CIERGB red = ChromaticityConversion.XYZ2RGB(fromRgb);
// RGB: (255, 0, 0)
```

### Units, precision, and valid inputs

- XYZ uses reference white **Y = 100**. Lab/Luv must use the same illuminant
  and observer when converting in either direction.
- Intermediate calculations retain full `double` precision. Each output XYZ
  component is rounded to four decimal places, with midpoints rounded away
  from zero. Reference fixtures check an absolute error of at most 0.00005
  per component. Round trips include rounding at both calls and can accumulate
  more error; the XYZ→Lab/Luv→XYZ fixtures use a 0.0003 tolerance per component.
- New inverse methods reject null inputs. Lab/Luv coordinates must be finite,
  L* must be nonnegative, and illuminant/observer enum values must be defined.
  Invalid coordinates or enum values throw `ArgumentOutOfRangeException`;
  null inputs throw `ArgumentNullException`.
- L* above 100 and extended colors are supported without clipping XYZ.
  Luv(0,0,0) maps to black. L*=0 with nonzero u*/v*, nonpositive reconstructed
  v′, and numeric overflow throw `ArgumentException`.
- sRGB conversions use its D65 chromaticity (x=0.3127, y=0.3290) and perform
  no chromatic adaptation. Its white is XYZ(95.0456, 100, 108.9058), which
  differs slightly from the library's tabulated D65/2° white
  (95.047, 100, 108.883). Use the appropriate white convention when comparing
  external reference values. `XYZ2RGB` clips colors outside the sRGB gamut
  and rounds to bytes, so arbitrary XYZ values cannot be recovered losslessly.

### Related numerical corrections

`XYZ2Labch` now uses the exact CIE piecewise constants, removing the previous
dark-region discontinuity. `XYZ2Luv` maps XYZ black to Luv(0,0,0).
`XYZ2RGB` and `RGB2XYZ` use matching rational matrices from
[W3C CSS Color 4](https://www.w3.org/TR/css-color-4/#color-conversion-code);
this can change byte results near rounding thresholds compared with the old
matrix. The Luv inverse was checked against the formula documented by
[Colour](https://colour.readthedocs.io/en/develop/_modules/colour/models/cie_luv.html#Luv_to_XYZ).

## Publishing

Pushes to `master` build and test the library with the .NET 10 SDK. For a new
`<Version>` in `ChromaticityDotNet.csproj`, the workflow publishes the NuGet
package, then creates a matching `v<version>` tag and GitHub Release with the
`.nupkg` attached. Release notes automatically include commit titles since the
previous reachable version tag, an installation command, and a NuGet link.
The first release includes the existing commit history. No separate changelog
file is required; write descriptive commit titles and bump the version for
each new package release.

An already published GitHub Release is skipped. Failed runs can be rerun:
duplicate NuGet versions are skipped and an existing draft release is resumed.
An existing tag on a different commit stops publication. Prerelease package
versions are marked as prereleases on GitHub. The workflow can also be started
manually on `master` from the Actions tab.

Publishing uses the existing `CHROMATICITYDOTNETAPI` repository secret for
NuGet and the built-in `GITHUB_TOKEN` with `contents: write` for GitHub Releases.

## What's more?
- give me a Star ~

## 免责申明

此项目(ChromaticityDotNet)为免费开源的代码，仅用于学习交流，非成熟的产品，并且不直接向中国提供服务，中国用户请于下载后立即删除。

任何中国境内的组织及个人不得使用此项目构建任何形式的面向中国境内用户的网站或服务。

不可用于任何违反中华人民共和国(含台湾省)或使用者所在地区法律法规的用途。

因为作者即本人仅完成代码的开发和开源活动(开源即任何人都可以下载使用)，从未参与用户的任何运营和盈利活动。

且不知晓用户后续将程序源代码用于何种用途，故用户使用过程中所带来的任何法律责任即由用户自己承担。

[《开源软件有漏洞，作者需要负责吗？是的！》](https://go.edi.wang/aka/os251)
