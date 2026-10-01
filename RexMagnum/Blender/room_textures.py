"""Rex Magnum - pictures for room 304: the *Maximum Justice* (1986) poster and the TV frame.
Uses Rex's own approval render, cut out from the dark studio background, in an 80s duotone."""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
TEX = os.path.join(HERE, "textures")
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"
SANS = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def rex_cutout(name="rex_front.png"):
    im = Image.open(os.path.join(ROOT, "Renders", "approval", name)).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    bg = a[5, 5]
    d = np.abs(a - bg).sum(-1)
    mask = np.clip((d - 18) / 30, 0, 1)
    m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    return im, m


def duotone(im, dark, light):
    g = np.asarray(im.convert("L")).astype(np.float32) / 255
    g = np.clip((g - 0.05) * 1.4, 0, 1)
    d, l = np.array(dark), np.array(light)
    out = d[None, None] * (1 - g[..., None]) + l[None, None] * g[..., None]
    return Image.fromarray(out.astype(np.uint8))


def font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default()


def poster(w=1024, h=1536):
    img = Image.new("RGB", (w, h))
    px = np.zeros((h, w, 3), np.float32)
    y = np.linspace(0, 1, h)[:, None]
    px[..., 0] = 40 + 200 * (1 - y)          # sunset red/orange to black
    px[..., 1] = 10 + 90 * (1 - y) ** 2
    px[..., 2] = 30 + 30 * y
    # sunburst rays
    yy, xx = np.mgrid[0:h, 0:w]
    ang = np.arctan2(yy - h * 0.42, xx - w / 2)
    rays = (np.sin(ang * 18) > 0.6) * (1 - y) * 40
    px += rays[..., None] * np.array([1, 0.6, 0.2])
    img = Image.fromarray(np.clip(px, 0, 255).astype(np.uint8))
    rex, m = rex_cutout()
    rex = duotone(rex, (25, 5, 30), (255, 190, 110))
    s = 1.25
    rex = rex.resize((int(rex.width * s), int(rex.height * s)))
    m = m.resize(rex.size)
    img.paste(rex, ((w - rex.width) // 2, int(h * 0.2)), m)
    d = ImageDraw.Draw(img)
    def text(t, f, yv, fill, stroke=0, sc=(0, 0, 0)):
        tw = d.textlength(t, font=f)
        d.text(((w - tw) / 2, yv), t, font=f, fill=fill, stroke_width=stroke, stroke_fill=sc)
    text("REX MAGNUM", font(SANS, 70), 40, (255, 235, 200), 3)
    text("HE IS THE LAW. HE IS THE JURY. HE IS LATE.", font(SANS, 26), 130, (255, 210, 160), 2)
    t1 = font(BOLD, 150)
    text("MAXIMUM", t1, h - 430, (255, 200, 60), 8, (120, 10, 10))
    text("JUSTICE", t1, h - 270, (255, 200, 60), 8, (120, 10, 10))
    text("IN THEATERS SUMMER 1986", font(SANS, 34), h - 80, (230, 230, 230), 2)
    # worn paper: light noise + folds + faded corners
    a = np.asarray(img).astype(np.float32)
    a *= 0.9 + 0.1 * np.random.default_rng(1).random((h, w, 1))
    for fy in (h // 3, 2 * h // 3):
        a[fy - 2:fy + 2] *= 1.25
    a[:, w // 2 - 2:w // 2 + 2] *= 1.2
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    img.save(os.path.join(TEX, "poster_maximum_justice.png"))


def tv_frame(w=1024, h=576):
    """A frame from the film: Rex in a cloud of fire, letterbox, scan lines."""
    yy, xx = np.mgrid[0:h, 0:w]
    rng = np.random.default_rng(4)
    fire = np.zeros((h, w, 3), np.float32)
    for _ in range(60):
        cx, cy, r = rng.uniform(0, w), rng.uniform(h * 0.3, h), rng.uniform(40, 160)
        f = np.exp(-((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * r * r))
        fire += f[..., None] * np.array([255, rng.uniform(60, 140), 15]) * rng.uniform(0.2, 0.5)
    fire = 255 * (1 - np.exp(-fire / 220))      # soft highlight roll-off
    img = Image.fromarray(np.clip(fire * np.array([1, 0.75, 0.55]) + 10, 0, 255).astype(np.uint8))
    rex, m = rex_cutout("rex_front.png")
    box = (0, 0, rex.width, int(rex.height * 0.55))      # chest-up shot (no studio floor)
    rex, m = rex.crop(box), m.crop(box)
    rex = duotone(rex, (10, 5, 5), (255, 220, 170)).resize((int(rex.width * 0.8), int(rex.height * 0.8)))
    m = m.resize(rex.size)
    img.paste(rex, (w // 2 - rex.width // 2 + 180, h - h // 9 - rex.height + 4), m)
    a = np.asarray(img).astype(np.float32)
    a[::3] *= 0.75                      # scan lines
    a[: h // 9] = 0                     # letterbox
    a[-h // 9:] = 0
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    ImageDraw.Draw(img).text((40, h - h // 9 + 14), "MAXIMUM JUSTICE  -  CHANNEL 4  MOVIE OF THE NIGHT",
                             font=font(SANS, 24), fill=(230, 230, 230))
    img.save(os.path.join(TEX, "tv_maximum_justice.png"))


if __name__ == "__main__":
    poster()
    tv_frame()
    print("ok")
