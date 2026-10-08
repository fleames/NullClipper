import { loadSettings, saveSettings, serverOriginPattern } from './lib/settings.js';

const statusEl = document.getElementById('status');
const niOptions = document.getElementById('ni-options');
const niEnabledSwitch = document.getElementById('ni-enabled-switch');
const niBurnSwitch = document.getElementById('ni-burn-switch');

function setStatus(text) {
  statusEl.textContent = text ?? '';
}

function setSwitch(el, on) {
  el.classList.toggle('on', on);
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
  setSwitch(niEnabledSwitch, settings.nullImageEnabled);
  niOptions.classList.toggle('hidden', !settings.nullImageEnabled);
  document.getElementById('ni-server').value = settings.nullImageServerUrl;
  document.getElementById('ni-expiry').value = settings.nullImageExpiry;
  setSwitch(niBurnSwitch, settings.nullImageBurnAfterView);
  document.getElementById('ni-password').value = settings.nullImagePassword;

  document.getElementById('ni-enabled').addEventListener('click', async () => {
    const next = !niEnabledSwitch.classList.contains('on');

    // Same ordering as popup.js, and for the same reason: persist the new
    // state before requesting permission, not after, since the permission
    // prompt can tear down whatever UI called it before an awaited promise
    // resolves. An options page tab is less fragile than a popup here, but
    // there's no upside to relying on that — same fix, same safety margin.
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
}

init();
