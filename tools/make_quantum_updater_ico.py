"""Build DeskVerve PNG renditions and compatibility ICOs from the V master."""
from __future__ import annotations

import os
import shutil
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON_SIZES = (16, 24, 32, 48, 64, 128, 256)
PNG_SIZES = (32, 64, 128, 256, 512)
ARTWORK_SCALE = 0.90


def verify_ico(path: str) -> list[tuple[int, int]]:
    with open(path, "rb") as f:
        if f.read(4) != b"\x00\x00\x01\x00":
            raise ValueError("not a valid ICO")
        count = struct.unpack("<H", f.read(2))[0]
        sizes: list[tuple[int, int]] = []
        for _ in range(count):
            entry = f.read(16)
            if len(entry) < 16:
                raise ValueError("truncated ICO directory")
            w, h = entry[0], entry[1]
            sizes.append((w if w else 256, h if h else 256))
        return sizes


def render_square(master, size: int):
    """Render from the V master with 5% minimum padding and no upscaling."""
    from PIL import Image

    scale = min(
        size * ARTWORK_SCALE / master.width,
        size * ARTWORK_SCALE / master.height,
        1.0,
    )
    artwork_size = (
        max(1, round(master.width * scale)),
        max(1, round(master.height * scale)),
    )
    if artwork_size == master.size:
        artwork = master.copy()
    else:
        artwork = master.resize(artwork_size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    position = ((size - artwork.width) // 2, (size - artwork.height) // 2)
    canvas.alpha_composite(artwork, position)
    return canvas


def main() -> int:
    try:
        from PIL import Image
    except ImportError:
        print("Pillow is required (see requirements.txt)", file=sys.stderr)
        return 1

    master_path = os.path.join(ROOT, "brand", "desk-verve-emblem.png")
    if not os.path.isfile(master_path):
        print(f"Missing {master_path}", file=sys.stderr)
        return 1

    master = Image.open(master_path).convert("RGBA")
    png_dir = os.path.join(ROOT, "brand", "png")
    os.makedirs(png_dir, exist_ok=True)
    for size in PNG_SIZES:
        output = render_square(master, size)
        png_path = os.path.join(png_dir, f"desk-verve-emblem-{size}.png")
        output.save(png_path, format="PNG")
        print("Wrote", png_path)
        if size in (256, 512):
            compatibility_path = os.path.join(png_dir, f"quantum-updater-{size}.png")
            shutil.copyfile(png_path, compatibility_path)
            print("Wrote", compatibility_path)

    frames = [render_square(master, size) for size in ICON_SIZES]
    icon_path = os.path.join(ROOT, "brand", "desk-verve-icon.ico")
    frames[-1].save(
        icon_path,
        format="ICO",
        sizes=[(size, size) for size in ICON_SIZES],
        append_images=frames[:-1],
    )
    compatibility_icon = os.path.join(ROOT, "brand", "quantum-updater.ico")
    shutil.copyfile(icon_path, compatibility_icon)

    expected_sizes = [(size, size) for size in ICON_SIZES]
    for output_path in (icon_path, compatibility_icon):
        sizes = verify_ico(output_path)
        if sizes != expected_sizes:
            raise ValueError(f"Unexpected ICO frames in {output_path}: {sizes}")
        print("Wrote", output_path)
        for width, height in sizes:
            print(f"  {width}x{height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
