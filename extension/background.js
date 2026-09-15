import { expirySeconds, loadSettings, saveSettings } from './lib/settings.js';
import { uploadToNullImage } from './lib/nullimage.js';
import { GifEncoder } from './lib/gif-encoder.js';
import {
  GIF_FPS,
  GIF_MAX_SECONDS,
  bitmapFromDataUrl,
  cropPixels,
  cropToImageData,
  imageDataToPngBlob,
  pngBlobToDataUrl,
  targetGifSize,
  timestampName,
} from './lib/image.js';

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

let state = 'idle';
let activeTabId = null;
let sourceWindowId = null;
let cropWindowId = null;
let cropTabId = null;
let cropPayload = null;
let hudWindowId = null;
let toastWindowId = null;
let stopGif = false;
let closingHud = false;
let closingCrop = false;
let copyJob = null;
let pendingSnipToast = null;

function isCapturable(url) {
  if (!url) return true;
  try {
    const parsed = new URL(url);
    if (['chrome:', 'edge:', 'opera:', 'about:', 'devtools:', 'chrome-extension:', 'moz-extension:', 'opera-extension:', 'view-source:'].includes(parsed.protocol)) {
      return false;
    }
    if (parsed.hostname === 'chrome.google.com' && parsed.pathname.startsWith('/webstore')) return false;
    if (parsed.hostname === 'chromewebstore.google.com') return false;
    if (parsed.hostname === 'microsoftedge.microsoft.com' && parsed.pathname.includes('/addons')) return false;
    if (parsed.hostname === 'addons.opera.com') return false;
    return true;
  } catch {
    return false;
  }
}

function captureFailDetail(error, url) {
  const msg = String(error?.message || '');
  if (/ExtensionsSettings|cannot be scripted/i.test(msg)) {
    return 'This browser blocked the page (ExtensionsSettings). Open a normal https:// website — not opera://, chrome://, or the add-ons store.';
  }
  if (/Cannot access|cannot capture|restricted|The extensions gallery|chrome:\/\//i.test(msg)) {
    return 'Restricted page (chrome://, opera://, edge://, or the store). Switch to a normal website tab.';
  }
  if (url && !isCapturable(url)) {
    return 'Restricted page (browser settings, chrome://, opera://, or the store). Open a normal website tab.';
  }
  return msg || 'Could not capture this tab. Try a normal website.';
}

async function activeTab() {
  const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  return tab;
}

function offscreenSupported() {
  return typeof chrome.offscreen?.createDocument === 'function';
}

async function ensureOffscreen() {
  if (!offscreenSupported()) return false;
  let existed = false;
  try {
    existed = await chrome.offscreen.hasDocument();
  } catch {
    return false;
  }
  if (existed) return true;
  try {
    await chrome.offscreen.createDocument({
      url: 'offscreen.html',
      reasons: ['CLIPBOARD'],
      justification: 'Copy PNG, GIF, or NullImage links to the clipboard',
    });
    await delay(40);
    return true;
  } catch (error) {
    if (/already exists|Only a single offscreen/i.test(String(error))) {
      return true;
    }
    return false;
  }
}

async function focusOverlay(overlayTabId) {
  if (cropWindowId) {
    try {
      await chrome.windows.update(cropWindowId, { focused: true });
    } catch {
      // Tab-hosted overlay still works if the tab is active.
    }
  }
  if (overlayTabId) {
    try {
      await chrome.tabs.update(overlayTabId, { active: true });
    } catch {
      // Copy message still targets this tab if it is open.
    }
  }
}

async function copyViaOverlayTab(overlayTabId, { png, gif, text } = {}) {
  await focusOverlay(overlayTabId);
  await delay(20);
  const payload = { type: 'NC_OVERLAY_COPY', png, gif, text };
  try {
    const response = await chrome.tabs.sendMessage(overlayTabId, payload);
    if (response?.ok) return response.copied;
    throw new Error(response?.error || 'Clipboard write failed');
  } catch (tabError) {
    const response = await chrome.runtime.sendMessage(payload);
    if (response?.ok) return response.copied;
    throw tabError;
  }
}

async function copyViaOffscreen({ png, gif, text } = {}) {
  const ready = await ensureOffscreen();
  if (!ready) {
    throw new Error('Offscreen clipboard is not available');
  }
  const response = await chrome.runtime.sendMessage({
    type: 'NC_OFFSCREEN_COPY',
    png,
    gif,
    text,
  });
  if (!response?.ok) {
    throw new Error(response?.error || 'Clipboard write failed');
  }
  return response.copied;
}

