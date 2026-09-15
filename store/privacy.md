# NullClipper privacy policy

Last updated: 15 September 2026

NullClipper is a browser extension that snips or records a short GIF of the **current tab** after you ask it to. This page is the privacy policy for that extension (Chrome, Edge, and Opera).

The Windows desktop app is a separate program that runs only on your PC. This policy covers the **browser extension**.

## Short version

- We capture the tab you are looking at **only after you click Capture or press the hotkey**.
- The image is processed **on your device**.
- By default the result is copied to **your clipboard**. Nothing is uploaded.
- Optional **NullImage** uploads go to a server **you configure**. The file is encrypted on your device first.
- We do **not** use analytics, advertising, or crash telemetry.
- We do **not** have user accounts.

## What the extension can access

### Current tab (after you act)

When you click the NullClipper icon and choose **Capture**, or you press the capture hotkey (default **Alt+Shift+X**), the extension takes a picture of the **visible area of that tab**. It does not capture other tabs, other windows, or your whole desktop.

It will not capture restricted pages such as `chrome://`, `edge://`, `opera://`, or the browser stores.

Cropping happens in an extension-owned window. NullClipper does **not** inject scripts into the website.

### Clipboard

The extension copies a PNG, a GIF, or a share link onto the clipboard so you can paste it. That copy stays on your device unless you paste it somewhere else.

### Settings stored on your device

These stay in the browser’s local extension storage on this computer:

- Snip vs GIF mode
- Whether NullImage is enabled
- NullImage server URL, expiry, burn-after-view, and optional password

Uninstalling the extension deletes that storage. We cannot read it from the internet.

### Optional NullImage upload

NullImage is **off** until you turn it on.

If you enable it, each snip or GIF is encrypted on your device (AES-256-GCM) and the **ciphertext** is sent to the server URL you set (default `https://nullimage.org`). The decryption key is kept in the `#fragment` of the share link so a typical server never sees it.

The browser will ask you to grant host permission for that server origin. The extension does not request permission to read arbitrary websites for capture; tab capture uses the current tab after your action.

If you set a different server, you are sending ciphertext to **that** operator. Their privacy practices are theirs, not ours.

If an upload fails, NullClipper copies the image locally instead.

## What we do not collect

The extension does **not**:

- Sell or share your snips with us (the authors never receive them)
- Run analytics, ads, or “phone-home” usage stats
- Create an account or require an email
- Capture tabs in the background
- Read the full contents of websites except the visible snapshot you requested
- Keep a copy of clipboard data on a server of ours

GitHub Releases, this repository, and the browser stores may see **download or install counts** as part of those platforms. That is not data the extension sends.

## Permissions (why they exist)

| Permission | Why |
| --- | --- |
| `activeTab` | Capture the tab you invoked the extension on |
| `tabs` | Find the current tab, skip restricted pages, open the crop UI |
| `clipboardWrite` | Copy the PNG, GIF, or link |
| `storage` | Remember your settings on this device |
| `commands` | Hotkey to start a capture |
| `offscreen` | Write images to the clipboard in Manifest V3 |
| Optional host access | Only if you enable NullImage, and only for the server origin you approve |

There is no `scripting` / content-script injection into web pages.

## Children

NullClipper is a general productivity tool. It is not directed at children and does not knowingly collect personal information from children.

## Changes

If this policy changes in a material way, we will update the date at the top of this file in the [NullClipper repository](https://github.com/fleames/NullClipper).

## Contact

Open an issue on [github.com/fleames/NullClipper](https://github.com/fleames/NullClipper/issues) or use GitHub to contact **fleames**.
