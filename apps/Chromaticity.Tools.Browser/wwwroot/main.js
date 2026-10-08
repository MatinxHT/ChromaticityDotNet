import { getLanguage, getToolName, onLanguageChange, showStartupError } from './language.js';
const toolNames = ['spectrum', 'difference', 'conversion', 'illuminant', 'grades'];
const requestedTool = document.body.dataset.tool;
const tool = toolNames.includes(requestedTool) ? requestedTool : 'spectrum';
document.title = `${getToolName(tool)} · ChromaticityDotNet`;
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
    const assembly = runtime.getConfig().mainAssemblyName;
    const exports = await runtime.getAssemblyExports(assembly);
    await runtime.runMain(assembly, [tool, getLanguage()]);
    exports.Program.SetLanguage(getLanguage());
    onLanguageChange(value => exports.Program.SetLanguage(value));
} catch (error) {
    console.error('Chromaticity startup failed', error);
    showStartupError();
}
