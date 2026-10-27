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

    // The device's voice for a language: one for exactly that tag (e.g. nl-NL) if there is, else any of the language.
    // Android reports some tags as nl_NL or with three-letter codes (nld).
    const threeLetters = { nld: 'nl', deu: 'de', ger: 'de', eng: 'en', fra: 'fr', fre: 'fr', spa: 'es', ita: 'it' };
    function voiceFor(language) {
        const tag = language.toLowerCase();
        const base = tag.split('-')[0];
        const voices = speechSynthesis.getVoices();
        const normal = v => {
            const parts = v.lang.toLowerCase().replaceAll('_', '-').split('-');
            parts[0] = threeLetters[parts[0]] ?? parts[0];
            return parts.join('-');
        };
        return voices.find(v => normal(v) === tag)
            ?? voices.find(v => normal(v).split('-')[0] === base && v.default)
            ?? voices.find(v => normal(v).split('-')[0] === base);
    }

    // The service worker keeps serving the version it has cached, and a new one only takes over once every window of
    // the app is closed, which an installed app on a phone hardly ever is. When the server runs another build
    // (AppUpdate in the client), let the new worker take over at once and reload.
    const updateKey = 'lk-update';

    // The new worker once it is installed, or null if there is none (yet).
    function installedWorker(registration, milliseconds) {
        return new Promise(resolve => {
            const worker = registration.installing;
            if (!worker) {
                resolve(registration.waiting);
                return;
            }

            setTimeout(() => resolve(registration.waiting), milliseconds);
            worker.addEventListener('statechange', () => {
                if (worker.state === 'installed' || worker.state === 'redundant') {
                    resolve(registration.waiting);
                }
            });
        });
    }

    async function takeOver() {
        const registration = await navigator.serviceWorker.getRegistration();
        if (!registration) {
            return;
        }

        await registration.update();
        const worker = await installedWorker(registration, 60000);
        if (worker) {
            await new Promise(resolve => {
                navigator.serviceWorker.addEventListener('controllerchange', resolve, { once: true });
                setTimeout(resolve, 5000);
                worker.postMessage('skipWaiting');
            });
        }
    }

    // Without the worker and its cache, the next load comes straight from the server.
    async function forget() {
        const registrations = await navigator.serviceWorker.getRegistrations();
        await Promise.all(registrations.map(r => r.unregister()));
        const keys = await caches.keys();
        await Promise.all(keys.filter(k => k.startsWith('offline-cache-')).map(k => caches.delete(k)));
    }

    window.lernkicker = {
        // Loads the server's build. Reloads once per build and browser session; returns false instead when that reload
        // already happened (or cannot be remembered), so a broken update never ends in a reload loop. force (a tap on
        // the notice) drops the worker and its cache.
        async update(build, force) {
            try {
                if (!force && sessionStorage.getItem(updateKey) === build) {
                    return false;
                }

                sessionStorage.setItem(updateKey, build);
            } catch {
                if (!force) {
                    return false;
                }
            }

            try {
                if ('serviceWorker' in navigator) {
                    await (force ? forget() : takeOver());
                }
            } catch {
                // Offline or no worker: the reload gets what it can
            }

            location.reload();
            return true;
        },

        onVisible(notice) {
            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible') {
                    notice.invokeMethodAsync('Visible');
                }
            });
        },

        setLanguage(code) {
            try {
                localStorage.setItem(key, code);
            } catch {
                // Storage blocked: the choice lasts until the page is closed
            }
            location.reload();
        },

        // Scales a photo down to maxSide pixels and re-encodes it as JPEG (which drops EXIF data such as GPS).
        // Transparent parts become white. Returns the original bytes when the picture is small enough already.
        async shrinkImage(bytes, type, maxSide) {
            const bitmap = await createImageBitmap(new Blob([bytes], { type }));
            const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.round(bitmap.width * scale);
            canvas.height = Math.round(bitmap.height * scale);
            const context = canvas.getContext('2d');
            context.fillStyle = '#fff';
            context.fillRect(0, 0, canvas.width, canvas.height);
            context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            bitmap.close();
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.85));
            const shrunk = new Uint8Array(await blob.arrayBuffer());
            return shrunk.length < bytes.length ? shrunk : bytes;
        },

        // The falling words game: lets the element fall to the bottom of its parent in the given time, then tells
        // the game. Web animations are allowed by the CSP, inline styles are not.
        fall(element, milliseconds, game) {
            element.getAnimations().forEach(a => a.cancel());
            const ground = Math.max(0, element.parentElement.clientHeight - element.offsetHeight);
            const animation = element.animate(
                [{ transform: 'translateY(0)' }, { transform: `translateY(${ground}px)` }],
                { duration: milliseconds, easing: 'linear', fill: 'forwards' });
            animation.finished.then(() => game.invokeMethodAsync('Landed'), () => { /* stopped */ });
        },

        // Once the child has picked an option, the word drops quickly the rest of the way.
        dropNow(element) {
            element.getAnimations().forEach(a => a.updatePlaybackRate(8));
        },

        stopFall(element) {
            element?.getAnimations().forEach(a => a.cancel());
        },

        // Reading words aloud uses the device's own voices (Web Speech API), so nothing is sent anywhere.
        canSpeak() {
            return 'speechSynthesis' in window;
        },

        // Whether the device lists a voice for the language: true, false, or null when it lists no voices at all.
        // Voices load late on some browsers, and Chrome on Android often lists none or only some although speaking
        // with just the language tag works, so only a list with voices but none for the language means "no".
        async hasVoice(language) {
            if (!('speechSynthesis' in window)) {
                return false;
            }

            if (speechSynthesis.getVoices().length === 0) {
                await new Promise(resolve => {
                    speechSynthesis.addEventListener('voiceschanged', resolve, { once: true });
                    setTimeout(resolve, 3000);
                });
            }

            return speechSynthesis.getVoices().length === 0 ? null : voiceFor(language) !== undefined;
        },

        speak(text, language) {
            // Cancelling right before speaking swallows the new word on some Android phones, so only when busy
            if (speechSynthesis.speaking || speechSynthesis.pending) {
                speechSynthesis.cancel();
            }
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = language;
            try {
                utterance.voice = voiceFor(language) ?? null;
            } catch {
                // Not a voice of this browser: the language tag alone picks one
            }
            utterance.rate = 0.9;
            speechSynthesis.speak(utterance);
        },

        // The name of a language in the UI language, e.g. 'nl' → 'Niederländisch'.
        languageName(code) {
            try {
                return new Intl.DisplayNames([document.documentElement.lang], { type: 'language' }).of(code) ?? code;
            } catch {
                return code;
            }
        },

        scrollIntoView(element) {
            element?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        },

        scrollToId(id) {
            document.getElementById(id)?.scrollIntoView({ block: 'start', behavior: 'smooth' });
        },
    };

    Blazor.start({ applicationCulture: language });

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
    }
})();
