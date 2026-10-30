"""Renders the Home page's picture: python -I branding/home/render.py (from the repo root; needs Pillow and Chrome).

home.html, drawn with headless Chrome at twice the size, becomes Assets/Previews/home.jpg (960 x 600).
"""
import os
import subprocess
import tempfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "src", "DispHub", "Assets", "Previews", "home.jpg")
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

with tempfile.TemporaryDirectory() as tmp:
    shot = os.path.join(tmp, "shot.png")
    url = "file:///" + os.path.join(HERE, "home.html").replace("\\", "/")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    "--window-size=1920,1200", "--virtual-time-budget=3000", f"--screenshot={shot}", url],
                   check=True, capture_output=True)
    Image.open(shot).convert("RGB").crop((0, 0, 1920, 1200)).resize((960, 600), Image.LANCZOS).save(OUT, quality=92, optimize=True)
print("home.jpg 960 x 600")
