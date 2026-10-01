"""Rex Magnum - everything on top of the MPFB body.

Skin tan, eyes, hair (bald top, thin blond sides, short mullet), 3-day stubble
with gray (NO mustache), eyebrows, mirror aviator sunglasses, open blue
flamingo shirt, knee-length shorts, white socks, flip-flops, gold chain,
gold pinky ring, cheap gold watch.

All parts are separate objects, so they can be fitted to the rig later.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))


# ------------------------------------------------------------------ landmarks

class Body:
    """Vertex positions (shape keys applied) and joint centers of the MPFB body."""

    def __init__(self, ob):
        self.ob = ob
        mask = [m for m in ob.modifiers if m.type == "MASK"]
        for m in mask:
            m.show_viewport = False
        dg = bpy.context.evaluated_depsgraph_get()
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ob.matrix_world
        self.co = np.array([tuple(mw @ v.co) for v in me.vertices])
        self.nrm = np.array([tuple((mw.to_3x3() @ v.normal).normalized()) for v in me.vertices])
        self.faces = [tuple(p.vertices) for p in me.polygons]
        ev.to_mesh_clear()
        for m in mask:
            m.show_viewport = True
        self.groups = {}
        idx = {g.index: g.name for g in ob.vertex_groups}
        for v in ob.data.vertices:
            for g in v.groups:
                if g.weight > 0.5:
                    self.groups.setdefault(idx[g.group], []).append(v.index)
        self.body_set = set(self.groups.get("body", []))

    def center(self, group):
        ids = self.groups.get(group)
        return Vector(self.co[ids].mean(axis=0)) if ids else None

    def verts(self, group):
        return self.co[self.groups[group]]

    def body_faces(self):
        return [f for f in self.faces if all(i in self.body_set for i in f)]


# ------------------------------------------------------------------ materials

def mat_principled(name, color, rough=0.5, metal=0.0, **extra):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    for k, v in extra.items():
        b.inputs[k].default_value = v
    return m


def fabric_material(name, image_path=None, color=(0.5, 0.5, 0.5), rough=0.8, weave_scale=900.0,
                    bump=0.25, sheen=0.4, scale=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = rough
    b.inputs["Sheen Weight"].default_value = sheen
    b.inputs["Sheen Roughness"].default_value = 0.5
    uv = nt.nodes.new("ShaderNodeUVMap")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (scale, scale, scale)
    nt.links.new(uv.outputs[0], mapping.inputs[0])
    if image_path:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(image_path, check_existing=True)
        nt.links.new(mapping.outputs[0], tex.inputs[0])
        nt.links.new(tex.outputs[0], b.inputs["Base Color"])
    else:
        b.inputs["Base Color"].default_value = (*color, 1)
    # Fine weave bump
    wave1 = nt.nodes.new("ShaderNodeTexWave")
    wave1.inputs["Scale"].default_value = weave_scale
    wave1.wave_type = "BANDS"
    wave2 = nt.nodes.new("ShaderNodeTexWave")
    wave2.inputs["Scale"].default_value = weave_scale
    wave2.bands_direction = "Y"
    mx = nt.nodes.new("ShaderNodeMath")
    mx.operation = "MULTIPLY"
    nt.links.new(uv.outputs[0], wave1.inputs["Vector"])
    nt.links.new(uv.outputs[0], wave2.inputs["Vector"])
    nt.links.new(wave1.outputs["Fac"], mx.inputs[0])
    nt.links.new(wave2.outputs["Fac"], mx.inputs[1])
    bp = nt.nodes.new("ShaderNodeBump")
    bp.inputs["Strength"].default_value = bump
    bp.inputs["Distance"].default_value = 0.0005
    nt.links.new(mx.outputs[0], bp.inputs["Height"])
    nt.links.new(bp.outputs[0], b.inputs["Normal"])
    return m


def hair_material(name, melanin, gray_mix, redness=0.15, rough=0.32):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    a = nt.nodes.new("ShaderNodeBsdfHairPrincipled")
    a.parametrization = "MELANIN"
    a.inputs["Melanin"].default_value = melanin
    a.inputs["Melanin Redness"].default_value = redness
    a.inputs["Roughness"].default_value = rough
    a.inputs["Random Color"].default_value = 0.2
    g = nt.nodes.new("ShaderNodeBsdfHairPrincipled")
    g.parametrization = "MELANIN"
    g.inputs["Melanin"].default_value = 0.08
    g.inputs["Melanin Redness"].default_value = 0.0
    g.inputs["Roughness"].default_value = rough
    info = nt.nodes.new("ShaderNodeHairInfo")
    ramp = nt.nodes.new("ShaderNodeMath")
    ramp.operation = "GREATER_THAN"
    ramp.inputs[1].default_value = 1.0 - gray_mix
    nt.links.new(info.outputs["Random"], ramp.inputs[0])
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(ramp.outputs[0], mix.inputs[0])
    nt.links.new(a.outputs[0], mix.inputs[1])
    nt.links.new(g.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    return m


def gold(name="Rex_Gold", cheap=False):
    col = (0.9, 0.68, 0.3) if cheap else (1.0, 0.766, 0.336)
    return mat_principled(name, col, rough=0.28 if cheap else 0.14, metal=1.0)


# ------------------------------------------------------------------ helpers

def link(ob, parent=None):
    coll = bpy.data.collections.get("Rex_Parts")
    if coll is None:
        coll = bpy.data.collections.new("Rex_Parts")
        bpy.context.scene.collection.children.link(coll)
    coll.objects.link(ob)
    if parent is not None:
        ob.parent = parent
    return ob


def mesh_object(name, verts, faces, mat=None, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.validate()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    if mat:
        me.materials.append(mat)
    return link(ob)


def add_mods(ob, solidify=0.0, subsurf=1, offset=1.0):
    if solidify:
        s = ob.modifiers.new("Solidify", "SOLIDIFY")
        s.thickness = solidify
        s.offset = offset
        s.use_even_offset = True
    if subsurf:
        d = ob.modifiers.new("Subsurf", "SUBSURF")
        d.levels = 0
        d.render_levels = subsurf


def cylinder_uv(ob, center, axis_up=Vector((0, 0, 1)), scale=0.28):
    """Wrap UVs around a vertical axis (good for shirt/shorts prints)."""
    me = ob.data
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            d = co - center
            ang = math.atan2(d.x, -d.y)
            r = 0.17
            uv.data[li].uv = (ang * r / scale, co.z / scale)
    # Fix the seam: faces that cross the back seam get u shifted
    for p in me.polygons:
        us = [uv.data[li].uv[0] for li in p.loop_indices]
        if max(us) - min(us) > 0.5 * (math.pi * 0.17 / scale):
            for li in p.loop_indices:
                if uv.data[li].uv[0] < 0:
                    uv.data[li].uv[0] += 2 * math.pi * 0.17 / scale


# ------------------------------------------------------------------ skin

def tan_skin(mat):
    nt = mat.node_tree
    for n in nt.nodes:
        if n.type != "GROUP":
            continue
        name = n.node_tree.name
        if name == "MpfbSkinMasterColor":
            n.inputs["SkinColor"].default_value = (0.42, 0.205, 0.115, 1)      # strong tan
            n.inputs["LipsColor"].default_value = (0.36, 0.13, 0.1, 1)
            n.inputs["SpotColor"].default_value = (0.25, 0.1, 0.05, 1)
        if name in ("MpfbBody", "MpfbFace", "MpfbEars"):
            for k, v in (("SpotStrength", 0.35), ("UnevennessBumpStrength", 0.25), ("DermalBumpStrength", 0.35),
                         ("ColorVariationStrength", 0.35), ("Roughness", 0.42)):
                if k in n.inputs:
                    n.inputs[k].default_value = v


# ------------------------------------------------------------------ eyes

def eye_material():
    m = bpy.data.materials.get("Rex_Eye") or bpy.data.materials.new("Rex_Eye")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.05
    b.inputs["Coat Weight"].default_value = 1.0
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    # Eye looks along -Y: radial distance in XZ from the front pole
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(sep.outputs[0], comb.inputs[0])
    nt.links.new(sep.outputs[2], comb.inputs[1])
    ln = nt.nodes.new("ShaderNodeVectorMath")
    ln.operation = "LENGTH"
    nt.links.new(comb.outputs[0], ln.inputs[0])
    front = nt.nodes.new("ShaderNodeMath")
    front.operation = "LESS_THAN"
    front.inputs[1].default_value = 0.0
    nt.links.new(sep.outputs[1], front.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (0.01, 0.01, 0.01, 1)
    els[1].position, els[1].color = 0.17, (0.07, 0.1, 0.05, 1)
    e = els.new(0.34)
    e.color = (0.2, 0.17, 0.07, 1)
    e = els.new(0.37)
    e.color = (0.85, 0.82, 0.78, 1)
    e = els.new(1.0)
    e.color = (0.8, 0.7, 0.68, 1)
    div = nt.nodes.new("ShaderNodeMath")
    div.operation = "DIVIDE"
    nt.links.new(ln.outputs["Value"], div.inputs[0])
    div.inputs[1].default_value = 1.0
    nt.links.new(div.outputs[0], ramp.inputs[0])
    white = nt.nodes.new("ShaderNodeMix")
    white.data_type = "RGBA"
    nt.links.new(front.outputs[0], white.inputs[0])
    white.inputs[6].default_value = (0.8, 0.7, 0.68, 1)
    nt.links.new(ramp.outputs[0], white.inputs[7])
    nt.links.new(white.outputs[2], b.inputs["Base Color"])
    return m, div


def make_eyes(body):
    mat, div = eye_material()
    obs = []
    for side in ("l", "r"):
        vs = body.verts(f"helper-{side}-eye")
        c = Vector(vs.mean(axis=0))
        r = float(np.linalg.norm(vs - np.array(c), axis=1).max())
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=r * 0.98)
        bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(math.pi / 2, 3, "X"))
        me = bpy.data.meshes.new(f"Rex_Eye_{side}")
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        me.materials.append(mat)
        ob = link(bpy.data.objects.new(f"Rex_Eye_{side}", me))
        ob.location = c
        div.inputs[1].default_value = r
        obs.append((ob, c, r))
    return obs


# ------------------------------------------------------------------ hair curves

def sample_surface(body, faces, count, weight_fn, rng):
    """Random points on the given faces; weight_fn(point, normal) -> density 0..1.
    `count` is the number of tries on the faces that touch the region."""
    faces = [f for f in faces if any(weight_fn(body.co[i], body.nrm[i]) > 0 for i in f)]
    tris, areas = [], []
    for f in faces:
        for k in range(1, len(f) - 1):
            a, b, c = body.co[f[0]], body.co[f[k]], body.co[f[k + 1]]
            ar = 0.5 * np.linalg.norm(np.cross(b - a, c - a))
            tris.append((f[0], f[k], f[k + 1]))
            areas.append(ar)
    areas = np.array(areas)
    if areas.sum() == 0:
        return []
    probs = areas / areas.sum()
    picks = rng.choice(len(tris), size=count, p=probs)
    out = []
    for t in picks:
        i, j, k = tris[t]
        u, v = rng.random(), rng.random()
        if u + v > 1:
            u, v = 1 - u, 1 - v
        p = body.co[i] * (1 - u - v) + body.co[j] * u + body.co[k] * v
        n = body.nrm[i] * (1 - u - v) + body.nrm[j] * u + body.nrm[k] * v
        n = n / (np.linalg.norm(n) + 1e-9)
        w = weight_fn(p, n)
        if w > 0 and rng.random() < w:
            out.append((p, n))
    return out


def make_curves(name, strands, mat, root_r, tip_r):
    """strands: list of point lists."""
    if not strands:
        return None
    sizes = [len(s) for s in strands]
    hc = bpy.data.hair_curves.new(name)
    hc.add_curves(sizes)
    flat = np.array([p for s in strands for p in s], dtype=np.float32).ravel()
    hc.position_data.foreach_set("vector", flat)
    rad = hc.attributes.get("radius") or hc.attributes.new("radius", "FLOAT", "POINT")
    rv = []
    for s in strands:
        n = len(s)
        rv += [root_r + (tip_r - root_r) * (i / (n - 1)) for i in range(n)]
    rad.data.foreach_set("value", rv)
    hc.materials.append(mat)
    ob = bpy.data.objects.new(name, hc)
    return link(ob)


SURFACE = {}


def surface_kd(body):
    if "kd" not in SURFACE:
        kd = KDTree(len(body.co))
        for i, c in enumerate(body.co):
            if i in body.body_set:
                kd.insert(c, i)
        kd.balance()
        SURFACE["kd"] = kd
        SURFACE["body"] = body
    return SURFACE["kd"]


def grow_on_surface(body, p, n, direction, length, segs, gravity, curl, rng, lift_root, lift_tip):
    """Strand that lies on the skin (like combed hair): each point is pulled back to the surface."""
    kd = surface_kd(body)
    pts = [Vector(p) + Vector(n) * lift_root]
    d = Vector(direction).normalized()
    seg = length / segs
    for i in range(segs):
        d = (d + Vector((0, 0, -gravity)) + Vector(rng.normal(0, curl, 3))).normalized()
        q = pts[-1] + d * seg
        co, idx, _ = kd.find(q)
        nn = Vector(body.nrm[idx])
        lift = lift_root + (lift_tip - lift_root) * (i + 1) / segs
        q = Vector(co) + nn * lift + (q - Vector(co) - nn * (q - Vector(co)).dot(nn))
        d = (q - pts[-1]).normalized()
        pts.append(q)
    return [tuple(v) for v in pts]


def grow(p, n, direction, length, segs, gravity, curl=0.0, rng=None, lift=0.002):
    pts = [Vector(p) + Vector(n) * 0.0005]
    d = Vector(direction).normalized()
    seg = length / segs
    nn = Vector(n)
    for i in range(segs):
        d = (d + Vector((0, 0, -gravity)) + Vector(rng.normal(0, curl, 3))).normalized()
        q = pts[-1] + d * seg
        # Keep the strand a little off the skin (it should lie on it, not go inside)
        rel = q - Vector(p)
        if rel.dot(nn) < lift * (i + 1) / segs:
            q += nn * (lift * (i + 1) / segs - rel.dot(nn))
        pts.append(q)
    return [tuple(v) for v in pts]


def make_head_hair(body, rng):
    scalp_ids = set(body.groups.get("scalp", []))
    faces = [f for f in body.faces if all(i in scalp_ids for i in f)]
    head_c = body.center("joint-head-2") or body.center("joint-head")
    sv = body.co[list(scalp_ids)]
    top = sv[:, 2].max()
    z_ear = float(body.center("joint-l-eye").z) - 0.01

    def density(p, n):
        rel = Vector(p) - head_c
        el = math.degrees(math.atan2(rel.z, math.hypot(rel.x, rel.y)))
        back = rel.y > 0.0
        if el > 38:                     # bald top
            return 0.0
        if el > 26:                     # thin fringe around the bald spot
            return 0.18
        if back and el < 0:
            return 1.0                  # mullet zone
        return 0.55                     # thin sides

    pts = sample_surface(body, faces, 16000, density, rng)
    strands = []
    for p, n in pts:
        rel = Vector(p) - head_c
        nn = Vector(n)
        back = rel.y > 0.0
        if back and rel.z < 0.0:                    # short mullet: hangs down the neck
            length = rng.uniform(0.045, 0.085)
            d = Vector((rel.x * 0.3, 0.15, -1.0))
            segs, lift = 8, 0.004
        else:                                       # thin sides, combed back and down, flat
            length = rng.uniform(0.012, 0.024)
            d = Vector((0, 0.8, -0.6))
            segs, lift = 4, 0.0015
        d = (d - nn * d.dot(nn)).normalized()       # along the skin, not out of it
        strands.append(grow_on_surface(body, p, n, d, length, segs, 0.05, 0.05, rng, 0.0008, lift))
    return make_curves("Rex_Hair", strands, hair_material("Rex_HairMat", 0.45, 0.3, 0.45), 0.00005, 0.00002)


def make_stubble_and_brows(body, rng):
    mouth = body.center("helper-upper-teeth")
    lower = body.center("helper-lower-teeth")
    eye_l, eye_r = body.center("joint-l-eye"), body.center("joint-r-eye")
    head_c = body.center("joint-head")
    face_faces = body.body_faces()
    mz = float((mouth.z + lower.z) / 2)
    front_y = float(mouth.y)

    def beard(p, n):
        x, y, z = p
        if y > head_c.y + 0.035:          # not on the back of the neck
            return 0.0
        if z > float(eye_l.z) - 0.045:   # below cheekbones
            return 0.0
        if z < mz - 0.11:                 # stop on the upper neck
            return 0.0
        if abs(x) > 0.075 and z > mz + 0.02:
            return 0.0
        # NO mustache: skip the upper lip
        if z > mz + 0.004 and abs(x) < 0.032 and y < front_y + 0.02:
            return 0.0
        # skip the lips
        if abs(z - mz) < 0.012 and abs(x) < 0.03 and y < front_y + 0.02:
            return 0.0
        fade = 1.0 if z > mz - 0.07 else max(0.0, 1.0 - (mz - 0.07 - z) / 0.04)
        return 0.9 * fade

    pts = sample_surface(body, face_faces, 14000, beard, rng)
    strands = []
    for p, n in pts:
        d = Vector(n).lerp(Vector((0, 0, -1)), 0.5)
        strands.append(grow(p, n, d, rng.uniform(0.002, 0.004), 2, 0.05, 0.3, rng, lift=0.0008))
    stub = make_curves("Rex_Stubble", strands, hair_material("Rex_StubbleMat", 0.55, 0.4, 0.2, 0.4),
                       0.00008, 0.00004)

    def brow(p, n):
        for e in (eye_l, eye_r):
            dx = p[0] - e.x
            dz = p[2] - (e.z + 0.022 - 0.12 * dx * dx / 0.03)
            if abs(dx) < 0.03 and abs(dz) < 0.006 and p[1] < e.y:
                return 1.0
        return 0.0

    pts = sample_surface(body, face_faces, 1600, brow, rng)
    strands = []
    for p, n in pts:
        out = 1 if p[0] > 0 else -1
        d = Vector((out * 0.9, 0, 0.35)).lerp(Vector(n), 0.2)
        strands.append(grow(p, n, d, rng.uniform(0.006, 0.011), 3, 0.05, 0.1, rng, lift=0.001))
    brows = make_curves("Rex_Brows", strands, hair_material("Rex_BrowMat", 0.45, 0.2, 0.25), 0.00006, 0.00002)
    return stub, brows


# ------------------------------------------------------------------ sunglasses

def make_sunglasses(body, eyes):
    """Mirror aviators. Separate object (Rex always wears them)."""
    (_, cl, r), (_, cr, _) = eyes
    mid = (cl + cr) / 2
    frame = gold("Rex_GlassesFrame", cheap=True)
    lens = mat_principled("Rex_MirrorLens", (0.62, 0.66, 0.75), rough=0.04, metal=1.0)
    b = lens.node_tree.nodes["Principled BSDF"]
    if "Thin Film Thickness" in b.inputs:
        b.inputs["Thin Film Thickness"].default_value = 380.0
        b.inputs["Thin Film IOR"].default_value = 1.5
    root = link(bpy.data.objects.new("Rex_Sunglasses", None))
    root.location = mid
    half = abs(cl.x - cr.x) / 2
    a_w, b_h = 0.028, 0.022
    front = -(r + 0.012)

    def lens_y(x):
        return front + 0.9 * (abs(x) - half) ** 2 + 0.25 * x * x

    rims = {}
    for side, sx in (("L", 1 if cl.x > cr.x else -1), ("R", -1 if cl.x > cr.x else 1)):
        cx = sx * half
        pts = []
        for i in range(48):
            t = i / 48 * math.tau
            x = math.cos(t) * a_w
            z = math.sin(t) * b_h
            if z < 0:                                   # aviator teardrop: deeper toward the nose
                z *= 1.0 + 0.8 * max(0.0, -math.cos(t))
            if z > 0:
                z *= 0.7                                # flat top bar
            px = cx + (x if sx > 0 else -x)
            pts.append(Vector((px, 0.0, z - 0.004)))
        for p in pts:
            p.y = lens_y(p.x)
        c = Vector((cx, lens_y(cx) - 0.003, -0.006))
        lens_ob = mesh_object(f"Rex_Lens_{side}", list(pts) + [c],
                              [(i, (i + 1) % len(pts), len(pts)) for i in range(len(pts))], lens)
        lens_ob.parent = root
        add_mods(lens_ob, solidify=0.0012, subsurf=1)
        cu = bpy.data.curves.new(f"Rex_Rim_{side}", "CURVE")
        cu.dimensions = "3D"
        cu.bevel_depth = 0.0009
        cu.bevel_resolution = 3
        sp = cu.splines.new("POLY")
        sp.points.add(len(pts) - 1)
        for p, v in zip(sp.points, pts):
            p.co = (*v, 1)
        sp.use_cyclic_u = True
        rim = link(bpy.data.objects.new(f"Rex_Rim_{side}", cu), root)
        rim.data.materials.append(frame)
        rims[sx] = pts
    # Ear position: widest head point at eye height, behind the eyes
    sel = body.co[(np.abs(body.co[:, 2] - mid.z) < 0.012) & (body.co[:, 1] > mid.y)]
    ear_x = float(np.abs(sel[:, 0] - mid.x).max()) if len(sel) else half + 0.05
    ear_y = float(sel[np.argmax(np.abs(sel[:, 0] - mid.x)), 1] - mid.y) if len(sel) else 0.08
    curves = [("Bridge1", [(-half + a_w * 0.9, lens_y(half - a_w), 0.012), (0, front - 0.002, 0.016),
                           (half - a_w * 0.9, lens_y(half - a_w), 0.012)]),
              ("Bridge2", [(-half + a_w * 0.75, lens_y(half - a_w), -0.004), (0, front + 0.003, 0.0),
                           (half - a_w * 0.75, lens_y(half - a_w), -0.004)])]
    for sx in (1, -1):
        x0 = sx * (half + a_w)
        curves.append((f"Temple{sx}", [(x0, lens_y(x0), 0.008), (sx * (ear_x + 0.002), ear_y * 0.5, 0.008),
                                        (sx * (ear_x - 0.001), ear_y + 0.005, -0.004),
                                        (sx * (ear_x - 0.008), ear_y + 0.02, -0.03)]))
    for name, pts in curves:
        cu = bpy.data.curves.new("Rex_" + name, "CURVE")
        cu.dimensions = "3D"
        cu.bevel_depth = 0.0011
        cu.bevel_resolution = 3
        sp = cu.splines.new("NURBS")
        sp.points.add(len(pts) - 1)
        for p, v in zip(sp.points, pts):
            p.co = (*v, 1)
        sp.use_endpoint_u = True
        sp.order_u = min(3, len(pts))
        ob = link(bpy.data.objects.new("Rex_" + name, cu), root)
        ob.data.materials.append(frame)
    return root


# ------------------------------------------------------------------ clothes

def shell(body, name, keep_fn, offset_fn, mat):
    """Copy body faces where keep_fn(vertex) is true, push out by offset_fn."""
    keep = np.array([keep_fn(Vector(c)) for c in body.co])
    faces = [f for f in body.body_faces() if all(keep[i] for i in f)]
    used = sorted({i for f in faces for i in f})
    remap = {o: n for n, o in enumerate(used)}
    verts = []
    for i in used:
        c, n = Vector(body.co[i]), Vector(body.nrm[i])
        verts.append(c + n * offset_fn(c, n))
    ob = mesh_object(name, verts, [tuple(remap[i] for i in f) for f in faces], mat)
    return ob


def smooth_shell(ob, iters=4, factor=0.5):
    md = ob.modifiers.new("Smooth", "SMOOTH")
    md.iterations = iters
    md.factor = factor


def make_clothes(body, rng):
    J = body.center
    neck = J("joint-neck") or J("joint-head")
    hip_z = float(J("joint-pelvis").z) if J("joint-pelvis") else float(J("joint-l-upper-leg").z) + 0.05
    knee_z = float((J("joint-l-knee").z + J("joint-r-knee").z) / 2)
    ankle_z = float((J("joint-l-ankle").z + J("joint-r-ankle").z) / 2)
    sh_l, el_l = J("joint-l-shoulder"), J("joint-l-elbow")
    sh_r, el_r = J("joint-r-shoulder"), J("joint-r-elbow")
    chest_z = float(sh_l.z) - 0.12
    belly_front = float(body.co[(body.co[:, 2] > hip_z) & (body.co[:, 2] < chest_z)][:, 1].min())

    def arm_t(c):
        """0 at shoulder, 1 at elbow (None if the point is not on an arm)."""
        for sh, el in ((sh_l, el_l), (sh_r, el_r)):
            if (c.x > 0) != (sh.x > 0):
                continue
            seg = el - sh
            t = (c - sh).dot(seg) / seg.length_squared
            if t < 0.0:
                continue
            closest = sh + seg * min(t, 1.6)
            if (c - closest).length < 0.095 and abs(c.x) > abs(sh.x) - 0.01:
                return t
        return None

    # ---- Shirt: open on top, buttoned on the bottom half, short loose sleeves
    hem_z = hip_z - 0.1
    v_apex_z = hem_z + (float(neck.z) - hem_z) * 0.5      # only the bottom half is buttoned

    def shirt_keep(c):
        if c.z > float(neck.z) + 0.02 or c.z < hem_z:
            return False
        t = arm_t(c)
        if t is not None:
            return t < 0.62
        return abs(c.x) < abs(sh_l.x) + 0.05     # torso only (no hands, no forearms)

    def shirt_off(c, n):
        base = 0.016
        if c.z < chest_z:                       # loose, hangs over the belly
            base += 0.012 + max(0.0, (c.y - belly_front)) * 0.12 * (1 if c.y < 0 else 0)
        t = arm_t(c)
        if t is not None:
            base = 0.014 + 0.02 * max(0.0, t)   # wide sleeves
        if c.z > float(neck.z) - 0.04:
            base = 0.01
        return base

    shirt_mat = fabric_material("Rex_ShirtFlamingo", os.path.join(HERE, "textures", "rex_shirt_flamingo.png"),
                                rough=0.62, weave_scale=1400, bump=0.15, sheen=0.5, scale=1.0)
    shirt = shell(body, "Rex_Shirt", shirt_keep, shirt_off, shirt_mat)
    # Loose shirt: below the belly, the front hangs straight down (it does not follow the body in)
    me = shirt.data
    zs = np.array([v.co.z for v in me.vertices])
    belly = [v for v in me.vertices if v.co.y < 0 and hip_z - 0.05 < v.co.z < chest_z and arm_t(v.co) is None]
    if belly:
        bv = min(belly, key=lambda v: v.co.y)
        lim_y, lim_z = bv.co.y, bv.co.z
        for v in me.vertices:
            if v.co.y < 0 and hem_z + 0.03 < v.co.z < lim_z and arm_t(v.co) is None and abs(v.co.x) < 0.16:
                k = max(0.0, 1.0 - abs(v.co.x) / 0.16)
                target = lim_y + 0.004 * (lim_z - v.co.z) / 0.1
                if v.co.y > target:
                    v.co.y = v.co.y + (target - v.co.y) * k * 0.85
    # Open V on the top half (front, above the last done-up button)
    bm = bmesh.new()
    bm.from_mesh(shirt.data)
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        if c.y < 0.0 and c.z > v_apex_z:
            w = 0.008 + ((c.z - v_apex_z) / (float(neck.z) - v_apex_z)) ** 1.3 * 0.07
            if abs(c.x) < w:
                kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(shirt.data)
    bm.free()
    # Collar: fold the edge of the V and neck opening out flat over the shirt (a lapel)
    bm = bmesh.new()
    bm.from_mesh(shirt.data)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    edges = [e for e in bm.edges if e.is_boundary and
             min(v.co.z for v in e.verts) > v_apex_z - 0.005 and
             max(abs(v.co.x) for v in e.verts) < 0.12]
    if edges:
        res = bmesh.ops.extrude_edge_only(bm, edges=edges)
        new_verts = [g for g in res["geom"] if isinstance(g, bmesh.types.BMVert)]
        for v in new_verts:
            base_n = v.normal if v.normal.length > 0 else Vector((0, -1, 0))
            out = Vector((v.co.x, 0.0, 0.0)).normalized() if abs(v.co.x) > 1e-4 else Vector((0, 0, 0))
            if v.co.z > float(neck.z) - 0.05:     # back/side of the neck: collar stands up a little
                out = (out + Vector((0, 0.3, 0.6))).normalized()
            v.co += out * 0.032 + base_n * 0.004 + Vector((0, -0.006, 0))
    bm.to_mesh(shirt.data)
    bm.free()
    cylinder_uv(shirt, Vector((0, float(J("joint-spine-2").y) if J("joint-spine-2") else 0.0, 0)))
    smooth_shell(shirt, 3, 0.6)
    add_mods(shirt, solidify=0.003, subsurf=2, offset=-1)
    # Buttons on the bottom half
    btn_mat = mat_principled("Rex_Button", (0.85, 0.8, 0.7), rough=0.3)
    sh_bm = shirt.data
    for i in range(4):
        z = hip_z - 0.06 + i * (v_apex_z - hip_z + 0.04) / 3.5
        # front-most shirt point at this height near x=0
        cand = [shirt.matrix_world @ v.co for v in sh_bm.vertices if abs(v.co.z - z) < 0.02 and abs(v.co.x) < 0.03]
        if not cand:
            continue
        p = min(cand, key=lambda q: q.y)
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.0055, depth=0.002, location=(p.x, p.y - 0.004, z),
                                            rotation=(math.pi / 2, 0, 0))
        b = bpy.context.active_object
        b.name = f"Rex_Button{i}"
        b.data.materials.append(btn_mat)
        for c in b.users_collection:
            c.objects.unlink(b)
        link(b)

    # ---- Shorts: loose, knee length
    def shorts_keep(c):
        if arm_t(c) is not None:
            return False
        return knee_z + 0.035 < c.z < hem_z + 0.09

    def shorts_off(c, n):
        flare = max(0.0, (hip_z - 0.12 - c.z) / (hip_z - knee_z)) * 0.035
        return 0.012 + flare

    shorts_mat = fabric_material("Rex_Shorts", None, (0.32, 0.27, 0.18), rough=0.85, weave_scale=1800,
                                 bump=0.3, sheen=0.2)
    shorts = shell(body, "Rex_Shorts", shorts_keep, shorts_off, shorts_mat)
    shorts.data.uv_layers.new(name="UVMap")
    smooth_shell(shorts, 3, 0.6)
    add_mods(shorts, solidify=0.003, subsurf=2, offset=-1)

    # ---- White socks
    sock_mat = fabric_material("Rex_Socks", None, (0.82, 0.82, 0.8), rough=0.95, weave_scale=2500, bump=0.5,
                               sheen=0.6)
    socks = shell(body, "Rex_Socks", lambda c: c.z < ankle_z + 0.07, lambda c, n: 0.0035, sock_mat)
    socks.data.uv_layers.new(name="UVMap")
    add_mods(socks, solidify=0.002, subsurf=1, offset=-1)

    # ---- Flip-flops: rubber sole under each foot + Y strap
    rubber = mat_principled("Rex_FlipFlop", (0.02, 0.1, 0.22), rough=0.7)
    strap_m = mat_principled("Rex_FlipFlopStrap", (0.02, 0.02, 0.025), rough=0.5)
    for side in ("l", "r"):
        fv = body.co[(body.co[:, 2] < ankle_z - 0.02) & ((body.co[:, 0] > 0) == (side == "l"))]
        x0, x1 = fv[:, 0].min() - 0.008, fv[:, 0].max() + 0.008
        y0, y1 = fv[:, 1].min() - 0.012, fv[:, 1].max() + 0.01
        cx = (x0 + x1) / 2
        ring = []
        for i in range(32):
            t = i / 32 * math.tau
            yy = (y0 + y1) / 2 + math.sin(t) * (y1 - y0) / 2
            k = (yy - y0) / (y1 - y0)  # wider at the toes (front is -Y)
            w = (x1 - x0) / 2 * (1.05 - 0.3 * k)
            ring.append((cx + math.cos(t) * w, yy))
        verts = [(x, y, 0.0) for x, y in ring] + [(x, y, 0.014) for x, y in ring]
        n = len(ring)
        faces = [tuple(range(n))[::-1], tuple(range(n, 2 * n))]
        faces += [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        sole = mesh_object(f"Rex_FlipFlop_{side}", verts, faces, rubber, smooth=False)
        bev = sole.modifiers.new("Bevel", "BEVEL")
        bev.width = 0.004
        bev.segments = 3
        top_z = 0.014 + 0.006
        toe = (cx + (0.012 if side == "l" else -0.012), y0 + 0.03, top_z)
        for sx in (-1, 1):
            cu = bpy.data.curves.new(f"Rex_Strap_{side}{sx}", "CURVE")
            cu.dimensions = "3D"
            cu.bevel_depth = 0.004
            sp = cu.splines.new("NURBS")
            pts = [toe, (cx + sx * (x1 - x0) * 0.3, y0 + 0.07, 0.05), (cx + sx * (x1 - x0) * 0.5, y0 + 0.1, top_z)]
            sp.points.add(len(pts) - 1)
            for p, v in zip(sp.points, pts):
                p.co = (*v, 1)
            sp.use_endpoint_u = True
            sp.order_u = 3
            ob = link(bpy.data.objects.new(f"Rex_Strap_{side}{sx}", cu))
            ob.data.materials.append(strap_m)
    return dict(shirt=shirt, shorts=shorts, socks=socks, v_apex_z=v_apex_z, neck=neck)


# ------------------------------------------------------------------ jewelry

def chain_link(name, mat):
    bm = bmesh.new()
    # oval torus link
    R, r, seg, ring = 0.0045, 0.0011, 16, 6
    rows = []
    for i in range(seg):
        a = math.tau * i / seg
        row = []
        for j in range(ring):
            b = math.tau * j / ring
            x = (R * 1.35 + r * math.cos(b)) * math.cos(a)
            y = (R + r * math.cos(b)) * math.sin(a)
            row.append(bm.verts.new((x, y, r * math.sin(b))))
        rows.append(row)
    for i in range(seg):
        for j in range(ring):
            bm.faces.new((rows[i][j], rows[(i + 1) % seg][j], rows[(i + 1) % seg][(j + 1) % ring],
                          rows[i][(j + 1) % ring]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return me


def make_jewelry(body, clothes):
    gmat = gold()
    J = body.center
    neck = J("joint-neck") or J("joint-head")
    # Neck base ring: body points near neck base height
    base_z = float(neck.z) - 0.035
    ring = body.co[np.abs(body.co[:, 2] - base_z) < 0.006]
    ring = ring[np.abs(ring[:, 0]) < 0.09]
    cx, cy = float(ring[:, 0].mean()), float(ring[:, 1].mean())
    rx = float(np.abs(ring[:, 0] - cx).max()) + 0.01
    ry = float(np.abs(ring[:, 1] - cy).max()) + 0.012
    link_me = chain_link("Rex_ChainLink", gmat)
    chain_root = link(bpy.data.objects.new("Rex_GoldChain", None))
    pts = []
    n = 400
    for i in range(n):
        a = math.tau * i / n
        x = cx + math.sin(a) * rx
        y = cy - math.cos(a) * ry
        front = max(0.0, -math.cos(a))
        z = base_z + 0.01 - 0.075 * front ** 2.2   # hangs low in front, into the open shirt
        y -= 0.012 * front
        pts.append(Vector((x, y, z)))
    # place links every ~7 mm along the loop
    total = 0.0
    step = 0.0068
    placed = 0
    kd = KDTree(len(body.co))
    for i, c in enumerate(body.co):
        kd.insert(c, i)
    kd.balance()
    acc = 0.0
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        seg = (b - a).length
        acc += seg
        if acc >= step:
            acc = 0.0
            d = (b - a).normalized()
            # push outside the skin
            co, idx, dist = kd.find(a)
            nrm = Vector(body.nrm[idx])
            p = a
            if (a - Vector(co)).dot(nrm) < 0.004:
                p = Vector(co) + nrm * 0.004
            ob = link(bpy.data.objects.new(f"Rex_ChainLink{placed}", link_me), chain_root)
            up = nrm
            rot = Matrix.Identity(3)
            x_ax = d
            z_ax = up if placed % 2 == 0 else d.cross(up).normalized()
            y_ax = z_ax.cross(x_ax).normalized()
            z_ax = x_ax.cross(y_ax).normalized()
            rot = Matrix((x_ax, y_ax, z_ax)).transposed()
            ob.matrix_world = Matrix.Translation(p) @ rot.to_4x4()
            placed += 1
    # Pinky ring (right hand, little finger, first segment)
    a, b = J("joint-r-finger-5-2"), J("joint-r-finger-5-3")
    if a and b:
        d = (b - a).normalized()
        fv = body.co[np.linalg.norm(body.co - np.array(a.lerp(b, 0.45)), axis=1) < 0.012]
        rr = 0.0095
        bpy.ops.mesh.primitive_torus_add(major_radius=rr, minor_radius=0.0022, major_segments=32, minor_segments=8,
                                         location=a.lerp(b, 0.45))
        ringo = bpy.context.active_object
        ringo.name = "Rex_PinkyRing"
        ringo.rotation_mode = "QUATERNION"
        ringo.rotation_quaternion = d.to_track_quat("Z", "Y")
        ringo.data.materials.append(gmat)
        for c in ringo.users_collection:
            c.objects.unlink(ringo)
        link(ringo)
    # Cheap gold watch (left wrist)
    hand, elbow = J("joint-l-hand"), J("joint-l-elbow")
    if hand and elbow:
        d = (hand - elbow).normalized()
        c = hand - d * 0.035
        wv = body.co[np.abs((body.co - np.array(c)) @ np.array(d)) < 0.004]
        wv = wv[np.linalg.norm(wv - np.array(c), axis=1) < 0.06]
        rad = float(np.linalg.norm(wv - np.array(c), axis=1).max()) + 0.003 if len(wv) else 0.03
        cheap = gold("Rex_WatchGold", cheap=True)
        bpy.ops.mesh.primitive_torus_add(major_radius=rad, minor_radius=0.0035, major_segments=40, minor_segments=8,
                                         location=c)
        band = bpy.context.active_object
        band.name = "Rex_WatchBand"
        band.scale = (1, 1, 2.2)
        band.rotation_mode = "QUATERNION"
        band.rotation_quaternion = d.to_track_quat("Z", "Y")
        band.data.materials.append(cheap)
        for col in band.users_collection:
            col.objects.unlink(band)
        link(band)
        # watch face on the back of the wrist (outside of the arm)
        out = Vector((1, 0, 0)) if c.x > 0 else Vector((-1, 0, 0))
        out = (out - d * out.dot(d)).normalized()
        bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.014, depth=0.006, location=c + out * (rad + 0.002))
        face = bpy.context.active_object
        face.name = "Rex_WatchFace"
        face.rotation_mode = "QUATERNION"
        face.rotation_quaternion = out.to_track_quat("Z", "Y")
        face.data.materials.append(cheap)
        for col in face.users_collection:
            col.objects.unlink(face)
        link(face)
    return chain_root


# ------------------------------------------------------------------ main

def dress(ob, skin_mat, seed=304):
    rng = np.random.default_rng(seed)
    random.seed(seed)
    sub = ob.modifiers.new("Subsurf", "SUBSURF")
    sub.levels = 0
    sub.render_levels = 1
    tan_skin(skin_mat)
    body = Body(ob)
    eyes = make_eyes(body)
    make_head_hair(body, rng)
    make_stubble_and_brows(body, rng)
    make_sunglasses(body, eyes)
    clothes = make_clothes(body, rng)
    make_jewelry(body, clothes)
    return body
