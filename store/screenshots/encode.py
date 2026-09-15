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

    for name in (
        "screenshot-1.png",
        "screenshot-2.png",
        "screenshot-3.png",
        "promo-small.png",
        "promo-marquee.png",
    ):
        print(inspect_png(HERE / name))


if __name__ == "__main__":
    if len(sys.argv) == 5:
        src, dest = Path(sys.argv[1]), Path(sys.argv[2])
        write_store_image(src, dest, int(sys.argv[3]), int(sys.argv[4]))
        print(inspect_png(dest))
    else:
        main()