async function copyViaFocusedOverlay({ png, gif, text } = {}) {
  if (copyJob) {
    throw new Error('Clipboard is busy — try again');
  }
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      fail(new Error('Clipboard write timed out'));
    }, 10000);

    const finish = (fn, value) => {
      clearTimeout(timeout);
      const winId = copyJob?.windowId;
      const tabId = copyJob?.tabId;
      const closeWindow = copyJob?.closeWindow;
      copyJob = null;
      const close = () => {
        if (closeWindow && winId) {
          chrome.windows.remove(winId).catch(() => {});
        } else if (tabId) {
          chrome.tabs.remove(tabId).catch(() => {});
        }
      };
      delay(60).then(close);
      fn(value);
    };

    const succeed = (value) => finish(resolve, value);
    const fail = (error) => finish(reject, error);

    copyJob = {
      payload: { png, gif, text },
      resolve: succeed,
      reject: fail,
      windowId: null,
      tabId: null,
      closeWindow: false,
    };

    const copyUrl = chrome.runtime.getURL('overlay.html?copy=1');
    chrome.windows.create({
      url: copyUrl,
      type: 'popup',
      focused: true,
      width: 280,
      height: 120,
    }).then((created) => {
      if (!copyJob) return;
      copyJob.windowId = created.id ?? null;
      copyJob.tabId = created.tabs?.[0]?.id ?? null;
      copyJob.closeWindow = true;
      if (created.id) {
        chrome.windows.update(created.id, { focused: true }).catch(() => {});
      }
      if (copyJob.tabId) {
        chrome.tabs.update(copyJob.tabId, { active: true }).catch(() => {});
      }
    }).catch(async () => {
      try {
        const tab = await chrome.tabs.create({ url: copyUrl, active: true });
        if (!copyJob) {
          if (tab.id) chrome.tabs.remove(tab.id).catch(() => {});
          return;
        }
        copyJob.tabId = tab.id ?? null;
        copyJob.windowId = tab.windowId ?? null;
        copyJob.closeWindow = false;
      } catch (error) {
        fail(error);
      }
    });
  });
}

async function copyClipboard({ png, gif, text } = {}, overlayTabId = cropTabId) {
  if (overlayTabId) {
    try {
      return await copyViaOverlayTab(overlayTabId, { png, gif, text });
    } catch {
      // Overlay may have closed; use a focused copy window next.
    }
  }
  try {
    return await copyViaFocusedOverlay({ png, gif, text });
  } catch (focusedError) {
    if (!offscreenSupported()) {
      throw focusedError;
    }
    try {
      return await copyViaOffscreen({ png, gif, text });
    } catch {
      throw focusedError;
    }
  }
}

async function showToast({ title, detail = '', previewDataUrl = '', gif = false }) {
  await chrome.storage.session.set({ toast: { title, detail, previewDataUrl, gif } });
  if (toastWindowId) {
    try {
      await chrome.windows.get(toastWindowId);
      return;
    } catch {
      toastWindowId = null;
    }
  }

  let left = 80;
  let top = 80;
  try {
    const win = await chrome.windows.getLastFocused();
    left = Math.round((win.left ?? 80) + (win.width ?? 800) - 440);
    top = Math.round((win.top ?? 80) + (win.height ?? 600) - 180);
  } catch {
    // Place with defaults.
  }

  const created = await chrome.windows.create({
    url: chrome.runtime.getURL('toast.html'),
    type: 'popup',
    focused: false,
    width: 430,
    height: 150,
    left: Math.max(0, left),
    top: Math.max(0, top),
  });
  toastWindowId = created.id ?? null;
}

async function persistCrop(payload) {
  cropPayload = payload;
  try {
    await chrome.storage.session.set({
      crop: {
        dataUrl: payload.dataUrl,
        mode: payload.mode,
        tabId: payload.tabId,
        windowId: payload.windowId,
      },
    });
  } catch {
    // Session quota may reject a huge screenshot; in-memory payload is enough if the worker stays alive.
  }
}

async function takeCropPayload() {
  if (cropPayload?.dataUrl) return cropPayload;
  try {
    const { crop } = await chrome.storage.session.get('crop');
    if (crop?.dataUrl) {
      cropPayload = crop;
      return crop;
    }
  } catch {
    // Fall through.
  }
  return null;
}

