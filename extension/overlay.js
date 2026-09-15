import { copyInDocument } from './lib/clipboard.js';

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const copyOnly = new URLSearchParams(location.search).get('copy') === '1';

if (copyOnly) {
  document.documentElement.classList.add('copy-only');
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type !== 'NC_OVERLAY_COPY') return undefined;
  (async () => {
    try {
      window.focus();
    } catch {
      // write() still requires this document to be focused.
    }
    await delay(30);
    const copied = await copyInDocument({
      png: message.png,
      gif: message.gif,
      text: message.text,
    });
    sendResponse({ ok: true, copied });
  })().catch((error) => {
    sendResponse({ ok: false, error: error?.message || String(error) });
  });
  return true;
});

if (copyOnly) {
  chrome.runtime.sendMessage({ type: 'NC_COPY_WINDOW_READY' }).then(async (response) => {
    try {
      if (!response?.ok) {
        throw new Error(response?.error || 'Clipboard write failed');
      }
      try {
        window.focus();
      } catch {
        // write() requires this document to be focused.
      }
      await delay(30);
      const copied = await copyInDocument({
        png: response.png,
        gif: response.gif,
        text: response.text,
      });
      await chrome.runtime.sendMessage({ type: 'NC_COPY_WINDOW_DONE', ok: true, copied });
    } catch (error) {
      await chrome.runtime.sendMessage({
        type: 'NC_COPY_WINDOW_DONE',
        ok: false,
        error: error?.message || String(error),
      }).catch(() => {});
    }
  }).catch(() => {});
} else {
  bootCropUi();
}

