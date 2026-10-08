# 浏览器色彩工具

五个 Avalonia Browser 工具复用仓库根目录的 `ChromaticityDotNet` 项目，使用现有算法和数据；不引入新的照明体、色适应或颜色公式。所有用户输入都在浏览器本地计算。05「色差分级色卡」的逐级求解也在 .NET WASM 中完成，界面仅收集输入并展示结果。

界面采用黑白灰主题，首页仅保留五张工具入口卡片与必要的项目/许可链接；卡片整块可点击，宽屏双列、窄屏单列。工具页直接显示表单与结果，sRGB 色块保留实际颜色，曲线、控件状态及错误提示使用灰阶。光谱图在 380–780 nm 区域使用淡色可见光背景；背景仅为波长的屏幕近似，不影响计算。

## 工程与发布边界

- `Chromaticity.Tools`：Avalonia 界面、输入校验、结果展示及 CSV 导出。
- `Chromaticity.Tools.Browser`：.NET 10 WebAssembly 启动与静态资源。
- `Chromaticity.Tools.Tests`：输入解析、参考计算、条件约束、批量模式和导出测试。
- `Chromaticity.Tools.sln`：独立应用 solution；原来的根 solution 仍只构建库和原测试。
- `Chromaticity.Tools.Browser/wwwroot`：首页与五个独立 HTML 工具入口。
- `site`：Pages 响应头；`prepare_site.py` 将其与 Browser 发布文件组成站点。

根项目通过 `DefaultItemExcludes` 排除整个 `apps/**`，应用项目均为 `IsPackable=false`，Avalonia 依赖不会进入 NuGet 库。

`.github/workflows/dotnet.yml` 保留库构建、数据检查、测试、NuGet 和 GitHub Release 步骤。仅改 `apps/**` 时不触发它。网站由 Cloudflare Pages 拉取 GitHub 仓库后构建和发布，不使用 NuGet 发布密钥，也不创建 Release。

## 本地运行

需要 .NET 10 SDK，以及官方 `wasm-tools` 工作负载。以下命令从仓库根目录运行：

```sh
dotnet workload install wasm-tools
dotnet run --project apps/Chromaticity.Tools.Browser/Chromaticity.Tools.Browser.csproj
```

默认地址为 `http://localhost:5235`，先显示工具选择首页。开发与发布使用相同路径，不再使用 `?tool=` 参数。

`wasm-tools` 必须安装在**实际运行构建的 SDK** 中。在 VS Code 终端运行 `dotnet --info` 和 `dotnet workload list` 确认；其他目录中的临时 SDK 即使已安装工作负载，也不会补齐系统 SDK。macOS/Linux 的系统 SDK 若提示权限不足，使用 `sudo dotnet workload install wasm-tools` 安装。项目显式启用原生链接，并在缺少工作负载时中止构建，避免生成启动后报 `DllNotFoundException: libSkiaSharp` 的页面。安装完成后停止旧服务，执行 `dotnet clean apps/Chromaticity.Tools.Browser/Chromaticity.Tools.Browser.csproj`，再重新启动。

生产站点首页位于 `/`，五个工具分别位于 `/tools/spectrum.html`、`/tools/difference.html`、`/tools/conversion.html`、`/tools/illuminant.html`、`/tools/grades.html`。每个 URL 都有独立 HTML 入口，支持直接访问、刷新及在多个标签页同时计算；首页和工具导航均使用普通链接，点击即在新标签页打开，保留当前输入与结果。各入口共用根目录下的 WASM 与静态资源。`/tools/` 同样显示工具选择首页。

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

1. 在 Cloudflare Pages 选择 **Import an existing Git repository**，连接 GitHub 并选择本仓库。
2. 构建设置填写：

| 配置项 | 值 |
| --- | --- |
| Production branch | `master` |
| Framework preset | `None` |
| Root directory | 留空，使用仓库根目录 |
| Build command | `bash apps/build_cloudflare.sh` |
| Build output directory | `artifacts/site` |
| Environment variables | 无需额外设置 |

3. 保存并部署。之后推送到 `master` 时由 Cloudflare 自动拉取、构建和发布。

`build_cloudflare.sh` 在忽略的 `artifacts/cloudflare-dotnet` 目录安装 .NET 10 SDK 和 `wasm-tools`，仅发布 Browser 项目，然后运行 `prepare_site.py` 整理站点。重复构建只替换生成的 `artifacts/site` 目录。构建命令通过 Bash 调用，无需设置脚本执行权限。Cloudflare 构建不运行测试；修改计算或输入处理代码时，先按上面的本地命令运行测试。

