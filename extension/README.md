# NullClipper for Chrome, Edge, and Opera

A Manifest V3 extension with the same job as the Windows tray app: snip or record a short GIF, copy it, optionally upload an encrypted [NullImage](https://nullimage.org) link instead.

v1 captures the **current tab** only. It does not grab the whole desktop, other windows, or `chrome://` / `opera://` / store pages. Use the [Windows app](https://github.com/fleames/NullClipper/releases/latest) when you need a screen-wide overlay.

It never injects into the website. The tab is snapshotted with `captureVisibleTab`; you crop on an extension-owned window. That is why it works on google.com in **Opera** (unpacked extensions are otherwise blocked from scripting pages by `ExtensionsSettings`).

## Install

True 1-click is the Chrome Web Store / Edge Add-ons / Opera add-ons listing. Those URLs are not live yet. Listing copy, privacy policy, and screenshots: [`store/`](../store/).

Until then, the smallest path:

1. Download **NullClipper-extension.zip** from the [latest GitHub Release](https://github.com/fleames/NullClipper/releases/latest) (or run `pack.ps1` in this folder).
2. Unzip it and keep the folder.
3. Load unpacked (Developer mode) — two extra clicks:

| Browser | Extensions page |
| --- | --- |
| Chrome | `chrome://extensions` → Developer mode → **Load unpacked** |
| Edge | `edge://extensions` → Developer mode → **Load unpacked** |
| Opera | `opera://extensions` → Developer mode → **Load unpacked** |

Pin NullClipper from the puzzle-piece (or Opera extensions) menu if you want the icon visible.

Full steps, including store-badge placeholders: [docs/install.md](../docs/install.md).

To capture: click the icon → **Capture**, or press **Alt+Shift+X**.

## Hotkey

Default suggestion is **Alt+Shift+X** (Chromium only applies `suggested_key` on first install).

Change it any time:

1. Chrome: `chrome://extensions/shortcuts`
2. Edge: `edge://extensions/shortcuts`
3. Opera: `opera://extensions/shortcuts` or `chrome://extensions/shortcuts`
4. Find **NullClipper** → **Capture the current tab** → pencil → press the keys you want.

If the shortcut field is empty, the browser refused the default because another extension already owns it.

## What it can do

| | Extension | Windows app |
| --- | --- | --- |
| Region snip | Visible area of the **current tab** | Any monitor / window |
| Full capture | Full **visible tab** | Fullscreen monitor |
| GIF | Region of the tab, ~12 fps, max 8s, 640px long-edge cap | Same limits, any screen region |
| Clipboard | PNG (GIF when Chromium accepts `image/gif`) | PNG / GIF via the Windows clipboard |
| NullImage | Same `ni1-aes-gcm-256` scheme, AES-256-GCM, 2 MiB chunks, `#k=` fragment | Same |
| Hotkey | `chrome.commands` | Global OS hotkey |

Snip and GIF share a mode selector in the popup and on the crop toolbar. Esc or the close button cancels. During GIF recording a HUD shows `0:00 / 0:08` and **Stop** (Esc also stops and encodes what you have).

If a NullImage upload fails, the image or GIF is copied instead.

## NullImage

Off until you enable it. Settings: server URL (default `https://nullimage.org`), expiry **1 hour / 1 day / 3 days / 7 days** (no “never”), burn after first view, optional password.

The browser will prompt for host permission for that origin so the service worker can POST/PUT without CORS. The key never leaves the `#fragment` of the share URL.

## Pack the zip

```powershell
powershell -File extension/pack.ps1
```

Writes `dist/NullClipper-extension.zip` with `manifest.json` at the zip root (Chrome Web Store / Edge / Opera upload layout). GitHub Releases attach the same file on `v*` tags.

## Files

```
extension/
  manifest.json
  pack.ps1                 builds dist/NullClipper-extension.zip
  popup.html / popup.css / popup.js
  background.js            service worker
  overlay.html / overlay.js / overlay.css
                           extension-owned crop UI (not injected)
  offscreen.html / .js     clipboard writes
  hud.html / hud.css / hud.js
  toast.html / toast.css / toast.js
  lib/settings.js
  lib/nullimage.js         Web Crypto client
  lib/gif-encoder.js
  lib/image.js
  icons/
```