function bootCropUi() {
  const freeze = document.querySelector('.nc-freeze');
  const dim = document.querySelector('.nc-dim');
  const selectionEl = document.querySelector('.nc-selection');
  const badge = document.querySelector('.nc-badge');
  const snipBtn = document.querySelector('[data-kind="snip"]');
  const gifBtn = document.querySelector('[data-kind="gif"]');
  const regionBtn = document.querySelector('.region');
  const fullBtn = document.querySelector('.full');
  const wrap = document.querySelector('.nc-root');

  let selecting = false;
  let start = null;
  let rect = null;
  let mode = 'snip';
  let closed = false;
  let sourceTabId = null;
  let sourceWinId = null;

  function setKind(next, persist) {
    mode = next === 'gif' ? 'gif' : 'snip';
    snipBtn.classList.toggle('on', mode === 'snip');
    gifBtn.classList.toggle('on', mode === 'gif');
    if (persist) {
      chrome.runtime.sendMessage({ type: 'NC_SET_MODE', mode }).catch(() => {});
    }
  }

  function show(dataUrl, nextMode) {
    closed = false;
    selecting = false;
    start = null;
    rect = null;
    setKind(nextMode, false);
    regionBtn.classList.add('on');
    fullBtn.classList.remove('on');
    selectionEl.style.display = 'none';
    badge.style.display = 'none';
    dim.style.display = 'block';
    return new Promise((resolve, reject) => {
      const done = () => resolve();
      freeze.onload = done;
      freeze.onerror = () => reject(new Error('Could not load screenshot'));
      freeze.src = dataUrl;
      if (freeze.complete && freeze.naturalWidth) done();
    });
  }

  function finish() {
    if (closed) return;
    closed = true;
    window.close();
  }

  function cancel() {
    if (closed) return;
    closed = true;
    chrome.runtime.sendMessage({ type: 'NC_CANCEL' }).catch(() => {}).finally(() => window.close());
  }

  function viewport() {
    return { width: window.innerWidth, height: window.innerHeight };
  }

  function pixelSize(box) {
    if (!freeze?.naturalWidth) {
      return { width: Math.round(box.w), height: Math.round(box.h) };
    }
    const scaleX = freeze.naturalWidth / Math.max(1, freeze.clientWidth);
    const scaleY = freeze.naturalHeight / Math.max(1, freeze.clientHeight);
    return {
      width: Math.max(1, Math.ceil(box.w * scaleX)),
      height: Math.max(1, Math.ceil(box.h * scaleY)),
    };
  }

  function normalizeBox(a, b) {
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    const w = Math.abs(a.x - b.x);
    const h = Math.abs(a.y - b.y);
    return {
      x: Math.max(0, x),
      y: Math.max(0, y),
      w: Math.min(w, window.innerWidth - Math.max(0, x)),
      h: Math.min(h, window.innerHeight - Math.max(0, y)),
    };
  }

  function paintSelection(box) {
    if (!box || box.w < 1 || box.h < 1) {
      selectionEl.style.display = 'none';
      badge.style.display = 'none';
      dim.style.display = 'block';
      return;
    }
    dim.style.display = 'none';
    selectionEl.style.display = 'block';
    selectionEl.style.left = `${box.x}px`;
    selectionEl.style.top = `${box.y}px`;
    selectionEl.style.width = `${box.w}px`;
    selectionEl.style.height = `${box.h}px`;
    const px = pixelSize(box);
    badge.textContent = `${px.width} × ${px.height}`;
    badge.style.display = 'block';
    let badgeY = box.y + box.h + 8;
    if (badgeY + 32 > window.innerHeight) badgeY = Math.max(8, box.y - 32);
    const badgeX = Math.min(Math.max(8, box.x), Math.max(8, window.innerWidth - 120));
    badge.style.left = `${badgeX}px`;
    badge.style.top = `${badgeY}px`;
  }

  function fromChrome(event) {
    return Boolean(event.target.closest('.nc-toolbar'));
  }

  function onMouseDown(event) {
    if (event.button !== 0) return;
    if (fromChrome(event)) return;
    event.preventDefault();
    selecting = true;
    start = { x: event.clientX, y: event.clientY };
    rect = { x: start.x, y: start.y, w: 0, h: 0 };
    paintSelection(rect);
  }

  function onMouseMove(event) {
    if (!selecting || !start) return;
    rect = normalizeBox(start, { x: event.clientX, y: event.clientY });
    paintSelection(rect);
  }

  function onMouseUp() {
    if (!selecting) return;
    selecting = false;
    if (!rect || rect.w < 3 || rect.h < 3) {
      paintSelection(null);
      return;
    }
    completeRect(rect);
  }

  function onKeyDown(event) {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      cancel();
    }
  }

  async function cropPng(box) {
    const scaleX = freeze.naturalWidth / Math.max(1, freeze.clientWidth);
    const scaleY = freeze.naturalHeight / Math.max(1, freeze.clientHeight);
    const sx = Math.max(0, Math.floor(box.x * scaleX));
    const sy = Math.max(0, Math.floor(box.y * scaleY));
    const sw = Math.max(1, Math.min(freeze.naturalWidth - sx, Math.ceil(box.w * scaleX)));
    const sh = Math.max(1, Math.min(freeze.naturalHeight - sy, Math.ceil(box.h * scaleY)));
    const canvas = document.createElement('canvas');
    canvas.width = sw;
    canvas.height = sh;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(freeze, sx, sy, sw, sh, 0, 0, sw, sh);
    const blob = await new Promise((resolve, reject) => {
      canvas.toBlob((next) => (next ? resolve(next) : reject(new Error('Could not crop snip'))), 'image/png');
    });
    return { buffer: await blob.arrayBuffer(), width: sw, height: sh };
  }

  async function completeRect(box) {
    if (closed) return;
    const kind = mode;
    const view = viewport();
    if (kind === 'gif') {
      closed = true;
      await chrome.runtime.sendMessage({
        type: 'NC_GIF_REGION',
        rect: box,
        viewport: view,
        tabId: sourceTabId,
        windowId: sourceWinId,
      }).catch(() => {});
      window.close();
      return;
    }
    let copied = null;
    try {
      const png = await cropPng(box);
      closed = true;
      wrap.style.pointerEvents = 'none';
      try {
        window.focus();
      } catch {
        // write() requires this document to stay focused.
      }

      let copyError = '';
      try {
        copied = await copyInDocument({ png: png.buffer });
      } catch (error) {
        copyError = error?.message || 'Clipboard write failed';
      }

      let prepared = null;
      try {
        prepared = await chrome.runtime.sendMessage({
          type: 'NC_SNIP',
          png: png.buffer,
          width: png.width,
          height: png.height,
        });
      } catch (error) {
        if (!copied) throw error;
      }

      if (prepared?.copy === 'text' && prepared.text) {
        try {
          window.focus();
        } catch {
          // Recapture focus after the upload toast.
        }
        try {
          copied = await copyInDocument({ text: prepared.text });
          copyError = '';
        } catch (error) {
          if (!copied) {
            copyError = error?.message || 'Clipboard write failed';
          }
        }
      }

      await chrome.runtime.sendMessage({
        type: 'NC_SNIP_COPIED',
        copied,
        copyError,
        width: png.width,
        height: png.height,
        shareUrl: prepared?.text || '',
        uploadError: prepared?.uploadError || '',
        nullImage: Boolean(prepared?.nullImage),
      }).catch(() => {});
      await delay(80);
      window.close();
    } catch (error) {
      closed = true;
      if (copied) {
        await chrome.runtime.sendMessage({
          type: 'NC_SNIP_COPIED',
          copied,
          copyError: '',
        }).catch(() => {});
        await delay(80);
        window.close();
        return;
      }
      await chrome.runtime.sendMessage({
        type: 'NC_ERROR',
        message: error?.message || 'Could not crop snip',
      }).catch(() => {});
      window.close();
    }
  }

  function completeFull() {
    completeRect({ x: 0, y: 0, w: window.innerWidth, h: window.innerHeight });
  }

  wrap.addEventListener('mousedown', onMouseDown);
  window.addEventListener('mousemove', onMouseMove, true);
  window.addEventListener('mouseup', onMouseUp, true);
  window.addEventListener('keydown', onKeyDown, true);
  wrap.addEventListener('contextmenu', (event) => {
    event.preventDefault();
    cancel();
  });
  snipBtn.addEventListener('click', () => setKind('snip', true));
  gifBtn.addEventListener('click', () => setKind('gif', true));
  regionBtn.addEventListener('click', () => {
    regionBtn.classList.add('on');
    fullBtn.classList.remove('on');
  });
  fullBtn.addEventListener('click', () => completeFull());
  document.querySelector('.close').addEventListener('click', cancel);

  chrome.runtime.sendMessage({ type: 'NC_GET_CROP' }).then(async (response) => {
    if (!response?.ok || !response.dataUrl) {
      await chrome.runtime.sendMessage({
        type: 'NC_ERROR',
        message: 'Screenshot expired. Click Capture again.',
      }).catch(() => {});
      finish();
      return;
    }
    try {
      sourceTabId = response.tabId || null;
      sourceWinId = response.windowId || null;
      await show(response.dataUrl, response.mode);
    } catch (error) {
      await chrome.runtime.sendMessage({
        type: 'NC_ERROR',
        message: error?.message || 'Could not load screenshot',
      }).catch(() => {});
      finish();
    }
  }).catch(async (error) => {
    await chrome.runtime.sendMessage({
      type: 'NC_ERROR',
      message: error?.message || 'Could not load screenshot',
    }).catch(() => {});
    finish();
  });
}
