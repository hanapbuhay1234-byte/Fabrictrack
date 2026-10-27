(function () {
    "use strict";

    var storageKey = "ft-click-sound";
    var audioContext = null;
    var enabled = true;

    try {
        enabled = localStorage.getItem(storageKey) !== "off";
    } catch (error) {
        enabled = true;
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

    function scheduleClickSound() {
        try {
            var startTime = audioContext.currentTime;
            var oscillator = audioContext.createOscillator();
            var volume = audioContext.createGain();

            oscillator.type = "sine";
            oscillator.frequency.setValueAtTime(1050, startTime);
            oscillator.frequency.exponentialRampToValueAtTime(680, startTime + 0.045);
            volume.gain.setValueAtTime(0.035, startTime);
            volume.gain.exponentialRampToValueAtTime(0.001, startTime + 0.05);

            oscillator.connect(volume);
            volume.connect(audioContext.destination);
            oscillator.start(startTime);
            oscillator.stop(startTime + 0.05);
            oscillator.addEventListener("ended", function () {
                oscillator.disconnect();
                volume.disconnect();
            }, { once: true });
        } catch (error) {
            console.warn("Could not play the click sound.", error);
        }
    }

    function playClickSound() {
        var AudioContextType = window.AudioContext || window.webkitAudioContext;
        if (!enabled || !AudioContextType) return;

        try {
            if (!audioContext) audioContext = new AudioContextType();
            if (audioContext.state === "suspended") {
                audioContext.resume().then(scheduleClickSound).catch(function (error) {
                    console.warn("Could not resume click-sound playback.", error);
                });
            } else {
                scheduleClickSound();
            }
        } catch (error) {
            console.warn("Could not initialize click-sound playback.", error);
        }
    }

    document.addEventListener("click", playClickSound, true);

    document.addEventListener("DOMContentLoaded", function () {
        var toggle = document.getElementById("soundToggle");
        updateToggle();

        if (!toggle) return;
        toggle.addEventListener("click", function () {
            enabled = !enabled;
            try {
                localStorage.setItem(storageKey, enabled ? "on" : "off");
            } catch (error) {
                console.warn("Could not save the click-sound preference.", error);
            }
            updateToggle();
        });
    });
})();
