"""Quick look at the forest set with Walt on the path."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from characters import build_walt  # noqa: E402
from lib import reset_scene  # noqa: E402
from sets import build_forest, ground_height, mist, sky  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else "/tmp/forest.png"
SAMPLES = int(sys.argv[2]) if len(sys.argv) > 2 else 24

scn = reset_scene()
scn.render.engine = "CYCLES"
scn.cycles.device = "CPU"
scn.cycles.samples = SAMPLES
scn.cycles.use_denoising = True
scn.render.resolution_x, scn.render.resolution_y = 1280, 720
scn.view_settings.view_transform = "AgX"
scn.view_settings.look = "AgX - Punchy"

build_forest(seed=3)
sky(scn, sun_elev=30, sun_rot=30)
mist(0.012)
build_walt((0, 0, ground_height(0, 0)))

bpy.ops.object.camera_add(location=(1.6, -4.5, 1.3), rotation=(math.radians(88), 0, math.radians(18)))
scn.camera = bpy.context.object
scn.camera.data.lens = 32
scn.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("WROTE", OUT)
