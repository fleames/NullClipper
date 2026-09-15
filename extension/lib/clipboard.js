const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function asBlob(data, type) {
  if (!data) return null;
  if (data instanceof Blob) {
    return data.type ? data : new Blob([data], { type });
  }
  return new Blob([data], { type });
}

async function focusDocument() {
  try {
    window.focus();
  } catch {
    // Popup may already be focused.
  }
  const root = document.body;
  if (root) {
    if (!root.hasAttribute('tabindex')) {
      root.setAttribute('tabindex', '-1');
    }
    try {
      root.focus({ preventScroll: true });
    } catch {
      try {
        root.focus();
      } catch {
        // Focus is best-effort; clipboard.write still needs the document active.
      }
    }
  }
  await delay(0);
  await new Promise((resolve) => requestAnimationFrame(() => resolve()));
}

function fallbackTextarea() {
  let area = document.getElementById('nc-clip-text');
  if (area) return area;
  area = document.createElement('textarea');
  area.id = 'nc-clip-text';
  area.setAttribute('aria-hidden', 'true');
  area.setAttribute('readonly', '');
  area.style.cssText = 'position:fixed;left:-9999px;top:0;width:1px;height:1px;opacity:0';
  document.body.appendChild(area);
  return area;
}

function fallbackEditable() {
  let host = document.getElementById('nc-clip-html');
  if (host) return host;
  host = document.createElement('div');
  host.id = 'nc-clip-html';
  host.setAttribute('aria-hidden', 'true');
  host.setAttribute('contenteditable', 'true');
  host.style.cssText = 'position:fixed;left:-9999px;top:0;width:1px;height:1px;opacity:0';
  document.body.appendChild(host);
  return host;
}

function copyTextFallback(text) {
  const area = fallbackTextarea();
  area.value = String(text);
  area.style.display = 'block';
  area.focus();
  area.select();
  area.setSelectionRange(0, area.value.length);
  const ok = document.execCommand('copy');
  area.value = '';
  area.style.display = '';
  if (!ok) {
    throw new Error('Could not copy text');
  }
}

async function copyImageFallback(blob) {
  const host = fallbackEditable();
  const url = URL.createObjectURL(blob);
  try {
    const img = document.createElement('img');
    img.src = url;
    await new Promise((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error('Could not copy image'));
    });
    host.replaceChildren(img);
    host.focus();
    const selection = window.getSelection();
    if (!selection) {
      throw new Error('Could not copy image');
    }
    const range = document.createRange();
    range.selectNodeContents(host);
    selection.removeAllRanges();
    selection.addRange(range);
    const ok = document.execCommand('copy');
    selection.removeAllRanges();
    host.replaceChildren();
    if (!ok) {
      throw new Error('Could not copy image');
    }
  } finally {
    URL.revokeObjectURL(url);
  }
}

async function writeClipboardItem(payloads) {
  if (typeof ClipboardItem !== 'function' || !navigator.clipboard?.write) {
    throw new Error('ClipboardItem is not available');
  }
  await navigator.clipboard.write([new ClipboardItem(payloads)]);
}

async function copyText(text) {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return;
    }
  } catch {
    // Fall through to execCommand while this document is focused.
  }
  copyTextFallback(text);
}

/**
 * Copy from a focused extension page (overlay). Service workers cannot do this.
 * @returns {Promise<'png' | 'gif' | 'png-fallback' | 'text'>}
 */
export async function copyInDocument({ png, gif, text } = {}) {
  await focusDocument();

  const payloads = {};
  if (text) payloads['text/plain'] = new Blob([text], { type: 'text/plain' });
  if (png) payloads['image/png'] = asBlob(png, 'image/png');
  if (gif) payloads['image/gif'] = asBlob(gif, 'image/gif');

  if (!Object.keys(payloads).length) {
    throw new Error('Nothing to copy');
  }

  if (gif && !text) {
    try {
      await writeClipboardItem({ 'image/gif': payloads['image/gif'] });
      await delay(40);
      return 'gif';
    } catch {
      if (payloads['image/png']) {
        try {
          await writeClipboardItem({ 'image/png': payloads['image/png'] });
          await delay(40);
          return 'png-fallback';
        } catch {
          await copyImageFallback(payloads['image/png']);
          await delay(40);
          return 'png-fallback';
        }
      }
      throw new Error('Could not copy GIF');
    }
  }

  if (text && !png && !gif) {
    await copyText(text);
    await delay(40);
    return 'text';
  }

  try {
    await writeClipboardItem(payloads);
    await delay(40);
    return png ? 'png' : 'text';
  } catch {
    if (text) {
      await copyText(text);
      await delay(40);
      return 'text';
    }
    if (payloads['image/png']) {
      await copyImageFallback(payloads['image/png']);
      await delay(40);
      return 'png';
    }
    throw new Error('Clipboard write failed');
  }
}
