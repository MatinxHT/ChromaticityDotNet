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

[`apps/`](apps/README.md) 提供基于 Avalonia Browser 的反射光谱计算、批量色差计算、颜色空间转换和标准光源查询工具，复用本库已有数据与算法。网站采用独立 solution，由 Cloudflare Pages 拉取仓库构建部署，不向 NuGet 包引入 Avalonia 依赖。欢迎使用 [ChromaticityDotNet](https://chromaticitydotnet.martinphysics.club/?utm_source=GithubREADME)。

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
| `REFtoXYZ(Spectrum, Standardilluminant, StandardObserver)` | 主入口：反射率百分数转 XYZ，完全反射体归一化到 Y = 100 |
| `REFtoXYZ(Spectrum, Spectrum, StandardObserver)` | 使用自定义照明体光谱，前两个参数依次为反射率、照明体 |
| `SPDtoXYZ(Spectrum, StandardObserver)` | 自发光 SPD 的未归一化 XYZ 加权和，尺度约定见 [TODO](TODO.md) |
| `CieSpectralData.Illuminants` | 50 条光源的 ID、来源、原始波段、间隔和质量标注 |
| `CieSpectralData.GetIlluminantSpectrum(illuminant)` / `GetIlluminantSpectrum("D50")` | 获取旧枚举或完整目录中光源光谱的独立副本，保留原始采样网格 |
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
var xyz = ChromaticityConversion.REFtoXYZ(
    reflectance, Standardilluminant.D65, StandardObserver.Degree2); // Y = 18

// 直接传入照明体 Spectrum；也可替换为自定义光谱。
var light = CieSpectralData.GetIlluminantSpectrum("D50"); // 也可使用 "LED-B1"、"FL3.1"、"HP1" 等目录 ID
var customXyz = ChromaticityConversion.REFtoXYZ(reflectance, light, StandardObserver.Degree2);
var (xBar, yBar, zBar) = CieSpectralData.GetColorMatchingFunctions(StandardObserver.Degree2);

// 旧版快速计算：400–700 nm、10 nm 间隔，固定 31 点。
var fastXyz = ChromaticityConversion.REFtoXYZ(
    Enumerable.Repeat(18.0, 31).ToArray(), Standardilluminant.D65, StandardObserver.Degree2);
```

- 波长范围包含首尾；点数必须为 `(结束波长 - 起始波长) / 间隔 + 1`。间隔为正整数 nm，样本值必须有限且非负。
- 反射率以百分数输入，允许超过 100。按线性插值生成 1 nm 数据，在输入范围内求和；范围必须同时位于观察者和照明体覆盖区间内，不外推、不自动裁剪。
- 新入口的 XYZ 结果保留四位小数，中点向远离零的方向舍入。照明体在计算范围内的参考亮度必须大于零；无效输入和数值溢出会抛出异常。
- 两个裸数组 `double[]` 入口均保留旧表和旧算法，用于 **31 点、400–700 nm、10 nm 间隔的快速计算**。
- `SPDtoXYZ` 暂不重采样、不乘波长间隔、不做 Y 归一化；结果随采样间隔变化，不代表绝对光度 XYZ。

## 颜色空间转换

以下均为 `ChromaticityConversion` 的静态方法；颜色模型位于 `DataModel`。

| 函数 | 输入 → 输出 |
| --- | --- |
| `XYZ2Labch(xyz, illuminant, observer)` / `Labch2XYZ(lab, illuminant, observer)` | XYZ ↔ Lab；`CIELABCH` 自动计算 C*、h° |
| `XYZ2Luv(xyz, illuminant, observer)` / `Luv2XYZ(luv, illuminant, observer)` | XYZ ↔ L*u*v* |
| `XYZ2RGB(xyz)` / `RGB2XYZ(rgb)` | XYZ ↔ sRGB，RGB 为 0–255 字节 |
| `XYZ2xyY(xyz)` / `xy2XYZ(xyY)` | XYZ ↔ xyY |
| `xy2uv(xyY)` | xyY → CIE 1976 u′v′ 色度坐标 |
| `xy2CCT(xyY)` | 从 xy 色度近似计算相关色温 |

以下示例沿用上面的 `using`：

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

### 白点与精度

- Lab/Luv 的 XYZ 白点尺度为 Y = 100，双向转换应使用相同的照明体和观察者。它们仍使用旧版固定白点，可能与新光谱积分所得白点不同。
- 逆转换保留中间 `double` 精度，XYZ 输出保留四位小数，中点远离零舍入。参考测试每分量误差不超过 0.00005，XYZ → Lab/Luv → XYZ 往返测试容差为 0.0003。
- Lab/Luv 逆转换要求有限坐标和非负 L*，允许 L* 超过 100。Luv(0,0,0) 返回黑色；L* 为零但 u*/v* 非零、重建 v′ 非正或数值溢出时抛出异常。
- sRGB 使用 D65 色度 `(0.3127, 0.3290)`，白点 XYZ 为 `(95.0456, 100, 108.9058)`，与库内 D65/2° 固定白点 `(95.047, 100, 108.883)` 略有差异；不进行色适应。
- `XYZ2RGB` 会裁剪超出 sRGB 色域的颜色并舍入为字节，不能保证任意 XYZ 无损往返。

sRGB 转换矩阵参考 [W3C CSS Color 4](https://www.w3.org/TR/css-color-4/#color-conversion-code)，Luv 逆转换公式核对参考 [Colour 文档](https://colour.readthedocs.io/en/develop/_modules/colour/models/cie_luv.html#Luv_to_XYZ)。

## 色差计算

以下均为 `ChromaticityDeltaEFormulations` 的静态方法。`standard` 为标准色，`sample` 为样品色，
均使用 `CIELABCH`，并应采用相同的参考白点和观察者条件。

| 公式 / 函数 | 参数与计算约定 | 返回值 |
| --- | --- | --- |
| CIE76：`DeltaE1976(standard, sample)` | Lab 空间的欧氏距离，无额外权重参数 | `double` |
| CIE94：`DeltaE1994(standard, sample)` | 固定使用图形印刷参数：kL = kC = kH = 1，K1 = 0.045，K2 = 0.015；以标准色彩度计算权重 | `double` |
| CIEDE2000：`DeltaE2000(standard, sample, kL, kC, kH)` | 明度、彩度、色相权重需显式传入，通常为 1、1、1 | `ColorDifferenceEquationResults`，总色差取 `.DeltaE` |
| CMC(l:c)：`DeltaEcmc(standard, sample, pl, pc)` | `pl`、`pc` 分别为明度、彩度权重，常用 1:1 或 2:1；以标准色计算权重 | `double` |

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

## 许可

程序代码采用 [MIT](LICENSE)，CIE 数据采用 [CC BY-SA 4.0](CIE-DATA-NOTICE.md)。
