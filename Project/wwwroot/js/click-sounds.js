(function () {
    "use strict";

    var storageKey = "ft-click-sound";
    var audioContext = null;
    var enabled = true;
    var lastHoverTime = 0;
    var interactiveSelector = [
        "a[href]",
        "button:not([disabled])",
        'input[type="button"]:not([disabled])',
        'input[type="submit"]:not([disabled])',
        'input[type="reset"]:not([disabled])',
        'input[type="checkbox"]:not([disabled])',
        'input[type="radio"]:not([disabled])',
        "select:not([disabled])",
        "[role='button']",
        "[role='tab']",
        "summary",
        "[tabindex='0']"
    ].join(",");

    try {
        enabled = localStorage.getItem(storageKey) !== "off";
    } catch (error) {
        console.warn("Could not read the click-sound preference; sounds are enabled for this page.", error);
    }

    function updateToggle() {
        var toggle = document.getElementById("soundToggle");
        if (!toggle) return;

        var icon = toggle.querySelector("i");
        if (icon) icon.className = enabled ? "bi bi-volume-up" : "bi bi-volume-mute";
        toggle.setAttribute("aria-pressed", enabled ? "true" : "false");
        toggle.setAttribute("aria-label", enabled ? "Turn click sounds off" : "Turn click sounds on");
        toggle.setAttribute("title", enabled ? "Click sounds on" : "Click sounds off");
    }

    function getAudioContext() {
        var AudioContextType = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextType) return null;
        if (!audioContext) audioContext = new AudioContextType();
        return audioContext;
    }

    function playTone(context, frequency, duration, volume) {
        var startTime = context.currentTime;
        var oscillator = context.createOscillator();
        var gain = context.createGain();

        oscillator.type = "sine";
        oscillator.frequency.setValueAtTime(frequency, startTime);
        gain.gain.setValueAtTime(0.0001, startTime);
        gain.gain.exponentialRampToValueAtTime(volume, startTime + 0.004);
        gain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);
        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.start(startTime);
        oscillator.stop(startTime + duration + 0.01);
        oscillator.addEventListener("ended", function () {
            oscillator.disconnect();
            gain.disconnect();
        }, { once: true });
    }

    function playNoise(context, filterType, frequency, duration, volume) {
        var startTime = context.currentTime;
        var frameCount = Math.max(1, Math.floor(context.sampleRate * duration));
        var buffer = context.createBuffer(1, frameCount, context.sampleRate);
        var samples = buffer.getChannelData(0);
        for (var i = 0; i < frameCount; i++) samples[i] = Math.random() * 2 - 1;

        var source = context.createBufferSource();
        var filter = context.createBiquadFilter();
        var gain = context.createGain();
        source.buffer = buffer;
        filter.type = filterType;
        filter.frequency.setValueAtTime(frequency, startTime);
        filter.Q.setValueAtTime(1.2, startTime);
        gain.gain.setValueAtTime(0.0001, startTime);
        gain.gain.exponentialRampToValueAtTime(volume, startTime + 0.002);
        gain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);
        source.connect(filter);
        filter.connect(gain);
        gain.connect(context.destination);
        source.start(startTime);
        source.stop(startTime + duration + 0.01);
        source.addEventListener("ended", function () {
            source.disconnect();
            filter.disconnect();
            gain.disconnect();
        }, { once: true });
    }

    function playSound(kind) {
        if (!enabled) return;

        try {
            var context = getAudioContext();
            if (!context) return;

            var play = function () {
                if (kind === "hover") {
                    playTone(context, 1900, 0.025, 0.009);
                } else if (kind === "press") {
                    playNoise(context, "lowpass", 1500, 0.035, 0.045);
                    playTone(context, 360, 0.04, 0.018);
                } else if (kind === "release") {
                    playNoise(context, "bandpass", 3100, 0.028, 0.035);
                } else if (kind === "toggle") {
                    playTone(context, 740, 0.065, 0.035);
                    playTone(context, 990, 0.075, 0.024);
                }
            };

            if (context.state === "suspended") {
                context.resume().then(play).catch(function (error) {
                    console.warn("Could not resume click-sound playback.", error);
                });
            } else {
                play();
            }
        } catch (error) {
            console.warn("Could not play the interface sound.", error);
        }
    }

    function getInteractiveTarget(event) {
        return event.target instanceof Element ? event.target.closest(interactiveSelector) : null;
    }

    function isToggleControl(target) {
        return target.matches(
            "[role='tab'], [role='switch'], input[type='checkbox'], input[type='radio'], select"
        );
    }

    document.addEventListener("pointerover", function (event) {
        if (event.pointerType !== "mouse" || !window.matchMedia("(hover: hover) and (pointer: fine)").matches)
            return;

        var target = getInteractiveTarget(event);
        if (!target || target.id === "soundToggle" ||
            (event.relatedTarget instanceof Node && target.contains(event.relatedTarget)))
            return;

        var now = performance.now();
        if (now - lastHoverTime < 100) return;
        lastHoverTime = now;
        playSound("hover");
    }, true);

    document.addEventListener("pointerdown", function (event) {
        if (event.button !== 0) return;
        var target = getInteractiveTarget(event);
        if (target && target.id !== "soundToggle" && !isToggleControl(target)) playSound("press");
    }, true);

    document.addEventListener("pointerup", function (event) {
        if (event.button !== 0) return;
        var target = getInteractiveTarget(event);
        if (target && target.id !== "soundToggle" && !isToggleControl(target)) playSound("release");
    }, true);

    document.addEventListener("click", function (event) {
        var target = getInteractiveTarget(event);
        if (event.detail === 0 && target && target.id !== "soundToggle" && !isToggleControl(target))
            playSound("toggle");

        if (!target || target.id !== "soundToggle") return;
        var wasEnabled = enabled;
        enabled = !enabled;
        try {
            localStorage.setItem(storageKey, enabled ? "on" : "off");
        } catch (error) {
            console.warn("Could not save the click-sound preference.", error);
        }
        updateToggle();
        if (!wasEnabled && enabled) playSound("toggle");
    }, true);

    document.addEventListener("change", function (event) {
        if (event.target.matches("select, input[type='checkbox'], input[type='radio']"))
            playSound("toggle");
    }, true);

    document.addEventListener("DOMContentLoaded", updateToggle);
})();
