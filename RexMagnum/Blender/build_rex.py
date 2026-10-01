"""Build Rex Magnum (approval version) and render front/side/3-4/face pictures.

Run (Blender 5.x with MPFB installed):
  blender.exe -b --python build_rex.py -- --out "<folder for renders>" [--preview] [--save "<file>.blend"]
"""
import os
import sys

import bpy
import addon_utils  # noqa: E402  (bpy first, for the bpy Python module)

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(out=os.path.join(HERE, "..", "Renders"), preview=False,
             save=os.path.join(HERE, "Rex_v01.blend"))
    i = 0
    while i < len(a):
        if a[i] == "--out":
            o["out"] = a[i + 1]; i += 1
        elif a[i] == "--save":
            o["save"] = a[i + 1]; i += 1
        elif a[i] == "--preview":
            o["preview"] = True
        elif a[i] == "--no-render":
            o["no_render"] = True
        i += 1
    return o


def main():
    o = args()
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)
    bpy.ops.wm.read_homefile(use_empty=True)
    import rex_body
    import rex_look
    import rex_render
    import rex_rig
    ob, skin = rex_body.build_body("Rex")
    rex_look.dress(ob, skin)
    arm = rex_rig.rig(ob)
    import rex_anim
    rex_anim.make_all(arm)
    # Save before render (system-safety rule)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(o["save"]), compress=True)
    print("[Rex] saved", o["save"])
    if o.get("no_render"):
        return
    arm.animation_data.action = None
    import rex_rig as _r
    _r.reset_pose(arm)
    if o["preview"]:
        rex_render.setup(samples=64, res=(450, 600))
    else:
        rex_render.setup(samples=192, res=(900, 1200))
    rex_render.approval_set(os.path.abspath(o["out"]), height=ob.dimensions.z)


if __name__ == "__main__":
    main()
