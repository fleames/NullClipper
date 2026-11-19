# Install NullClipper

One-click browser install is the **Chrome Web Store** or **Opera add-ons** listing. **Edge Add-ons is not live yet** — Chromium will not 1-click-install an unpacked GitHub zip, so use Developer mode there.

**Publishing the remaining store:** the MV3 zip, listing copy, privacy policy, and 1280×800 screenshots are in [`store/`](../store/). Follow [`store/PUBLISH.md`](../store/PUBLISH.md).

[![Chrome Web Store](https://img.shields.io/badge/Chrome-Web_Store-4285F4?style=flat-square&logo=googlechrome&logoColor=white)](https://chromewebstore.google.com/detail/nullclipper/epfnlcdfgpobfagfoeghabmdbbjoohck)
[![Edge Add-ons](https://img.shields.io/badge/Edge-Coming_soon-9ca3af?style=flat-square&logo=microsoftedge&logoColor=white)](#edge)
[![Opera add-ons](https://img.shields.io/badge/Opera-add--ons-FF1B2D?style=flat-square&logo=opera&logoColor=white)](https://addons.opera.com/extensions/details/nullclipper/)

## Windows (desktop app)

**Easiest:** download **NullClipper-Setup.exe** from the [latest GitHub Release](https://github.com/fleames/NullClipper/releases/latest), double-click it, Next, Finish. The tray icon appears by the clock.

**Portable:** download **NullClipper-win-x64.zip**, unzip, run `NullClipper.exe`. Same app, no installer.

Requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) for Windows x64.

Default hotkey: **Ctrl+Shift+X** (change it in Settings).

## Browser extension

Download **NullClipper-extension.zip** from the [latest GitHub Release](https://github.com/fleames/NullClipper/releases/latest) (or pack it yourself with `extension/pack.ps1`).

The zip is a valid Manifest V3 package: `manifest.json` at the root. Store listing kit (copy, privacy policy, screenshots): [`store/`](../store/). After a listing exists, installing from the store is the real 1-click path.

### Chrome

1. Unzip `NullClipper-extension.zip` to a folder you will keep.
2. Open `chrome://extensions`.
3. Turn on **Developer mode**.
4. **Load unpacked** → select that folder.
5. Pin NullClipper from the puzzle-piece menu.

Hotkey: **Alt+Shift+X**. Change it at `chrome://extensions/shortcuts`.

### Edge

Same as Chrome, using `edge://extensions` and `edge://extensions/shortcuts`.

### Opera

Opera (and some Chromium policies) blocks **scripting** web pages for unpacked/sideloaded extensions (`ExtensionsSettings`). NullClipper does **not** inject into Google or any other site — it snapshots the tab and crops on an extension page — so Opera can capture google.com.

**Opera add-ons is live** — [install from the store](https://addons.opera.com/extensions/details/nullclipper/) for the real 1-click path.

**Sideload (Developer mode):**

1. Unzip `NullClipper-extension.zip` to a folder you will keep.
2. Open `opera://extensions` (or `chrome://extensions`).
3. Turn on **Developer mode**.
4. **Load unpacked** → select that folder.

Hotkey: **Alt+Shift+X**. Shortcuts: `opera://extensions/shortcuts` or `chrome://extensions/shortcuts`.

## Capture a tab

Click the NullClipper icon → **Capture**, or press **Alt+Shift+X**. Draw a region on the freeze-frame. **Snip** copies a PNG; **GIF** records that region for up to 8 seconds. Esc cancels.

Restricted pages (`chrome://`, `opera://`, `edge://`, the stores) cannot be captured. Use a normal website tab, or the Windows app for the whole desktop.
