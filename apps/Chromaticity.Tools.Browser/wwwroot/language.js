const storageKey = 'chromaticity-language';
const toolNames = {
    spectrum: ['光谱计算', 'Spectrum calculation'],
    difference: ['色差计算', 'Color difference'],
    conversion: ['颜色转换', 'Color conversion'],
    illuminant: ['标准光源查询', 'Standard illuminants'],
    grades: ['色差分级色卡', 'Color grade chart']
};
const listeners = new Set();
const chineseText = new WeakMap();
let language = 'zh-CN';
try {
    if (localStorage.getItem(storageKey) === 'en') language = 'en';
} catch { /* Language switching still works when storage is unavailable. */ }

export function getLanguage() { return language; }
export function onLanguageChange(listener) {
    listeners.add(listener);
    return () => listeners.delete(listener);
}
export function getToolName(tool) {
    return (toolNames[tool] ?? toolNames.spectrum)[language === 'en' ? 1 : 0];
}

function render() {
    document.documentElement.lang = language;
    for (const node of document.querySelectorAll('[data-en]')) {
        const attribute = node.dataset.i18nAttribute;
        if (!chineseText.has(node)) chineseText.set(node, attribute ? node.getAttribute(attribute) : node.textContent);
        const value = language === 'en' ? node.dataset.en : chineseText.get(node);
        if (attribute) node.setAttribute(attribute, value);
        else node.textContent = value;
    }
    for (const select of document.querySelectorAll('.language-select')) select.value = language;
    const tool = document.body.dataset.tool;
    document.title = (tool ? getToolName(tool) : language === 'en' ? 'Color toolbox' : '色彩工具箱') + ' · ChromaticityDotNet';
}

function setLanguage(value, persist = true) {
    language = value === 'en' ? 'en' : 'zh-CN';
    if (persist) {
        try { localStorage.setItem(storageKey, language); } catch { }
    }
    render();
    for (const listener of listeners) listener(language);
}

for (const select of document.querySelectorAll('.language-select')) {
    select.addEventListener('change', () => setLanguage(select.value));
}
window.addEventListener('storage', event => {
    if (event.key === storageKey || event.key === null) setLanguage(event.newValue, false);
});
render();
