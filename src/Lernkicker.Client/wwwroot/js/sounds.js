// Game sounds, made right here with the Web Audio API: no recorded files, so nothing to license or download.
// Each sound is noise or a tone shaped by filters and a volume curve. Browsers only let a page make sounds after a tap,
// so the audio starts on the first tap anywhere; a sound the opponent's move brings in later then plays as well.
// Whether sounds are on is kept on the device (lk-sound), for all games alike.
(() => {
    const key = 'lk-sound';
    let context = null;
    let noise = null;
    let chosen = null;

    function audio() {
        if (!context) {
            const Context = window.AudioContext || window.webkitAudioContext;
            if (!Context) {
                return null;
            }

            context = new Context();
        }

        if (context.state === 'suspended') {
            context.resume().catch(() => { });
        }

        return context;
    }

    ['pointerdown', 'keydown'].forEach(type => document.addEventListener(type, () => audio(), { capture: true, passive: true }));

    // Two seconds of white noise, made once and played through filters for splashes, blasts and rumbling
    function noiseBuffer(ctx) {
        if (!noise) {
            noise = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
            const data = noise.getChannelData(0);
            for (let i = 0; i < data.length; i++) {
                data[i] = Math.random() * 2 - 1;
            }
        }

        return noise;
    }

    // A volume curve: up quickly to peak, then fading out over the length (seconds)
    function envelope(ctx, start, peak, attack, length) {
        const gain = ctx.createGain();
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(peak, start + attack);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + length);
        gain.connect(ctx.destination);
        return gain;
    }

    function noiseBurst(ctx, start, length, filterType, from, to, peak, attack = 0.01) {
        const source = ctx.createBufferSource();
        source.buffer = noiseBuffer(ctx);
        const filter = ctx.createBiquadFilter();
        filter.type = filterType;
        filter.frequency.setValueAtTime(from, start);
        filter.frequency.exponentialRampToValueAtTime(to, start + length);
        source.connect(filter);
        filter.connect(envelope(ctx, start, peak, attack, length));
        source.start(start, Math.random());
        source.stop(start + length + 0.05);
    }

    function tone(ctx, start, length, type, from, to, peak, attack = 0.005) {
        const oscillator = ctx.createOscillator();
        oscillator.type = type;
        oscillator.frequency.setValueAtTime(from, start);
        oscillator.frequency.exponentialRampToValueAtTime(to, start + length);
        oscillator.connect(envelope(ctx, start, peak, attack, length));
        oscillator.start(start);
        oscillator.stop(start + length + 0.05);
    }

    const sounds = {
        // The cannon fires: a short low thump
        shot(ctx, t) {
            tone(ctx, t, 0.25, 'sine', 150, 45, 0.5);
            noiseBurst(ctx, t, 0.18, 'lowpass', 2400, 300, 0.35);
        },
        // Into the water: a hiss that falls, then a plop and some drops
        splash(ctx, t) {
            noiseBurst(ctx, t, 0.75, 'bandpass', 2600, 380, 0.55, 0.02);
            tone(ctx, t + 0.02, 0.18, 'sine', 520, 140, 0.25);
            for (let i = 0; i < 4; i++) {
                const at = t + 0.25 + Math.random() * 0.45;
                tone(ctx, at, 0.07, 'sine', 900 + Math.random() * 700, 1800 + Math.random() * 600, 0.06);
            }
        },
        // A hit: a deep boom with a crackling tail
        hit(ctx, t) {
            tone(ctx, t, 0.9, 'sine', 95, 30, 0.8);
            noiseBurst(ctx, t, 1.3, 'lowpass', 3200, 140, 0.9, 0.005);
            for (let i = 0; i < 6; i++) {
                noiseBurst(ctx, t + 0.25 + Math.random() * 0.8, 0.05, 'highpass', 2500, 1800, 0.12);
            }
        },
        // A ship goes down: a long rumble, the hull groaning and air bubbling up
        sink(ctx, t) {
            noiseBurst(ctx, t, 2.8, 'lowpass', 400, 70, 0.6, 0.3);
            tone(ctx, t + 0.2, 1.8, 'sawtooth', 80, 38, 0.07, 0.4);
            for (let i = 0; i < 14; i++) {
                const at = t + 0.5 + Math.random() * 2.2;
                tone(ctx, at, 0.09, 'sine', 260 + Math.random() * 300, 700 + Math.random() * 500, 0.09);
            }
        },
        // A ship is set on the sea
        place(ctx, t) {
            tone(ctx, t, 0.12, 'triangle', 330, 220, 0.25);
            noiseBurst(ctx, t, 0.2, 'bandpass', 1200, 500, 0.12);
        },
        // The last ship of the other fleet went down
        win(ctx, t) {
            [523, 659, 784, 1047].forEach((f, i) => tone(ctx, t + i * 0.14, i === 3 ? 0.7 : 0.18, 'triangle', f, f, 0.3));
        },
        // The own fleet is gone
        lose(ctx, t) {
            [392, 330, 262].forEach((f, i) => tone(ctx, t + i * 0.25, 0.35, 'triangle', f, f * 0.97, 0.25));
        },
        // Reaction games: a mole is hit on the head (a hollow knock)
        bonk(ctx, t) {
            tone(ctx, t, 0.12, 'square', 420, 160, 0.12);
            noiseBurst(ctx, t, 0.08, 'lowpass', 1800, 300, 0.2);
        },
        // A golden mole or balloon: a bright double chime
        gold(ctx, t) {
            tone(ctx, t, 0.12, 'triangle', 988, 988, 0.22);
            tone(ctx, t + 0.09, 0.3, 'triangle', 1319, 1319, 0.22);
        },
        // A balloon bursts: a sharp crack of noise
        pop(ctx, t) {
            noiseBurst(ctx, t, 0.09, 'highpass', 3000, 900, 0.5, 0.002);
            tone(ctx, t, 0.05, 'sine', 900, 200, 0.15);
        },
        // Ouch: a hedgehog or a black balloon (a low buzz going down)
        ouch(ctx, t) {
            tone(ctx, t, 0.3, 'sawtooth', 220, 110, 0.12);
        },
        // The light turns green
        go(ctx, t) {
            tone(ctx, t, 0.15, 'sine', 880, 880, 0.25);
        },
        // Tapped before the light turned green
        early(ctx, t) {
            tone(ctx, t, 0.12, 'square', 180, 170, 0.1);
            tone(ctx, t + 0.15, 0.18, 'square', 160, 150, 0.1);
        },
        // The countdown before a round
        tick(ctx, t) {
            tone(ctx, t, 0.06, 'sine', 660, 660, 0.15);
        },
    };

    function stored() {
        if (chosen !== null) {
            return chosen;
        }

        try {
            return localStorage.getItem(key) !== 'off';
        } catch {
            return true;
        }
    }

    window.lernkickerSounds = {
        isOn: () => stored(),
        setOn(on) {
            chosen = on;
            try {
                localStorage.setItem(key, on ? 'on' : 'off');
            } catch {
                // Not remembered (private mode); the toggle still works until the page reloads
            }

            return on;
        },
        // Plays the sounds one after another, each after its delay in seconds: [["hit", 0], ["sink", 0.6]]
        play(steps) {
            if (!stored()) {
                return;
            }

            const ctx = audio();
            if (!ctx || ctx.state !== 'running' && ctx.state !== 'suspended') {
                return;
            }

            const now = ctx.currentTime + 0.02;
            for (const [name, delay] of steps) {
                sounds[name]?.(ctx, now + (delay || 0));
            }
        },
    };
})();
