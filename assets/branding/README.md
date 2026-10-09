# 共享图标资源

本目录统一管理应用与 NuGet 的图标，浏览器工程的 `wwwroot` 不维护副本。

- `app-icon-source.png`：使用内置 imagegen 工具生成的原始图像，仅供维护，不发布到网站或 NuGet 包。
- `app-icon.png`：256 px 的透明 PNG，网页品牌标志、加载图标、Apple touch icon 与 NuGet 包图标共用。
- `favicon-16.png`、`favicon-32.png`：同一图标的小尺寸版本。
- `favicon.ico`：包含上述 16 px 和 32 px PNG，保留透明度。

Browser 工程通过 `Content` / `Link` 引用本目录，在开发及发布时映射到 `/assets/app-icon.png`、`/assets/favicon-16.png`、`/assets/favicon-32.png` 和 `/favicon.ico`。页面无需修改资源地址；构建输出中的副本由 SDK 管理，不提交到仓库。

NuGet 工程直接将本目录的 `app-icon.png` 打包到包根目录，并以 `PackageIcon` 声明。GitHub Actions 校验声明、包内文件和源文件一致；仅修改本目录也会触发库 workflow。

更新图标时在本目录同步替换 PNG 与 ICO。图像内容变化后，更新 HTML 中图标 URL 的版本参数以刷新浏览器缓存。随后运行 `dotnet pack` 和 Browser 发布，确认包与网站均使用更新后的资源。

初始生成提示词：

> Use case: logo-brand. Asset type: production app icon for ChromaticityDotNet, a practical color science and spectral calculation WebAssembly toolbox. Generate ONE square icon, centered, front-facing flat design. A bold charcoal rounded square contains a clean white geometric letter C that subtly suggests a circular measurement aperture; a small integrated light-gray dot marks the aperture opening. Simple, distinctive, monochrome black/white/gray palette suited to a restrained utilitarian interface. Crisp solid silhouettes and generous stroke widths that remain legible at 16px. The rounded tile should occupy roughly 90% of the square canvas. Transparent outside the rounded square. No shadows, no gradients, no 3D, no extra words, no mockup, no decorative background, no watermark. Only the icon asset.

当前版本使用内置 imagegen 工具编辑，将圆点改为七彩色。页面图标 URL 使用 `v=2`，刷新后重新加载。

编辑提示词：

> Use case: precise-object-edit. Edit target: the attached ChromaticityDotNet app icon. Change ONLY the small gray circular dot in the opening of the white C to a vivid seven-color rainbow circle. The circle should contain smoothly connected red, orange, yellow, green, blue, indigo, and violet spectrum bands, all seven colors contained inside the existing circular silhouette, with a clean flat appearance and no shine. Preserve exactly the dot's position, size, and round shape. Keep the deep charcoal rounded-square background, white C silhouette, proportions, composition, edges, and transparent area outside the tile unchanged. No new symbols, text, shadows, or objects. Return the single edited square icon, with genuine transparency preserved.
