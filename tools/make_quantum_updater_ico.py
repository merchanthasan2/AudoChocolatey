"""Build brand/quantum-updater.ico from web/icon.png (multi-size). Run once after brand assets change."""
from __future__ import annotations

import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def verify_ico(path: str) -> list[tuple[int, int]]:
    with open(path, "rb") as f:
        if f.read(4) != b"\x00\x00\x01\x00":
            raise ValueError("not a valid ICO")
        count = struct.unpack("<H", f.read(2))[0]
        sizes: list[tuple[int, int]] = []
        for _ in range(count):
            entry = f.read(16)
            if len(entry) < 16:
                break
            w, h = entry[0], entry[1]
            sizes.append((w if w else 256, h if h else 256))
        return sizes


def main() -> int:
    try:
        from PIL import Image
    except ImportError:
        print("Pillow is required (see requirements.txt)", file=sys.stderr)
        return 1

    out_path = os.path.join(ROOT, "brand", "quantum-updater.ico")
    png_path = os.path.join(ROOT, "web", "icon.png")
    if not os.path.isfile(png_path):
        print(f"Missing {png_path}", file=sys.stderr)
        return 1

    base = Image.open(png_path).convert("RGBA")
    icon_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (256, 256)]
    frames = [base.resize(size, Image.Resampling.LANCZOS) for size in icon_sizes]
    frames[0].save(
        out_path,
        format="ICO",
        sizes=[(im.width, im.height) for im in frames],
        append_images=frames[1:],
    )
    sizes = verify_ico(out_path)
    print("Wrote", out_path)
    for w, h in sizes:
        print(f"  {w}x{h}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
