"""Draw Rex's shirt fabric: blue Hawaiian print with pink flamingos.

Original pattern, drawn in code. The image tiles without seams.
Needs Pillow (pip install pillow). Usage: python rex_flamingo_print.py out.png [size]
"""
import math
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

BLUE = (28, 78, 168)
BLUE_DARK = (18, 52, 120)
LEAF = (22, 110, 120)
LEAF_DARK = (14, 78, 92)
PINK = (244, 120, 160)
PINK_DARK = (214, 82, 128)
PINK_LIGHT = (255, 170, 196)
WHITE = (245, 240, 230)
BLACK = (25, 20, 25)
YELLOW = (250, 205, 80)


def bezier(p0, p1, p2, p3, n=40):
    pts = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 3
        b = 3 * (1 - t) ** 2 * t
        c = 3 * (1 - t) * t ** 2
        d = t ** 3
        pts.append((a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]))
    return pts


def thick_line(dr, pts, w0, w1, col):
    n = len(pts)
    for i, (x, y) in enumerate(pts):
        w = w0 + (w1 - w0) * i / (n - 1)
        dr.ellipse([x - w, y - w, x + w, y + w], fill=col)


def leaf(dr, x, y, s, ang, col, col2):
    """Monstera-like leaf: an ellipse with cut slits."""
    ca, sa = math.cos(ang), math.sin(ang)
    def tr(px, py):
        return (x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s)
    outline = [tr(math.cos(t) * 1.0, math.sin(t) * 0.62) for t in [i * math.tau / 60 for i in range(60)]]
    dr.polygon(outline, fill=col)
    dr.line([tr(-1.0, 0), tr(1.0, 0)], fill=col2, width=max(2, int(s * 0.05)))
    for k in range(-3, 4):
        if k == 0:
            continue
        px = k * 0.25
        side = 1 if k > 0 else -1
        for sgn in (-1, 1):
            dr.line([tr(px * 0.6, 0), tr(px + side * 0.12, sgn * 0.6)], fill=BLUE, width=max(2, int(s * 0.045)))


def hibiscus(dr, x, y, s, rot):
    for i in range(5):
        a = rot + i * math.tau / 5
        cx, cy = x + math.cos(a) * s * 0.55, y + math.sin(a) * s * 0.55
        dr.ellipse([cx - s * 0.5, cy - s * 0.5, cx + s * 0.5, cy + s * 0.5], fill=WHITE)
    dr.ellipse([x - s * 0.22, y - s * 0.22, x + s * 0.22, y + s * 0.22], fill=YELLOW)


def flamingo(dr, x, y, s, flip):
    f = -1 if flip else 1
    P = lambda px, py: (x + px * s * f, y + py * s)
    # legs (one up, one down)
    dr.line([P(0.05, 0.25), P(0.08, 1.25)], fill=PINK_DARK, width=max(2, int(s * 0.05)))
    dr.line([P(0.0, 0.25), P(-0.15, 0.62), P(0.08, 0.7)], fill=PINK_DARK, width=max(2, int(s * 0.05)))
    dr.line([P(0.08, 1.25), P(0.22, 1.27)], fill=PINK_DARK, width=max(2, int(s * 0.05)))
    # body
    body = [P(-0.55 + 0.9 * (0.5 + 0.5 * math.cos(t)), 0.08 + 0.26 * math.sin(t)) for t in
            [i * math.tau / 50 for i in range(50)]]
    dr.polygon(body, fill=PINK)
    # wing + tail
    wing = [P(-0.25 + 0.55 * (0.5 + 0.5 * math.cos(t)), 0.02 + 0.15 * math.sin(t)) for t in
            [i * math.tau / 40 for i in range(40)]]
    dr.polygon(wing, fill=PINK_DARK)
    dr.polygon([P(-0.55, 0.02), P(-0.78, -0.06), P(-0.62, 0.16)], fill=PINK_DARK)
    # S neck
    neck = bezier(P(0.32, -0.05), P(0.62, -0.45), P(0.05, -0.65), P(0.28, -1.0))
    thick_line(dr, neck, s * 0.075, s * 0.055, PINK)
    # head + beak (bent, black tip)
    hx, hy = P(0.3, -1.03)
    dr.ellipse([hx - s * 0.11, hy - s * 0.1, hx + s * 0.11, hy + s * 0.1], fill=PINK_LIGHT)
    dr.polygon([P(0.36, -1.08), P(0.58, -0.98), P(0.5, -0.86), P(0.36, -0.96)], fill=WHITE)
    dr.polygon([P(0.5, -0.98), P(0.6, -0.97), P(0.53, -0.84), P(0.48, -0.88)], fill=BLACK)
    ex, ey = P(0.31, -1.06)
    dr.ellipse([ex - s * 0.022, ey - s * 0.022, ex + s * 0.022, ey + s * 0.022], fill=BLACK)


def make(size=2048, seed=86):
    random.seed(seed)
    img = Image.new("RGB", (size, size), BLUE)
    dr = ImageDraw.Draw(img)
    # Background texture: darker blue soft blotches
    for _ in range(140):
        x, y, r = random.uniform(0, size), random.uniform(0, size), random.uniform(20, 70) * size / 2048
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                dr.ellipse([x + ox - r, y + oy - r, x + ox + r, y + oy + r], fill=BLUE_DARK)
    img = img.filter(ImageFilter.GaussianBlur(size / 300))
    dr = ImageDraw.Draw(img)
    items = []
    cells = 4
    cs = size / cells
    for i in range(cells):
        for j in range(cells):
            x = (i + 0.5 + random.uniform(-0.18, 0.18) + (0.5 if j % 2 else 0)) * cs
            y = (j + 0.5 + random.uniform(-0.12, 0.12)) * cs
            items.append(("leaf", x - cs * 0.3, y + cs * 0.25, random.uniform(0, math.tau)))
            items.append(("leaf", x + cs * 0.32, y - cs * 0.3, random.uniform(0, math.tau)))
            items.append(("hib", x - cs * 0.38, y - cs * 0.35, random.uniform(0, 1)))
            items.append(("fla", x, y, (i + j) % 2 == 1))
    order = {"leaf": 0, "hib": 1, "fla": 2}
    items.sort(key=lambda it: order[it[0]])
    s = cs * 0.34
    for kind, x, y, extra in items:
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                if kind == "leaf":
                    leaf(dr, x + ox, y + oy, s * 0.9, extra, LEAF, LEAF_DARK)
                elif kind == "hib":
                    hibiscus(dr, x + ox, y + oy, s * 0.22, extra)
                else:
                    flamingo(dr, x + ox, y + oy - s * 0.1, s * 0.8, extra)
    return img


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "rex_shirt_flamingo.png"
    size = int(sys.argv[2]) if len(sys.argv) > 2 else 2048
    make(size).save(out)
    print("[Rex] wrote", out)