async function clearCropPayload() {
  cropPayload = null;
  try {
    await chrome.storage.session.remove('crop');
  } catch {
    // Ignore.
  }
}

async function closeCropUi() {
  closingCrop = true;
  const windowId = cropWindowId;
  const tabId = cropTabId;
  cropWindowId = null;
  cropTabId = null;
  if (windowId) {
    try {
      await chrome.windows.remove(windowId);
    } catch {
      // Already closed.
    }
  } else if (tabId) {
    try {
      await chrome.tabs.remove(tabId);
    } catch {
      // Already closed.
    }
  }
  await delay(30);
  closingCrop = false;
}

async function cancelOverlay() {
  state = 'idle';
  activeTabId = null;
  sourceWindowId = null;
  await clearCropPayload();
  await closeCropUi();
}

async function openCropUi(tab, dataUrl, mode) {
  closingCrop = false;
  await persistCrop({
    dataUrl,
    mode,
    tabId: tab.id,
    windowId: tab.windowId,
  });

  let bounds = { width: 1200, height: 800, left: 80, top: 80 };
  try {
    const win = await chrome.windows.get(tab.windowId);
    bounds = {
      width: Math.max(480, Math.round(win.width ?? 1200)),
      height: Math.max(320, Math.round(win.height ?? 800)),
      left: Math.round(win.left ?? 80),
      top: Math.round(win.top ?? 80),
    };
  } catch {
    // Defaults.
  }

  const url = chrome.runtime.getURL('overlay.html');
  try {
    const created = await chrome.windows.create({
      url,
      type: 'popup',
      focused: true,
      width: bounds.width,
      height: bounds.height,
      left: bounds.left,
      top: bounds.top,
    });
    cropWindowId = created.id ?? null;
    cropTabId = created.tabs?.[0]?.id ?? null;
    return;
  } catch {
    // Opera or policy may refuse a popup; open an extension tab instead.
  }

  const cropTab = await chrome.tabs.create({ url, active: true });
  cropTabId = cropTab.id ?? null;
  cropWindowId = null;
}

async function openHud(tab) {
  closingHud = false;
  let left = 200;
  let top = 120;
  try {
    const win = await chrome.windows.get(tab.windowId);
    left = Math.round((win.left ?? 0) + ((win.width ?? 280) - 280) / 2);
    top = Math.round((win.top ?? 0) + 90);
  } catch {
    // Defaults.
  }
  const hud = await chrome.windows.create({
    url: chrome.runtime.getURL('hud.html'),
    type: 'popup',
    focused: false,
    width: 280,
    height: 120,
    left,
    top,
  });
  hudWindowId = hud.id ?? null;
}

async function closeHud() {
  closingHud = true;
  const id = hudWindowId;
  hudWindowId = null;
  if (id) {
    try {
      await chrome.windows.remove(id);
    } catch {
      // Already closed.
    }
  }
}

function formatTick(elapsedMs) {
  const total = Math.min(GIF_MAX_SECONDS, Math.max(0, elapsedMs / 1000));
  const minutes = Math.floor(total / 60);
  const seconds = Math.floor(total % 60);
  return `${minutes}:${String(seconds).padStart(2, '0')} / 0:08`;
}

async function broadcastTick(elapsedMs) {
  try {
    await chrome.runtime.sendMessage({ type: 'NC_GIF_TICK', label: formatTick(elapsedMs) });
  } catch {
    // HUD may not be listening yet.
  }
}

async function startCapture() {
  if (state === 'overlay') {
    await cancelOverlay();
    return;
  }
  if (state === 'gif') {
    stopGif = true;
    return;
  }

  const tab = await activeTab();
  if (!tab?.id) {
    await showToast({ title: 'Nothing to capture', detail: 'Focus a browser tab first.' });
    return;
  }
  if (tab.id === cropTabId) {
    await showToast({
      title: 'Nothing to capture',
      detail: 'Focus the website tab, then capture again.',
    });
    return;
  }
  if (!isCapturable(tab.url)) {
    await showToast({
      title: 'Can’t capture this page',
      detail: 'Browser pages (chrome://, opera://, edge://) and the add-ons store are locked down. Open a normal website tab.',
    });
    return;
  }

  try {
    await chrome.windows.update(tab.windowId, { focused: true });
    await chrome.tabs.update(tab.id, { active: true });
  } catch {
    // captureVisibleTab still needs the tab visible if we can manage it.
  }
  await delay(40);

  let dataUrl;
  try {
    dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: 'png' });
  } catch (error) {
    await showToast({
      title: 'Could not capture tab',
      detail: captureFailDetail(error, tab.url),
    });
    return;
  }

  const settings = await loadSettings();
  try {
    await openCropUi(tab, dataUrl, settings.captureMode);
  } catch (error) {
    await clearCropPayload();
    await showToast({
      title: 'Could not open crop window',
      detail: error?.message || 'Try again from the toolbar icon.',
    });
    return;
  }

  state = 'overlay';
  activeTabId = tab.id;
  sourceWindowId = tab.windowId;
}

