"""Cuts the DispHub monitor out of the original artwork (rendered on a light background) into the masters here.

Usage: python -I branding/cutout.py <artwork> branding [preview-dir]
Writes icon.png (the monitor, no shadow) and icon-shadow.png (its soft shadow alone) on the same square canvas.
With a preview directory, also writes check-*.png on dark, light and blue backgrounds to inspect the edges.
Needs Pillow. Then run `dotnet run build/gen-assets.cs` to regenerate every icon size.
"""
import sys
from collections import deque
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]
preview = sys.argv[3] if len(sys.argv) > 3 else None
img = Image.open(src).convert("RGB")
W, H = img.size
px = img.load()

# Background colour: median of the border.
border = [px[x, 0] for x in range(W)] + [px[x, H - 1] for x in range(W)] + [px[0, y] for y in range(H)] + [px[W - 1, y] for y in range(H)]
bg = tuple(sorted(c[i] for c in border)[len(border) // 2] for i in range(3))
bg_lum = sum(bg) / 3
print("background", bg)

STEP = 3      # max per-channel change between neighbouring background pixels (smooth shadow passes, edges stop)
MIN_LUM = 150 # never wander into dark areas

bgmask = bytearray(W * H)
q = deque()
for x in range(W):
    for y in (0, H - 1):
        if not bgmask[y * W + x]:
            bgmask[y * W + x] = 1; q.append((x, y))
for y in range(H):
    for x in (0, W - 1):
        if not bgmask[y * W + x]:
            bgmask[y * W + x] = 1; q.append((x, y))
while q:
    x, y = q.popleft()
    c = px[x, y]
    for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
        if 0 <= nx < W and 0 <= ny < H and not bgmask[ny * W + nx]:
            n = px[nx, ny]
            if max(abs(n[0] - c[0]), abs(n[1] - c[1]), abs(n[2] - c[2])) <= STEP and sum(n) / 3 >= MIN_LUM:
                bgmask[ny * W + nx] = 1
                q.append((nx, ny))

fg = Image.frombytes("L", (W, H), bytes(0 if b else 255 for b in bgmask))
# Drop the outermost anti-aliased ring (a blend with the background), then soften the edge by a pixel.
fg = fg.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))

# Shadow layer: in the background, how much darker than the background each pixel is.
shadow_alpha = Image.new("L", (W, H))
sa = shadow_alpha.load()
fgl = fg.load()
for y in range(H):
    for x in range(W):
        if fgl[x, y] < 255:
            c = px[x, y]
            a = 1 - (sum(c) / 3) / bg_lum
            sa[x, y] = max(0, min(255, int(round((a - 0.012) * 255 / 0.988 * 1.0)))) if a > 0.012 else 0
shadow_alpha = shadow_alpha.filter(ImageFilter.GaussianBlur(1.5))

icon = img.convert("RGBA")
icon.putalpha(fg)
shadow = Image.new("RGBA", (W, H), (20, 26, 38, 0))
shadow.putalpha(shadow_alpha)

# Square canvas around the monitor (shadow may extend a little).
bbox = fg.point(lambda v: 255 if v > 8 else 0).getbbox()
x0, y0, x1, y1 = bbox
side = max(x1 - x0, y1 - y0)
side = int(side * 1.04)
cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
box = (cx - side // 2, cy - side // 2, cx - side // 2 + side, cy - side // 2 + side)
print("monitor bbox", bbox, "canvas", box)
icon = icon.crop(box)
shadow = shadow.crop(box)
icon.save(f"{out}/icon.png")
shadow.save(f"{out}/icon-shadow.png")

for name, colour in (("dark", (32, 32, 32)), ("light", (243, 243, 243)), ("blue", (0, 90, 158))) if preview else ():
    base = Image.new("RGBA", icon.size, colour + (255,))
    base.alpha_composite(shadow)
    base.alpha_composite(icon)
    base.convert("RGB").resize((400, 400), Image.LANCZOS).save(f"{preview}/check-{name}.png")
print("done", icon.size)
