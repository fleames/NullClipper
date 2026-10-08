import { loadSettings, saveSettings, serverOriginPattern } from './lib/settings.js';

const statusEl = document.getElementById('status');
const niOptions = document.getElementById('ni-options');
const niEnabledSwitch = document.getElementById('ni-enabled-switch');
const captureTitle = document.getElementById('capture-title');
const captureSub = document.getElementById('capture-sub');
const modeHint = document.getElementById('mode-hint');

function setStatus(text) {
  statusEl.textContent = text ?? '';
}

function setSwitch(el, on) {
  el.classList.toggle('on', on);
}

function applyMode(mode) {
  const gif = mode === 'gif';
  document.getElementById('mode-snip').classList.toggle('on', !gif);
  document.getElementById('mode-gif').classList.toggle('on', gif);
  captureTitle.textContent = gif ? 'Record GIF' : 'Capture';
  captureSub.textContent = gif
    ? 'Region of this tab, up to 8 seconds'
    : 'Current tab — region or full view';
  modeHint.textContent = gif
    ? 'GIF records a region at 12 fps for up to 8s. Longest side capped at 640px.'
    : 'Snip copies a still image. Switch to GIF to record a short clip of the same region.';
}

function renderHotkey(shortcut) {
  const host = document.getElementById('hotkey-chips');
  host.replaceChildren();
  const parts = (shortcut || '').split('+').map((part) => part.trim()).filter(Boolean);
  if (!parts.length) {
    const chip = document.createElement('span');
    chip.className = 'chip';
    chip.textContent = 'Not set';
    host.append(chip);
    return;
  }
  parts.forEach((part, index) => {
    if (index) {
      const plus = document.createElement('span');
      plus.className = 'chip-plus';
      plus.textContent = '+';
      host.append(plus);
    }
    const chip = document.createElement('span');
    chip.className = 'chip';
    chip.textContent = part;
    host.append(chip);
  });
}

async function requestServerPermission(serverUrl) {
  try {
    const origin = serverOriginPattern(serverUrl);
    return chrome.permissions.request({ origins: [origin] });
  } catch {
    setStatus('Enter a valid NullImage server URL (https://…) in Settings.');
    return false;
  }
}

async function init() {
  const settings = await loadSettings();
  applyMode(settings.captureMode);
  setSwitch(niEnabledSwitch, settings.nullImageEnabled);
  niOptions.classList.toggle('hidden', !settings.nullImageEnabled);

  // chrome.permissions.request() (below) opens a native prompt that steals
  // focus — Chromium tears down this popup's document the instant that
  // happens, killing the in-flight click handler before it reaches any code
  // after the request call. That's the exact bug reported: toggling the
  // switch on never actually persisted, because the save used to happen
  // *after* requestServerPermission. Two defenses now: the save below moved
  // *before* the permission request (so a grant always persists even if the
  // popup dies the instant the prompt appears), and this self-heal, which
  // catches the other half — a stored "enabled: true" left over from a
  // previous attempt the user actually denied (the rollback on denial, a
  // few lines down, is just as vulnerable to the same popup-teardown race).
  if (settings.nullImageEnabled) {
    const hasPermission = await chrome.permissions.contains({
      origins: [serverOriginPattern(settings.nullImageServerUrl)],
    }).catch(() => false);
    if (!hasPermission) {
      settings.nullImageEnabled = false;
      setSwitch(niEnabledSwitch, false);
      niOptions.classList.add('hidden');
      await saveSettings({ nullImageEnabled: false });
    }
  }

  const commands = await chrome.commands.getAll();
  const capture = commands.find((item) => item.name === 'start-capture');
  renderHotkey(capture?.shortcut || 'Alt+Shift+X');

  document.getElementById('mode-snip').addEventListener('click', async () => {
    applyMode('snip');
    await saveSettings({ captureMode: 'snip' });
  });
  document.getElementById('mode-gif').addEventListener('click', async () => {
    applyMode('gif');
    await saveSettings({ captureMode: 'gif' });
  });

  document.getElementById('ni-enabled').addEventListener('click', async () => {
    const next = !niEnabledSwitch.classList.contains('on');

    // Update the UI and persist first — see the long comment in init()
    // above for why this has to happen before requestServerPermission,
    // not after.
    setSwitch(niEnabledSwitch, next);
    niOptions.classList.toggle('hidden', !next);
    const saved = await saveSettings({ nullImageEnabled: next });

    if (next) {
      const granted = await requestServerPermission(saved.nullImageServerUrl);
      if (!granted) {
        setSwitch(niEnabledSwitch, false);
        niOptions.classList.add('hidden');
        await saveSettings({ nullImageEnabled: false });
        setStatus('NullImage needs permission for that server origin.');
        return;
      }
    }
    setStatus('');
  });

  document.getElementById('ni-open-settings').addEventListener('click', () => {
    chrome.runtime.openOptionsPage();
  });

  document.getElementById('capture').addEventListener('click', async () => {
    setStatus('');
    try {
      await chrome.runtime.sendMessage({ type: 'NC_START_CAPTURE' });
      window.close();
    } catch (error) {
      setStatus(error?.message || 'Could not start a capture.');
    }
  });
}

init();
