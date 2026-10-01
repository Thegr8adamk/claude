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


def setup_mpfb():
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)


def build_body(g):
    from bl_ext.user_default.mpfb.services.humanservice import HumanService
    from bl_ext.user_default.mpfb.services.materialservice import MaterialService
    macro = dict(g["macro"])
    macro.setdefault("cupsize", 0.5)
    macro.setdefault("firmness", 0.5)
    macro["race"] = {"asian": 0.0, "caucasian": 1.0, "african": 0.0}
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
    """Straighten jagged cut edges: every open edge loop is projected onto its best-fit plane, then the cloth
    gets a little thickness so the hem reads as fabric, not a paper edge."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bnd = {v for e in bm.edges if e.is_boundary for v in e.verts}
    seen = set()
    for v0 in bnd:
        if v0 in seen:
            continue
        loop, stack = [], [v0]
        while stack:                       # connected boundary vertices = one hem loop
            v = stack.pop()
            if v in seen:
                continue
            seen.add(v)
            loop.append(v)
            for e in v.link_edges:
                if e.is_boundary:
                    o = e.other_vert(v)
                    if o not in seen:
                        stack.append(o)
        if len(loop) < 4:
            continue
        pts = np.array([tuple(v.co) for v in loop])
        c = pts.mean(axis=0)
        _, _, vt = np.linalg.svd(pts - c)
        nrm = Vector(vt[2])
        cv = Vector(c)
        for v in loop:
            v.co -= nrm * (v.co - cv).dot(nrm)
    bm.to_mesh(ob.data)
    bm.free()
    so = ob.modifiers.new("Thickness", "SOLIDIFY")
    so.thickness = thickness
    so.offset = 1.0


def make_top(body, L, kind, colour, name, look):
    neck_z = float(L["neck"].z)
    hip, chest = L["hip_z"], L["chest_z"]
    mat = look.fabric_material(f"{name}_Top", None, colour, rough=0.75, weave_scale=1200, bump=0.15, sheen=0.08)

    def keep(c):
        t = arm_t(L, c)
        torso = abs(c.x) < abs(L["sh_l"].x) + 0.04 and t is None
        if kind == "bikini":
            return torso and chest - 0.09 < c.z < chest + 0.07 and (c.y < 0.02 or chest - 0.03 < c.z < chest + 0.0)
        if kind == "tank":
            if t is not None:
                return False
            return torso and hip - 0.06 < c.z < neck_z - 0.06 and (abs(c.x) > 0.04 or c.z < chest + 0.04)
        if kind in ("tshirt", "polo"):
            if t is not None:
                return t < (0.45 if kind == "tshirt" else 0.4)
            return torso and hip - 0.1 < c.z < neck_z + 0.01
        if kind == "dress":
            if t is not None:
                return False
            if c.z >= hip - 0.02:
                return torso and c.z < neck_z - 0.05 and (abs(c.x) > 0.035 or c.z < chest + 0.05)
            return c.z > L["knee_z"] + 0.04 and not (is_leg(L, c) and False)
        return False

    def off(c, n):
        if kind == "bikini":
            return 0.004
        if kind == "dress" and c.z < hip:
            return 0.012 + (hip - c.z) * 0.18          # skirt flares out
        if c.z < hip + 0.06:
            return 0.018                                # hem hangs over the shorts
        return 0.009 if kind == "tank" else 0.012

    ob = look.shell(body, f"{name}_Top", keep, off, mat)
    clean_hems(ob)
    look.smooth_shell(ob, 6 if kind == "dress" else 3, 0.5)
    if kind == "dress":     # close the gap between the legs: pull inner skirt vertices to the middle line
        for v in ob.data.vertices:
            if v.co.z < L["crotch_z"] and abs(v.co.x) < 0.07:
                v.co.x *= 0.35
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
    rex_render.setup(samples=48, res=(1800, 700))
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
        render_lineup([g["name"] for g, _, _ in built], os.path.join(ROOT, "Renders", "guests", "guests_lineup.png"))
    rows = [export_guest(g, ob, arm, a.size, rex_export) for g, ob, arm in built]
    path = os.path.join(OUT, "Guests.json")
    old = json.load(open(path))["guests"] if os.path.exists(path) else []
    keep = [r for r in old if r["name"] not in {x["name"] for x in rows}]
    json.dump(dict(guests=keep + rows), open(path, "w"), indent=1)
    print(f"[Guests] exported {[r['name'] for r in rows]}", flush=True)


if __name__ == "__main__":
    main()
