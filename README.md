# ChromaticityDotNet

中文（主文档） | [English](README.en.md)

**文档以中文版为准。**

[![Code: MIT](https://img.shields.io/badge/Code-MIT-yellow.svg)](LICENSE)
[![.NET C#](https://img.shields.io/badge/.NET-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
![NuGet](https://img.shields.io/nuget/vpre/ChromaticityDotNet)

用于相对光谱转换为颜色空间值以及色差计算的 .NET 库，以`netstandard2.0`为开发目标。

## 安装

```sh
dotnet add package ChromaticityDotNet
```

## 浏览器在线评估工具

[`apps/`](apps/README.md) 提供基于 Avalonia Browser 的反射光谱计算、批量色差计算、颜色空间转换、标准光源查询、色差分级色卡以及主波长与补色波长工具，复用本库已有数据与算法。网站采用独立 solution，由 Cloudflare Pages 拉取仓库构建部署，不向 NuGet 包引入 Avalonia 依赖。欢迎使用 [ChromaticityDotNet](https://chromaticitydotnet.martinphysics.club/?utm_source=GithubREADME)。

## 数据来源

带波长信息的光谱接口使用 [CIE 官方数据](https://www.cie.co.at/data-tables)：

| 数据 | 波长范围 / 间隔 | 来源 |
| --- | --- | --- |
| CIE 1931 2° / 1964 10° 配色函数 | 360–830 nm / 1 nm | [1931](https://doi.org/10.25039/CIE.DS.xvudnb9b)、[1964](https://doi.org/10.25039/CIE.DS.sqksu2n5) |
| 照明体 A / D65 | 300–830 nm / 1 nm | [A](https://doi.org/10.25039/CIE.DS.8jsxjrsn)、[D65](https://doi.org/10.25039/CIE.DS.hjfjmt59) |
| FL1–FL12 / FL3.1–FL3.15 | 380–780 nm / 1 nm | [荧光灯照明体](https://doi.org/10.25039/CIE.DS.54hy6srn) |

库内名称对应关系：**CWF → FL2、F7 → FL7、TL84 → FL11、U30 → FL12**。
FL 的官方 1 nm 数据标注为近似数据（`approximated`）；1964 配色函数中未定义的 z̄ 尾部值在计算时按零处理。

[`reference/`](reference/README.md) 按类别保存官方 CSV 和用于联网检查更新的 JSON。
CSV 供 Codex 和维护者核对、转入代码；运行时使用编译后的常量。完整光源目录已转入代码，共 50 条；包括 D50、C、D55/D75、ID50/65、L41、完整 FL、HP 和 LED 系列。各光源源表间隔为 1 或 5 nm，详情见 `CieSpectralData.Illuminants`。
数据许可及署名见 [CIE 数据说明](CIE-DATA-NOTICE.md)。

## 光谱转 XYZ

转换方法位于 `ChromaticityConversion`，`Spectrum` 位于 `DataModel`。

| 函数 | 用途 |
| --- | --- |
| `REFToXYZ(Spectrum, Standardilluminant, StandardObserver)` | 主入口：反射率百分数转 XYZ，完全反射体归一化到 Y = 100 |
| `REFToXYZ(Spectrum, Spectrum, StandardObserver)` | 使用自定义照明体光谱，前两个参数依次为反射率、照明体 |
| `SPDToXYZ(Spectrum, StandardObserver)` | 自发光 SPD 的未归一化 XYZ 加权和，尺度约定见 [TODO](TODO.md) |
| `CieSpectralData.Illuminants` | 50 条光源的 ID、来源、原始波段、间隔和质量标注 |
| `CieSpectralData.GetIlluminantSpectrum(illuminant)` / `GetIlluminantSpectrum("D50")` | 枚举和光源 ID 均支持全部 50 条光源，返回原始采样网格上的独立副本 |
| `CieSpectralData.GetIlluminantId(illuminant)` | 获取枚举对应的规范目录 ID，包含旧名称映射 |
| `ChromaticityMatch.GetStandardilluminantdata(illuminant)` | 保留旧接口：缓存 CIE 数据生成的 31 点光谱，以及原始光谱的 1 nm 积分白点 |
| `CieSpectralData.GetColorMatchingFunctions(observer)` | 获取配色函数光谱的独立副本 `(X, Y, Z)` |

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
    Spectrums = Enumerable.Repeat(18.0, 401).ToArray() // 18% 反射率
};
var xyz = ChromaticityConversion.REFToXYZ(
    reflectance, Standardilluminant.D65, StandardObserver.Degree2); // Y = 18

// 直接传入照明体 Spectrum；也可替换为自定义光谱。
var light = CieSpectralData.GetIlluminantSpectrum("D50"); // 也可使用 "LED-B1"、"FL3.1"、"HP1" 等目录 ID
var customXyz = ChromaticityConversion.REFToXYZ(reflectance, light, StandardObserver.Degree2);
var (xBar, yBar, zBar) = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);

// 旧版快速计算：400–700 nm、10 nm 间隔，固定 31 点。
var fastXyz = ChromaticityConversion.REFToXYZ(
    Enumerable.Repeat(18.0, 31).ToArray(), Standardilluminant.D65, StandardObserver.Degree2);
```

- 波长范围包含首尾；点数必须为 `(结束波长 - 起始波长) / 间隔 + 1`。间隔为正整数 nm，样本值必须有限且非负。
- 反射率以百分数输入，允许超过 100。按线性插值生成 1 nm 数据，在输入范围内求和；范围必须同时位于观察者和照明体覆盖区间内，不外推、不自动裁剪。
- 新入口的 XYZ 结果保留四位小数，中点向远离零的方向舍入。照明体在计算范围内的参考亮度必须大于零；无效输入和数值溢出会抛出异常。
- 两个裸数组 `double[]` 入口保留旧的 31 点配色函数和求和算法，用于 **31 点、400–700 nm、10 nm 间隔的快速计算**；反射率入口的照明体光谱现由 CIE 原始数据生成。
- `SPDToXYZ` 暂不重采样、不乘波长间隔、不做 Y 归一化；结果随采样间隔变化，不代表绝对光度 XYZ。

## 颜色空间转换

以下均为 `ChromaticityConversion` 的静态方法；颜色模型位于 `DataModel`。

| 函数 | 输入 → 输出 |
| --- | --- |
| `XYZToLab(xyz, illuminant, observer)` / `LabToXYZ(lab, illuminant, observer)` | XYZ ↔ Lab；`CIELAB` 自动计算 C*、h° |
| `LabToLch(lab)` | Lab → 只读 `CIELCH`；直接由 a*、b* 计算 C*、h°，保留中间精度 |
| `XYZToLuv(xyz, illuminant, observer)` / `LuvToXYZ(luv, illuminant, observer)` | XYZ ↔ L*u*v* |
| `XYZToRGB(xyz)` / `RGBToXYZ(rgb)` | XYZ ↔ sRGB，RGB 为 0–255 字节 |
| `RGBToHSL(rgb)` / `RGBToHSV(rgb)` | sRGB → `DataModel.CIEHSL` / `DataModel.CIEHSV`；H 为 [0,360) 度，S/L/V 为 [0,1] 比例 |
| `RGBToHex(rgb)` | sRGB → 大写 `#RRGGBB` 字符串 |
| `XYZToxyY(xyz)` / `xyToXYZ(xyY)` | XYZ ↔ xyY |
| `xyTouv(xyY)` | xyY → CIE 1976 u′v′ 色度坐标 |
| `xyToCCT(xyY)` | 从 xy 色度近似计算相关色温 |
| `xyYToWavelengths(color, observer, whitePoint / illuminant)` | xyY → 主波长、补色波长（nm，两位小数） |

HSL/HSV 基于编码后的 sRGB 通道，不先做线性化，返回值保留中间精度；界面可将 S/L/V 乘以 100 显示为百分数。黑色和灰色均返回有效结果，S = 0、H = 0（无确定色相时的占位值），白色也不会产生 NaN。HSL/HSV 不是 CIE 色彩空间，模型名称沿用本库的数据模型命名。

```csharp
var rgb = new CIERGB { redValue = 32, greenValue = 160, blueValue = 144 };
var hsl = ChromaticityConversion.RGBToHSL(rgb); // H=172.5°, S≈0.6667, L≈0.3765
var hsv = ChromaticityConversion.RGBToHSV(rgb); // H=172.5°, S=0.8, V≈0.6275
var hex = ChromaticityConversion.RGBToHex(rgb); // #20A090
```

以下示例沿用上面的 `using`：

```csharp
var lab = new CIELAB(50.0, 20.0, -30.0);
var fromLab = ChromaticityConversion.LabToXYZ(
    lab, Standardilluminant.D65, StandardObserver.Degree2);
// XYZ = (21.4643, 18.4187, 40.4654)

var fromRgb = ChromaticityConversion.RGBToXYZ(
    new CIERGB { redValue = 255, greenValue = 0, blueValue = 0 });
// XYZ = (41.2391, 21.2639, 1.9331)
var red = ChromaticityConversion.XYZToRGB(fromRgb); // RGB = (255, 0, 0)

var white = ChromaticityMatch.GetStandardWhitePoint(
    Standardilluminant.D65, StandardObserver.Degree2);
```

### 白点与精度

- Lab/Luv 的 XYZ 白点尺度为 Y = 100，双向转换应使用相同的照明体和观察者。`Standardilluminant` 枚举与光源 ID 字符串入口均使用目录原始光谱的 1 nm 积分白点，支持全部 50 条光源；也可直接传入 `CIEXYZ` 白点。
- 逆转换保留中间 `double` 精度，XYZ 输出保留四位小数，中点远离零舍入。参考测试每分量误差不超过 0.00005，XYZ → Lab/Luv → XYZ 往返测试容差为 0.0003。
- Lab/Luv 逆转换要求有限坐标和非负 L*，允许 L* 超过 100。Luv(0,0,0) 返回黑色；L* 为零但 u*/v* 非零、重建 v′ 非正或数值溢出时抛出异常。
- sRGB 使用 D65 色度 `(0.3127, 0.3290)`，白点 XYZ 为 `(95.0456, 100, 108.9058)`，与库内 D65/2° 积分白点约 `(95.0471, 100, 108.8829)` 略有差异；不进行色适应。
- `XYZToRGB` 会裁剪超出 sRGB 色域的颜色并舍入为字节，不能保证任意 XYZ 无损往返。

sRGB 转换矩阵参考 [W3C CSS Color 4](https://www.w3.org/TR/css-color-4/#color-conversion-code)，Luv 逆转换公式核对参考 [Colour 文档](https://colour.readthedocs.io/en/develop/_modules/colour/models/cie_luv.html#Luv_to_XYZ)。

`IStandardilluminant` 的属性和原有 `D65/A/CWF/F7/TL84/U30` 类均保留。`Spectrum` 固定为 400–700 nm、10 nm 间隔、31 点；`WhitePoint_Degree2/WhitePoint_Degree10` 使用同一光源的原始 CIE 光谱，在与观察者的完整共同波段内按 1 nm 网格积分，Y = 100，内部不舍入。原生 5 nm 数据先线性插值到 1 nm，不外推。每个光源首次访问时生成并缓存两种白点和 31 点光谱，属性返回独立副本。

原六个枚举数值 0–5 及字符串名称保持不变；`CWF/FL2`、`F7/FL7`、`TL84/FL11`、`U30/FL12` 保留新旧名称，并按规范光源 ID 共享缓存。新增规范名称使用独立枚举数值，避免改变旧名称的 `ToString()` 和字符串序列化。`Enum.GetValues` 返回 54 个名称，对应 50 条光源；枚举唯一光源时使用 `CieSpectralData.Illuminants`，或先映射为规范 ID 再 `Distinct()`。目录 ID 使用 `GetIlluminantId` 获取，例如 `FL3_1 → "FL3.1"`、`LED_B1 → "LED-B1"`，不要依赖枚举 `ToString()`。

这是数据行为变更：旧调用方式继续有效，但硬编码白点已替换为积分白点，旧光谱表中与 CIE 数据不同的点也已更新，相关 Lab/Luv、波长和快速反射率结果可能改变。31 点快速算法的完全反射体 XYZ 不保证等于完整光谱白点；需要让快速结果的灰色保持中性时，可用同一快速算法计算 100% 反射体 XYZ，再作为显式白点传给 Lab/Luv 转换。完整光谱计算则使用相同积分波段的白点。

```csharp
IStandardilluminant data = ChromaticityMatch.GetStandardilluminantdata(Standardilluminant.LED_B1);
var spectrum31 = data.Spectrum;                         // 400–700 nm / 10 nm
var white10 = data.WhitePoint_Degree10.WhitePointXnYnZn; // 原始光谱 / 1 nm / 完整共同波段
var compatible = new TL84();                            // 旧类继续使用，数据对应 FL11
```

### 从完整光源目录计算白点

`ChromaticityMatch.GetStandardWhitePoint("D50", observer)` 在光源与观察者的共同波段内，以 1 nm 网格积分 `S(λ) × x̄/ȳ/z̄(λ)`，再归一化为 Y = 100。原始 5 nm 光谱线性插值，不外推；白点保留完整 `double` 精度。也可传入自定义 `Spectrum`，或指定积分起止波长。

```csharp
var observer = StandardObserver.Degree10;
var fullWhite = ChromaticityMatch.GetStandardWhitePoint("LED-B1", observer);
var lab = ChromaticityConversion.XYZToLab(fullWhite, "LED-B1", observer); // (100, 0, 0)
var xyz = ChromaticityConversion.LabToXYZ(lab, "LED-B1", observer);
// 反射光谱的参考白点须使用相同波段。
var rangeWhite = ChromaticityMatch.GetStandardWhitePoint("D50", observer, 380, 780);
var reflected = ChromaticityConversion.REFToXYZ(reflectance, "D50", observer);
var reflectedLab = ChromaticityConversion.XYZToLab(reflected, rangeWhite);
```

光源 ID 与观察者必须有效。显式波段须同时被光源与观察者覆盖；参考亮度非正或数值溢出会抛出异常。应用的 01、03、06 使用目录积分白点；01 使用输入反射光谱的波段，03/06 使用光源与观察者的完整共同波段。

## 主波长与补色波长

`ChromaticityConversion.xyYToWavelengths` 接受 `DataModel.CIExyY`、观察者和参考白点。
第三个参数可为自定义 `CIExyY`、光源 ID 字符串（如 `"D50"`），或 `Standardilluminant`；字符串和枚举均使用原始光谱积分白点。转为 xy 时不先舍入。

```csharp
var sample = new CIExyY { CIEx = 0.3, CIEy = 0.6, CIEY = 100 };
var white = new CIExyY { CIEx = 0.3127, CIEy = 0.3290, CIEY = 100 };
var wavelengths = ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, white);
// DominantWavelength = 549.13 nm; ComplementaryWavelength = null
var d65Result = ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, Standardilluminant.D65);
// 积分 D65 白点与手填白点略有不同，计算使用其完整精度。
var d50Result = ChromaticityConversion.xyYToWavelengths(sample, StandardObserver.Degree2, "D50");
```

返回只读 `ChromaticityWavelengthResult`：`double? DominantWavelength`、`double? ComplementaryWavelength`、
`bool IsAchromatic`。正向（白点→样品）和反向分别求光谱轨迹交点；落在紫边的方向返回 `null`，
所以一个颜色可能同时有主波长和补色波长。紫色没有主波长；样品与白点距离 ≤ 1e-12 时两者均为 `null`，
并标记 `IsAchromatic`。定义参考 [CIE 主波长](https://cie.co.at/eilvterm/17-23-062)和
[补色波长](https://cie.co.at/eilvterm/17-23-063)。

- 使用编译后的 1931 2° / 1964 10° 配色函数生成 360–830 nm / 1 nm 未舍入 xy 轨迹；相邻轨迹点线性插值，
  仅最终波长按半值进一保留两位小数。两位小数为输出分辨率，不代表 0.01 nm 测量精度。
- x、y、Y 必须有限，Y ≥ 0；Y 不影响结果。样品必须在物理色域内或边界上，参考白点必须严格在内部。
  坐标和白点必须属于同一观察者；接口不执行色适应。
- 物理色域取光谱坐标的凸包，避免 10° 长波红端回折及数据微小不规则导致错误拒绝。
  光谱求交仍使用原始 1 nm 轨迹。红端不同波长可能对应相同或难以区分的 xy，重复交点取最短波长。
- `CieSpectralData.GetSpectralLocus(observer)` 和 `GetChromaticityBoundary(observer)` 返回只读轨迹和物理色域边界，
  供 API 与应用马蹄图共用。近白点波长对 xy 误差敏感，xy 不能恢复原始光谱。

Lab 模型现名为 `DataModel.CIELAB`，仍自动派生 C*、h°；对应转换 API 为 `XYZToLab`、`LabToXYZ`。
所有 `ChromaticityConversion` 转换 API 统一使用 `To`，包括 `SPDToXYZ`、`REFToXYZ`、`XYZToxyY`、`xyToXYZ`、`xyTouv`、`xyToCCT` 和 `xyYToWavelengths`。类型和方法更名属于源码兼容性变更，调用方需要同步更新。

`CIELAB` 与 `LabToLch` 共用安全的极坐标计算：缩放求模避免平方溢出/下溢，零彩度的 h° 为 0，色相归一化为 [0,360)。模型的 C*/h° 保留四位小数，舍入到 360° 时归零；`LabToLch` 保留完整精度。a*/b* 为 NaN/Infinity 或 C* 真正溢出时拒绝构造/更新，失败的属性更新保留原坐标及派生值。

## 色差计算

以下均为 `ChromaticityDeltaEFormulations` 的静态方法。`standard` 为标准色，`sample` 为样品色，
均使用 `CIELAB`，并应采用相同的参考白点和观察者条件。

| 公式 / 函数 | 参数与计算约定 | 返回值 |
| --- | --- | --- |
| CIE76：`DeltaE1976(standard, sample)` | Lab 空间的欧氏距离，无额外权重参数 | `double` |
| CIE94：`DeltaE1994(standard, sample)` | 固定使用图形印刷参数：kL = kC = kH = 1，K1 = 0.045，K2 = 0.015；以标准色彩度计算权重 | `double` |
| CIEDE2000：`DeltaE2000(standard, sample, kL, kC, kH)` | 明度、彩度、色相权重需显式传入，通常为 1、1、1 | `ColorDifferenceEquationResults`，总色差取 `.DeltaE` |
| CMC(l:c)：`DeltaEcmc(standard, sample, pl, pc)` | `pl`、`pc` 分别为明度、彩度权重，常用 1:1 或 2:1；以标准色计算权重 | `double` |

```csharp
using ChromaticityDotNet.Controller;
using static ChromaticityDotNet.Model.DataModel;

var standard = new CIELAB(50, 20, 0);
var sample = new CIELAB(50, 0, 20);

double de76 = ChromaticityDeltaEFormulations.DeltaE1976(standard, sample); // 28.2843
double de94 = ChromaticityDeltaEFormulations.DeltaE1994(standard, sample); // 21.7571
var result00 = ChromaticityDeltaEFormulations.DeltaE2000(standard, sample, 1, 1, 1);
double de00 = result00.DeltaE;
double cmc11 = ChromaticityDeltaEFormulations.DeltaEcmc(standard, sample, 1, 1); // 24.8752
double cmc21 = ChromaticityDeltaEFormulations.DeltaEcmc(standard, sample, 2, 1);
```

### CIEDE2000 返回字段

| 字段 | 含义 |
| --- | --- |
| `DeltaE` | 总色差 ΔE₀₀ |
| `DeltaLonly`、`DeltaConly`、`DeltaHonly` | 经权重归一化的明度、彩度、色相分量的绝对值 |
| `DL`、`DA`、`DB` | 样品减标准的原始 L*、a*、b* 差值 |
| `DC` | 样品减标准的原始彩度 C* 差值，未使用 CIEDE2000 修正彩度 |
| `DH` | 当前实现返回修正色相角 h′ 的直接差值（度），未折算为最短角度差，也不是公式中的 ΔH′ |

- CIE94 和 CMC 不对称，交换标准色与样品色可能改变结果。
- 权重应为有限正数；CIE94 当前不提供纺织参数切换。
- CIEDE2000 总色差包含彩度与色相的交叉项，不能仅由三个 `Delta*only` 分量平方和开方重建。
- 中间计算保留 `double` 精度，返回数值保留四位小数，中点远离零舍入；显示固定四位小数可用 `F4`。

## 颜色比较与英文评价

`ChromaticityMatch.CompareColors(CIELAB reference, CIELAB sample, ColorComparisonOptions? options = null)`
同时计算 ΔE1976、CMC、CIEDE2000，返回数值、枚举判断、分项简短英文评价，以及只读的输入和配置快照。
参数及结果类型位于 `ChromaticityDotNet.Model`；所有差值和方向均为 **sample 相对于 reference**。
两种 Lab 必须使用相同的白点、观察者条件；接口不执行色适应。

```csharp
using ChromaticityDotNet.Controller;
using ChromaticityDotNet.Model;
using static ChromaticityDotNet.Model.DataModel;

var reference = new CIELAB(50, 20, 20);
var sample = new CIELAB(52, 18, 24);

// 默认 CMC 1:1、E00 1:1:1，无彩色条件为 L* < 10 或 C* < 5。
var result = ChromaticityMatch.CompareColors(reference, sample);
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
    Cmc = new CmcParameters(l: 2, c: 1), // 例如纺织应用显式选择 2:1
    Ciede2000 = new Ciede2000Parameters(kL: 1, kC: 1, kH: 1),
    AchromaticLightnessThreshold = 10,
    AchromaticChromaThreshold = 5,
    Evaluation = new ColorEvaluationOptions
    {
        Formula = ComparisonFormula.Ciede2000,
        AcceptanceTolerance = 1.0, // 业务示例，不是通用验收标准
        PerceptibilityThreshold = 0.5,
        LightnessTolerance = 0.0001,
        ChromaTolerance = 0.0001,
        HueAngleToleranceDegrees = 0.0001
    }
};
// 可替换各轴坐标、名称及单一英文偏色描述，也可替换整份 HueAxes 列表。
options.HueAxes[0] = new HueAxis("Red", 20, "More reddish");
var configured = ChromaticityMatch.CompareColors(reference, sample, options);
```

| 结果属性 | 用途 |
| --- | --- |
| `Reference` / `Sample` | 只读 Lab、未舍入的 C*/h°、`IsAchromatic`；`ToLab()` 返回独立副本 |
| `DeltaE1976` / `DeltaECmc` / `DeltaE2000` | 三种总色差，沿用已有公式的四位小数输出 |
| `Differences` | `DeltaL`、`DeltaA`、`DeltaB`、`DeltaChroma`、`HueAngleDifferenceDegrees`，保留四位小数 |
| `Evaluation.Lightness` / `Chroma` | `Lower` / `Unchanged` / `Higher` 枚举及英文文字；使用未舍入差值与容差判断 |
| `Evaluation.Hue` | `NotApplicable` / `Unchanged` / `Shifted`、增减方向、样品最近主色轴和唯一目标主色轴 |
| `Evaluation.Neutrality` | 标准色、样品各自的无彩色判断及英文说明 |
| `Evaluation.Overall` | 选择的公式及其色差、验收和可感知阈值评价 |
| `AppliedParameters` | 实际权重、阈值、容差与按 h° 排序的只读主色轴列表 |
| `Evaluation.IsAchromatic` | 任一输入落入无彩色范围时为 `true` |
| `Evaluation.LightnessCommentsEnglish` / `ChromaCommentsEnglish` / `HueCommentsEnglish` | 独立简短评价，例如 `Lighter`、`Higher chroma`、`More yellowish`；无变化为 `No change`，色相不适用为 `Not applicable` |
| `Evaluation.AchromaticCommentsEnglish` / `TotalCommentsEnglish` | 无彩色判定和整体阈值评价；未设置整体阈值时为 `Not evaluated` |

接口不拼接评价段落；开发者按需引用数值、枚举或文字字段，组合和翻译由调用方完成。

### 无彩色与单一色相偏色规则

- 各颜色满足 **L* 严格低于明度阈值，或 C* 严格低于彩度阈值** 即为无彩色。
  默认 `L*=10, C*=5` 不属于无彩色。用未舍入的 C* 判断，避免边界值先舍入造成误判。
- 任一输入是无彩色时，不判断色相偏差，`HueAngleDifferenceDegrees` 和 `TargetAxis` 为 `null`。
  即使把两项阈值设为零，C*=0 仍没有色相，跳过偏色判断；三种色差、明度和彩度评价照常计算。
- 有彩色通过 `ChromaticityConversion.LabToLch` 获取 h°，计算 `sample.h - reference.h`，
  在色相环上折算到 `(-180, 180]`。例如 359° → 1° 为 +2°，相差 180° 统一取正方向。
  这与旧 E00 返回字段 `DH` 的修正色相角直接差值不同。
- 默认主色轴为 Red 22°、Yellow 85°、Green 158°、Blue 263°、Purple 310°。
  h 增大取样品 h 前方最近的主色轴，h 减小取后方最近的主色轴；与样品 h 重合的轴跳过。
  例如 40° → 50° 输出 `More yellowish`，60° → 50° 输出 `More reddish`。
  只输出一个目标轴的偏色描述，不同时输出两种偏色；仅彩度变化而 h 不变时不报告偏色。
- `HueAxes` 至少有两项，名称不区分大小写时不能重复，坐标在 `[0, 360)` 且不能重复；输入顺序不限。
  `SampleMainAxis` 仅供查看样品最近的轴，以环形距离判断，等距时取角度较小者。
  偏色名称和坐标是可配置的库规则，不用于反推配方或荧光情况。

权重必须有限且大于零，按传入数值使用，不约分。阈值和容差必须有限且非负，色相角容差不超过 180°。
Lab 坐标必须有限、L* 非负，允许 L* 超过 100；计算溢出时抛出异常。
默认不指定验收或可感知阈值，对应状态为 `NotEvaluated`；调用方配置后，验收按 `ΔE ≤ AcceptanceTolerance`，
可感知按 `ΔE ≥ PerceptibilityThreshold` 判断。修改权重不改变 Lab 的方向判断。
配置和输入不会被修改，返回结果也不会随调用方之后的修改而改变；二次开发应根据枚举和数值判断，不解析英文句子。

## 许可

程序代码采用 [MIT](LICENSE)，CIE 数据采用 [CC BY-SA 4.0](CIE-DATA-NOTICE.md)。
