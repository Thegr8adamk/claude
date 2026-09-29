"""Approval pictures in the forest: front and side line-ups, with fur.

Usage: python3 hero_sheet.py [out_dir] [samples]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from characters import build_bramble, build_hazel, build_pip, build_walt  # noqa: E402
from lib import reset_scene  # noqa: E402
from sets import build_forest, ground_height, mist, sky  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else "/tmp/hero"
SAMPLES = int(sys.argv[2]) if len(sys.argv) > 2 else 32
os.makedirs(OUT, exist_ok=True)

scn = reset_scene()
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = SAMPLES
scn.cycles.use_denoising = True
scn.render.resolution_x, scn.render.resolution_y = 1600, 900
scn.view_settings.view_transform = "AgX"
scn.view_settings.look = "AgX - Punchy"

build_forest(seed=3)
sky(scn, sun_elev=28, sun_rot=-35)
mist(0.009, color=(1.0, 0.86, 0.7))

spots = [("walt", -1.3, 0.0), ("pip", -0.45, -0.25), ("hazel", 0.05, -0.15), ("bramble", 1.2, 0.2)]
builders = dict(walt=build_walt, pip=build_pip, hazel=build_hazel, bramble=build_bramble)
chars = [builders[n]((x, y, ground_height(x, y)), True) for n, x, y in spots]

# Soft warm bounce from the camera side so faces read under the canopy.
bpy.ops.object.light_add(type="AREA", location=(-1.5, -5, 2.2))
fill = bpy.context.object
fill.data.energy = 900
fill.data.size = 4
fill.data.color = (1.0, 0.85, 0.7)
tgt = bpy.data.objects.new("T", None)
tgt.location = (0, 0, 0.9)
scn.collection.objects.link(tgt)
fill.constraints.new("TRACK_TO").target = tgt

bpy.ops.object.camera_add(location=(0.0, -6.8, 1.15))
cam = bpy.context.object
cam.data.lens = 40
cam.constraints.new("TRACK_TO").target = tgt
cam.data.dof.use_dof = True
cam.data.dof.focus_object = tgt
cam.data.dof.aperture_fstop = 2.8
scn.camera = cam


def shot(name):
    scn.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("WROTE", scn.render.filepath, flush=True)


shot("forest_front")
for c in chars:
    c.rotation_euler.z = math.radians(-90)
shot("forest_side")
for c in chars:
    c.rotation_euler.z = math.radians(-30)
shot("forest_three_quarter")
