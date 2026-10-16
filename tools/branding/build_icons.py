#!/usr/bin/env python3
"""Generate transparent PNG and multi-size Windows ICO from the user-approved logo SVG.
Requires build-only dependencies: pip install cairosvg pillow
No network access or Telegram account is needed.
"""
from pathlib import Path
from io import BytesIO
from PIL import Image
import cairosvg

ROOT = Path(__file__).resolve().parents[2]
src = ROOT / "assets" / "branding" / "icon.svg"
out = ROOT / "assets" / "branding" / "generated"
out.mkdir(parents=True, exist_ok=True)
png = cairosvg.svg2png(bytestring=src.read_bytes(), output_width=512, output_height=512)
im = Image.open(BytesIO(png)).convert("RGBA")
assert im.getpixel((0,0))[3] == 0, "icon background must be transparent"
im.save(out / "icon-512.png", optimize=True)
im.save(out / "app.ico", format="ICO", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print(f"Created {out/'icon-512.png'} and {out/'app.ico'}")
