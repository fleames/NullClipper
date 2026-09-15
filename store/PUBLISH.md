# Publish NullClipper to the stores

The zip and listing kit are ready. **You** still have to sign in (Google / Microsoft / Opera) and pay Chrome’s one-time developer fee. Nobody else can finish that step.

Package: `dist/NullClipper-extension.zip` (rebuild with `powershell -File extension/pack.ps1`).  
`manifest.json` is at the **zip root**. Icons 16 / 32 / 48 / 128 are inside `icons/`.

Privacy URL to paste everywhere:

**https://github.com/fleames/NullClipper/blob/main/store/privacy.md**

Copy-paste text: [`LISTING.md`](LISTING.md). Screenshots: [`screenshots/screenshot-1.png`](screenshots/) … `screenshot-3.png` (exactly 1280×800, 24-bit PNG, no alpha). Small promo: `screenshots/promo-small.png`. Marquee: `screenshots/promo-marquee.png`. Opera optional promo: `screenshots/promo-opera-300x188.png` (300×188). Opera media icon: `screenshots/icon64.png` (64×64).

---

## 1. Chrome Web Store (do this first)

Pay **$5 USD once** with a Google account, then upload.

1. Open **https://chrome.google.com/webstore/devconsole**
2. If prompted, complete developer registration (phone / identity checks Google asks for, then the $5 fee).
3. **New item** → upload `dist/NullClipper-extension.zip`.
4. Store listing:
   - Name: NullClipper  
   - Summary + description from [`LISTING.md`](LISTING.md)  
   - Category: **Productivity**  
   - Language: **English**  
   - Screenshots: `screenshot-1.png`, `screenshot-2.png`, `screenshot-3.png` (folder: `store/screenshots/`)  
   - Small promo tile (optional): `promo-small.png` (440×280)  
   - Marquee (optional): `promo-marquee.png` (1400×560)  
   - Homepage: https://github.com/fleames/NullClipper
5. Privacy:
   - Privacy policy URL: the GitHub link above  
   - Single purpose + permission justifications from [`LISTING.md`](LISTING.md)  
   - Certify you do not sell personal data; you are not using remote code.
6. Distribution:
   - **Unlisted** = anyone with the link can install (good first publish).  
   - **Public** = searchable on the store (what you want for 1-click).  
   - You can ship Unlisted, confirm install, then switch to Public.
7. Submit for review.

**Review time:** often **1–3 business days** for a new item; first-time publishers can take longer (a week is not rare). Google emails the account that paid the fee.

When it is live you get an ID like `abcdefghijklmnopqrstuvwxyzabcdef`. Then — and only then — put this URL in README badges:

`https://chromewebstore.google.com/detail/nullclipper/<ID>`

---

## 2. Edge Add-ons (import after Chrome)

Edge can **import the Chrome listing** so you do not rebuild the zip.

1. Open **https://partner.microsoft.com/dashboard/microsoftedge/overview**  
   (Microsoft Partner Center → Edge add-ons. Register the Microsoft account if needed — no Chrome-style $5 fee.)
2. **Import from Chrome Web Store** (or create a new listing and upload the same zip).
3. Reuse name, description, screenshots, and the same privacy URL.
4. Submit.

**Review time:** commonly **1–7 days**.

After approval, replace the Edge “Coming soon” badge with the Partner Center listing URL. Until Chrome is public, import may be unavailable — upload the zip manually instead.

---

## 3. Opera add-ons (needed for 1-click on Opera)

Sideload on Opera still wants **Developer mode** and can hit **ExtensionsSettings**. The **store listing** is the real 1-click path.

1. Open **https://addons.opera.com/developer/**
2. Sign in with the Opera account you want as publisher.
3. New extension → upload `dist/NullClipper-extension.zip`.
4. Listing: same name, English, Productivity-style category Opera offers, screenshots, privacy URL.  
   **Media tab icon:** upload `store/screenshots/icon64.png` (exactly **64×64**, 24-bit PNG, no alpha). Opera rejects 48×48 and other sizes.  
   **Promotional image (optional):** upload `store/screenshots/promo-opera-300x188.png` (exactly **300×188**, 24-bit PNG, no alpha).
5. Submit.

**Review time:** often a few days.

Opera listing URL looks like `https://addons.opera.com/extensions/details/nullclipper/`. Put that in README only after it exists.

---

## After all three are live

1. Swap the “Coming soon” badges in `README.md` and `docs/install.md` for the three real URLs.  
2. Cut a GitHub Release if you want the zip on Releases to match the store version.  
3. Do not bump `extension/manifest.json` `version` unless you are uploading an update (CWS rejects the same version twice).
