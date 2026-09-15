export const DEFAULT_SERVER_URL = 'https://nullimage.org';

export const DEFAULTS = {
  captureMode: 'snip',
  nullImageEnabled: false,
  nullImageServerUrl: DEFAULT_SERVER_URL,
  nullImageExpiry: '1d',
  nullImageBurnAfterView: false,
  nullImagePassword: '',
};

export const EXPIRY_PRESETS = [
  { value: '1h', label: '1 hour', seconds: 60 * 60 },
  { value: '1d', label: '1 day', seconds: 24 * 60 * 60 },
  { value: '3d', label: '3 days', seconds: 3 * 24 * 60 * 60 },
  { value: '7d', label: '7 days', seconds: 7 * 24 * 60 * 60 },
];

export function expirySeconds(preset) {
  return EXPIRY_PRESETS.find((item) => item.value === preset)?.seconds ?? 24 * 60 * 60;
}

export function normalizeSettings(raw = {}) {
  const settings = { ...DEFAULTS, ...raw };
  settings.captureMode = settings.captureMode === 'gif' ? 'gif' : 'snip';
  if (!['1h', '1d', '3d', '7d'].includes(settings.nullImageExpiry)) {
    settings.nullImageExpiry = '1d';
  }
  const url = String(settings.nullImageServerUrl ?? '').trim().replace(/\/+$/, '');
  settings.nullImageServerUrl = url || DEFAULT_SERVER_URL;
  settings.nullImagePassword = settings.nullImagePassword ?? '';
  settings.nullImageEnabled = Boolean(settings.nullImageEnabled);
  settings.nullImageBurnAfterView = Boolean(settings.nullImageBurnAfterView);
  return settings;
}

export async function loadSettings() {
  const stored = await chrome.storage.local.get(DEFAULTS);
  return normalizeSettings(stored);
}

export async function saveSettings(patch) {
  const current = await loadSettings();
  const next = normalizeSettings({ ...current, ...patch });
  await chrome.storage.local.set(next);
  return next;
}

export function serverOriginPattern(serverUrl) {
  const url = new URL(serverUrl);
  return `${url.origin}/*`;
}
