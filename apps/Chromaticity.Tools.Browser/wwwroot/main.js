const toolNames = { spectrum: '光谱计算', difference: '色差计算', conversion: '颜色转换', illuminant: '标准光源查询', grades: '色差分级色卡' };
const requestedTool = document.body.dataset.tool;
const tool = Object.hasOwn(toolNames, requestedTool) ? requestedTool : 'spectrum';
document.title = `${toolNames[tool]} · ChromaticityDotNet`;
for (const link of document.querySelectorAll('.tools-nav a')) {
    if (link.dataset.tool === tool) link.setAttribute('aria-current', 'page');
}

try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    runtime.setModuleImports('chromaticity-files', {
        downloadCsv(fileName, text) {
            const blob = new Blob(['\uFEFF', text], { type: 'text/csv;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 60000);
        }
    });
    await runtime.runMain(runtime.getConfig().mainAssemblyName, [tool]);
} catch (error) {
    console.error('Chromaticity startup failed', error);
    const message = document.getElementById('loading-message');
    if (message) message.textContent = '加载失败。请刷新页面，或检查浏览器是否支持 WebAssembly。';
}
