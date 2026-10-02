"""Rex Magnum - eye texture for guests + Carmen (Characters/Guests/Textures/Eye_BC.png).

The Blender eyeballs get their iris from shader nodes, which the FBX cannot carry, so in Unity they were blank
grey. GuestBuilder.EyeMeshes swaps in a sphere whose UVs are an azimuthal projection around the front pole:
    uv = 0.5 + r * (cos phi, sin phi),   r = 0.5 * (theta / pi) ** 0.6     (theta = angle from looking direction)
This script paints iris, pupil, limbal ring, sclera and a small highlight in that space. Plain numpy + PIL.

Run:  python eye_texture.py
"""
import math
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Unity", "Assets", "RexMagnum", "Characters", "Guests", "Textures", "Eye_BC.png")
RES = 512
IRIS = math.radians(31)       # iris half-angle on the eyeball (12 mm iris on a 24 mm eye)
PUPIL = math.radians(8.5)


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def main():
    rng = np.random.default_rng(4)
    y, x = (np.mgrid[0:RES, 0:RES] + 0.5) / RES
    u, v = x - 0.5, (1 - y) - 0.5                           # PNG row 0 = top = v 1
    r = np.hypot(u, v)
    phi = np.arctan2(v, u)
    theta = math.pi * np.clip(r / 0.5, 0, 1) ** (1 / 0.6)   # inverse of the mesh UV projection
    # sclera: warm off-white, a little pinker / darker towards the back, faint veins
    sclera = np.array([0.94, 0.92, 0.89])[None, None, :] * np.ones((RES, RES, 1))
    back = smooth(theta, 0.8, 2.0)[..., None]
    sclera = sclera * (1 - back * 0.06) + np.array([0.85, 0.62, 0.6]) * back * 0.06
    vein = np.zeros((RES, RES))
    for _ in range(8):
        a0 = rng.uniform(-math.pi, math.pi)
        wig = rng.uniform(0.01, 0.04)
        d = np.abs(np.angle(np.exp(1j * (phi - a0 - wig * np.sin(theta * 6 + a0)))))
        vein = np.maximum(vein, (1 - smooth(d, 0.0, 0.012)) * smooth(theta, 0.75, 1.3))
    sclera = sclera * (1 - vein[..., None] * 0.12) + np.array([0.75, 0.3, 0.28]) * vein[..., None] * 0.12
    # iris: hazel-brown with radial fibres, darker limbal ring, lighter collarette around the pupil
    fib = np.zeros((RES, RES))
    for k in (37, 61, 97, 143):
        fib += np.sin(phi * k + rng.uniform(0, 6.3) + np.sin(phi * 7) * 0.5) / 4
    t = np.clip((theta - PUPIL) / (IRIS - PUPIL), 0, 1)
    base = np.array([0.36, 0.24, 0.12])
    inner = np.array([0.55, 0.42, 0.2])
    iris = base * (1 - smooth(t, 0.0, 0.45))[..., None] * 0 + base
    iris = iris + (inner - base) * (1 - smooth(t, 0.1, 0.45))[..., None]
    iris = iris * (1 + 0.22 * fib[..., None])
    iris = iris * (1 - 0.65 * smooth(t, 0.78, 1.0))[..., None]           # limbal ring
    col = sclera.copy()
    m_iris = 1 - smooth(theta, IRIS - 0.015, IRIS + 0.03)
    col = col * (1 - m_iris[..., None]) + iris * m_iris[..., None]
    m_pupil = 1 - smooth(theta, PUPIL - 0.012, PUPIL + 0.012)
    col = col * (1 - m_pupil[..., None]) + np.array([0.015, 0.012, 0.01]) * m_pupil[..., None]
    # small window highlight, upper left, partly over the pupil
    hu, hv = -0.045, 0.05
    hl = 1 - smooth(np.hypot(u - hu, v - hv), 0.008, 0.02)
    col = col * (1 - hl[..., None] * 0.9) + np.array([1.0, 1.0, 1.0]) * hl[..., None] * 0.9
    img = (np.clip(col, 0, 1) * 255 + 0.5).astype(np.uint8)              # values are already sRGB
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(img, "RGB").save(OUT)
    print("[Eye] wrote", OUT)


if __name__ == "__main__":
    main()
