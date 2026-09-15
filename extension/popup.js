import { loadSettings, saveSettings, serverOriginPattern } from './lib/settings.js';

const statusEl = document.getElementById('status');
const niOptions = document.getElementById('ni-options');
const niEnabledSwitch = document.getElementById('ni-enabled-switch');
const niBurnSwitch = document.getElementById('ni-burn-switch');
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
    setStatus('Enter a valid NullImage server URL (https://…).');
    return false;
  }
}

async function init() {
  const settings = await loadSettings();
  applyMode(settings.captureMode);
  setSwitch(niEnabledSwitch, settings.nullImageEnabled);
  niOptions.classList.toggle('hidden', !settings.nullImageEnabled);
  document.getElementById('ni-server').value = settings.nullImageServerUrl;
  document.getElementById('ni-expiry').value = settings.nullImageExpiry;
  setSwitch(niBurnSwitch, settings.nullImageBurnAfterView);
  document.getElementById('ni-password').value = settings.nullImagePassword;

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
    if (next) {
      const serverUrl = document.getElementById('ni-server').value;
      const granted = await requestServerPermission(serverUrl);
      if (!granted) {
        setStatus('NullImage needs permission for that server origin.');
        return;
      }
    }
    setSwitch(niEnabledSwitch, next);
    niOptions.classList.toggle('hidden', !next);
    await saveSettings({ nullImageEnabled: next });
    setStatus('');
  });

  document.getElementById('ni-burn').addEventListener('click', async () => {
    const next = !niBurnSwitch.classList.contains('on');
    setSwitch(niBurnSwitch, next);
    await saveSettings({ nullImageBurnAfterView: next });
  });

  document.getElementById('ni-server').addEventListener('change', async (event) => {
    const serverUrl = event.target.value.trim();
    const saved = await saveSettings({ nullImageServerUrl: serverUrl });
    event.target.value = saved.nullImageServerUrl;
    if (niEnabledSwitch.classList.contains('on')) {
      const granted = await requestServerPermission(saved.nullImageServerUrl);
      if (!granted) setStatus('Grant host permission for that NullImage origin, or uploads will fail.');
      else setStatus('');
    }
  });

  document.getElementById('ni-expiry').addEventListener('change', async (event) => {
    await saveSettings({ nullImageExpiry: event.target.value });
  });

  let passwordTimer = 0;
  document.getElementById('ni-password').addEventListener('input', (event) => {
    clearTimeout(passwordTimer);
    passwordTimer = setTimeout(() => {
      saveSettings({ nullImagePassword: event.target.value });
    }, 250);
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
