# 浏览器色彩工具

四个 Avalonia Browser 工具复用仓库根目录的 `ChromaticityDotNet` 项目，使用现有算法和数据；不引入新的照明体、色适应或颜色公式。所有用户输入都在浏览器本地计算。

界面采用黑白灰主题，首页仅保留四张工具入口卡片与必要的项目/许可链接；卡片整块可点击，宽屏双列、窄屏单列。工具页直接显示表单与结果，sRGB 色块保留实际颜色，曲线、控件状态及错误提示使用灰阶。光谱图在 380–780 nm 区域使用淡色可见光背景；背景仅为波长的屏幕近似，不影响计算。

## 工程与发布边界

- `Chromaticity.Tools`：Avalonia 界面、输入校验、结果展示及 CSV 导出。
- `Chromaticity.Tools.Browser`：.NET 10 WebAssembly 启动与静态资源。
- `Chromaticity.Tools.Tests`：输入解析、参考计算、条件约束、批量模式和导出测试。
- `Chromaticity.Tools.sln`：独立应用 solution；原来的根 solution 仍只构建库和原测试。
- `Chromaticity.Tools.Browser/wwwroot`：首页与四个独立 HTML 工具入口。
- `site`：Pages 响应头；`prepare_site.py` 将其与 Browser 发布文件组成站点。

根项目通过 `DefaultItemExcludes` 排除整个 `apps/**`，应用项目均为 `IsPackable=false`，Avalonia 依赖不会进入 NuGet 库。

`.github/workflows/dotnet.yml` 保留库构建、数据检查、测试、NuGet 和 GitHub Release 步骤。仅改 `apps/**` 或网站工作流时不触发它。网站工作流单独构建、测试和发布，不使用 NuGet 发布密钥，也不创建 Release。

## 本地运行

需要 .NET 10 SDK，以及官方 `wasm-tools` 工作负载。以下命令从仓库根目录运行：

```sh
dotnet workload install wasm-tools
dotnet run --project apps/Chromaticity.Tools.Browser/Chromaticity.Tools.Browser.csproj
```

默认地址为 `http://localhost:5235`，先显示工具选择首页。开发与发布使用相同路径，不再使用 `?tool=` 参数。

`wasm-tools` 必须安装在**实际运行构建的 SDK** 中。在 VS Code 终端运行 `dotnet --info` 和 `dotnet workload list` 确认；其他目录中的临时 SDK 即使已安装工作负载，也不会补齐系统 SDK。macOS/Linux 的系统 SDK 若提示权限不足，使用 `sudo dotnet workload install wasm-tools` 安装。项目显式启用原生链接，并在缺少工作负载时中止构建，避免生成启动后报 `DllNotFoundException: libSkiaSharp` 的页面。安装完成后停止旧服务，执行 `dotnet clean apps/Chromaticity.Tools.Browser/Chromaticity.Tools.Browser.csproj`，再重新启动。

生产站点首页位于 `/`，四个工具分别位于 `/tools/spectrum.html`、`/tools/difference.html`、`/tools/conversion.html`、`/tools/illuminant.html`。每个 URL 都有独立 HTML 入口，支持直接访问、刷新及在多个标签页同时计算；首页和工具导航均使用普通链接，点击即在新标签页打开，保留当前输入与结果。各入口共用根目录下的 WASM 与静态资源。`/tools/` 同样显示工具选择首页。

工具页脚从实际加载的 ChromaticityDotNet 程序集读取版本号，不手工维护版本字符串。

```sh
dotnet test apps/Chromaticity.Tools.Tests/Chromaticity.Tools.Tests.csproj -c Release -p:GeneratePackageOnBuild=false
dotnet publish apps/Chromaticity.Tools.Browser/Chromaticity.Tools.Browser.csproj -c Release -p:GeneratePackageOnBuild=false
python3 apps/prepare_site.py
python3 -m http.server 8080 --directory artifacts/site
```

打开 `http://localhost:8080`。再次暂存站点时传入新的输出目录，例如 `python3 apps/prepare_site.py artifacts/site-preview`；脚本拒绝覆盖已有目录。

## VS Code 启动

