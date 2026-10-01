"""Rex Magnum - face shape keys (blend shapes) and teeth.

Shape keys (Unity blend shapes): Blink_L, Blink_R, Mouth_A, Mouth_E, Mouth_O, Mouth_M, Smile, SmugGrin.
They are mixes of MPFB expression units, stored as clean deltas (body shape is not doubled).
"""
import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

import bl_ext.user_default.mpfb as mpfb_pkg
from bl_ext.user_default.mpfb.services.targetservice import TargetService

UNITS = os.path.join(os.path.dirname(mpfb_pkg.__file__), "data", "targets", "expression", "units", "caucasian")

FACE_KEYS = {
    "Blink_L": {"eye-left-closure": 1.0},
    "Blink_R": {"eye-right-closure": 1.0},
    "Mouth_A": {"mouth-open": 0.85, "mouth-depression": 0.25},
    "Mouth_E": {"mouth-retraction": 0.7, "mouth-open": 0.35, "mouth-upward-retraction": 0.2},
    "Mouth_O": {"mouth-pursing": 0.6, "mouth-open": 0.5, "mouth-protusion": 0.35},
    "Mouth_M": {"mouth-compression": 0.9},
    "Smile": {"mouth-corner-puller": 0.9, "mouth-upward-retraction": 0.35, "eye-left-slit": 0.25,
              "eye-right-slit": 0.25},
    # Smug grin: half smile, one brow up, one eye narrowed
    "SmugGrin": {"mouth-corner-puller": 0.55, "mouth-compression": 0.35, "eyebrows-left-up": 0.6,
                 "eye-right-slit": 0.45, "nose-left-elevation": 0.2},
}


def _coords(key):
    a = np.empty(len(key.data) * 3, dtype=np.float32)
    key.data.foreach_get("co", a)
    return a


def add_face_keys(body):
    keys = body.data.shape_keys.key_blocks
    basis = _coords(keys[0])
    units = {}
    for name in sorted({u for mix in FACE_KEYS.values() for u in mix}):
        path = os.path.join(UNITS, name + ".target.gz")
        if not os.path.exists(path):
            print("[Rex] missing expression unit", name)
            continue
        TargetService.load_target(body, path, weight=0.0, name="unit_" + name)
        units[name] = _coords(body.data.shape_keys.key_blocks["unit_" + name]) - basis
    for key_name, mix in FACE_KEYS.items():
        delta = np.zeros_like(basis)
        for u, w in mix.items():
            if u in units:
                delta += units[u] * w
        k = body.shape_key_add(name=key_name, from_mix=False)
        k.data.foreach_set("co", basis + delta)
        k.value = 0.0
    for name in units:
        body.shape_key_remove(body.data.shape_keys.key_blocks["unit_" + name])
    print("[Rex] face keys:", ", ".join(FACE_KEYS))


def add_teeth(body_info, link_fn):
    """Very white teeth: two arcs of rounded teeth on the MPFB teeth helpers."""
    mat = bpy.data.materials.get("Rex_Teeth") or bpy.data.materials.new("Rex_Teeth")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.95, 0.95, 0.93, 1)
    b.inputs["Roughness"].default_value = 0.12
    b.inputs["Subsurface Weight"].default_value = 0.2
    b.inputs["Coat Weight"].default_value = 0.5
    obs = []
    for grp, name in (("helper-upper-teeth", "Rex_TeethUpper"), ("helper-lower-teeth", "Rex_TeethLower")):
        vs = body_info.verts(grp)
        c = vs.mean(axis=0)
        w = (vs[:, 0].max() - vs[:, 0].min()) / 2
        depth = vs[:, 1].max() - vs[:, 1].min()
        front_y = vs[:, 1].min()
        upper = "upper" in grp
        bm = bmesh.new()
        n = 12
        span = math.radians(85)
        rw = max(w, 0.02) * 1.05
        arc = 2 * span * rw / n                             # width per tooth along the arc
        for i in range(n):
            t = (i + 0.5) / n * 2 - 1                      # -1 .. 1 across the arc
            ang = t * span
            x = c[0] + math.sin(ang) * rw
            y = front_y + (1 - math.cos(ang)) * max(depth, 0.02) * 0.9
            front = abs(t) < 0.35
            size_w = arc * (1.0 if front else 0.95)
            h = (0.0105 if front else 0.0085) * (1.0 if upper else 0.85)
            z = c[2] + (-h / 2 if upper else h / 2) * 0.5
            res = bmesh.ops.create_cube(bm, size=1.0)
            vv = res["verts"]
            bmesh.ops.scale(bm, vec=(size_w, 0.0045, h), verts=vv)
            bmesh.ops.rotate(bm, verts=vv, cent=(0, 0, 0),
                             matrix=Matrix.Rotation(-ang, 3, "Z"))
            bmesh.ops.translate(bm, verts=vv, vec=(x, y, z))
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(name, me)
        me.materials.append(mat)
        md = ob.modifiers.new("Bevel", "BEVEL")
        md.width = 0.0012
        md.segments = 2
        sub = ob.modifiers.new("Subsurf", "SUBSURF")
        sub.levels = 1
        sub.render_levels = 2
        for p in me.polygons:
            p.use_smooth = True
        link_fn(ob)
        obs.append(ob)
    return obs
