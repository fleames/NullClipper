# Store listing copy

Language: **English (United States)**  
Category: **Productivity**  
Name: **NullClipper** (must match the extension)

Paste these fields into Chrome Web Store, then reuse them on Edge and Opera. Drag screenshots from `store/screenshots/` (`screenshot-1.png` … `screenshot-3.png`, exactly 1280×800, 24-bit PNG, no alpha). Small promo: `promo-small.png` (440×280). Marquee: `promo-marquee.png` (1400×560).

Privacy policy URL (after this file is on `main`):

- Human: https://github.com/fleames/NullClipper/blob/main/store/privacy.md
- Raw: https://raw.githubusercontent.com/fleames/NullClipper/main/store/privacy.md

Do not put a fake Web Store / Edge / Opera item URL in README until the listing has a real ID.

## Short description (132 characters max)

Snip or record a short GIF of the current tab. Copy a PNG, or an encrypted NullImage share link.

## Long description

NullClipper snips the tab you are looking at. Draw a region, copy a PNG — or record a short GIF. Optionally upload an encrypted share link instead of the image.

It is the browser companion to the Windows tray app of the same name. The extension captures the **current tab only**, not your whole desktop.

**How to capture**

1. Pin NullClipper and click the icon, or press Alt+Shift+X (change the shortcut in the browser’s extension shortcuts page).
2. Drag a rectangle on the freeze-frame, or capture the full visible tab.
3. Snip copies a PNG. GIF records that region at 12 fps for up to 8 seconds (longest side capped at 640px).
4. Esc or the close button cancels.

Cropping happens in an extension window. NullClipper does **not** inject scripts into the website, so it can still capture normal https pages in Opera (unpacked/sideload is otherwise limited by ExtensionsSettings).

**NullImage (optional)**

Off until you enable it. Each upload is encrypted on your device. The key stays in the #fragment of the link. You choose the server URL (default https://nullimage.org), expiry (1 hour / 1 day / 3 days / 7 days), burn-after-view, and an optional password. If upload fails, the image is copied locally instead.

**What it will not do**

- Capture other tabs, other windows, or the whole desktop (use the Windows app for that)
- Capture chrome://, edge://, opera://, or the stores
- Run analytics or ads

Homepage: https://github.com/fleames/NullClipper  
Windows app: https://github.com/fleames/NullClipper/releases/latest  
Privacy: https://github.com/fleames/NullClipper/blob/main/store/privacy.md

## Chrome Web Store — additional fields

**Category:** Productivity  
**Language:** English  
**Visibility:** Public (or Unlisted first to test the install link, then Public)  
**Single purpose:** Capture a region of the current browser tab and copy it (or an optional encrypted link).

### Permission justifications (paste into the dashboard)

**activeTab**  
Used only when you click Capture or press the hotkey, so we can snapshot the visible area of that tab.

**tabs**  
Find the active tab, refuse restricted pages (chrome://, stores), and open the crop / GIF UI as an extension page.

**clipboardWrite**  
Copy the PNG, GIF, or NullImage share link to the clipboard on your device.

**storage**  
Save Snip/GIF mode and optional NullImage settings locally. Nothing is synced to us.

**commands**  
Keyboard shortcut to start a capture (default Alt+Shift+X).

**offscreen**  
Manifest V3 helper document used to write image bytes to the clipboard.

**Host permission (optional, user-granted)**  
Requested only if you enable NullImage, and only for the server origin you enter, so the encrypted upload can be sent. Not used to read websites for capture.

## Screenshot captions (optional)

1. `screenshot-1.png` — popup: capture the current tab, pick Snip or GIF.
2. `screenshot-2.png` — crop overlay: freeze-frame, drag a region, Esc cancels.
3. `screenshot-3.png` — settings: optional NullImage server, expiry, burn-after-view.

## Support

GitHub Issues: https://github.com/fleames/NullClipper/issues