已移除 GitHub 的 **Browser Build and Pages Deploy** 工作流，无需配置 `CLOUDFLARE_PAGES_PROJECT_NAME`、`CLOUDFLARE_ACCOUNT_ID` 或 `CLOUDFLARE_API_TOKEN`。以前为网站部署添加的 GitHub Variable/Secrets 可以删除。

如果已有 Direct Upload 项目，需要新建 Git 集成项目；Cloudflare 不支持将 Direct Upload 项目切换为 Git 集成。

发布后检查首页、`/tools/`、三类示例计算、标准光源查询、色差分级色卡的三种公式与 CMC 比例、中文字体、文件导入、CSV 下载以及 HTTPS 剪贴板。脚本会检查每个文件不超过 Pages 的 25 MiB 限制。浏览器首次访问需要下载 .NET 和 Avalonia 资源；当前不启用 AOT 或 WASM 多线程，不要求 COOP/COEP 跨源隔离。

参考：[Avalonia WASM 部署](https://docs.avaloniaui.net/docs/deployment/webassembly)、[Pages Git integration](https://developers.cloudflare.com/pages/get-started/git-integration/)、[Pages .NET 构建示例](https://developers.cloudflare.com/pages/framework-guides/deploy-a-blazor-site/)。

## 验证记录

色级阈值与两项分析功能通过 169 项应用测试（本次新增 34 项），Release WASM 发布成功。测试覆盖三种公式下的自定义阈值、逐级 L/C/h 推算与反向等级恢复、小数等级、CMC 前一步参考、跨 0° 色相、中性色、坐标边界、无效阈值/样本、1000 级计算上限和三方向自适应布局。本地 Chrome 验证默认 1.5 的 +2/+1/-3 推算及样本反算、CMC 2:1 与自定义阈值 1.2 的推算、无间隙双块预览、参数变更清除所有旧结果；目标色 CSV 已实际下载并核对四阶段坐标、等级、阈值与量化色差。验证结束后关闭临时浏览器标签页和本地预览进程。

05「色差分级色卡」通过 135 项应用测试（新增 35 项），Release WASM 发布成功。本地 Chrome 验证 CIE76、CMC 自定义 1.5:0.8、E00 固定 1:1:1、参数修改清除旧结果、复制 27 行色卡及 CSV 实际下载。CSV 保留参数与计算条件；390 px 窄屏验证横向滚动。新增测试覆盖逐级 ΔE=1.5、各轴固定分量、CMC 参考方向、比例作用、黑白与中性色、无法达到一步的边界、色相跨 0° 与半周限制、无效输入及导出精度。

首页页脚的计算库版本在构建时从引用的 `ChromaticityDotNet` 程序集生成，不使用应用版本或手写版本号，也无需启动 WASM。生成的脚本随静态站点发布。

本次实现通过 100 项应用测试、268 项原库测试、5 项 Python 数据测试，以及 39 份 CIE 数据校验。使用未安装 WASM 工作负载的系统 SDK 成功构建原 solution 并生成 NuGet 包，确认包中没有应用文件或 Avalonia 依赖。

Release WASM 已发布到本地预览，并在浏览器验证前三个独立入口、刷新、同时打开多个标签页、中文逗号 CSV 的色差与颜色转换计算、光谱样品卡片、实际库版本号和宽屏双列输入。应用测试也覆盖 CRLF、CR、Unicode 换行和窄屏布局。此前已验证错误提示及旧结果失效。当前最大静态文件约 8.49 MiB。Avalonia DataGrid 仍有上游 `IL2104` 裁剪警告，发布版的表格显示已实际验证；后续升级依赖仍应进行浏览器回归。

色差分级色卡的整数等级调整通过 196 项应用测试及 Release WASM 发布。浏览器确认默认 CMC 2:1、系数同行、标样与样本 Lab 并排、下一行取整选项，以及色卡和分析结果不含复制或 CSV 按钮。同一组样本在四舍五入与向上取整下分别显示 L/C/h 等级 +1/0/-2 与 +1/+1/-2，Lab、HEX 和整体 ΔE 保持一致；切换取整方式立即清除旧分析。验证完成后关闭临时浏览器标签页和本地预览服务。

标准光源查询已通过 Debug WASM 构建。浏览器验证新增的 D50、LED-B1 光源，380–780 nm / 10 nm 波段限制、41 行数据与曲线同步更新，以及查询和反射率曲线的可见光背景。此前已验证 D65/10 nm 与 TL84/1 nm 切换及 401 行数据复制；VS Code 验证浏览器退出联动终止服务，以及点击停止按钮联动关闭浏览器；两种路径均释放 5235 端口。

CSV 导出通过浏览器 Blob 下载，不依赖 File System Access 保存接口。内置浏览器中已确认下载调用成功，但自动化未取得下载落盘确认；上线后应在目标浏览器完成一次实际下载检查。Cloudflare 线上部署需要按上述配置连接 GitHub 仓库，本次未发布到线上。

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

- 色差分级色卡参考 `JQDigitalColorCard` 的 L*、C*、h° 三轴逐级展开方式，默认及示例标样为 Lab 50、30、20，默认每侧 4 级，可选择 1–10 级。默认公式为 CMC 2:1，系数与公式同行设置；也支持 CIE76、CMC（1:1、2:1、自定义有限正数 l:c）、CIEDE2000（固定 1:1:1）。一级 ΔE 阈值提供 0.5、1、1.5、2、3 与自定义，默认 1.5；自定义至少 0.0001、最多四位小数。每向外一级以前一级作标样，求解所选公式的阈值，精度遵循库的四位小数。级别表示步数，相对原始标样的累计 ΔE 不一定等于级数 × 阈值，CMC 方向性明确保留。
- 明度限于 0–100，彩度不能低于 0；色相每侧最多展开半周。中性色没有确定色相，彩度和色相方向不生成色级。无法达到阈值时保留不可生成的位置及原因，不输出不足一步的色块、不绕回重复色相。每组 sRGB 色块无间隙拼接成居中的连续色带，级别标注在上方，Lab、LCh、HEX 和相对标样 ΔE 居中显示在下方，窄屏可横向滚动。预览固定 D65/2° 并裁剪超色域 sRGB。
- 生成色卡后可选择三方向的有符号整数等级，固定按 L* → C* → h° 顺序逐级推算目标色。每一级保留其他坐标分量，并以前一步的实际颜色作标样；完成 L* 后以其中间值展开 C*，再以新的中间值展开 h°。不能把原始三个色带的坐标直接拼合。选择范围与色卡每侧级数一致；任一方向达不到阈值时整次推算报错，不裁剪坐标来冒充目标。
- 标样与样本分析的 Lab 输入在宽屏并排，窄屏上下排列，下一行选择等级计算方式。沿同一 L* → C* → h° 路径先累计完整步数及末段 ΔE / 阈值，再按所选方式输出三个 `int?` 等级：默认四舍五入（半级进一），或对幅度向上取整后保留方向，+1.2→+2、-1.2→-2。取整仅改变等级，不改变样本坐标与量化色差；不直接把方向总 ΔE 除以阈值。整步终点优先保留，避免 CMC 四位小数的求解残差被向上取整多算一级。色相走最短有符号路径，恰好 180° 时取正向。分析可以超过色卡显示的级数，每方向最多累计 1000 个完整等级，超过时提示提高阈值。中性色的 h° 等级未定义；中性标样向有彩样本的固定色相 C* 等级也未定义，但继续显示实际色差。
- 推算及分析结果将标样与目标/样本 sRGB 色块无间隙并排，显示双方 Lab、LCh、HEX、ΔL*、Δa*、Δb*、ΔC*、Δh°、所选公式整体 ΔE、各方向总 ΔE 与整数等级，不展示三阶段中间值。05 整个页面不提供复制结果或 CSV 下载。修改色卡参数清除所有关联结果；修改等级、分析 Lab 或取整方式只清除对应结果。


## 中文字体

`Chromaticity.Tools/Assets/ChromaticityUI.ttf` 是 [Google Fonts Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc) 的 400 字重子集，重命名为 Chromaticity UI；包含 GB2312、拉丁字母、希腊字母、常用符号和界面文字。许可见同目录 `OFL.txt`。字体嵌入在应用中，运行时不请求外部字体服务。

如需重新生成，下载上游 `NotoSansSC[wght].ttf`，安装 `fonttools==4.60.2`，执行：

```sh
python3 apps/subset_font.py '/path/to/NotoSansSC[wght].ttf'
```

字体许可、库许可和 CIE 数据说明会一起部署。
