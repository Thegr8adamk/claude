"""Rex Magnum - resort guests + Carmen Vega (brief section 3). Realistic MPFB humans like Rex, each with own body
shape, skin tone, hair (game hair cards), clothes (body shells), eyes and the same Mixamo rig, so in Unity they are
Humanoid and use Rex's animations (idle, walk, run, hit, death...).

Run:  python guests.py [--only Callie,Gerald] [--render] [--size 1024]
Out:  Unity/Assets/RexMagnum/Characters/Guests/<Name>.fbx + Textures/<Name>_Skin_*.png, Guests.json,
      Renders/guests/guests_lineup.png (with --render), Blender/Guests_v01.blend
"""
import argparse
import json
import math
import os
import shutil
import sys

import bpy
import addon_utils  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Unity", "Assets", "RexMagnum", "Characters", "Guests")

import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# name, macros, skin (r,g,b), hair (style, melanin, gray, redness), top (kind, colour), bottom (kind, colour),
# feet (kind, colour), voice id (brief section 9)
GUESTS = [
    dict(name="Callie", macro=dict(gender=0.0, age=0.47, muscle=0.45, weight=0.42, height=0.55, proportions=0.6,
                                   cupsize=0.55, firmness=0.6),
         skin=(0.58, 0.36, 0.25), hair=("long", 0.18, 0.0, 0.25), top=("bikini", (0.95, 0.35, 0.3)),
         bottom=("shorts", (0.08, 0.15, 0.3)), feet=("sandals", (0.85, 0.75, 0.6)), voice="7YaUDeaStRuoYg3FKsmU"),
    dict(name="Heather", macro=dict(gender=0.0, age=0.58, muscle=0.4, weight=0.52, height=0.5, proportions=0.55,
                                    cupsize=0.6, firmness=0.5),
         skin=(0.33, 0.19, 0.11), hair=("bob", 0.95, 0.0, 0.1), top=("dress", (0.95, 0.75, 0.15)),
         bottom=(None, None), feet=("sandals", (0.4, 0.25, 0.12)), voice="MnUw1cSnpiLoLhpd3Hqp"),
    dict(name="Lucy", macro=dict(gender=0.0, age=0.5, muscle=0.4, weight=0.45, height=0.45, proportions=0.55,
                                 cupsize=0.5, firmness=0.55),
         skin=(0.72, 0.52, 0.42), hair=("long", 0.45, 0.0, 0.85), top=("tank", (0.1, 0.55, 0.55)),
         bottom=("shorts", (0.92, 0.92, 0.9)), feet=("sandals", (0.9, 0.9, 0.9)), voice="lcMyyd2HUfFzxdCaC4Ta"),
    dict(name="Gerald", macro=dict(gender=1.0, age=0.86, muscle=0.3, weight=0.72, height=0.45, proportions=0.4),
         skin=(0.68, 0.45, 0.36), hair=("fringe", 0.2, 0.9, 0.1), top=("polo", (0.85, 0.8, 0.65)),
         bottom=("shorts", (0.55, 0.5, 0.38)), feet=("socks_sandals", (0.35, 0.22, 0.12)), voice="fGIZlgPQ75MMlvQ6WxgY"),
    dict(name="Justin", macro=dict(gender=1.0, age=0.49, muscle=0.75, weight=0.5, height=0.6, proportions=0.6),
         skin=(0.5, 0.3, 0.2), hair=("short", 0.6, 0.0, 0.2), top=("tank", (0.95, 0.95, 0.95)),
         bottom=("trunks", (0.85, 0.15, 0.12)), feet=("sandals", (0.1, 0.1, 0.1)), voice="fV5vDeUO8ghvK7ntjiVC"),
    dict(name="Fin", macro=dict(gender=1.0, age=0.53, muscle=0.5, weight=0.5, height=0.55, proportions=0.5),
         skin=(0.17, 0.09, 0.05), hair=("short", 0.98, 0.0, 0.05), top=("tshirt", (0.012, 0.02, 0.07)),
         bottom=("shorts", (0.35, 0.33, 0.25)), feet=("sandals", (0.2, 0.15, 0.1)), voice="MjEQaRiSe6jP1b0vagRU"),
    dict(name="Carmen", macro=dict(gender=0.0, age=0.54, muscle=0.6, weight=0.45, height=0.6, proportions=0.6,
                                   cupsize=0.5, firmness=0.6),
         skin=(0.45, 0.27, 0.16), hair=("ponytail", 0.92, 0.0, 0.15), top=("polo", (0.008, 0.012, 0.045)),
         bottom=("pants", (0.02, 0.02, 0.025)), feet=("boots", (0.05, 0.05, 0.05)), voice="y3CfH7z4dKrH4hDLOEQK",
         security=True),
]

