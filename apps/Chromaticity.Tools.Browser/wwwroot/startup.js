import { getLanguage, onLanguageChange } from './language.js';

// The SDK reports completed requests, not a byte total. Use the boot manifest
// for a stable denominator: the SDK's total only includes requests queued so far.
export function countStartupResources(config) {
    const resources = config.resources ?? {};
    const groups = ['wasmNative', 'coreAssembly', 'assembly', 'coreVfs', 'vfs'];
    if (config.debugLevel && /Chrome|Firefox/.test(navigator.userAgent)) groups.push('corePdb', 'pdb');
    const count = groups.reduce((sum, key) => sum + (resources[key]?.length ?? 0), 0);
    // The runtime selects one ICU archive for the browser's culture.
    return count + (config.globalizationMode !== 'invariant' && resources.icu?.length ? 1 : 0);
}

export function createStartupProgress() {
    const nodes = Object.fromEntries(['heading', 'stage', 'percent', 'progress', 'detail', 'warning', 'retry']
        .map(key => [key, document.getElementById(`loading-${key}`)]));
    let phase = 'preparing';
    let loaded = 0;
    let total = 0;
    let failure = '';
    const startedAt = performance.now();
    let lastActivity = startedAt;
    let stopped = false;
    const text = (chinese, english) => getLanguage() === 'en' ? english : chinese;
    const pendingResources = new Map();
    const resourceKey = uri => {
        const url = new URL(uri, document.baseURI);
        return url.origin + url.pathname;
    };
    const finishedResources = new Set(performance.getEntriesByType('resource').map(entry => resourceKey(entry.name)));
    // Observe the default SDK loader instead of replacing its fetches, so its
    // integrity checks, HTTP cache and streaming compilation remain in place.
    const observer = globalThis.PerformanceObserver?.supportedEntryTypes?.includes('resource') ? new PerformanceObserver(list => {
        for (const entry of list.getEntries()) {
            const key = resourceKey(entry.name);
            finishedResources.add(key);
            if (pendingResources.delete(key)) lastActivity = performance.now();
        }
    }) : null;
    observer?.observe({ type: 'resource', buffered: true });

    function render() {
        const elapsed = Math.floor((performance.now() - startedAt) / 1000);
        const idle = Math.floor((performance.now() - lastActivity) / 1000);
        const percent = total ? Math.min(100, Math.floor(loaded / total * 100)) : 0;
        const stages = {
            preparing: text('正在读取运行配置', 'Reading runtime configuration'),
            downloading: text('正在加载运行资源', 'Loading runtime resources'),
            initializing: text('正在初始化 WebAssembly 运行时', 'Initializing the WebAssembly runtime'),
            starting: text('正在启动工具界面', 'Starting the tool interface'),
            failed: text('加载失败', 'Loading failed'),
            complete: text('工具已就绪', 'Tool ready')
        };
        nodes.heading.textContent = phase === 'failed' ? stages.failed : text('正在启动工具', 'Starting the tool');
        // Avoid repeatedly announcing an unchanged stage to screen readers.
        if (nodes.stage.textContent !== stages[phase]) nodes.stage.textContent = stages[phase];
        nodes.percent.textContent = `${percent}%`;
        nodes.progress.value = percent;
        nodes.progress.setAttribute('aria-label', text('运行资源加载进度（按资源数）', 'Runtime resource progress (by resource count)'));
        const count = total ? text(`资源 ${loaded} / ${total} · `, `Resources ${loaded} / ${total} · `) : '';
        nodes.detail.textContent = count + text(`已用时 ${elapsed} 秒`, `Elapsed ${elapsed}s`);
        let warning = '';
        if (phase === 'failed') {
            warning = text('请重试；若仍失败，请检查网络和浏览器是否支持 WebAssembly。',
                'Retry; if loading still fails, check your connection and browser support for WebAssembly.')
                + (failure ? `\n${failure}` : '');
        } else if (navigator.onLine === false) {
            warning = text('网络已断开，正在等待连接恢复。', 'You are offline. Waiting for the connection to return.');
        } else if (idle >= 15) {
            const pending = [...pendingResources.values()];
            warning = phase === 'preparing' || phase === 'downloading'
                ? text('已超过 15 秒未收到新资源。仍在等待响应，大文件或慢速网络可能需要更久。',
                    'No new resources for over 15s. Still waiting for a response; large files or slow connections may take longer.')
                    + (observer && pending.length ? text(`\n未完成资源：${pending.slice(0, 3).join('、')}`,
                        `\nPending resources: ${pending.slice(0, 3).join(', ')}`) : '')
                : text('此阶段已等待超过 15 秒。资源已加载，正在等待初始化完成。',
                    'Waiting in this stage for over 15s. Resources are loaded; initialization is still pending.');
        }
        if (nodes.warning.textContent !== warning) nodes.warning.textContent = warning;
        nodes.warning.hidden = !warning;
        nodes.retry.hidden = phase !== 'failed' && idle < 15 && navigator.onLine !== false;
        nodes.retry.textContent = text('重新加载', 'Reload');
    }

    const timer = setInterval(render, 1000);
    const unsubscribe = onLanguageChange(render);
    const connectivityChanged = () => { lastActivity = performance.now(); render(); };
    window.addEventListener('online', connectivityChanged);
    window.addEventListener('offline', connectivityChanged);
    nodes.retry.addEventListener('click', () => location.reload());

    function stop() {
        stopped = true;
        clearInterval(timer);
        observer?.disconnect();
        unsubscribe();
        window.removeEventListener('online', connectivityChanged);
        window.removeEventListener('offline', connectivityChanged);
    }

    render();
    return {
        configLoaded(config) {
            if (stopped) return;
            total = countStartupResources(config);
            this.setPhase('downloading');
        },
        resourceRequested(name, uri) {
            if (stopped) return;
            const key = resourceKey(uri);
            if (!finishedResources.has(key)) pendingResources.set(key, name);
        },
        resourcesLoaded(count, requested) {
            if (stopped) return;
            loaded = count;
            total = Math.max(total, requested, loaded);
            lastActivity = performance.now();
            if (loaded >= total) phase = 'initializing';
            render();
        },
        setPhase(value) {
            if (stopped) return;
            phase = value;
            lastActivity = performance.now();
            render();
        },
        complete() {
            phase = 'complete';
            loaded = total;
            render();
            stop();
        },
        fail(error) {
            phase = 'failed';
            failure = error instanceof Error ? error.message : String(error ?? '');
            // Keep error details visible even if Avalonia already closed the splash.
            document.querySelector('.avalonia-splash')?.classList.remove('splash-close');
            render();
            clearInterval(timer);
            observer?.disconnect();
            stopped = true;
        }
    };
}
