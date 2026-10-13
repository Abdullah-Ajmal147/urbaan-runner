"""Renders the Play Store marketing screenshots (1080x1920) from raw phone screenshots.

Usage: python3 -I tools/store_graphics.py
Reads release/store-listing/raw/phone-N-*.png, writes release/store-listing/graphics/store-N.png.
Needs google-chrome (headless) on PATH. Fonts: app/assets/fonts (OFL).
"""
import pathlib, subprocess, sys, tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
RAW = ROOT / "release/store-listing/raw"
OUT = ROOT / "release/store-listing/graphics"
FONTS = ROOT / "app/assets/fonts"

# (raw file, headline, sub line, accent A, accent B)
SLIDES = [
    ("phone-1-07_tutorial.png", "Dash through<br>Lantern City", "Swipe to dodge, jump and slide", "#FF3DA6", "#22195A"),
    ("phone-2-11_home.png", "A new run<br>every day", "Daily Run and the Lantern Hunt", "#FFB547", "#3A1B5C"),
    ("phone-3-13_locker.png", "Build your<br>crew", "Couriers, trails and upgrades", "#2EE6FF", "#16205A"),
    ("phone-4-12_missions.png", "Missions that<br>keep you moving", "Earn sparks and XP on every run", "#7CF2C0", "#142A4A"),
    ("phone-5-10_results.png", "Beat your<br>best", "Every run levels you up", "#FF3DA6", "#2A1458"),
]

# Screens whose lower half is empty: fade the phone out sooner.
FADE_MORE = {"phone-3-13_locker.png", "phone-4-12_missions.png"}

CSS = """
@font-face{font-family:H;src:url('file://%(f)s/BricolageGrotesque-ExtraBold.ttf')}
@font-face{font-family:B;src:url('file://%(f)s/InstrumentSans-SemiBold.ttf')}
*{box-sizing:border-box;margin:0}
body{width:1080px;height:1920px;overflow:hidden;position:relative;font-family:B,sans-serif;
  background:linear-gradient(165deg,%(b)s 0%%,#0B0F29 70%%)}
.blob{position:absolute;border-radius:50%%;filter:blur(90px);opacity:.55}
.b1{width:620px;height:620px;left:-180px;top:-160px;background:%(a)s}
.b2{width:560px;height:560px;right:-220px;top:760px;background:%(a)s;opacity:.28}
.dots{position:absolute;inset:0;background-image:radial-gradient(rgba(244,246,255,.16) 2px,transparent 2.5px);
  background-size:64px 64px;-webkit-mask-image:linear-gradient(#000,transparent 45%%)}
h1{position:absolute;left:80px;top:96px;right:80px;font:800 104px/1.02 H,sans-serif;color:#F4F6FF;letter-spacing:-1px}
p{position:absolute;left:84px;top:%(py)spx;font-size:42px;color:%(a)s}
.phone{position:absolute;left:150px;top:%(ty)spx;width:780px;border-radius:96px;padding:16px;
  background:linear-gradient(#232a63,#0B0F29);box-shadow:0 40px 120px rgba(0,0,0,.55),0 0 0 3px rgba(255,255,255,.08) inset}
.phone img{display:block;width:748px;border-radius:80px}
.fade{position:absolute;left:0;right:0;bottom:0;height:%(fade)spx;
  background:linear-gradient(transparent,#0B0F29 92%%)}
"""

def render(raw, head, sub, a, b, out):
    two = head.count("<br>") >= 1
    css = CSS % {"f": FONTS, "a": a, "b": b, "py": 96 + (2 * 106 if two else 106) + 24, "ty": 560 if two else 480,
                      "fade": 700 if raw.name in FADE_MORE else 420}
    html = f"<html><head><meta charset=utf-8><style>{css}</style></head><body>" \
           f"<div class='blob b1'></div><div class='blob b2'></div><div class=dots></div>" \
           f"<h1>{head}</h1><p>{sub}</p><div class=phone><img src='file://{raw}'></div><div class=fade></div></body></html>"
    with tempfile.TemporaryDirectory() as d:
        p = pathlib.Path(d) / "s.html"
        p.write_text(html)
        subprocess.run(["google-chrome", "--headless=new", "--disable-gpu", "--hide-scrollbars",
                        "--allow-file-access-from-files", "--virtual-time-budget=3000",
                        "--window-size=1080,1920", f"--screenshot={out}", f"file://{p}"],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for i, (f, h, s, a, b) in enumerate(SLIDES, 1):
        src = RAW / f
        if not src.exists():
            sys.exit(f"missing {src}")
        render(src, h, s, a, b, OUT / f"store-{i}.png")
        print("wrote", OUT / f"store-{i}.png")