async function prepareSnip(png, width, height) {
  state = 'idle';
  activeTabId = null;
  sourceWindowId = null;
  await clearCropPayload();
  const settings = await loadSettings();
  const previewDataUrl = await pngBlobToDataUrl(new Blob([png], { type: 'image/png' }));
  pendingSnipToast = { previewDataUrl, width, height };

  if (!settings.nullImageEnabled) {
    return {
      ok: true,
      copy: 'png',
      width,
      height,
    };
  }

  await showToast({
    title: 'Uploading to NullImage…',
    detail: `${width} × ${height}`,
    previewDataUrl,
  });
  try {
    const uploaded = await uploadToNullImage(new Uint8Array(png), timestampName('png'), 'image/png', {
      serverUrl: settings.nullImageServerUrl,
      expirySeconds: expirySeconds(settings.nullImageExpiry),
      burnAfterView: settings.nullImageBurnAfterView,
      password: settings.nullImagePassword || undefined,
    });
    return {
      ok: true,
      copy: 'text',
      text: uploaded.shareUrl,
      width,
      height,
      nullImage: true,
    };
  } catch (error) {
    return {
      ok: true,
      copy: 'png',
      width,
      height,
      nullImage: true,
      uploadError: error?.message || 'Upload failed',
    };
  }
}

async function toastSnipResult({
  copied,
  width,
  height,
  shareUrl,
  uploadError,
  nullImage,
  copyError,
}) {
  const pending = pendingSnipToast;
  pendingSnipToast = null;
  const previewDataUrl = pending?.previewDataUrl || '';
  const snipWidth = width ?? pending?.width;
  const snipHeight = height ?? pending?.height;
  if (copied === 'text' && nullImage && shareUrl) {
    await showToast({
      title: 'Link copied to clipboard',
      detail: shareUrl,
      previewDataUrl,
    });
    return;
  }
  if (nullImage && uploadError) {
    await showToast({
      title: copied ? 'Upload failed — copied snip instead' : 'Upload failed',
      detail: uploadError,
      previewDataUrl,
    });
    return;
  }
  if (copied) {
    await showToast({
      title: 'Snip saved to clipboard',
      detail: `${snipWidth} × ${snipHeight}`,
      previewDataUrl,
    });
    return;
  }
  await showToast({
    title: 'Could not copy snip',
    detail: copyError || '',
    previewDataUrl,
  });
}

async function recordGif(tab, rect, viewport) {
  stopGif = false;
  state = 'gif';
  const encoder = new GifEncoder({ delayCs: Math.round(100 / GIF_FPS) });
  const interval = 1000 / GIF_FPS;
  const deadline = Date.now() + GIF_MAX_SECONDS * 1000;
  let target = null;
  let frames = 0;
  let previewDataUrl = '';
  let previewPng = null;

  try {
    await chrome.tabs.update(tab.id, { active: true });
  } catch {
    // Keep going; captureVisibleTab still needs the tab visible.
  }

  try {
    await openHud(tab);
  } catch {
    // Timer HUD is optional; recording can still finish at 8s.
  }

  try {
    await chrome.windows.update(tab.windowId, { focused: true });
  } catch {
    // If focus fails, captureVisibleTab may still work.
  }

  await delay(80);

  const startedAt = Date.now();
  try {
    while (!stopGif && Date.now() < deadline) {
      const frameStarted = Date.now();
      await broadcastTick(Date.now() - startedAt);
      let dataUrl;
      try {
        dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: 'png' });
      } catch {
        break;
      }
      const bitmap = await bitmapFromDataUrl(dataUrl);
      if (!target) {
        const crop = cropPixels(bitmap, rect, viewport.width, viewport.height);
        target = targetGifSize(crop.sw, crop.sh);
      }
      const imageData = await cropToImageData(
        bitmap,
        rect,
        viewport.width,
        viewport.height,
        target,
      );
      bitmap.close?.();
      if (!previewDataUrl) {
        const blob = await imageDataToPngBlob(imageData);
        previewPng = await blob.arrayBuffer();
        previewDataUrl = await pngBlobToDataUrl(blob);
      }
      encoder.addFrame(imageData);
      frames += 1;
      const wait = interval - (Date.now() - frameStarted);
      if (wait > 0 && !stopGif) {
        await delay(wait);
      }
    }
  } finally {
    await closeHud();
  }

  if (!frames || !target) {
    return null;
  }

  await showToast({
    title: 'Encoding GIF…',
    detail: 'Quantizing frames',
    previewDataUrl,
    gif: true,
  });
  const bytes = encoder.finish();
  return {
    bytes,
    previewDataUrl,
    previewPng,
    frames,
    width: target.width,
    height: target.height,
  };
}

