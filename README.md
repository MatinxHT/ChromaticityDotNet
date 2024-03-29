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

## What's more?
- give me a Star ~

## 免责申明

此项目(ChromaticityDotNet)为免费开源的代码，仅用于学习交流，非成熟的产品，并且不直接向中国提供服务，中国用户请于下载后立即删除。

任何中国境内的组织及个人不得使用此项目构建任何形式的面向中国境内用户的网站或服务。

不可用于任何违反中华人民共和国(含台湾省)或使用者所在地区法律法规的用途。

因为作者即本人仅完成代码的开发和开源活动(开源即任何人都可以下载使用)，从未参与用户的任何运营和盈利活动。

且不知晓用户后续将程序源代码用于何种用途，故用户使用过程中所带来的任何法律责任即由用户自己承担。

[《开源软件有漏洞，作者需要负责吗？是的！》](https://go.edi.wang/aka/os251)