用 VS Code 打开仓库根目录，安装 .NET 10 SDK、`wasm-tools` 工作负载及 Chrome。
在“运行和调试”中选择唯一的 `Tools · 启动工具首页`，按 F5，再从首页选择工具。
启动配置会在 VS Code 管理的调试终端内构建并启动 `http://localhost:5235`，服务就绪后自动打开独立 Chrome 调试窗口。`.vscode/tasks.json` 另提供 `tools: build` 构建任务。
这些配置使用 VS Code 自带的 Chrome 调试器，适合页面功能测试和 JavaScript 调试，不包含 C# WASM 断点调试。

关闭调试浏览器的最后一个窗口，会联动停止服务；点击 VS Code 的停止按钮也会关闭调试浏览器。多个工具标签页共用这次服务，关闭其中一个标签页时其他页面仍可使用。修改 C# 后停止调试，再按 F5 重新构建启动。浏览器使用 `artifacts/tools-debug/chrome-profile` 独立配置目录，避免接管日常浏览器窗口。旧配置留下的 `tools: serve` 后台任务需先在“任务: 终止任务”中结束一次。

参考：[VS Code 浏览器调试](https://code.visualstudio.com/docs/nodejs/browser-debugging)、[调试启动配置](https://code.visualstudio.com/docs/debugtest/debugging-configuration)。

## Cloudflare Pages

1. 在 Cloudflare Pages 创建 **Direct Upload** 项目，生产分支设为 `master`。构建由 GitHub Actions 完成。
2. 在 GitHub 仓库的 Actions 配置中添加：

| 类型 | 名称 | 值 |
| --- | --- | --- |
| Variable | `CLOUDFLARE_PAGES_PROJECT_NAME` | 已创建的 Pages 项目名 |
| Secret | `CLOUDFLARE_ACCOUNT_ID` | Cloudflare Account ID |
| Secret | `CLOUDFLARE_API_TOKEN` | 对目标账户具有 Cloudflare Pages Edit 权限的 API Token |

3. 推送到 `master`，或手动运行 **Browser Build and Pages Deploy**。

未设置项目名时，工作流仍构建并上传 `cloudflare-pages-site` artifact，但跳过部署。PR 只构建测试，不使用 Cloudflare 凭据。该流程与 NuGet 发布互相独立。

发布后检查首页、`/tools/`、三类示例计算、标准光源查询、中文字体、文件导入、CSV 下载以及 HTTPS 剪贴板。脚本会检查每个文件不超过 Pages 的 25 MiB 限制。浏览器首次访问需要下载 .NET 和 Avalonia 资源；当前不启用 AOT 或 WASM 多线程，不要求 COOP/COEP 跨源隔离。

参考：[Avalonia WASM 部署](https://docs.avaloniaui.net/docs/deployment/webassembly)、[Pages CI Direct Upload](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/)。

## 验证记录

首页页脚的计算库版本在构建时从引用的 `ChromaticityDotNet` 程序集生成，不使用应用版本或手写版本号，也无需启动 WASM。生成的脚本随静态站点发布。

本次实现通过 100 项应用测试、268 项原库测试、5 项 Python 数据测试，以及 39 份 CIE 数据校验。使用未安装 WASM 工作负载的系统 SDK 成功构建原 solution 并生成 NuGet 包，确认包中没有应用文件或 Avalonia 依赖。

Release WASM 已发布到本地预览，并在浏览器验证前三个独立入口、刷新、同时打开多个标签页、中文逗号 CSV 的色差与颜色转换计算、光谱样品卡片、实际库版本号和宽屏双列输入。应用测试也覆盖 CRLF、CR、Unicode 换行和窄屏布局。此前已验证错误提示及旧结果失效。当前最大静态文件约 8.49 MiB。Avalonia DataGrid 仍有上游 `IL2104` 裁剪警告，发布版的表格显示已实际验证；后续升级依赖仍应进行浏览器回归。

标准光源查询已通过 Debug WASM 构建。浏览器验证新增的 D50、LED-B1 光源，380–780 nm / 10 nm 波段限制、41 行数据与曲线同步更新，以及查询和反射率曲线的可见光背景。此前已验证 D65/10 nm 与 TL84/1 nm 切换及 401 行数据复制；VS Code 验证浏览器退出联动终止服务，以及点击停止按钮联动关闭浏览器；两种路径均释放 5235 端口。

CSV 导出通过浏览器 Blob 下载，不依赖 File System Access 保存接口。内置浏览器中已确认下载调用成功，但自动化未取得下载落盘确认；上线后应在目标浏览器完成一次实际下载检查。Cloudflare 线上部署需要配置上述账户参数，本次未发布到线上。

## 输入与计算约定

- 纯数字 CSV、TSV 或空格分隔文本（支持中文逗号 `，` 与英文逗号混用；换行区分不同记录），无表头、无样品名称列；UTF-8 编码。HEX 输入为每行一个 `#RRGGBB`。
- 限制为 10,000 行、2,000,000 个文本字符。导出 CSV 使用 UTF-8 BOM，适合 Excel；复制结果使用制表符。
- 光谱与颜色转换结果按颜色卡片展示序号、sRGB 色块与 HEX、XYZ、Lab、LCh、Luv、xyY 和计算条件。每组坐标横向排列，小屏自动换行，仍可复制及导出全部数值。色差标准与样品输入在宽屏等宽并排，窄屏自动上下排列。
- 光谱工具目前处理**反射率**，一列数值或两列“波长、反射率”。单位显式选择百分数或比例，不自动猜测；允许超过 100% 的反射率，遵循库约定。SPD 尺度约定仍未定，因此本版不提供自发光入口。
- 光谱颜色计算的照明体提供 D65、A、CWF（FL2）、F7、TL84（FL11）、U30（FL12）；观察者为 2° 和 10°。标准光源查询独立提供归档中的全部 50 条 CIE 光源光谱。光谱波段必须在相应数据的覆盖区间内。
- Lab/Luv 沿用库内固定白点，可能与截取波段的光谱积分白点不同。结果和导出标明这个约定。
- 色差工具支持一个标准对多个样品，或严格逐行配对。表格按序号、标样与样品 sRGB 预览、ΔL* / Δa* / Δb* / ΔC* / Δh°、CIE76 / CIE94 / CIEDE2000 / CMC 排列；色块同时显示 HEX，序号列固定。差值为样品减标样，Δh° 为 Lab 色相角的最短有符号差。预览按 D65/2° 计算。CIE94 固定图形印刷参数，CMC 支持 1:1 与 2:1，ΔE00 权重可设置；参数写在表头，复制与 CSV 导出保留这些表头，不提供容差判定。
- 转换输入为 XYZ（Y=100 尺度）、Lab、Luv、xyY、8-bit sRGB、HEX；LCh 为库的派生输出，不额外实现逆转换。
- sRGB/HEX 输入固定 D65/2°。颜色转换始终提供 RGB/HEX 与屏幕预览，其他照明体或观察者下从 XYZ 直接映射为 sRGB，标明未经色适应的屏幕近似。光谱工具仍仅在 D65/2° 提供预览，其他条件的 RGB/HEX 显示 `—`。sRGB 超色域输出会裁剪，屏幕预览不等于实物测量。
- 黑色的 xy 色度未定义，显示 `—`，其余有效结果仍可导出。
- 输入或条件变更立即清除上次结果；遇到无效行时整批拒绝并提示错误，不静默丢弃。

- 标准光源查询使用生成器编译的完整 50 条光源目录，提供 1、5、10、20 nm 间隔和起始/结束波长。修改波段后点击“更新波段”，表格、复制、导出与曲线同步裁剪；光源切换时保留可用的波段交集，不重叠时恢复该光源完整范围。不会在指定结束波长额外补点，范围外查询和不足两个采样点的波段会报错。
- 查询保留原始相对功率尺度和数值精度。源表为 5 nm 时，在必要位置线性插值并标明；FL/LED 使用 CIE 标注为 approximated 的官方 1 nm 表。界面显示来源 CSV、原始波段和间隔。其他颜色计算仍沿用原有六种照明体及其固定白点。


## 中文字体

`Chromaticity.Tools/Assets/ChromaticityUI.ttf` 是 [Google Fonts Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc) 的 400 字重子集，重命名为 Chromaticity UI；包含 GB2312、拉丁字母、希腊字母、常用符号和界面文字。许可见同目录 `OFL.txt`。字体嵌入在应用中，运行时不请求外部字体服务。

如需重新生成，下载上游 `NotoSansSC[wght].ttf`，安装 `fonttools==4.60.2`，执行：

```sh
python3 apps/subset_font.py '/path/to/NotoSansSC[wght].ttf'
```

字体许可、库许可和 CIE 数据说明会一起部署。
