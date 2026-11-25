# Writes the smaller copies of the website's pictures that phones and small screens load instead of the full size
# (the pages pick one through srcset). Run it from the repo root after changing a picture in site/assets/img:
#   python build/site-images.py
# Needs Pillow (pip install pillow).
from pathlib import Path

from PIL import Image

IMG = Path("site/assets/img")
WIDTHS = {
    "window-profiles.webp": (720, 1000),
    "window-apps.webp": (720, 1000),
    "window-schedule.webp": (720, 1000),
    "reef-gaming.webp": (720,),
}

for name, widths in WIDTHS.items():
    source = Image.open(IMG / name).convert("RGB")
    for width in widths:
        height = round(source.height * width / source.width)
        target = IMG / f"{Path(name).stem}-{width}.webp"
        source.resize((width, height), Image.Resampling.LANCZOS).save(target, "WEBP", quality=82, method=6)
        print(f"{target} {width}x{height} {target.stat().st_size // 1024} KiB")
