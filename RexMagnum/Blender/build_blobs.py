"""Build all blob types: model -> rig -> animations -> Unity/GLB export (+ approval pictures).

  blender -b --python build_blobs.py -- [--kinds Tourist,Bouncer] [--render] [--out <Unity Characters/Blobs folder>]
"""
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import blob_rig  # noqa: E402
import blobs  # noqa: E402

BUDGET = {"Tourist": 9000, "Lifeguard": 7000, "Bouncer": 11000, "KingGlorp": 22000}


def main():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    kinds = list(blobs.BLOB_TYPES)
    out = os.path.join(HERE, "..", "Unity", "Assets", "RexMagnum", "Characters", "Blobs")
    render = False
    for i, k in enumerate(a):
        if k == "--kinds":
            kinds = a[i + 1].split(",")
        if k == "--out":
            out = a[i + 1]
        if k == "--render":
            render = True
    out = os.path.abspath(out)
    manifest = {"blobs": []}
    for kind in kinds:
        bpy.ops.wm.read_homefile(use_empty=True)
        blob = blobs.build_blob(kind)
        coll = bpy.data.collections["Blob_" + kind]
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, f"Blob_{kind}_v01.blend"), compress=True)
        if render:
            blobs.render_approval(kind, os.path.join(HERE, "..", "Renders", "blobs"))
        arm = blob_rig.build_rig(kind, blobs.BLOB_DATA[kind], blob, coll)
        names = blob_rig.make_actions(kind, arm)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, f"Blob_{kind}_v02_rig.blend"), compress=True)
        blob_rig.actions_to_nla(arm, names)
        tris = blob_rig.export_game(kind, arm, blob, out, BUDGET[kind])
        spec = blobs.BLOB_TYPES[kind]
        manifest["blobs"].append(dict(kind=kind, colors=[list(c) for c in spec["colors"]], height=spec["height"],
                                      triangles=tris, clips=[n.split("_", 1)[1] for n in names]))
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, "Blobs.json")
    old = json.load(open(path)) if os.path.exists(path) else {"blobs": []}
    keep = [b for b in old["blobs"] if b["kind"] not in kinds]
    manifest["blobs"] = keep + manifest["blobs"]
    with open(path, "w") as f:
        json.dump(manifest, f, indent=1)
    print("[Blob] DONE", kinds)


if __name__ == "__main__":
    main()
