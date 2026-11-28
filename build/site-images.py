# Writes the smaller copies of the website's pictures that phones and small screens load instead of the full size
# (the pages pick one through srcset). Run it from the repo root after changing a picture in site/assets/img:
#   python build/site-images.py
# Needs Pillow (pip install pillow).
from pathlib import Path

from PIL import Image

IMG = Path("site/assets/img")
WIDTHS = {
    "window-profiles.webp": (720, 1000),
    "window-profiles-standard.webp": (720, 1000),
    "window-profiles-night.webp": (720, 1000),
    "window-apps.webp": (720, 1000),
    "window-schedule.webp": (720, 1000),
    "reef-gaming.webp": (720,),
}
# a photo hides more compression than a screenshot of the app
QUALITY = {"reef-gaming.webp": 72}
# the app icon at twice the sizes the pages show it: 24-28 px in the header, 64-88 px elsewhere
ICONS = {"icon-56.webp": 56, "icon-176.webp": 176}

for name, widths in WIDTHS.items():
    source = Image.open(IMG / name).convert("RGB")
    for width in widths:
        height = round(source.height * width / source.width)
        target = IMG / f"{Path(name).stem}-{width}.webp"
        quality = QUALITY.get(name, 82)
        source.resize((width, height), Image.Resampling.LANCZOS).save(target, "WEBP", quality=quality, method=6)
        print(f"{target} {width}x{height} {target.stat().st_size // 1024} KiB")

icon = Image.open(IMG / "icon-192.png").convert("RGBA")
for name, size in ICONS.items():
    target = IMG / name
    icon.resize((size, size), Image.Resampling.LANCZOS).save(target, "WEBP", quality=90, alpha_quality=90, method=6)
    print(f"{target} {size}x{size} {target.stat().st_size // 1024} KiB")