# 12 extra guests (owner request). Voices reuse the brief's guest voices (women: Callie / Heather / Lucy,
# men: Gerald / Justin / Fin); their flirt answers come from the shared list.
AS = {"asian": 1.0, "caucasian": 0.0, "african": 0.0}
AF = {"asian": 0.0, "caucasian": 0.0, "african": 1.0}
MIX = {"asian": 0.2, "caucasian": 0.5, "african": 0.3}
GUESTS += [
    dict(name="Marge", macro=dict(gender=0.0, age=0.9, muscle=0.25, weight=0.8, height=0.4, proportions=0.4, cupsize=0.6, firmness=0.3),
         skin=(0.7, 0.5, 0.42), hair=("bob", 0.1, 0.85, 0.05), top=("dress", (0.85, 0.3, 0.45)), bottom=(None, None),
         feet=("sandals", (0.85, 0.85, 0.82)), voice="MnUw1cSnpiLoLhpd3Hqp"),
    dict(name="Dwayne", macro=dict(gender=1.0, age=0.55, muscle=0.95, weight=0.7, height=0.75, proportions=0.6), race=AF,
         skin=(0.14, 0.075, 0.045), hair=("short", 0.99, 0.0, 0.0), top=("tank", (0.02, 0.02, 0.02)),
         bottom=("trunks", (0.05, 0.25, 0.6)), feet=("sandals", (0.05, 0.05, 0.05)), voice="MjEQaRiSe6jP1b0vagRU"),
    dict(name="Priya", macro=dict(gender=0.0, age=0.52, muscle=0.4, weight=0.4, height=0.45, proportions=0.6, cupsize=0.5, firmness=0.6),
         skin=(0.36, 0.2, 0.11), hair=("long", 0.99, 0.0, 0.05), top=("tank", (0.95, 0.7, 0.05)),
         bottom=("shorts", (0.9, 0.9, 0.88)), feet=("sandals", (0.6, 0.35, 0.15)), voice="lcMyyd2HUfFzxdCaC4Ta"),
    dict(name="Hiro", macro=dict(gender=1.0, age=0.6, muscle=0.45, weight=0.5, height=0.45, proportions=0.5), race=AS,
         skin=(0.6, 0.45, 0.32), hair=("short", 0.97, 0.15, 0.0), top=("polo", (0.02, 0.3, 0.3)),
         bottom=("shorts", (0.02, 0.03, 0.08)), feet=("sandals", (0.3, 0.2, 0.1)), voice="fGIZlgPQ75MMlvQ6WxgY"),
    dict(name="Brenda", macro=dict(gender=0.0, age=0.68, muscle=0.35, weight=0.6, height=0.5, proportions=0.45, cupsize=0.65, firmness=0.4),
         skin=(0.85, 0.48, 0.38), hair=("bob", 0.35, 0.0, 0.9), top=("bikini", (0.05, 0.4, 0.15)),
         bottom=("shorts", (0.85, 0.82, 0.7)), feet=("sandals", (0.9, 0.2, 0.3)), voice="7YaUDeaStRuoYg3FKsmU"),
    dict(name="Tyler", macro=dict(gender=1.0, age=0.44, muscle=0.3, weight=0.3, height=0.6, proportions=0.5),
         skin=(0.75, 0.55, 0.45), hair=("short", 0.25, 0.0, 0.2), top=("tshirt", (0.9, 0.35, 0.02)),
         bottom=("trunks", (0.05, 0.05, 0.05)), feet=("sandals", (0.9, 0.9, 0.9)), voice="fV5vDeUO8ghvK7ntjiVC"),
    dict(name="Rosa", macro=dict(gender=0.0, age=0.62, muscle=0.4, weight=0.55, height=0.45, proportions=0.55, cupsize=0.6, firmness=0.5),
         skin=(0.48, 0.29, 0.17), hair=("ponytail", 0.95, 0.1, 0.1), top=("dress", (0.6, 0.02, 0.03)), bottom=(None, None),
         feet=("sandals", (0.15, 0.08, 0.04)), voice="MnUw1cSnpiLoLhpd3Hqp"),
    dict(name="Walter", macro=dict(gender=1.0, age=0.95, muscle=0.2, weight=0.35, height=0.5, proportions=0.4),
         skin=(0.72, 0.52, 0.45), hair=("fringe", 0.1, 1.0, 0.0), top=("polo", (0.35, 0.65, 0.05)),
         bottom=("shorts", (0.5, 0.45, 0.33)), feet=("socks_sandals", (0.25, 0.15, 0.08)), voice="fGIZlgPQ75MMlvQ6WxgY"),
    dict(name="Amara", macro=dict(gender=0.0, age=0.47, muscle=0.5, weight=0.45, height=0.6, proportions=0.65, cupsize=0.5, firmness=0.65), race=AF,
         skin=(0.2, 0.1, 0.055), hair=("long", 0.99, 0.0, 0.0), top=("bikini", (0.0, 0.45, 0.45)),
         bottom=("shorts", (0.95, 0.95, 0.95)), feet=("sandals", (0.85, 0.65, 0.1)), voice="7YaUDeaStRuoYg3FKsmU"),
    dict(name="Chad", macro=dict(gender=1.0, age=0.47, muscle=0.8, weight=0.5, height=0.65, proportions=0.6),
         skin=(0.62, 0.38, 0.25), hair=("short", 0.55, 0.0, 0.25), top=("tank", (0.95, 0.35, 0.55)),
         bottom=("trunks", (0.95, 0.75, 0.05)), feet=("sandals", (0.2, 0.2, 0.2)), voice="fV5vDeUO8ghvK7ntjiVC"),
    dict(name="Doris", macro=dict(gender=0.0, age=0.85, muscle=0.3, weight=0.5, height=0.35, proportions=0.45, cupsize=0.5, firmness=0.35), race=AS,
         skin=(0.65, 0.48, 0.35), hair=("bob", 0.4, 0.95, 0.0), top=("tshirt", (0.55, 0.35, 0.7)),
         bottom=("pants", (0.9, 0.9, 0.88)), feet=("sandals", (0.85, 0.85, 0.85)), voice="lcMyyd2HUfFzxdCaC4Ta"),
    dict(name="Earl", macro=dict(gender=1.0, age=0.7, muscle=0.35, weight=0.9, height=0.55, proportions=0.4), race=MIX,
         skin=(0.42, 0.25, 0.15), hair=("fringe", 0.6, 0.5, 0.1), top=("tshirt", (0.75, 0.75, 0.75)),
         bottom=("shorts", (0.08, 0.2, 0.08)), feet=("sandals", (0.1, 0.07, 0.05)), voice="MjEQaRiSe6jP1b0vagRU"),
]


