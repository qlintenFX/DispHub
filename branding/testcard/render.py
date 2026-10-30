"""Renders the test image pages with headless Chrome and writes the app's preview pictures.

Run from the repo root: python -I branding/testcard/render.py  (needs Pillow and Chrome)
"""
import os
import subprocess
import tempfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "src", "DispHub", "Assets", "Previews")
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

# page, size it's drawn at, size the app uses, file
PAGES = [
    ("testcard.html", (1920, 1200), (960, 600), "testcard-wide.jpg"),
    ("testcard-strip.html", (1440, 180), (720, 90), "testcard.jpg"),
]

for page, (w, h), size, name in PAGES:
    with tempfile.TemporaryDirectory() as tmp:
        shot = os.path.join(tmp, "shot.png")
        url = "file:///" + os.path.join(HERE, page).replace("\\", "/")
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                        "--allow-file-access-from-files", f"--window-size={w},{h}", "--virtual-time-budget=3000",
                        f"--screenshot={shot}", url], check=True, capture_output=True)
        image = Image.open(shot).convert("RGB").crop((0, 0, w, h))
        image.resize(size, Image.LANCZOS).save(os.path.join(OUT, name), quality=92, optimize=True)
        print(name, size)
