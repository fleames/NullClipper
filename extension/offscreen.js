import { copyInDocument } from './lib/clipboard.js';

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type !== 'NC_OFFSCREEN_COPY') return undefined;

  copyInDocument({
    png: message.png,
    gif: message.gif,
    text: message.text,
  }).then((copied) => {
    sendResponse({ ok: true, copied });
  }).catch((error) => {
    sendResponse({ ok: false, error: error?.message || String(error) });
  });

  return true;
});
