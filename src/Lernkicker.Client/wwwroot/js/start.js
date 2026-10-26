// Starts Blazor in the UI language: the choice stored on this device, else the browser's language, else German.
// Kept out of index.html so the Content-Security-Policy needs no inline scripts besides the import map.
(() => {
    const supported = ['de', 'en'];
    const key = 'lk-lang';
    const texts = {
        de: { loading: 'Lernkicker wird geladen …', error: 'Da ist etwas schiefgegangen.', reload: 'Neu laden' },
        en: { loading: 'Loading Lernkicker …', error: 'Something went wrong.', reload: 'Reload' },
    };

    function stored() {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    }

    const browser = (navigator.languages || [navigator.language || ''])
        .map(l => l.slice(0, 2).toLowerCase())
        .find(l => supported.includes(l));
    const language = supported.includes(stored()) ? stored() : browser || 'de';

    document.documentElement.lang = language;
    document.querySelectorAll('[data-text]').forEach(e => e.textContent = texts[language][e.dataset.text]);

    window.lernkicker = {
        setLanguage(code) {
            try {
                localStorage.setItem(key, code);
            } catch {
                // Storage blocked: the choice lasts until the page is closed
            }
            location.reload();
        },
        scrollIntoView(element) {
            element?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        },
    };

    Blazor.start({ applicationCulture: language });

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
    }
})();