def setup_mpfb():
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)


def build_body(g):
    from bl_ext.user_default.mpfb.services.humanservice import HumanService
    from bl_ext.user_default.mpfb.services.materialservice import MaterialService
    macro = dict(g["macro"])
    macro.setdefault("cupsize", 0.5)
    macro.setdefault("firmness", 0.5)
    macro["race"] = g.get("race", {"asian": 0.0, "caucasian": 1.0, "african": 0.0})
    ob = HumanService.create_human(macro_detail_dict=macro)
    ob.name = g["name"]
    mat = MaterialService.create_v2_skin_material(f"{g['name']}_Skin", ob)
    for n in mat.node_tree.nodes:
        if n.type != "GROUP":
            continue
        if n.node_tree.name == "MpfbSkinMasterColor":
            n.inputs["SkinColor"].default_value = (*g["skin"], 1)
            lips = tuple(c * 0.75 for c in g["skin"])
            n.inputs["LipsColor"].default_value = (lips[0] * 1.2, lips[1] * 0.8, lips[2] * 0.8, 1)
        if n.node_tree.name in ("MpfbBody", "MpfbFace", "MpfbEars"):
            for k, v in (("SpotStrength", 0.2), ("ColorVariationStrength", 0.3), ("Roughness", 0.45)):
                if k in n.inputs:
                    n.inputs[k].default_value = v
    return ob, mat


# ------------------------------------------------------------------ clothes (body shells)

def landmarks(body):
    J = body.center
    d = dict(
        neck=J("joint-neck") or J("joint-head"),
        hip_z=float(J("joint-pelvis").z) if J("joint-pelvis") else float(J("joint-l-upper-leg").z) + 0.05,
        knee_z=float((J("joint-l-knee").z + J("joint-r-knee").z) / 2),
        ankle_z=float((J("joint-l-ankle").z + J("joint-r-ankle").z) / 2),
        sh_l=J("joint-l-shoulder"), el_l=J("joint-l-elbow"), sh_r=J("joint-r-shoulder"), el_r=J("joint-r-elbow"),
    )
    d["chest_z"] = float(d["sh_l"].z) - 0.12
    d["crotch_z"] = float(J("joint-l-upper-leg").z) - 0.04
    return d


def arm_t(L, c):
    for sh, el in ((L["sh_l"], L["el_l"]), (L["sh_r"], L["el_r"])):
        if (c.x > 0) != (sh.x > 0):
            continue
        seg = el - sh
        t = (c - sh).dot(seg) / seg.length_squared
        if t < 0.0:
            continue
        closest = sh + seg * min(t, 1.6)
        if (c - closest).length < 0.09 and abs(c.x) > abs(sh.x) - 0.01:
            return t
    return None


def is_leg(L, c):
    return c.z < L["crotch_z"] + 0.06 and abs(c.x) > 0.005