async function finishGif(tab, rect, viewport) {
  let result;
  try {
    result = await recordGif(tab, rect, viewport);
  } catch (error) {
    state = 'idle';
    activeTabId = null;
    sourceWindowId = null;
    await showToast({
      title: 'Could not record GIF',
      detail: error?.message || 'Try again on this tab.',
      gif: true,
    });
    return;
  }

  state = 'idle';
  activeTabId = null;
  sourceWindowId = null;
  if (!result) {
    return;
  }

  const settings = await loadSettings();
  const detail = `${result.width} × ${result.height} · ${result.frames} frames`;

  if (settings.nullImageEnabled) {
    await showToast({
      title: 'Uploading to NullImage…',
      detail,
      previewDataUrl: result.previewDataUrl,
      gif: true,
    });
    try {
      const uploaded = await uploadToNullImage(result.bytes, timestampName('gif'), 'image/gif', {
        serverUrl: settings.nullImageServerUrl,
        expirySeconds: expirySeconds(settings.nullImageExpiry),
        burnAfterView: settings.nullImageBurnAfterView,
        password: settings.nullImagePassword || undefined,
      });
      await copyClipboard({ text: uploaded.shareUrl });
      await showToast({
        title: 'Link copied to clipboard',
        detail: uploaded.shareUrl,
        previewDataUrl: result.previewDataUrl,
        gif: true,
      });
    } catch (error) {
      let copied = 'gif';
      try {
        copied = await copyClipboard({ gif: result.bytes.buffer, png: result.previewPng });
      } catch {
        copied = 'none';
      }
      await showToast({
        title: copied === 'none' ? 'Upload failed' : 'Upload failed — copied GIF instead',
        detail: error?.message || 'Clipboard fallback',
        previewDataUrl: result.previewDataUrl,
        gif: true,
      });
    }
    return;
  }

  try {
    const copied = await copyClipboard({ gif: result.bytes.buffer, png: result.previewPng });
    await showToast({
      title: copied === 'png-fallback' ? 'GIF encoded — copied PNG preview' : 'GIF saved to clipboard',
      detail: copied === 'png-fallback'
        ? `${detail}. This browser wouldn’t accept image/gif on the clipboard.`
        : detail,
      previewDataUrl: result.previewDataUrl,
      gif: true,
    });
  } catch (error) {
    await showToast({
      title: 'Could not copy GIF',
      detail: error?.message || 'Clipboard is busy — try again',
      previewDataUrl: result.previewDataUrl,
      gif: true,
    });
  }
}

chrome.commands.onCommand.addListener((command) => {
  if (command === 'start-capture') {
    startCapture();
  }
});

chrome.runtime.onConnect.addListener((port) => {
  if (port.name !== 'nc-hud') return;
  port.onDisconnect.addListener(() => {
    if (state === 'gif' && !closingHud) {
      stopGif = true;
    }
  });
});

chrome.windows.onRemoved.addListener((windowId) => {
  if (windowId === hudWindowId) {
    hudWindowId = null;
    if (state === 'gif' && !closingHud) {
      stopGif = true;
    }
  }
  if (windowId === toastWindowId) {
    toastWindowId = null;
  }
  if (windowId === cropWindowId) {
    cropWindowId = null;
    cropTabId = null;
    if (state === 'overlay' && !closingCrop) {
      state = 'idle';
      activeTabId = null;
      sourceWindowId = null;
      clearCropPayload();
    }
  }
  if (copyJob && windowId === copyJob.windowId) {
    copyJob.windowId = null;
  }
});

