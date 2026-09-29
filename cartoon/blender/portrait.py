"""Close-up portrait of one character, for checking faces.

Usage: python3 portrait.py <walt|pip|hazel|bramble> <out.png> [samples] [--fur] [--full]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import characters  # noqa: E402
from lib import principled, reset_scene  # noqa: E402

args = [a for a in sys.argv[1:] if not a.startswith("--")]
WHO, OUT = args[0], args[1]
SAMPLES = int(args[2]) if len(args) > 2 else 16
FUR, FULL = "--fur" in sys.argv, "--full" in sys.argv

# (face center z, framing height) for face and full-body shots
FRAME = {"walt": (1.4, 0.75, 0.85, 1.9), "pip": (0.27, 0.22, 0.24, 0.6),
         "hazel": (0.44, 0.3, 0.3, 0.7), "bramble": (1.68, 0.9, 1.05, 2.4)}

scn = reset_scene()
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = SAMPLES
scn.cycles.use_denoising = True
scn.render.resolution_x = scn.render.resolution_y = 800
scn.view_settings.view_transform = "AgX"
scn.view_settings.look = "AgX - Punchy"
w = bpy.data.worlds.new("W")
scn.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.62, 0.55, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.3
bpy.ops.mesh.primitive_plane_add(size=20)
bpy.context.object.data.materials.append(principled("Floor", (0.35, 0.45, 0.33), 0.8))

getattr(characters, "build_" + WHO)((0, 0, 0), FUR)
fz, fh, bz, bh = FRAME[WHO]
cz, h = (bz, bh) if FULL else (fz, fh)
tgt = bpy.data.objects.new("T", None)
tgt.location = (0, 0, cz)
scn.collection.objects.link(tgt)

lens = 85
dist = h / (36 / lens) * 1.5
d = Vector((0.45, -1, 0.08)).normalized() * dist
bpy.ops.object.camera_add(location=tgt.location + d)
cam = bpy.context.object
cam.data.lens = lens
cam.constraints.new("TRACK_TO").target = tgt
scn.camera = cam

for name, loc, e, col in (("Key", (-1.2, -1.6, 1.3), 1.0, (1, 0.88, 0.74)),
                          ("Fill", (1.5, -1.2, 0.2), 0.3, (0.75, 0.85, 1)),
                          ("Rim", (0.8, 1.6, 1.0), 1.1, (1, 0.95, 0.85))):
    bpy.ops.object.light_add(type="AREA", location=tgt.location + Vector(loc) * dist)
    lt = bpy.context.object
    lt.data.energy = e * 160 * dist * dist
    lt.data.color = col
    lt.data.size = dist * 0.8
    lt.constraints.new("TRACK_TO").target = tgt

scn.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("WROTE", OUT)
