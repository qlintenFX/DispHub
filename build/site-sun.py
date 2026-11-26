# Renders the sunbeams behind the closing icon on the website (site/assets/img/sun-rays*.webp): a few broad, soft
# shafts of light of different widths and lengths, all round the icon as if the sun were behind it, cool on the left
# and warm on the right. Black around them, for mix-blend-mode: screen; dithered so their soft falloff can't band.
# The sun is in the middle of the picture.
#   python build/site-sun.py
from pathlib import Path

import numpy as np
from PIL import Image

W, H = 1800, 1200
rng = np.random.default_rng(5)
y, x = np.mgrid[0:H, 0:W].astype(np.float32)
dx, dy = x - W / 2, y - H / 2
r = np.hypot(dx, dy) + 1e-3
a = np.arctan2(dy, dx)

# (direction in degrees, 0 is right and -90 up; width in radians; length; strength). The ones pointing down, behind
# the heading and text, are short and faint.
SHAFTS = [
    (-98, 0.15, 330, 1.00),
    (-150, 0.08, 250, 0.75),
    (-46, 0.10, 230, 0.80),
    (8, 0.12, 190, 0.70),
    (178, 0.07, 230, 0.60),
    (56, 0.07, 120, 0.40),
    (128, 0.09, 140, 0.45),
]
beams = np.zeros_like(r)
for deg, width, reach, strength in SHAFTS:
    d = np.angle(np.exp(1j * (a - np.radians(deg))))
    beams += strength * np.exp(-(d / width) ** 2) * np.exp(-r / reach)
beams *= np.clip((r - 20) / 120, 0, 1)
glow = np.exp(-(r / 120) ** 2) * 0.45 + 1.0 / (1.0 + (r / 200) ** 2) * 0.08

cool = np.array([0.62, 0.78, 1.00], np.float32)
warm = np.array([1.00, 0.84, 0.62], np.float32)
side = np.clip(0.5 + dx / 700, 0, 1)[..., None]
tint = cool * (1 - side) + warm * side
light = (beams * 0.5 + glow * 0.65)[..., None] * tint

rgb = 1.0 - np.exp(-light * 1.2)
# everything fades to black well inside the edges, so the picture has no edge on the page
e = np.hypot(dx / (W / 2 - 30), dy / (H / 2 - 30))
rgb *= (np.clip(1 - e ** 4, 0, 1) ** 2)[..., None]
lum = rgb.max(axis=2, keepdims=True)
rgb += rng.normal(0, 0.9 / 255, rgb.shape).astype(np.float32) * np.clip(lum * 40, 0, 1)
img = Image.fromarray(np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8), "RGB")

out = Path("site/assets/img")
for w, name in ((1800, "sun-rays.webp"), (900, "sun-rays-900.webp")):
    path = out / name
    img.resize((w, round(H * w / W)), Image.LANCZOS).save(path, "WEBP", quality=90, method=6)
    print(path, path.stat().st_size // 1024, "KiB")