chrome.tabs.onRemoved.addListener((tabId) => {
  if (tabId === cropTabId) {
    cropTabId = null;
    if (state === 'overlay' && !closingCrop) {
      state = 'idle';
      activeTabId = null;
      sourceWindowId = null;
      cropWindowId = null;
      clearCropPayload();
    }
  }
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || typeof message.type !== 'string') return undefined;

  if (message.type === 'NC_START_CAPTURE') {
    startCapture().then(() => sendResponse({ ok: true })).catch((error) => {
      sendResponse({ ok: false, error: error?.message || String(error) });
    });
    return true;
  }

  if (message.type === 'NC_GET_CROP') {
    takeCropPayload().then((payload) => {
      if (!payload?.dataUrl) {
        sendResponse({ ok: false });
        return;
      }
      sendResponse({
        ok: true,
        dataUrl: payload.dataUrl,
        mode: payload.mode === 'gif' ? 'gif' : 'snip',
        tabId: payload.tabId,
        windowId: payload.windowId,
      });
    }).catch(() => sendResponse({ ok: false }));
    return true;
  }

  if (message.type === 'NC_SET_MODE') {
    saveSettings({ captureMode: message.mode === 'gif' ? 'gif' : 'snip' });
    sendResponse({ ok: true });
    return undefined;
  }

  if (message.type === 'NC_CANCEL') {
    state = 'idle';
    activeTabId = null;
    sourceWindowId = null;
    clearCropPayload();
    sendResponse({ ok: true });
    return undefined;
  }

  if (message.type === 'NC_STOP_GIF') {
    stopGif = true;
    sendResponse({ ok: true });
    return undefined;
  }

  if (message.type === 'NC_ERROR') {
    state = 'idle';
    activeTabId = null;
    sourceWindowId = null;
    clearCropPayload();
    showToast({ title: 'Capture failed', detail: message.message || '' });
    sendResponse({ ok: true });
    return undefined;
  }

  if (message.type === 'NC_COPY_WINDOW_READY') {
    const job = copyJob;
    const tabId = sender.tab?.id;
    if (!job) {
      sendResponse({ ok: false, error: 'Clipboard write failed' });
      return undefined;
    }
    if (tabId) job.tabId = job.tabId ?? tabId;
    if (sender.tab?.windowId) {
      job.windowId = job.windowId ?? sender.tab.windowId;
      chrome.windows.update(sender.tab.windowId, { focused: true }).catch(() => {});
    }
    sendResponse({
      ok: true,
      png: job.payload.png,
      gif: job.payload.gif,
      text: job.payload.text,
    });
    return undefined;
  }

  if (message.type === 'NC_COPY_WINDOW_DONE') {
    const job = copyJob;
    if (job) {
      if (message.ok) {
        job.resolve(message.copied);
      } else {
        job.reject(new Error(message.error || 'Clipboard write failed'));
      }
    }
    sendResponse({ ok: true });
    return undefined;
  }

  if (message.type === 'NC_SNIP') {
    prepareSnip(message.png, message.width, message.height)
      .then((result) => sendResponse(result))
      .catch((error) => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message.type === 'NC_SNIP_COPIED') {
    toastSnipResult(message)
      .then(() => sendResponse({ ok: true }))
      .catch(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === 'NC_GIF_REGION') {
    const tabId = message.tabId || activeTabId || cropPayload?.tabId;
    const windowId = message.windowId || sourceWindowId || cropPayload?.windowId;
    state = 'gif';
    clearCropPayload();
    const loadTab = tabId
      ? chrome.tabs.get(tabId)
      : Promise.reject(new Error('Source tab is gone'));
    loadTab.then(async (tab) => {
      const target = windowId ? { ...tab, windowId } : tab;
      try {
        await chrome.windows.update(target.windowId, { focused: true });
        await chrome.tabs.update(target.id, { active: true });
      } catch {
        // Recording can still proceed if the tab is visible.
      }
      await finishGif(target, message.rect, message.viewport);
    }).catch((error) => {
      state = 'idle';
      showToast({ title: 'Could not record GIF', detail: error?.message || '' });
    });
    sendResponse({ ok: true });
    return undefined;
  }

  return undefined;
});
