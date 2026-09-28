"""Render the character approval sheet: front and side line-ups.

Usage: python3 sheet.py [out_dir] [samples] [--fur]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from characters import build_bramble, build_hazel, build_pip, build_walt  # noqa: E402
from lib import principled, reset_scene  # noqa: E402

args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = args[0] if args else "/tmp/sheet"
SAMPLES = int(args[1]) if len(args) > 1 else 48
FUR = "--fur" in sys.argv
os.makedirs(OUT, exist_ok=True)

scn = reset_scene()
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = SAMPLES
scn.cycles.use_denoising = True
scn.render.resolution_x, scn.render.resolution_y = 1600, 900
scn.view_settings.view_transform = "AgX"
scn.view_settings.look = "AgX - Punchy"

world = bpy.data.worlds.new("World")
scn.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.68, 0.6, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.25

# Soft cyclorama floor that curves up into the backdrop.
bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 4, 0))
floor = bpy.context.object
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.subdivide(number_cuts=40)
bpy.ops.object.mode_set(mode="OBJECT")
for v in floor.data.vertices:
    y = v.co.y + 4
    if y > 3:
        v.co.z = (y - 3) ** 2 * 0.6
floor.data.materials.append(principled("Cyc", (0.42, 0.55, 0.42), 0.8))
for p in floor.data.polygons:
    p.use_smooth = True


def area(name, loc, energy, color, size):
    bpy.ops.object.light_add(type="AREA", location=loc)
    lt = bpy.context.object
    lt.name = name
    lt.data.energy = energy
    lt.data.color = color
    lt.data.size = size
    c = lt.constraints.new("TRACK_TO")
    c.target = target
    return lt


target = bpy.data.objects.new("Target", None)
target.location = (0, 0, 0.9)
scn.collection.objects.link(target)
area("Key", (-4, -6, 5), 900, (1.0, 0.88, 0.74), 4)
area("Fill", (5, -5, 2.5), 220, (0.75, 0.85, 1.0), 5)
area("Rim", (2, 5, 4), 900, (1.0, 0.95, 0.85), 3)

chars = [
    build_walt((-2.0, 0, 0), FUR),
    build_pip((-0.95, -0.2, 0), FUR),
    build_hazel((-0.4, -0.1, 0), FUR),
    build_bramble((0.85, 0.1, 0), FUR),
]

bpy.ops.object.camera_add(location=(-0.45, -8.5, 1.15), rotation=(math.radians(88), 0, 0))
cam = bpy.context.object
cam.data.lens = 50
scn.camera = cam


def shot(name):
    scn.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("WROTE", scn.render.filepath, flush=True)


shot("lineup_front")
for c in chars:
    c.rotation_euler.z = math.radians(-90)
shot("lineup_side")
for c in chars:
    c.rotation_euler.z = math.radians(-35)
cam.location = (-0.45, -7.2, 1.3)
shot("lineup_three_quarter")