def clean_hems(ob, thickness=0.003):
    """Straighten jagged cut edges, then give the cloth a little thickness so the hem reads as fabric, not a
    paper edge. A flat hem loop (sleeve, waist, leg) is projected onto its best-fit plane. A loop that is not flat
    (tank-top neckline over the straps, armholes) is smoothed along the loop instead, so its shape is kept."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bnd = {v for e in bm.edges if e.is_boundary for v in e.verts}
    seen = set()
    for v0 in bnd:
        if v0 in seen:
            continue
        loop, v, prev = [], v0, None
        while v is not None and v not in seen:      # walk the hem loop in order
            seen.add(v)
            loop.append(v)
            nxt = [e.other_vert(v) for e in v.link_edges if e.is_boundary and e.other_vert(v) is not prev]
            nxt = [o for o in nxt if o not in seen]
            prev, v = v, (nxt[0] if nxt else None)
        if len(loop) < 4:
            continue
        pts = np.array([tuple(v.co) for v in loop])
        c = pts.mean(axis=0)
        _, sv, vt = np.linalg.svd(pts - c)
        if sv[2] / math.sqrt(len(loop)) < 0.008:    # flat loop: project onto its plane
            nrm, cv = Vector(vt[2]), Vector(c)
            for v in loop:
                v.co -= nrm * (v.co - cv).dot(nrm)
            continue
        n = len(loop)                               # curved loop: Taubin smoothing (no shrinking)
        for it in range(140):
            k = 0.6 if it % 2 == 0 else -0.63
            new = [(loop[i - 1].co + loop[(i + 1) % n].co) * 0.5 for i in range(n)]
            for i, v in enumerate(loop):
                v.co = v.co + (new[i] - v.co) * k
    bm.to_mesh(ob.data)
    bm.free()
    so = ob.modifiers.new("Thickness", "SOLIDIFY")
    so.thickness = thickness
    so.offset = 1.0


def make_top(body, L, kind, colour, name, look):
    neck_z = float(L["neck"].z)
    hip, chest = L["hip_z"], L["chest_z"]
    mat = look.fabric_material(f"{name}_Top", None, colour, rough=0.75, weave_scale=1200, bump=0.15, sheen=0.08)
    cy = float(body.co[(body.co[:, 2] > chest - 0.05) & (body.co[:, 2] < chest + 0.05)][:, 1].mean())

    def keep(c):
        t = arm_t(L, c)
        torso = abs(c.x) < abs(L["sh_l"].x) + 0.04 and t is None
        if kind == "bikini":
            return torso and chest - 0.09 < c.z < chest + 0.07 and (c.y < 0.02 or chest - 0.03 < c.z < chest + 0.0)
        if kind in ("tank", "dress"):               # straight neckline, curving down into the armholes;
            if t is not None or not torso or c.z < hip - 0.06:   # straps are separate ribbons (make_straps)
                return False
            sx = abs(L["sh_l"].x)
            e = min(1.0, max(0.0, (abs(c.x) - 0.5 * sx) / (0.45 * sx)))
            top = 0.05 if c.y < cy else 0.09           # neckline height: front, back
            return c.z < chest + top + (-0.015 - top) * e * e * (3 - 2 * e)  # front + back meet under the arm (covers the bust side)
        if kind in ("tshirt", "polo"):
            if t is not None:
                return t < (0.45 if kind == "tshirt" else 0.4)
            return torso and hip - 0.1 < c.z < neck_z + 0.01
        return False

    def off(c, n):
        if kind == "bikini":
            return 0.004
        if kind == "dress":
            return 0.008 if c.z < hip + 0.1 else 0.012  # under the skirt: close to the body
        if c.z < hip + 0.06:
            return 0.018                                # hem hangs over the shorts
        return 0.009 if kind == "tank" else 0.012

    ob = look.shell(body, f"{name}_Top", keep, off, mat)
    clean_hems(ob)
    look.smooth_shell(ob, 6 if kind == "dress" else 3, 0.5)
    if kind in ("tank", "dress"):
        make_straps(body, L, mat, name, cy, width=0.016 if kind == "dress" else 0.032)
    if kind == "dress":
        make_skirt(body, L, mat, name, ob)
    return ob


def make_straps(body, L, mat, name, cy, width, segs=24):
    """Two clean ribbon straps from the front neckline, over the shoulder, to the back neckline. Each follows the
    body surface (largest radius per direction in a thin slice), 6 mm out, so it never cuts into the skin."""
    sx = abs(float(L["sh_l"].x))
    chest = L["chest_z"]
    zc = chest - 0.02                                   # centre of the arc, inside the chest
    bm = bmesh.new()
    for side in (-1, 1):
        xp = side * 0.6 * sx
        sl = body.co[(np.abs(body.co[:, 0] - xp) < 0.012) & (body.co[:, 2] > chest - 0.02)]
        sl = sl[sl[:, 2] < float(L["sh_l"].z) + 0.07]       # shoulder top only (not the neck, head or hair)
        ang = np.arctan2(sl[:, 2] - zc, cy - sl[:, 1])   # 0 = front, pi/2 = up, pi = back
        rad = np.hypot(sl[:, 2] - zc, sl[:, 1] - cy)
        # arc from below the front neckline, over the shoulder, to below the back neckline
        a0, a1 = 0.12, math.pi - 0.35           # wide arc; cut below to the neckline heights
        prev = None
        angs = [a0 + (a1 - a0) * i / segs for i in range(segs + 1)]
        rs = []
        for a in angs:
            m = np.abs(ang - a) < 0.12
            rs.append(float(rad[m].max()) if m.any() else float(rad.max()))
        rs = np.array(rs)
        for _ in range(4):                      # smooth the path (a raw max per angle makes a saw-tooth strap)
            rs = np.maximum(rs, np.convolve(np.pad(rs, 2, mode="edge"), np.ones(5) / 5, mode="valid"))
            rs = np.convolve(np.pad(rs, 2, mode="edge"), np.ones(5) / 5, mode="valid")
        for a, r in zip(angs, rs):
            r = float(r) + 0.006
            y, z = cy - math.cos(a) * r, zc + math.sin(a) * r
            # end just under the neckline (front ~chest+5 cm, back ~chest+9 cm): the strap meets the top's edge
            # instead of lying on top of the cloth over the bust
            if a < math.pi / 2 and z < chest + 0.03:
                continue
            if a > math.pi / 2 and z < chest + 0.07:
                break
            pair = [bm.verts.new((xp - width / 2, y, z)), bm.verts.new((xp + width / 2, y, z))]
            if prev:
                bm.faces.new((prev[0], prev[1], pair[1], pair[0]))
            prev = pair
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(f"{name}_Straps")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    me.materials.append(mat)
    ob = bpy.data.objects.new(f"{name}_Straps", me)
    bpy.context.scene.collection.objects.link(ob)
    so = ob.modifiers.new("Thickness", "SOLIDIFY")     # no Smooth modifier: it rolled the flat ribbon into a tube
    so.thickness = 0.003
    so.offset = 1.0
    return ob


def make_skirt(body, L, mat, name, bodice, rings=12, segs=48):
    """Skirt from the waist (overlapping the bodice) down to the knee. The top ring follows the body shape at the
    bodice distance, so there is no step at the waist; lower rings flare out with soft folds. The radius per
    direction never gets smaller going down (widest body point above), so the legs never poke through."""
    hip = L["hip_z"]
    top_z, bot_z = hip + 0.09, L["knee_z"] + 0.03
    sx = abs(float(L["sh_l"].x))
    co = body.co[np.abs(body.co[:, 0]) < sx * 1.3]       # torso + legs only (the A-pose hands hang beside the hips)
    cy = float(co[(co[:, 2] > hip - 0.03) & (co[:, 2] < hip + 0.03)][:, 1].mean())
    ang = np.arctan2(co[:, 1] - cy, co[:, 0])
    rad = np.hypot(co[:, 0], co[:, 1] - cy)
    bins = ((ang % math.tau) / math.tau * segs).astype(int) % segs

    def body_r(z0, z1):
        """Largest body radius per direction between heights z0..z1 (holes filled from the neighbours)."""
        m = (co[:, 2] > z0) & (co[:, 2] < z1)
        r = np.zeros(segs)
        np.maximum.at(r, bins[m], rad[m])
        for _ in range(segs):
            if r.min() > 0:
                break
            r = np.where(r > 0, r, np.maximum(np.roll(r, 1), np.roll(r, -1)))
        return np.maximum(r, (np.roll(r, 1) + r + np.roll(r, -1)) / 3)   # no single-bin dips

    # finished bodice (smoothing + thickness applied): the skirt top sits right on it, so there is no ledge
    dg = bpy.context.evaluated_depsgraph_get()
    ev = bodice.evaluated_get(dg)
    bme = ev.to_mesh()
    bco = np.array([tuple(bodice.matrix_world @ v.co) for v in bme.vertices])
    ev.to_mesh_clear()
    bb = bco[(bco[:, 2] > top_z - 0.015) & (bco[:, 2] < top_z + 0.015)]
    top = np.zeros(segs)
    bbins = ((np.arctan2(bb[:, 1] - cy, bb[:, 0]) % math.tau) / math.tau * segs).astype(int) % segs
    np.maximum.at(top, bbins, np.hypot(bb[:, 0], bb[:, 1] - cy))
    bm = bmesh.new()
    prev, widest = None, np.zeros(segs)
    for r_i in range(rings + 1):
        t = r_i / rings
        z = top_z + (bot_z - top_z) * t
        widest = np.maximum(widest, body_r(z - 0.03, z + 0.03 if r_i else top_z + 0.01))
        ring = []
        for k in range(segs):
            a = math.tau * (k + 0.5) / segs
            flare = (0.09 * math.cos(a) ** 2 + 0.07 * math.sin(a) ** 2) * t * t
            if r_i == 0 and top[k] > 0:
                base = top[k] + 0.0015                # top ring: right on the bodice
            else:
                ease = min(r_i / 3.0, 1.0)
                ease = ease * ease * (3 - 2 * ease)       # ease the gap in over 3 rings: no step below the waist
                base = max(widest[k] + 0.003 + (0.006 + 0.012 * t) * ease, top[k] + 0.0015)   # never inside
            rr = (base + flare) * (1 + 0.035 * t * math.sin(a * 8))
            ring.append(bm.verts.new((math.cos(a) * rr, cy + math.sin(a) * rr, z)))
        if prev:
            for k in range(segs):
                bm.faces.new((prev[k], prev[(k + 1) % segs], ring[(k + 1) % segs], ring[k]))
        prev = ring
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(f"{name}_Skirt")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    me.materials.append(mat)
    ob = bpy.data.objects.new(f"{name}_Skirt", me)
    bpy.context.scene.collection.objects.link(ob)
    so = ob.modifiers.new("Thickness", "SOLIDIFY")
    so.thickness = 0.003
    so.offset = -1.0
    return ob


def make_bottom(body, L, kind, colour, name, look):
    if not kind:
        return None
    hip = L["hip_z"]
    mat = look.fabric_material(f"{name}_Bottom", None, colour, rough=0.8, weave_scale=900, bump=0.2, sheen=0.08)
    low = {"shorts": (L["knee_z"] + L["crotch_z"]) / 2 - 0.02, "trunks": L["crotch_z"] - 0.12,
           "pants": L["ankle_z"] + 0.03}[kind]

    def keep(c):
        return low < c.z < hip + 0.04 and arm_t(L, c) is None and abs(c.x) < 0.3 and \
            (c.z < hip - 0.02 or abs(c.x) < abs(L["sh_l"].x))

    ob = look.shell(body, f"{name}_Bottom", keep, lambda c, n: 0.011 if kind != "pants" else 0.01, mat)
    clean_hems(ob)
    look.smooth_shell(ob, 3, 0.5)
    return ob


def make_feet(body, L, kind, colour, name, look):
    a = L["ankle_z"]
    sole = look.mat_principled(f"{name}_Sole", colour, 0.7)
    if kind == "boots":
        ob = look.shell(body, f"{name}_Boots", lambda c: c.z < a + 0.16, lambda c, n: 0.009, sole)
        clean_hems(ob, 0.004)
    else:
        ob = look.shell(body, f"{name}_Sandals", lambda c: c.z < a - 0.035, lambda c, n: 0.004, sole)
        if kind == "socks_sandals":
            socks = look.mat_principled(f"{name}_Socks", (0.92, 0.92, 0.9), 0.9)
            look.shell(body, f"{name}_Socks", lambda c: a - 0.03 < c.z < a + 0.12, lambda c, n: 0.004, socks)
    return ob


# ------------------------------------------------------------------ hair (game cards)

def strand_texture(path):
    from PIL import Image
    rng = np.random.default_rng(5)
    w, h = 256, 512
    a = np.zeros((h, w), np.float32)
    for _ in range(140):
        x = rng.uniform(0, w)
        wid = rng.uniform(1.0, 2.6)
        length = rng.uniform(0.6, 1.0)
        xs = np.arange(w)
        col = np.exp(-((xs - x) ** 2) / (2 * wid * wid))
        ys = np.arange(h) / h
        a += np.outer((ys < length) * (1 - ys * 0.5), col) * rng.uniform(0.5, 1.0)
    a = np.clip(a, 0, 1)
    shade = 0.75 + 0.25 * np.sin(np.linspace(0, 30, w))[None, :]
    rgb = np.dstack([shade * np.ones((h, w))] * 3)
    img = np.dstack([rgb, a])
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(path)


def hair_style(style):
    """-> density(rel_elevation_deg, back, rel) , length(rel, back), direction(rel)"""
    def dens(el, back, rel):
        if style == "fringe":                   # old man: bald top, short sides
            return 0.0 if el > 30 else 0.8
        return 1.0
    def length(rng, el, back, rel):
        if style == "long":
            return rng.uniform(0.24, 0.32)
        if style == "bob":
            return rng.uniform(0.11, 0.15)
        if style == "ponytail":
            return rng.uniform(0.06, 0.09)
        if style == "fringe":
            return rng.uniform(0.01, 0.02)
        return rng.uniform(0.02, 0.035)         # short
    return dens, length


def make_hair(body, g, look, rng):
    style, melanin, gray, red = g["hair"]
    name = g["name"]
    scalp_ids = set(body.groups.get("scalp", []))
    faces = [f for f in body.faces if all(i in scalp_ids for i in f)]
    head_c = body.center("joint-head-2") or body.center("joint-head")
    dens, length = hair_style(style)
    look.SURFACE.clear()                      # the surface KD tree is cached per body
    kd = look.surface_kd(body)
    sv = body.co[list(scalp_ids)]
    R = float(np.median(np.linalg.norm(sv - np.array(head_c), axis=1))) + 0.004

    def fall(p, n, L, segs):
        """Long hair: lies on the scalp (combed back and down), then falls under gravity behind the head and
        over the shoulders; kept just off the head and body."""
        nn = Vector(n)
        rel0 = Vector(p) - head_c
        d = Vector((rel0.x * 0.6, 0.6, -1.0))
        d = (d - nn * d.dot(nn)).normalized()          # along the scalp
        pts = [Vector(p) + nn * 0.002]
        r0 = rel0.length + 0.004                        # each strand keeps its own distance from the head
        seg = L / segs
        for i in range(segs):
            q = pts[-1] + d * seg
            rel = q - head_c
            if rel.length < r0 and q.z > head_c.z - 0.12:
                q = head_c + rel.normalized() * r0
            co, idx, dist = kd.find(q)
            if dist < 0.01 and q.z < head_c.z - 0.06:
                q = Vector(co) + Vector(body.nrm[idx]) * 0.01
            d = (q - pts[-1]).normalized()
            d = (d + Vector((0, 0.02, -0.45)) + Vector(rng.normal(0, 0.03, 3))).normalized()
            pts.append(q)
        return [tuple(v) for v in pts]

    def density(p, n):
        rel = Vector(p) - head_c
        el = math.degrees(math.atan2(rel.z, math.hypot(rel.x, rel.y)))
        if rel.y < -0.03 and el < 20 and style not in ("long", "bob"):   # keep the face clear
            return 0.0
        if rel.y < -0.06 and el < 25:
            return 0.0
        if style in ("long", "bob") and rel.y < -0.02 and el < 40 and abs(rel.x) < 0.07:
            return 0.0                                  # no strands over the face
        return dens(el, rel.y > 0, rel)

    count = {"long": 3400, "bob": 2800, "ponytail": 1600, "fringe": 900, "short": 1800}[style]
    pts = look.sample_surface(body, faces, count * 3, density, rng)
    strands = []
    for p, n in pts:
        rel = Vector(p) - head_c
        el = math.degrees(math.atan2(rel.z, math.hypot(rel.x, rel.y)))
        nn = Vector(n)
        L = length(rng, el, rel.y > 0, rel)
        if style in ("long", "bob"):
            strands.append(fall(p, n, L, 9 if style == "long" else 6))
            continue
        elif style == "ponytail":
            d = Vector((-rel.x, 1.0, 0.2 - rel.z * 2))    # combed back to the tie
            segs, grav = 5, 0.05
        else:
            d = Vector((0, 0.7, -0.4))
            segs, grav = 3, 0.05
        d = (d - nn * d.dot(nn)).normalized()
        strands.append(look.grow_on_surface(body, p, n, d, L, segs, grav, 0.05, rng, 0.001, 0.004))
    if style == "ponytail":        # the tail: a bundle from the back of the head down to the shoulders
        tie = head_c + Vector((0, 0.1, -0.02))
        for k in range(260):
            o = Vector((rng.normal(0, 0.012), rng.normal(0, 0.008), 0))
            pts_ = [tie + o + Vector((o.x * j * 0.4, 0.02 * j, -0.045 * j)) for j in range(7)]
            strands.append([tuple(q) for q in pts_])
    # scalp cap under the strands (hair colour), so no bare skin shows between the cards
    if style != "fringe":
        used = sorted({i for f in faces for i in f})
        remap = {o: k for k, o in enumerate(used)}
        cverts = [tuple(Vector(body.co[i]) + Vector(body.nrm[i]) * 0.0025) for i in used]
        cap = look.mesh_object(f"{name}_HairCap", cverts, [tuple(remap[i] for i in f) for f in faces],
                               look.mat_principled(f"{name}_HairCap", hair_rgb(melanin, gray, red), 0.55))
        cap["hair_rgb"] = list(hair_rgb(melanin, gray, red))
    # cards: one ribbon per strand, facing out from the head
    verts, faces_, uvs = [], [], []
    for si, s in enumerate(strands):
        s = [Vector(q) for q in s]
        if len(s) < 2:
            continue
        u0 = (si % 8) / 8.0
        base = len(verts)
        for j, q in enumerate(s):
            t = s[min(j + 1, len(s) - 1)] - s[max(j - 1, 0)]
            out = (q - head_c).normalized()
            side = t.cross(out).normalized() * (0.0045 if style in ("long", "bob", "ponytail") else 0.004)
            verts += [q - side, q + side]
            v = j / (len(s) - 1)
            uvs += [(u0, 1 - v), (u0 + 0.125, 1 - v)]
        for j in range(len(s) - 1):
            a = base + j * 2
            faces_.append((a, a + 1, a + 3, a + 2))
    me = bpy.data.meshes.new(f"{name}_Hair")
    me.from_pydata([tuple(v) for v in verts], [], faces_)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            uvl.data[li].uv = uvs[me.loops[li].vertex_index]
    tex = os.path.join(OUT, "Textures", "Hair_Strands.png")
    if not os.path.exists(tex):
        strand_texture(tex)
    m = bpy.data.materials.new(f"{name}_Hair")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    it = nt.nodes.new("ShaderNodeTexImage")
    it.image = bpy.data.images.load(tex, check_existing=True)
    col = hair_rgb(melanin, gray, red)
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Roughness"].default_value = 0.45
    nt.links.new(it.outputs["Alpha"], b.inputs["Alpha"])
    m["hair_rgb"] = list(col)
    me.materials.append(m)
    ob = bpy.data.objects.new(f"{name}_Hair", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def hair_rgb(melanin, gray, red):
    """Approximate the hair colour (linear RGB) for the game material."""
    dark = np.array([0.02, 0.012, 0.008])
    blond = np.array([0.55, 0.42, 0.22])
    redc = np.array([0.45, 0.12, 0.04])
    c = blond * (1 - melanin) + dark * melanin
    c = c * (1 - red * 0.6) + redc * red * 0.6 * (1 - melanin * 0.5)
    c = c * (1 - gray) + np.array([0.55, 0.55, 0.55]) * gray
    return tuple(float(x) for x in c)


# ------------------------------------------------------------------ build + export

def build_guest(g, look, rig_mod):
    rng = np.random.default_rng(abs(hash(g["name"])) % 2 ** 31)
    ob, skin = build_body(g)
    body = look.Body(ob)
    L = landmarks(body)
    eyes = look.make_eyes(body)
    for i, (eo, c, r) in enumerate(eyes):
        eo.name = f"{g['name']}_Eye_{'lr'[i]}"
    parts = [make_top(body, L, *g["top"], g["name"], look)]
    b = make_bottom(body, L, *g["bottom"], g["name"], look)
    if b:
        parts.append(b)
    make_feet(body, L, *g["feet"], g["name"], look)
    make_hair(body, g, look, rng)
    if g.get("security"):          # radio on the belt + badge
        r = look.mat_principled("Carmen_Radio", (0.02, 0.02, 0.02), 0.5)
        hp = Vector((abs(L["sh_l"].x) * 0.85, 0.02, L["hip_z"] - 0.02))
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        bmesh.ops.scale(bm, vec=(0.035, 0.06, 0.12), verts=bm.verts)
        me = bpy.data.meshes.new("Carmen_Radio")
        bm.to_mesh(me)
        me.materials.append(r)
        ro = bpy.data.objects.new("Carmen_Radio", me)
        ro.location = hp
        bpy.context.scene.collection.objects.link(ro)
    # rig + skin every part to the body
    from bl_ext.user_default.mpfb.services.humanservice import HumanService
    arm = HumanService.add_builtin_rig(ob, "mixamo_unity") or ob.parent
    arm.name = f"{g['name']}_Rig"
    bpy.context.view_layer.update()
    for o in list(bpy.data.objects):
        if o.type != "MESH" or o == ob or not o.name.startswith(g["name"] + "_"):
            continue
        if "_Eye_" in o.name:
            rig_mod.parent_to_bone(o, arm, "Head")
        elif o.name.endswith("_Radio"):
            rig_mod.parent_to_bone(o, arm, "Hips")
        else:
            rig_mod.skin_from_body(o, ob, arm)
    return ob, arm


def to_game(ob):
    """Bake MPFB shape keys, delete helper geometry, keep only bone weight groups."""
    me = ob.data
    if me.shape_keys:
        kb = me.shape_keys.key_blocks
        n = len(me.vertices)
        basis = np.empty(n * 3, dtype=np.float32)
        kb[0].data.foreach_get("co", basis)
        mix = basis.copy()
        for k in kb[1:]:
            if k.value:
                a = np.empty(n * 3, dtype=np.float32)
                k.data.foreach_get("co", a)
                mix += (a - basis) * k.value
        ob.shape_key_clear()
        me.vertices.foreach_set("co", mix)
    gi = ob.vertex_groups["body"].index
    keep = np.zeros(len(me.vertices), dtype=bool)
    for v in me.vertices:
        for gr in v.groups:
            if gr.group == gi and gr.weight > 0.5:
                keep[v.index] = True
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in range(len(keep)) if not keep[i]], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    for md in list(ob.modifiers):
        if md.type != "ARMATURE":
            ob.modifiers.remove(md)
    for gr in list(ob.vertex_groups):
        if not gr.name.startswith("mixamorig:"):
            ob.vertex_groups.remove(gr)


def export_guest(g, ob, arm, size, rex_export):
    name = g["name"]
    tex = os.path.join(OUT, "Textures")
    tmp = os.path.join(tex, "_tmp")
    rex_export.bake_skin(ob, tmp, size)
    for k in ("BC", "MS", "N"):
        shutil.move(os.path.join(tmp, f"Rex_Skin_{k}.png"), os.path.join(tex, f"{name}_Skin_{k}.png"))
    shutil.rmtree(tmp, ignore_errors=True)
    to_game(ob)
    objs = [arm] + [o for o in bpy.data.objects if o.type == "MESH" and (o == ob or o.name.startswith(name + "_"))]
    for o in objs:
        if o.type == "MESH":
            for md in [m for m in o.modifiers if m.type not in ("ARMATURE",)]:
                rex_export.apply_non_armature(o)
                break
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"{name}.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False, add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP")
    mats = []
    for o in objs:
        if o.type != "MESH":
            continue
        for m in o.data.materials:
            if m is None:
                continue
            row = dict(name=m.name)
            if m.name == f"{name}_Skin":
                row.update(baseMap=f"Textures/{name}_Skin_BC.png", normalMap=f"Textures/{name}_Skin_N.png",
                           maskMap=f"Textures/{name}_Skin_MS.png", color=[1, 1, 1, 1])
            elif m.name.endswith("_Hair"):
                row.update(baseMap="Textures/Hair_Strands.png", color=[*m["hair_rgb"], 1], alphaClip=True,
                           twoSided=True, smoothness=0.45)
            else:
                b = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
                c = b.inputs["Base Color"].default_value if b else (0.5, 0.5, 0.5, 1)
                row.update(color=[c[0], c[1], c[2], 1], smoothness=round(1 - b.inputs["Roughness"].default_value, 2) if b else 0.3)
            if row not in mats:
                mats.append(row)
    return dict(name=name, fbx=f"{name}.fbx", voice=g["voice"], security=bool(g.get("security")), materials=mats)


def render_lineup(names, path):
    import rex_render
    rex_render.setup(samples=48, res=(1800, 700) if len(names) > 1 else (360, 700))
    sc = bpy.context.scene
    cam = sc.camera
    if cam is None:
        cam = bpy.data.objects.new("LineupCam", bpy.data.cameras.new("LineupCam"))
        sc.collection.objects.link(cam)
        sc.camera = cam
    rigs = [bpy.data.objects[f"{n}_Rig"] for n in names]
    for i, r in enumerate(rigs):
        r.location.x = (i - (len(rigs) - 1) / 2) * 0.8
    cam.location = (0, -6.5, 1.0)
    cam.rotation_euler = (math.radians(88), 0, 0)
    cam.data.lens = 40
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--render", action="store_true")
    ap.add_argument("--size", type=int, default=1024)
    a = ap.parse_args()
    setup_mpfb()
    bpy.ops.wm.read_homefile(use_empty=True)
    import rex_export
    import rex_look as look
    import rex_rig
    os.makedirs(os.path.join(OUT, "Textures"), exist_ok=True)
    todo = [g for g in GUESTS if not a.only or g["name"] in a.only.split(",")]
    built = []
    for g in todo:
        ob, arm = build_guest(g, look, rex_rig)
        built.append((g, ob, arm))
        print(f"[Guests] built {g['name']}", flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Guests_v01.blend"), compress=True)
    if a.render:
        os.makedirs(os.path.join(ROOT, "Renders", "guests"), exist_ok=True)
        names = [g["name"] for g, _, _ in built]
        fn = f"guest_{names[0]}.png" if len(names) == 1 else "guests_lineup.png"
        render_lineup(names, os.path.join(ROOT, "Renders", "guests", fn))
    rows = [export_guest(g, ob, arm, a.size, rex_export) for g, ob, arm in built]
    path = os.path.join(OUT, "Guests.json")
    old = json.load(open(path))["guests"] if os.path.exists(path) else []
    keep = [r for r in old if r["name"] not in {x["name"] for x in rows}]
    json.dump(dict(guests=keep + rows), open(path, "w"), indent=1)
    print(f"[Guests] exported {[r['name'] for r in rows]}", flush=True)


if __name__ == "__main__":
    main()
