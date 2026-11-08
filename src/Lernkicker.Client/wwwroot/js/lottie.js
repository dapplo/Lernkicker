// Lottie animations without any C# interop: an element with data-lottie="name" plays animations/name.json as soon as
// it is on the page and is cleaned up when it leaves. data-lottie-loop loops it; otherwise it plays once.
// The player (lib/lottie, the light SVG build: no expressions, so no eval for the CSP) loads only when the first
// animation shows up. With "reduce motion" a looping animation stands still on its first frame and a one-off is left out.
(() => {
    const players = new Map();
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
    let loading = null;

    function player() {
        loading ??= new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'lib/lottie/lottie_light.min.js';
            script.onload = () => resolve(window.lottie);
            script.onerror = reject;
            document.head.appendChild(script);
        });
        return loading;
    }

    async function start(element) {
        if (players.has(element)) {
            return;
        }

        const loop = element.hasAttribute('data-lottie-loop');
        if (reduced.matches && !loop) {
            return;
        }

        players.set(element, null);
        try {
            const lottie = await player();
            if (!element.isConnected || !players.has(element)) {
                players.delete(element);
                return;
            }

            const animation = lottie.loadAnimation({
                container: element,
                renderer: 'svg',
                loop,
                autoplay: !reduced.matches,
                path: `animations/${element.dataset.lottie}.json`,
                rendererSettings: { preserveAspectRatio: element.dataset.lottieFit || 'xMidYMid meet' },
            });
            animation.addEventListener('DOMLoaded', () => element.classList.add('lottie-on'));
            players.set(element, animation);
        } catch {
            // The player did not load (offline before it was cached): the CSS look underneath stays
            players.delete(element);
        }
    }

    function stop(element) {
        players.get(element)?.destroy();
        players.delete(element);
    }

    function each(node, action) {
        if (node.nodeType !== Node.ELEMENT_NODE) {
            return;
        }

        if (node.hasAttribute('data-lottie')) {
            action(node);
        }

        node.querySelectorAll('[data-lottie]').forEach(action);
    }

    new MutationObserver(changes => {
        for (const change of changes) {
            change.removedNodes.forEach(node => each(node, stop));
            change.addedNodes.forEach(node => each(node, start));
        }
    }).observe(document.documentElement, { childList: true, subtree: true });

    // Waves keep still while the app is not on screen, which saves the battery
    document.addEventListener('visibilitychange', () => {
        if (window.lottie) {
            document.hidden ? window.lottie.freeze() : window.lottie.unfreeze();
        }
    });
})();
