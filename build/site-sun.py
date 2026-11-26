# Renders the sunbeams behind the closing icon on the website (site/assets/img/sun-rays*.webp): broad, soft shafts of
# light fanning up from behind the icon, as when the sun is behind a cloud. Black around them, for
# mix-blend-mode: screen; dithered so their soft falloff can't band. The sun sits at SX, SY of the picture.
#   python build/site-sun.py
from pathlib import Path

import numpy as np
from PIL import Image

W, H = 2000, 1100
SX, SY = W / 2, 0.82 * H
rng = np.random.default_rng(5)
y, x = np.mgrid[0:H, 0:W].astype(np.float32)
dx, dy = x - SX, y - SY
r = np.hypot(dx, dy) + 1e-3
a = np.arctan2(dy, dx)

# the shafts: the whole sky round the sun is lit, in broad brighter and darker bands with soft edges
bands = np.zeros_like(a)
for f in (9, 13, 19, 27, 37, 53):
    bands += rng.uniform(0.5, 1.0) / f ** 0.3 * np.sin(a * f + rng.uniform(0, 2 * np.pi))
bands = (bands - bands.min()) / (bands.max() - bands.min())
shafts = 0.28 + 0.72 * bands ** 1.4
# each shaft reaches its own length, and they fade out as the light spreads
reach = 360 + 200 * np.sin(a * 4 + 1.3) ** 2
fade = np.exp(-r / reach) * np.clip((r - 30) / 140, 0, 1)
# they rise from behind the icon: the upper half, easing out just below the horizontal
up = np.clip((-dy / r + 0.12) / 0.55, 0, 1) ** 1.4

beams = shafts * fade * up
glow = np.exp(-(r / 150) ** 2) * 0.5 + 1.0 / (1.0 + (r / 260) ** 2) * 0.12 * up

cool = np.array([0.62, 0.78, 1.00], np.float32)
warm = np.array([1.00, 0.86, 0.66], np.float32)
# cool daylight in the shafts, warmer near the sun
mix = np.clip(1 - r / 500, 0, 1)[..., None]
tint = cool * (1 - mix) + warm * mix
light = (beams * 0.42 + glow * 0.8)[..., None] * tint

rgb = 1.0 - np.exp(-light * 1.2)
# everything fades to black well inside the edges, so the picture has no edge on the page
e = np.hypot((x - W / 2) / (W / 2 - 30), (y - H / 2) / (H / 2 - 20))
rgb *= (np.clip(1 - e ** 4, 0, 1) ** 2)[..., None]
lum = rgb.max(axis=2, keepdims=True)
rgb += rng.normal(0, 0.9 / 255, rgb.shape).astype(np.float32) * np.clip(lum * 40, 0, 1)
img = Image.fromarray(np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8), "RGB")

out = Path("site/assets/img")
for w, name in ((2000, "sun-rays.webp"), (1000, "sun-rays-1000.webp")):
    path = out / name
    img.resize((w, round(H * w / W)), Image.LANCZOS).save(path, "WEBP", quality=90, method=6)
    print(path, path.stat().st_size // 1024, "KiB")
