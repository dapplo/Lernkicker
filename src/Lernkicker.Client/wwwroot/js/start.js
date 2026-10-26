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
    function voiceFor(language) {
        const tag = language.toLowerCase();
        const base = tag.split('-')[0];
        const voices = speechSynthesis.getVoices();
        const normal = v => v.lang.toLowerCase().replace('_', '-');
        return voices.find(v => normal(v) === tag)
            ?? voices.find(v => normal(v).split('-')[0] === base && v.default)
            ?? voices.find(v => normal(v).split('-')[0] === base);
    }

    window.lernkicker = {
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

        // Reading words aloud uses the device's own voices (Web Speech API), so nothing is sent anywhere. Voices load
        // late on some browsers, so wait a moment for them before saying there is none for the language.
        async hasVoice(language) {
            if (!('speechSynthesis' in window)) {
                return false;
            }

            if (speechSynthesis.getVoices().length === 0) {
                await new Promise(resolve => {
                    speechSynthesis.addEventListener('voiceschanged', resolve, { once: true });
                    setTimeout(resolve, 1500);
                });
            }

            return voiceFor(language) !== undefined;
        },

        speak(text, language) {
            speechSynthesis.cancel();
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
