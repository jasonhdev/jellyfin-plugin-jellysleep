/**
 * Jellysleep - Auto Sleep Timer
 *
 * Automatically pauses playback every 30 minutes.
 * No user interaction required to start the timer.
 * The moon button shows remaining time and allows cancellation.
 */

(function () {
  "use strict";

  const PLUGIN_NAME = "Jellysleep";
  const AUTO_PAUSE_INTERVAL_MS = 30 * 60 * 1000; // 30 minutes

  let sleepTimer = null;
  let timerStartTime = null;
  let timerButton = null;
  let tickInterval = null;

  // ─── Utilities ────────────────────────────────────────────────────────────────

  function formatRemainingTime(ms) {
    const totalSeconds = Math.max(0, Math.floor(ms / 1000));
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds.toString().padStart(2, "0")}`;
  }

  function log(msg) {
    console.log(`[${PLUGIN_NAME}] ${msg}`);
  }

  // ─── Timer Logic ──────────────────────────────────────────────────────────────

  function startAutoTimer() {
    if (sleepTimer) return; // already running

    log("Auto sleep timer started (30 min)");
    timerStartTime = Date.now();

    sleepTimer = setTimeout(() => {
      pausePlayback();
      clearCountdown();
      // Restart the timer for the next 30-minute cycle
      sleepTimer = null;
      timerStartTime = null;
      startAutoTimer();
    }, AUTO_PAUSE_INTERVAL_MS);

    startCountdown();
    updateButtonState();
  }

  function cancelTimer() {
    if (!sleepTimer) return;
    clearTimeout(sleepTimer);
    sleepTimer = null;
    timerStartTime = null;
    clearCountdown();
    updateButtonState();
    log("Sleep timer cancelled by user");
  }

  function clearCountdown() {
    if (tickInterval) {
      clearInterval(tickInterval);
      tickInterval = null;
    }
  }

  function startCountdown() {
    clearCountdown();
    tickInterval = setInterval(() => {
      updateButtonState();
    }, 1000);
  }

  function getRemainingMs() {
    if (!timerStartTime) return 0;
    return AUTO_PAUSE_INTERVAL_MS - (Date.now() - timerStartTime);
  }

  // ─── Playback Control ─────────────────────────────────────────────────────────

  function pausePlayback() {
    log("Pausing playback");

    // Use Jellyfin's ApiClient to send a pause command to the current session
    try {
      const apiClient = window.ApiClient;
      if (!apiClient) {
        log("ApiClient not available");
        return;
      }

      const sessions = apiClient.getSessions
        ? apiClient.getSessions({ ControllableByUserId: apiClient.getCurrentUserId() })
        : Promise.resolve([]);

      sessions.then((data) => {
        const currentSession = Array.isArray(data) ? data[0] : null;
        if (currentSession && currentSession.Id) {
          apiClient.sendPlayStateCommand(currentSession.Id, "Pause");
          log(`Pause sent to session ${currentSession.Id}`);
        } else {
          // Fallback: pause via the video element directly
          pauseVideoElement();
        }
      }).catch(() => {
        pauseVideoElement();
      });
    } catch (e) {
      pauseVideoElement();
    }
  }

  function pauseVideoElement() {
    const video = document.querySelector("video");
    if (video && !video.paused) {
      video.pause();
      log("Paused via video element");
    }
  }

  // ─── Button UI ────────────────────────────────────────────────────────────────

  function updateButtonState() {
    if (!timerButton) return;

    if (sleepTimer) {
      const remaining = getRemainingMs();
      timerButton.title = `Sleep timer active — pausing in ${formatRemainingTime(remaining)}\nClick to cancel`;
      timerButton.classList.add("jellysleep-active");
      // Show countdown in the button label if the element exists
      const label = timerButton.querySelector(".jellysleep-label");
      if (label) label.textContent = formatRemainingTime(remaining);
    } else {
      timerButton.title = "Sleep timer off — click to enable";
      timerButton.classList.remove("jellysleep-active");
      const label = timerButton.querySelector(".jellysleep-label");
      if (label) label.textContent = "";
    }
  }

  function createButton() {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "paper-icon-button-light jellysleep-btn";
    btn.title = "Sleep timer off — click to enable";
    btn.innerHTML = `
      <span class="material-icons md-18">bedtime</span>
      <span class="jellysleep-label" style="
        font-size: 10px;
        position: absolute;
        bottom: 2px;
        right: 2px;
        line-height: 1;
        color: var(--accent-color, #00a4dc);
        pointer-events: none;
      "></span>
    `;
    btn.style.position = "relative";

    btn.addEventListener("click", () => {
      if (sleepTimer) {
        cancelTimer();
      } else {
        startAutoTimer();
      }
    });

    return btn;
  }

  function injectButton(controls) {
    if (document.querySelector(".jellysleep-btn")) return; // already injected

    timerButton = createButton();

    // Try to insert before the settings/menu button at the end of controls
    const settingsBtn =
      controls.querySelector(".btnVideoOsdSettings") ||
      controls.querySelector('[data-action="settings"]') ||
      controls.querySelector(".rightButtons") ||
      controls.lastElementChild;

    if (settingsBtn && settingsBtn.parentNode) {
      settingsBtn.parentNode.insertBefore(timerButton, settingsBtn);
    } else {
      controls.appendChild(timerButton);
    }

    injectStyles();
    log("Button injected into player controls");
  }

  function injectStyles() {
    if (document.getElementById("jellysleep-styles")) return;
    const style = document.createElement("style");
    style.id = "jellysleep-styles";
    style.textContent = `
      .jellysleep-btn {
        opacity: 0.7;
        transition: opacity 0.2s, color 0.2s;
      }
      .jellysleep-btn:hover {
        opacity: 1;
      }
      .jellysleep-btn.jellysleep-active {
        opacity: 1;
        color: var(--accent-color, #00a4dc) !important;
      }
      .jellysleep-btn .material-icons {
        font-size: 18px;
      }
    `;
    document.head.appendChild(style);
  }

  // ─── Player Detection ─────────────────────────────────────────────────────────

  /**
   * Watches for the OSD controls to appear in the DOM.
   * Jellyfin renders the video player controls dynamically,
   * so we use a MutationObserver to detect them.
   */
  function observePlayer() {
    const observer = new MutationObserver(() => {
      // Common selectors for Jellyfin's OSD control bar
      const controlSelectors = [
        ".osdControls",
        ".videoOsdBottom",
        ".nowPlayingBar",
      ];

      for (const sel of controlSelectors) {
        const controls = document.querySelector(sel);
        if (controls) {
          injectButton(controls);
          break;
        }
      }

      // If a video is playing and the timer isn't running yet, auto-start it
      const video = document.querySelector("video");
      if (video && !video.paused && !sleepTimer) {
        startAutoTimer();
      }
    });

    observer.observe(document.body, { childList: true, subtree: true });

    // Also listen for video play events to restart the timer after each pause/resume
    document.addEventListener("play", onVideoPlay, true);
    document.addEventListener("pause", onVideoPause, true);
    document.addEventListener("ended", onVideoEnded, true);
  }

  function onVideoPlay(e) {
    if (e.target.tagName !== "VIDEO") return;
    if (!sleepTimer) {
      // Video resumed — restart the 30-min countdown fresh
      startAutoTimer();
    }
  }

  function onVideoPause(e) {
    if (e.target.tagName !== "VIDEO") return;
    // When paused (whether by us or the user), freeze the timer
    // so we don't double-count paused time.
    // We do NOT cancel — cancelling would lose the user's remaining time.
    // Instead, restart from full 30 min when they press play again.
    if (sleepTimer) {
      cancelTimer();
      log("Playback paused — timer reset (will restart on next play)");
    }
  }

  function onVideoEnded(e) {
    if (e.target.tagName !== "VIDEO") return;
    cancelTimer();
    log("Playback ended — timer cleared");
  }

  // ─── Init ─────────────────────────────────────────────────────────────────────

  function init() {
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", observePlayer);
    } else {
      observePlayer();
    }
    log("Initialized — auto sleep timer will activate on playback start");
  }

  init();
})();