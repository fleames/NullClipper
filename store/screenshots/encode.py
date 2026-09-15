"""Write Chrome Web Store images: exact pixels, 24-bit RGB PNG, no alpha, no DPI."""

from __future__ import annotations

import struct
import sys
import zlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CANVAS = (0x0B, 0x0F, 0x14)
HERE = Path(__file__).resolve().parent
ICON = HERE.parent.parent / "extension" / "icons" / "icon128.png"


def png_chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def save_png24(im: Image.Image, path: Path) -> None:
    rgb = im.convert("RGB")
    w, h = rgb.size
    pixels = rgb.tobytes()
    stride = w * 3
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw.extend(pixels[y * stride : (y + 1) * stride])
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    png = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + png_chunk(b"IEND", b"")
    )
    path.write_bytes(png)


def fit_on_canvas(im: Image.Image, width: int, height: int, canvas: tuple[int, int, int] = CANVAS) -> Image.Image:
    src = im.convert("RGB")
    out = Image.new("RGB", (width, height), canvas)
    scale = min(width / src.width, height / src.height)
    nw = max(1, round(src.width * scale))
    nh = max(1, round(src.height * scale))
    if (nw, nh) != src.size:
        src = src.resize((nw, nh), Image.Resampling.LANCZOS)
    out.paste(src, ((width - nw) // 2, (height - nh) // 2))
    return out


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(rf"C:\Windows\Fonts\{name}", size)


def cover_canvas(im: Image.Image, width: int, height: int) -> Image.Image:
    src = im.convert("RGB")
    scale = max(width / src.width, height / src.height)
    nw = max(1, round(src.width * scale))
    nh = max(1, round(src.height * scale))
    if (nw, nh) != src.size:
        src = src.resize((nw, nh), Image.Resampling.LANCZOS)
    left = max(0, (nw - width) // 2)
    top = max(0, (nh - height) // 2)
    return src.crop((left, top, left + width, top + height))


def compose_opera_promo(bg: Image.Image | None = None) -> Image.Image:
    w, h = 300, 188
    if bg is not None:
        filled = cover_canvas(bg, w, h)
        brand = Image.new("RGB", (w, h), CANVAS)
        out = Image.blend(brand, filled, 0.78)
    else:
        out = Image.new("RGB", (w, h), CANVAS)
        glow = Image.new("RGB", (w, h), CANVAS)
        ImageDraw.Draw(glow).ellipse((-90, -100, 210, 170), fill=(59, 130, 246))
        out = Image.blend(out, glow, 0.28)

    icon_path = HERE.parent.parent / "Assets" / "clipper-icon.png"
    if not icon_path.exists():
        icon_path = HERE.parent.parent / "extension" / "icons" / "clipper-icon.png"
    icon_px = 64
    icon = Image.open(icon_path).convert("RGBA").resize((icon_px, icon_px), Image.Resampling.LANCZOS)
    layer = out.convert("RGBA")
    ix, iy = 16, (h - icon_px) // 2
    layer.paste(icon, (ix, iy), icon)
    rgb = layer.convert("RGB")

    draw = ImageDraw.Draw(rgb)
    draw.rectangle((0, 0, 3, h), fill=(59, 130, 246))

    title_font = font("segoeuib.ttf", 22)
    sub_font = font("segoeui.ttf", 13)
    title = "NullClipper"
    sub = "Snip or GIF this tab"
    tx = ix + icon_px + 12
    title_box = draw.textbbox((0, 0), title, font=title_font)
    sub_box = draw.textbbox((0, 0), sub, font=sub_font)
    title_h = title_box[3] - title_box[1]
    sub_h = sub_box[3] - sub_box[1]
    block_h = title_h + 6 + sub_h
    ty = (h - block_h) // 2 - 2
    draw.text((tx, ty), title, fill=(243, 244, 246), font=title_font)
    draw.text((tx, ty + title_h + 6), sub, fill=(156, 163, 175), font=sub_font)
    return rgb


def compose_marquee(preview: Image.Image) -> Image.Image:
    w, h = 1400, 560
    out = Image.new("RGB", (w, h), CANVAS)
    glow = Image.new("RGB", (w, h), CANVAS)
    ImageDraw.Draw(glow).ellipse((-420, -260, 680, 540), fill=(30, 58, 95))
    out = Image.blend(out, glow, 0.42)

    icon = Image.open(ICON).convert("RGBA").resize((120, 120), Image.Resampling.LANCZOS)
    layer = out.convert("RGBA")
    layer.paste(icon, (72, 220), icon)
    rgb = layer.convert("RGB")
    text = ImageDraw.Draw(rgb)
    text.text((216, 228), "NullClipper", fill=(243, 244, 246), font=font("segoeuib.ttf", 56))
    text.text((216, 308), "Snip or GIF the current tab.", fill=(156, 163, 175), font=font("segoeui.ttf", 24))

    # Uniform 50% of the 1280x800 popup shot — no stretch.
    preview = fit_on_canvas(preview, 640, 400)
    rgb.paste(preview, (1400 - 640 - 48, (560 - 400) // 2))
    return rgb


def write_store_image(src: Path, dest: Path, width: int, height: int) -> None:
    save_png24(fit_on_canvas(Image.open(src), width, height), dest)


def inspect_png(path: Path) -> str:
    data = path.read_bytes()
    i = 8
    ln = struct.unpack(">I", data[i : i + 4])[0]
    typ = data[i + 4 : i + 8]
    assert typ == b"IHDR", typ
    chunk = data[i + 8 : i + 8 + ln]
    w, h, bit, ct, _comp, _filt, inter = struct.unpack(">IIBBBBB", chunk)
    colors = {0: "gray", 2: "truecolor", 3: "indexed", 4: "gray+alpha", 6: "truecolor+alpha"}
    chunks: list[str] = []
    while i < len(data):
        n = struct.unpack(">I", data[i : i + 4])[0]
        t = data[i + 4 : i + 8].decode()
        chunks.append(t)
        i += 12 + n
        if t == "IEND":
            break
    return f"{path.name}: {w}x{h} {bit}-bit {colors.get(ct, ct)} interlace={inter} chunks={'+'.join(chunks)}"


def main() -> None:
    shots = [
        (HERE / "popup.png", HERE / "screenshot-1.png", 1280, 800),
        (HERE / "overlay.png", HERE / "screenshot-2.png", 1280, 800),
        (HERE / "settings.png", HERE / "screenshot-3.png", 1280, 800),
        (HERE / "promo-440x280.png", HERE / "promo-small.png", 440, 280),
    ]
    for src, dest, w, h in shots:
        if not src.exists():
            if dest.exists():
                src = dest
            else:
                raise SystemExit(f"Missing source {src}")
        write_store_image(src, dest, w, h)

    preview_path = HERE / "screenshot-1.png"
    save_png24(compose_marquee(Image.open(preview_path)), HERE / "promo-marquee.png")

    opera_bg = HERE / "src" / "promo-opera-bg.png"
    save_png24(compose_opera_promo(Image.open(opera_bg) if opera_bg.exists() else None), HERE / "promo-opera-300x188.png")

    for name in (
        "screenshot-1.png",
        "screenshot-2.png",
        "screenshot-3.png",
        "promo-small.png",
        "promo-marquee.png",
        "promo-opera-300x188.png",
        "icon64.png",
    ):
        print(inspect_png(HERE / name))


if __name__ == "__main__":
    if len(sys.argv) == 5:
        src, dest = Path(sys.argv[1]), Path(sys.argv[2])
        write_store_image(src, dest, int(sys.argv[3]), int(sys.argv[4]))
        print(inspect_png(dest))
    else:
        main()
