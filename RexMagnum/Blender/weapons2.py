"""Rex Magnum - weapons v2: realistic, connected parts (every piece touches or sits inside its neighbor).

Built from side outlines (extruded), lathe shapes and tubes, with bevels, weighted normals and
real cut-outs (booleans) for slide serrations, ejection port and club grooves.
Front of every weapon = +Y, up = +Z. Markers: GRIP, TIP or MUZZLE, UPREF.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

import weapons as W1   # textures + bottle/towel/knife (kept, small fixes)

HERE = os.path.dirname(os.path.abspath(__file__))


# ------------------------------------------------------------------ geometry helpers

def finish(ob, bevel=0.0, segs=3, smooth_angle=40, subsurf=0):
    if bevel:
        b = ob.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = segs
        b.limit_method = "ANGLE"
        b.angle_limit = math.radians(smooth_angle)
        b.harden_normals = True
    if subsurf:
        s = ob.modifiers.new("Subsurf", "SUBSURF")
        s.levels = subsurf
        s.render_levels = subsurf
    wn = ob.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.keep_sharp = True
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def profile(name, pts, thickness, coll, mat, x=0.0):
    """Extrude a side outline (list of (y, z)) along X, centered at x."""
    bm = bmesh.new()
    a = [bm.verts.new((x - thickness / 2, y, z)) for y, z in pts]
    b = [bm.verts.new((x + thickness / 2, y, z)) for y, z in pts]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(pts)
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # triangulate the big side faces so bevel and normals behave
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return W1.obj(name, me, coll, mat)


def lathe_y(name, prof, coll, mat, segs=32, loc=(0, 0, 0), rot=None):
    me = W1.lathe(name, prof, segs=segs, cap_bottom=True, cap_top=True)
    ob = W1.obj(name, me, coll, mat)
    ob.location = loc
    if rot is not None:
        ob.rotation_euler = rot
    return ob


def tube(name, pts, r, coll, mat, segs=12, r_end=None):
    """Smooth tube along a polyline (Catmull-Rom resampled)."""
    pts = [Vector(p) for p in pts]
    sm = []
    for i in range(len(pts) - 1):
        p0, p1 = pts[max(i - 1, 0)], pts[i]
        p2, p3 = pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        for k in range(6):
            t = k / 6
            t2, t3 = t * t, t * t * t
            sm.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    sm.append(pts[-1])
    bm = bmesh.new()
    rings = []
    prev = None
    for i, p in enumerate(sm):
        t = (sm[min(i + 1, len(sm) - 1)] - sm[max(i - 1, 0)]).normalized()
        nrm = (prev if prev is not None else t.orthogonal()).normalized()
        nrm = (nrm - t * nrm.dot(t)).normalized()
        prev = nrm
        bv = t.cross(nrm)
        rr = r if r_end is None else r + (r_end - r) * i / (len(sm) - 1)
        rings.append([bm.verts.new(p + (nrm * math.cos(a) + bv * math.sin(a)) * rr)
                      for a in [math.tau * k / segs for k in range(segs)]])
    for j in range(len(rings) - 1):
        for k in range(segs):
            bm.faces.new((rings[j][k], rings[j][(k + 1) % segs], rings[j + 1][(k + 1) % segs], rings[j + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = W1.obj(name, me, coll, mat)
    return ob


def cutter(name, size, loc, coll, rot=(0, 0, 0)):
    ob = W1.obj(name, W1.box_mesh(name, size), coll, None)
    ob.location = loc
    ob.rotation_euler = rot
    ob.hide_render = True
    ob.display_type = "WIRE"
    return ob


def cut(ob, cutters):
    for i, c in enumerate(cutters):
        m = ob.modifiers.new(f"Cut{i}", "BOOLEAN")
        m.operation = "DIFFERENCE"
        m.object = c
        m.solver = "EXACT"
    return ob


def apply_all(coll):
    """Apply modifiers (booleans, bevels) so the export is clean; remove cutter objects."""
    dg = bpy.context.evaluated_depsgraph_get()
    for ob in list(coll.objects):
        if ob.type == "MESH" and ob.modifiers and not ob.hide_render:
            me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
            old = ob.data
            ob.modifiers.clear()
            ob.data = me
            me.name = ob.name
            if old.users == 0:
                bpy.data.meshes.remove(old)
    for ob in list(coll.objects):
        if ob.type == "MESH" and ob.hide_render:
            bpy.data.objects.remove(ob)


# ------------------------------------------------------------------ materials (realistic)

def mat_chrome():
    m = bpy.data.materials.get("W2_Chrome") or bpy.data.materials.new("W2_Chrome")
    n = _nodes(m)
    tc = n.new("ShaderNodeTexCoord")
    sc = _noise(n, tc.outputs["Object"], 300, (1, 1, 1), stretch=(1, 40, 1))     # fine polish scratches
    fp = _noise(n, tc.outputs["Object"], 30, (1, 1, 1))                         # fingerprints / smudges
    p = n.new("ShaderNodeBsdfPrincipled")
    p.inputs["Base Color"].default_value = (0.93, 0.93, 0.95, 1)
    p.inputs["Metallic"].default_value = 1.0
    r = n.new("ShaderNodeMapRange")
    r.inputs["To Min"].default_value = 0.03
    r.inputs["To Max"].default_value = 0.16
    mx = n.new("ShaderNodeMath", operation="MAXIMUM")
    m1 = n.new("ShaderNodeMath", operation="MULTIPLY")
    m1.inputs[1].default_value = 0.7
    n.link(fp, m1.inputs[0])
    n.link(sc, mx.inputs[0])
    n.link(m1.outputs[0], mx.inputs[1])
    gt = n.new("ShaderNodeMapRange")
    gt.inputs["From Min"].default_value = 0.55
    gt.inputs["From Max"].default_value = 0.8
    n.link(mx.outputs[0], gt.inputs["Value"])
    n.link(gt.outputs[0], r.inputs["Value"])
    n.link(r.outputs[0], p.inputs["Roughness"])
    n.out(p)
    return m


def mat_steel(name="W2_DarkSteel", color=(0.045, 0.045, 0.05), rough=(0.3, 0.55)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    n = _nodes(m)
    tc = n.new("ShaderNodeTexCoord")
    f = _noise(n, tc.outputs["Object"], 120, (1, 1, 1))
    wear = _noise(n, tc.outputs["Object"], 18, (1, 1, 1))
    p = n.new("ShaderNodeBsdfPrincipled")
    col = n.new("ShaderNodeMix", data_type="RGBA")
    edge = n.new("ShaderNodeMapRange")
    edge.inputs["From Min"].default_value = 0.68
    edge.inputs["From Max"].default_value = 0.78
    n.link(wear, edge.inputs["Value"])
    n.link(edge.outputs[0], col.inputs[0])
    col.inputs[6].default_value = (*color, 1)
    col.inputs[7].default_value = (0.35, 0.35, 0.37, 1)                       # worn bare steel spots
    n.link(col.outputs[2], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 1.0
    rr = n.new("ShaderNodeMapRange")
    rr.inputs["To Min"].default_value = rough[0]
    rr.inputs["To Max"].default_value = rough[1]
    n.link(f, rr.inputs["Value"])
    n.link(rr.outputs[0], p.inputs["Roughness"])
    n.out(p)
    return m


def mat_checkered_wood():
    m = bpy.data.materials.get("W2_GripWood") or bpy.data.materials.new("W2_GripWood")
    n = _nodes(m)
    tc = n.new("ShaderNodeTexCoord")
    wv = n.new("ShaderNodeTexWave", bands_direction="Z")
    wv.inputs["Scale"].default_value = 350
    wv.inputs["Distortion"].default_value = 3
    n.link(tc.outputs["Object"], wv.inputs["Vector"])
    ramp = n.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.13, 0.05, 0.02, 1)
    ramp.color_ramp.elements[1].color = (0.42, 0.19, 0.07, 1)
    n.link(wv.outputs["Fac"], ramp.inputs[0])
    # diamond checkering (two rotated fine wave bands)
    mp1 = n.new("ShaderNodeMapping")
    mp1.inputs["Rotation"].default_value = (math.radians(45), 0, 0)
    mp2 = n.new("ShaderNodeMapping")
    mp2.inputs["Rotation"].default_value = (math.radians(-45), 0, 0)
    n.link(tc.outputs["Object"], mp1.inputs[0])
    n.link(tc.outputs["Object"], mp2.inputs[0])
    c1 = n.new("ShaderNodeTexWave", bands_direction="Y")
    c1.inputs["Scale"].default_value = 900
    c2 = n.new("ShaderNodeTexWave", bands_direction="Y")
    c2.inputs["Scale"].default_value = 900
    n.link(mp1.outputs[0], c1.inputs["Vector"])
    n.link(mp2.outputs[0], c2.inputs["Vector"])
    chk = n.new("ShaderNodeMath", operation="MULTIPLY")
    n.link(c1.outputs["Fac"], chk.inputs[0])
    n.link(c2.outputs["Fac"], chk.inputs[1])
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.6
    bump.inputs["Distance"].default_value = 0.0004
    n.link(chk.outputs[0], bump.inputs["Height"])
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(ramp.outputs[0], p.inputs["Base Color"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    p.inputs["Roughness"].default_value = 0.45
    p.inputs["Coat Weight"].default_value = 0.4
    n.out(p)
    return m


def mat_rubber(name="W2_Rubber", color=(0.018, 0.018, 0.02), wrap=False):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    n = _nodes(m)
    tc = n.new("ShaderNodeTexCoord")
    p = n.new("ShaderNodeBsdfPrincipled")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = 0.75
    if wrap:   # golf grip wrap pattern
        wv = n.new("ShaderNodeTexWave", bands_direction="Y")
        wv.inputs["Scale"].default_value = 120
        wv.inputs["Distortion"].default_value = 0.0
        mp = n.new("ShaderNodeMapping")
        mp.inputs["Rotation"].default_value = (0, 0, math.radians(30))
        n.link(tc.outputs["Object"], mp.inputs[0])
        n.link(mp.outputs[0], wv.inputs["Vector"])
        h = wv.outputs["Fac"]
    else:
        h = _noise(n, tc.outputs["Object"], 400, (1, 1, 1))
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.5
    bump.inputs["Distance"].default_value = 0.0005
    n.link(h, bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.out(p)
    return m


def mat_paint(name, color, rough=0.35, metal=0.0, chips=True):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    n = _nodes(m)
    tc = n.new("ShaderNodeTexCoord")
    p = n.new("ShaderNodeBsdfPrincipled")
    if chips:
        ch = _noise(n, tc.outputs["Object"], 40, (1, 1, 1))
        mr = n.new("ShaderNodeMapRange")
        mr.inputs["From Min"].default_value = 0.72
        mr.inputs["From Max"].default_value = 0.74
        n.link(ch, mr.inputs["Value"])
        col = n.new("ShaderNodeMix", data_type="RGBA")
        n.link(mr.outputs[0], col.inputs[0])
        col.inputs[6].default_value = (*color, 1)
        col.inputs[7].default_value = (0.6, 0.6, 0.62, 1)
        n.link(col.outputs[2], p.inputs["Base Color"])
        mm = n.new("ShaderNodeMath", operation="MULTIPLY")
        n.link(mr.outputs[0], mm.inputs[0])
        mm.inputs[1].default_value = 1.0
        n.link(mm.outputs[0], p.inputs["Metallic"])
    else:
        p.inputs["Base Color"].default_value = (*color, 1)
        p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    p.inputs["Coat Weight"].default_value = 0.5
    n.out(p)
    return m


class _N:
    def __init__(self, m):
        m.use_nodes = True
        self.nt = m.node_tree
        self.nt.nodes.clear()
        self.o = self.nt.nodes.new("ShaderNodeOutputMaterial")

    def new(self, t, **kw):
        node = self.nt.nodes.new(t)
        for k, v in kw.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    def out(self, shader):
        self.nt.links.new(shader.outputs[0], self.o.inputs["Surface"])


def _nodes(m):
    return _N(m)


def _noise(n, vec, scale, _unused, stretch=(1, 1, 1)):
    mp = n.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = stretch
    n.link(vec, mp.inputs[0])
    t = n.new("ShaderNodeTexNoise")
    t.inputs["Scale"].default_value = scale
    t.inputs["Detail"].default_value = 8
    n.link(mp.outputs[0], t.inputs["Vector"])
    return t.outputs["Fac"]


# ------------------------------------------------------------------ pistol

def build_pistol():
    """80s chrome pistol: slide on frame, grip frame with wood checkered panels, trigger guard joined to
    the frame, hammer, sights, serrations and ejection port cut into the slide."""
    coll = W1.collection("Weapon_Pistol")
    chrome, steel, wood = mat_chrome(), mat_steel(), mat_checkered_wood()
    # frame (one piece: dust cover + grip frame + beavertail), 22 mm thick
    frame_pts = [(0.092, 0.013), (0.092, -0.002), (0.034, -0.002), (0.004, -0.004), (-0.010, -0.014),
                 (-0.024, -0.122), (-0.074, -0.128), (-0.080, -0.118), (-0.083, -0.03), (-0.092, -0.006),
                 (-0.106, 0.004), (-0.104, 0.013)]
    frame = finish(profile("Pistol_Frame", frame_pts, 0.022, coll, chrome), bevel=0.0015)
    # slide sits exactly on top of the frame (z 0.013), 24 mm wide
    slide_pts = [(0.104, 0.013), (0.104, 0.036), (0.099, 0.041), (-0.088, 0.041), (-0.098, 0.034),
                 (-0.1, 0.013)]
    slide = finish(profile("Pistol_Slide", slide_pts, 0.024, coll, chrome), bevel=0.0015)
    cs = [cutter(f"SerrCut{k}", (0.03, 0.0012, 0.022), (0, -0.088 + k * 0.0035, 0.027), coll) for k in range(9)]
    cs.append(cutter("EjectPort", (0.016, 0.032, 0.012), (0.011, 0.03, 0.037), coll))
    cut(slide, cs)
    # barrel inside the slide (seen through the ejection port), bushing + muzzle in the slide nose
    lathe_y("Pistol_Barrel", [(0.0, -0.03), (0.0068, -0.03), (0.0068, 0.1), (0.0, 0.1)], coll, steel,
            loc=(0, 0, 0.026))
    lathe_y("Pistol_Bushing", [(0.0, 0.098), (0.0085, 0.098), (0.0085, 0.106), (0.0, 0.106)], coll, steel,
            loc=(0, 0, 0.026))
    lathe_y("Pistol_Bore", [(0.0, 0.1), (0.0045, 0.1), (0.0045, 0.1065), (0.0, 0.1065)], coll,
            W1.principled("W2_Bore", (0.005, 0.005, 0.005), 0.6), loc=(0, 0, 0.026))
    # grip panels: inside the grip outline, 3 mm proud of each side of the frame (touching it)
    gp = [(-0.017, -0.016), (-0.029, -0.116), (-0.07, -0.12), (-0.077, -0.028)]
    for sx in (-1, 1):
        g = finish(profile(f"Pistol_Grip{sx}", gp, 0.004, coll, wood, x=sx * (0.011 + 0.0019)), bevel=0.0012)
        lathe_y(f"Pistol_GripScrew{sx}", [(0, 0), (0.0028, 0), (0.0028, 0.0012), (0, 0.0016)], coll, steel,
                loc=(sx * 0.0148, -0.045, -0.04), rot=(0, 0, math.radians(-90 * sx)))
    # magazine base plate under the grip
    finish(profile("Pistol_MagBase", [(-0.022, -0.122), (-0.074, -0.128), (-0.075, -0.133), (-0.021, -0.127)],
                   0.019, coll, steel), bevel=0.001)
    # trigger guard: tube from the frame front, down and back into the grip front strap (ends inside metal)
    tube("Pistol_Guard", [(0, 0.03, -0.001), (0, 0.033, -0.022), (0, 0.022, -0.036), (0, 0.0, -0.036),
                          (0, -0.016, -0.03)], 0.0028, coll, chrome, segs=12)
    # trigger (curved blade from the frame)
    tube("Pistol_Trigger", [(0, 0.012, -0.002), (0, 0.014, -0.012), (0, 0.011, -0.021)], 0.0028, coll, steel,
         segs=8, r_end=0.0022)
    # hammer at the back (touches the slide rear and frame)
    finish(profile("Pistol_Hammer", [(-0.098, 0.016), (-0.106, 0.02), (-0.112, 0.036), (-0.106, 0.04),
                                     (-0.099, 0.03)], 0.007, coll, steel), bevel=0.001)
    # sights on the slide top
    finish(profile("Pistol_FrontSight", [(0.094, 0.041), (0.098, 0.041), (0.097, 0.047), (0.094, 0.047)],
                   0.0035, coll, steel), bevel=0.0005)
    rs = profile("Pistol_RearSight", [(-0.086, 0.041), (-0.077, 0.041), (-0.077, 0.047), (-0.086, 0.047)],
                 0.014, coll, steel)
    cut(finish(rs, bevel=0.0005), [cutter("RSNotch", (0.0035, 0.02, 0.006), (0, -0.081, 0.047), coll)])
    # slide stop + safety (small parts on the left side, touching)
    finish(profile("Pistol_SlideStop", [(0.01, 0.006), (0.03, 0.006), (0.03, 0.012), (0.01, 0.012)], 0.003,
                   coll, steel, x=-0.0125), bevel=0.0006)
    finish(profile("Pistol_Safety", [(-0.075, 0.012), (-0.06, 0.012), (-0.06, 0.018), (-0.075, 0.018)], 0.003,
                   coll, steel, x=-0.0125), bevel=0.0006)
    W1.empty("GRIP", (0, -0.045, -0.055), coll)
    W1.empty("MUZZLE", (0, 0.107, 0.026), coll)
    apply_all(coll)
    return coll


# ------------------------------------------------------------------ harpoon gun

def build_harpoon_gun():
    coll = W1.collection("Weapon_HarpoonGun")
    yellow = mat_paint("W2_HarpoonYellow", (1.0, 0.68, 0.02), rough=0.3)
    black = mat_rubber("W2_RubberBlack")
    steel = mat_steel("W2_Stainless", (0.7, 0.7, 0.72), (0.15, 0.3))
    band = mat_rubber("W2_BandRubber", (0.03, 0.03, 0.035))
    rope = W1.principled("W2_Line", (0.9, 0.85, 0.7), 0.9)
    R = 0.016
    lathe_y("Harpoon_Barrel", [(0.0, -0.04), (R, -0.04), (R, 0.74), (0.0, 0.74)], coll, yellow, segs=32)
    # spear rail on top of the barrel (touching), spear lies in it
    finish(profile("Harpoon_Rail", [(-0.03, R - 0.002), (0.74, R - 0.002), (0.74, R + 0.003), (-0.03, R + 0.003)],
                   0.008, coll, yellow), bevel=0.001)
    # muzzle head: block with band holes; barrel end goes inside it
    finish(profile("Harpoon_Muzzle", [(0.72, -0.022), (0.8, -0.018), (0.8, 0.03), (0.72, 0.034)], 0.07, coll, black),
           bevel=0.006)
    # handle: grip outline whose top overlaps the barrel underside
    hp = [(0.06, -0.006), (0.12, -0.006), (0.115, -0.03), (0.07, -0.045), (0.06, -0.16), (0.02, -0.165),
          (0.012, -0.05), (-0.03, -0.01), (-0.03, -0.004)]
    finish(profile("Harpoon_Handle", hp, 0.034, coll, black), bevel=0.006)
    tube("Harpoon_TriggerGuard", [(0, 0.115, -0.028), (0, 0.118, -0.06), (0, 0.095, -0.075), (0, 0.068, -0.06)],
         0.003, coll, black)
    tube("Harpoon_Trigger", [(0, 0.09, -0.04), (0, 0.093, -0.05), (0, 0.089, -0.062)], 0.003, coll, steel, r_end=0.002)
    # reel: bracket bolted to the handle front, spool on an axle, line wrapped on the spool
    finish(profile("Harpoon_ReelArm", [(0.12, -0.03), (0.2, -0.03), (0.2, -0.04), (0.12, -0.045)], 0.012, coll,
                   steel), bevel=0.001)
    ax = (0.2, -0.035)
    for sx in (-1, 1):
        lathe_y(f"Harpoon_ReelPlate{sx}", [(0, -0.002), (0.038, -0.002), (0.038, 0.002), (0, 0.002)], coll, black,
                loc=(sx * 0.012, ax[0], ax[1] - 0.04), rot=(0, 0, math.radians(90)))
    lathe_y("Harpoon_ReelAxle", [(0, -0.016), (0.006, -0.016), (0.006, 0.016), (0, 0.016)], coll, steel,
            loc=(0, ax[0], ax[1] - 0.04), rot=(0, 0, math.radians(90)))
    lathe_y("Harpoon_ReelLine", [(0, -0.01), (0.026, -0.01), (0.028, 0), (0.026, 0.01), (0, 0.01)], coll, rope,
            loc=(0, ax[0], ax[1] - 0.04), rot=(0, 0, math.radians(90)))
    finish(profile("Harpoon_ReelPost", [(0.19, -0.04), (0.21, -0.04), (0.21, -0.075), (0.19, -0.075)], 0.006, coll,
                   steel, x=0.0), bevel=0.0005)
    # spear in the rail, flopper barb at the tip, line from the spear back to the reel
    sp_z = R + 0.0065
    lathe_y("Harpoon_Spear", [(0, -0.02), (0.0034, -0.02), (0.0034, 1.0), (0, 1.0)], coll, steel, segs=12,
            loc=(0, 0, sp_z))
    lathe_y("Harpoon_Tip", [(0, 1.0), (0.0055, 1.0), (0.0055, 1.012), (0, 1.07)], coll, steel, segs=12, loc=(0, 0, sp_z))
    finish(profile("Harpoon_Barb", [(1.0, 0.0), (1.045, 0.003), (1.045, 0.006), (1.0, 0.003)], 0.002, coll, steel,
                   x=0.0), bevel=0.0003).location = (0, 0, sp_z)
    # two rubber bands: from the muzzle sides back to a wishbone hooked on the spear notch
    notch_y = 0.36
    for sx in (-1, 1):
        tube(f"Harpoon_Band{sx}", [(sx * 0.03, 0.76, 0.01), (sx * 0.027, 0.6, sp_z + 0.004),
                                   (sx * 0.016, 0.45, sp_z + 0.006), (sx * 0.008, notch_y, sp_z + 0.006)],
             0.006, coll, band, segs=10)
    tube("Harpoon_Wishbone", [(-0.009, notch_y, sp_z + 0.006), (0, notch_y - 0.006, sp_z + 0.004),
                              (0.009, notch_y, sp_z + 0.006)], 0.0014, coll, steel, segs=6)
    tube("Harpoon_Shockline", [(0, -0.02, sp_z), (0, -0.03, -0.02), (0, 0.1, -0.06), (0, ax[0], ax[1] - 0.04 + 0.026)],
         0.0012, coll, rope, segs=6)
    lathe_y("Harpoon_ButtCap", [(0, -0.05), (R + 0.002, -0.05), (R + 0.002, -0.035), (0, -0.035)], coll, black)
    W1.empty("GRIP", (0, 0.04, -0.09), coll)
    W1.empty("MUZZLE", (0, 1.07, sp_z), coll)
    apply_all(coll)
    return coll


# ------------------------------------------------------------------ golf club

def build_golf_club():
    coll = W1.collection("Weapon_GolfClub")
    steel = mat_steel("W2_ClubSteel", (0.75, 0.75, 0.77), (0.12, 0.3))
    chrome = mat_chrome()
    grip = mat_rubber("W2_GolfGrip", (0.03, 0.03, 0.035), wrap=True)
    ferr = W1.principled("W2_Ferrule", (0.01, 0.01, 0.01), 0.3)
    L = 0.92
    # stepped tapered shaft
    prof = [(0.0, 0.0), (0.0078, 0.0)]
    for k in range(9):
        y = 0.28 + k * 0.05
        r = 0.0076 - k * 0.00025
        prof += [(r, y), (r - 0.00015, y + 0.003)]
    prof += [(0.0052, L), (0.0, L)]
    lathe_y("Club_Shaft", prof, coll, chrome, segs=20)
    lathe_y("Club_Grip", [(0.0, -0.27), (0.0125, -0.27), (0.013, -0.262), (0.0118, -0.2), (0.0095, 0.0),
                          (0.0085, 0.01), (0.0, 0.01)], coll, grip, segs=28)
    lathe_y("Club_Ferrule", [(0, L - 0.035), (0.0065, L - 0.035), (0.0068, L - 0.015), (0, L - 0.015)], coll, ferr,
            segs=20)
    # iron head: blade outline in (toe, height), extruded to 2 cm, then turned so the toe points +X,
    # the height runs up the shaft (+Y) and the face is lofted 30 deg. The hosel (heel) holds the shaft end.
    head_pts = [(0.0, 0.0), (0.072, -0.002), (0.08, 0.006), (0.082, 0.03), (0.074, 0.046), (0.006, 0.036),
                (-0.004, 0.02)]
    head = profile("Club_Head", head_pts, 0.02, coll, steel)
    head.data.transform(Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1))))   # (x,y,z)->(y,z,x)
    cs = [cutter(f"Groove{k}", (0.06, 0.0011, 0.004), (0.04, 0.006 + k * 0.0042, -0.0105), coll) for k in range(8)]
    cut(finish(head, bevel=0.002), cs)
    head.location = (0.004, L - 0.03, 0)
    head.rotation_euler = (math.radians(-30), 0, 0)
    for c_ in cs:
        c_.parent = head
    lathe_y("Club_Hosel", [(0.0, L - 0.045), (0.0085, L - 0.045), (0.0078, L - 0.005), (0.0095, L + 0.012),
                           (0.0, L + 0.012)], coll, steel, segs=20)
    W1.empty("GRIP", (0, -0.13, 0), coll)
    W1.empty("TIP", (0.04, L + 0.04, 0), coll)
    apply_all(coll)
    return coll


# ------------------------------------------------------------------ sniper rifle

def build_sniper():
    coll = W1.collection("Weapon_Sniper")
    steel = mat_steel()
    stock_m = mat_paint("W2_SniperStock", (0.16, 0.17, 0.12), rough=0.65, chips=False)
    rubber = mat_rubber()
    lens = W1.principled("W2_ScopeLens", (0.02, 0.05, 0.1), 0.01, **{"Coat Weight": 1.0})
    # stock: one outline (butt + thumbhole grip + fore-end)
    sp = [(-0.43, -0.02), (-0.43, 0.035), (-0.2, 0.02), (-0.08, 0.016), (0.42, 0.016), (0.44, 0.0),
          (0.42, -0.03), (0.06, -0.03), (0.0, -0.05), (-0.02, -0.12), (-0.06, -0.125), (-0.075, -0.05),
          (-0.16, -0.05), (-0.43, -0.11)]
    stock = finish(profile("Sniper_Stock", sp, 0.046, coll, stock_m), bevel=0.006)
    cut(stock, [cutter("ThumbHole", (0.06, 0.05, 0.035), (0, -0.11, -0.035), coll),
                cutter("BarrelChannel", (0.024, 0.6, 0.024), (0, 0.2, 0.02), coll)])
    finish(profile("Sniper_RecoilPad", [(-0.45, -0.115), (-0.43, -0.11), (-0.43, 0.035), (-0.45, 0.04)], 0.05,
                   coll, rubber), bevel=0.004)
    # receiver sits in the stock (overlaps), barrel goes into the receiver
    lathe_y("Sniper_Receiver", [(0, -0.12), (0.018, -0.12), (0.018, 0.12), (0, 0.12)], coll, steel, segs=28,
            loc=(0, 0, 0.026))
    lathe_y("Sniper_Barrel", [(0, 0.11), (0.0125, 0.11), (0.0125, 0.2), (0.0095, 0.95), (0, 0.95)], coll, steel,
            segs=24, loc=(0, 0, 0.026))
    lathe_y("Sniper_Brake", [(0, 0.94), (0.0145, 0.94), (0.0145, 1.01), (0, 1.01)], coll, steel, segs=24,
            loc=(0, 0, 0.026))
    for k in range(4):
        cut(coll.objects["Sniper_Brake"], [cutter(f"BrakePort{k}", (0.04, 0.006, 0.008), (0, 0.955 + k * 0.014, 0.026), coll)])
    # bolt handle and knob from the receiver right side
    tube("Sniper_Bolt", [(0.015, 0.02, 0.03), (0.04, 0.02, 0.025), (0.062, 0.015, 0.012)], 0.004, coll, steel, segs=10)
    lathe_y("Sniper_BoltKnob", [(0, -0.01), (0.009, -0.007), (0.009, 0.007), (0, 0.01)], coll, steel, segs=16,
            loc=(0.066, 0.015, 0.01))
    # trigger guard + trigger under the receiver (touching the stock)
    tube("Sniper_TrigGuard", [(0, 0.03, -0.03), (0, 0.035, -0.055), (0, 0.0, -0.06), (0, -0.01, -0.05)], 0.003,
         coll, steel)
    tube("Sniper_Trigger", [(0, 0.012, -0.03), (0, 0.015, -0.04), (0, 0.012, -0.05)], 0.0025, coll, steel)
    # detachable magazine in the stock bottom
    finish(profile("Sniper_Mag", [(0.06, -0.03), (0.12, -0.03), (0.115, -0.07), (0.065, -0.07)], 0.03, coll, steel),
           bevel=0.002)
    # Picatinny rail on the receiver top, scope rings on it, scope tube inside the rings
    rail_z = 0.026 + 0.018
    finish(profile("Sniper_Rail", [(-0.1, rail_z - 0.002), (0.12, rail_z - 0.002), (0.12, rail_z + 0.008),
                                   (-0.1, rail_z + 0.008)], 0.021, coll, steel), bevel=0.0008)
    cut(coll.objects["Sniper_Rail"], [cutter(f"RailSlot{k}", (0.03, 0.005, 0.004), (0, -0.09 + k * 0.01, rail_z + 0.008), coll)
                                     for k in range(21)])
    sz = rail_z + 0.032
    lathe_y("Sniper_Scope", [(0, -0.09), (0.021, -0.09), (0.021, -0.06), (0.015, -0.035), (0.015, 0.17),
                             (0.025, 0.2), (0.025, 0.25), (0, 0.25)], coll, steel, segs=36, loc=(0, 0, sz))
    for y in (-0.089, 0.249):
        lathe_y(f"Sniper_Lens{y}", [(0, 0), (0.0195 if y < 0 else 0.023, 0), (0, 0.0015)], coll, lens, segs=24,
                loc=(0, y, sz))
    for y in (0.0, 0.12):   # rings: base on the rail, ring around the tube
        finish(profile(f"Sniper_RingBase{y}", [(y - 0.008, rail_z + 0.008), (y + 0.008, rail_z + 0.008),
                                               (y + 0.008, sz - 0.012), (y - 0.008, sz - 0.012)], 0.022, coll, steel),
               bevel=0.001)
        lathe_y(f"Sniper_Ring{y}", [(0.015, y - 0.008), (0.0195, y - 0.008), (0.0195, y + 0.008), (0.015, y + 0.008)],
                coll, steel, segs=32, loc=(0, 0, sz))
    for k, (dx, dz) in enumerate(((0, 0.021), (0.021, 0))):   # turrets touching the tube
        t = lathe_y(f"Sniper_Turret{k}", [(0, 0), (0.011, 0), (0.011, 0.016), (0, 0.016)], coll, steel, segs=20,
                    loc=(dx, 0.06, sz + dz))
        t.rotation_euler = (math.radians(-90), 0, 0) if k == 0 else (0, 0, math.radians(-90))
    # bipod: clamp around the fore-end front, legs hinged on it, folded forward
    finish(profile("Sniper_BipodClamp", [(0.36, -0.035), (0.4, -0.035), (0.4, -0.02), (0.36, -0.02)], 0.05, coll,
                   steel), bevel=0.002)
    for sx in (-1, 1):
        tube(f"Sniper_BipodLeg{sx}", [(sx * 0.02, 0.38, -0.035), (sx * 0.03, 0.5, -0.05), (sx * 0.035, 0.6, -0.055)],
             0.0055, coll, steel, segs=10)
        lathe_y(f"Sniper_BipodFoot{sx}", [(0, 0), (0.008, 0), (0.008, 0.02), (0, 0.02)], coll, rubber, segs=12,
                loc=(sx * 0.035, 0.6, -0.055))
    W1.empty("GRIP", (0, -0.04, -0.07), coll)
    W1.empty("MUZZLE", (0, 1.01, 0.026), coll)
    W1.empty("SCOPE", (0, -0.15, sz), coll)
    apply_all(coll)
    return coll


# ------------------------------------------------------------------ coconut

def build_coconut():
    coll = W1.collection("Weapon_Coconut")
    husk = bpy.data.materials.get("W2_Husk") or bpy.data.materials.new("W2_Husk")
    n = _nodes(husk)
    tc = n.new("ShaderNodeTexCoord")
    fib = _noise(n, tc.outputs["Object"], 25, None, stretch=(30, 30, 1.5))     # fibers run pole to pole
    blot = _noise(n, tc.outputs["Object"], 18, None)
    ramp = n.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0.3, (0.06, 0.03, 0.012, 1)
    e[1].position, e[1].color = 0.7, (0.32, 0.18, 0.08, 1)
    mix = n.new("ShaderNodeMath", operation="MULTIPLY")
    n.link(fib, mix.inputs[0])
    n.link(blot, mix.inputs[1])
    n.link(fib, ramp.inputs[0])
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(ramp.outputs[0], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.95
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 1.0
    bump.inputs["Distance"].default_value = 0.003
    n.link(mix.outputs[0], bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.out(p)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.06)
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        v.co.z *= 1.15
        v.co.x *= 1 + 0.05 * math.cos(3 * a)          # 3 soft ridges
        v.co.y *= 1 + 0.05 * math.cos(3 * a)
        v.co += v.co.normalized() * noise.noise(v.co * 50) * 0.002
    me = bpy.data.meshes.new("Coconut")
    bm.to_mesh(me)
    bm.free()
    W1.obj("Coconut_Shell", me, coll, husk)
    dark = W1.principled("W2_CocoEye", (0.025, 0.012, 0.008), 0.85)
    top = 0.069
    for k in range(3):   # eyes sunk into the shell
        a = math.tau * k / 3
        e = lathe_y(f"Coconut_Eye{k}", [(0, 0), (0.0065, 0), (0.0055, 0.003), (0, 0.004)], coll, dark, segs=16,
                    loc=(math.cos(a) * 0.012, math.sin(a) * 0.012, top - 0.004))
        e.rotation_euler = (math.radians(-90), 0, 0)
    fuse = mat_rubber("W2_FuseCord", (0.55, 0.48, 0.35))
    fx, fy = 0.012, 0.0
    tube("Coconut_Fuse", [(fx, fy, top - 0.006), (fx + 0.002, 0, top + 0.012), (fx + 0.01, 0.003, top + 0.03),
                          (fx + 0.025, 0.008, top + 0.04)], 0.0024, coll, fuse, segs=8)
    spark = W1.principled("W2_Spark", (1.0, 0.6, 0.1), 0.3, **{"Emission Color": (1.0, 0.55, 0.1, 1),
                                                              "Emission Strength": 40.0})
    bm = bmesh.new()
    rng = random.Random(2)
    for k in range(10):   # spark burst at the fuse tip
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0, 1))).normalized()
        base = Vector((fx + 0.025, 0.008, top + 0.04))
        v0 = bm.verts.new(base)
        v1 = bm.verts.new(base + d * rng.uniform(0.006, 0.014))
        v2 = bm.verts.new(base + d * 0.004 + d.orthogonal().normalized() * 0.0008)
        bm.faces.new((v0, v1, v2))
    me = bpy.data.meshes.new("Spark")
    bm.to_mesh(me)
    bm.free()
    W1.obj("Coconut_Spark", me, coll, spark, smooth=False)
    W1.empty("GRIP", (0, 0, 0), coll)
    apply_all(coll)
    return coll


# ------------------------------------------------------------------ swordfish

def build_swordfish():
    """Frozen swordfish: lofted body from cross-sections, bill grows out of the head,
    fins rooted inside the body, crescent tail on a keeled tail stalk. Held by the tail."""
    coll = W1.collection("Weapon_Swordfish")
    m = bpy.data.materials.get("W2_FrozenFish") or bpy.data.materials.new("W2_FrozenFish")
    n = _nodes(m)
    tc = n.new("ShaderNodeTexCoord")
    sep = n.new("ShaderNodeSeparateXYZ")
    n.link(tc.outputs["Object"], sep.inputs[0])
    zr = n.new("ShaderNodeMapRange")
    zr.inputs["From Min"].default_value = -0.04
    zr.inputs["From Max"].default_value = 0.06
    n.link(sep.outputs[2], zr.inputs["Value"])
    ramp = n.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0.35, (0.72, 0.74, 0.78, 1)       # silver belly
    e[1].position, e[1].color = 0.7, (0.07, 0.07, 0.16, 1)        # dark purple-blue back
    n.link(zr.outputs[0], ramp.inputs[0])
    vor = n.new("ShaderNodeTexVoronoi")
    vor.inputs["Scale"].default_value = 140
    n.link(tc.outputs["Object"], vor.inputs["Vector"])
    frost_mask = n.new("ShaderNodeMapRange")
    frost_mask.inputs["From Min"].default_value = 0.15
    frost_mask.inputs["From Max"].default_value = 0.05
    n.link(vor.outputs["Distance"], frost_mask.inputs["Value"])
    big = _noise(n, tc.outputs["Object"], 6, None)
    fm = n.new("ShaderNodeMath", operation="MULTIPLY")
    n.link(frost_mask.outputs[0], fm.inputs[0])
    n.link(big, fm.inputs[1])
    col = n.new("ShaderNodeMix", data_type="RGBA")
    n.link(fm.outputs[0], col.inputs[0])
    n.link(ramp.outputs[0], col.inputs[6])
    col.inputs[7].default_value = (0.93, 0.97, 1.0, 1)
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(col.outputs[2], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 0.35
    p.inputs["Roughness"].default_value = 0.35
    p.inputs["Coat Weight"].default_value = 1.0          # ice glaze
    p.inputs["Coat Roughness"].default_value = 0.08
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.4
    n.link(vor.outputs["Distance"], bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.out(p)
    # loft: (y, half-width, half-height, z-offset) from tail stalk to head; bill continues from the head
    secs = [(0.05, 0.012, 0.018, 0.0), (0.12, 0.016, 0.03, 0.0), (0.3, 0.035, 0.07, 0.004), (0.55, 0.06, 0.11, 0.01),
            (0.85, 0.075, 0.13, 0.012), (1.1, 0.07, 0.12, 0.01), (1.28, 0.05, 0.085, 0.006), (1.4, 0.03, 0.05, 0.004),
            (1.47, 0.016, 0.024, 0.003), (1.5, 0.012, 0.013, 0.003)]
    bill = [(1.6, 0.01, 0.006, 0.003), (1.8, 0.008, 0.004, 0.003), (2.0, 0.005, 0.0025, 0.002), (2.08, 0.001, 0.001, 0.002)]
    bm = bmesh.new()
    rings = []
    segs = 24
    for y, hw, hh, zo in secs + bill:
        ring = []
        for k in range(segs):
            a = math.tau * k / segs
            sx, sz = math.cos(a), math.sin(a)
            flat = 0.85 if y > 1.55 else 1.0                       # bill is flat
            ring.append(bm.verts.new((sx * hw, y, zo + sz * hh * flat)))
        rings.append(ring)
    for j in range(len(rings) - 1):
        for k in range(segs):
            bm.faces.new((rings[j][k], rings[j][(k + 1) % segs], rings[j + 1][(k + 1) % segs], rings[j + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    me = bpy.data.meshes.new("FishBody")
    bm.to_mesh(me)
    bm.free()
    finish(W1.obj("Fish_Body", me, coll, m), subsurf=2)

    def fin(name, pts, x=0.0, thick=0.006, rot=None, loc=None):
        f = profile(name, pts, thick, coll, m, x=x)
        if rot:
            f.rotation_euler = rot
        if loc:
            f.location = loc
        return finish(f, bevel=0.002)

    # tail: crescent, root inside the tail stalk; keel ridges on the stalk
    fin("Fish_Tail", [(0.08, 0.0), (-0.02, 0.32), (0.0, 0.33), (0.04, 0.05), (0.04, -0.05), (0.0, -0.33),
                      (-0.02, -0.32)], thick=0.01)
    # dorsal fin rooted into the back, pectoral fins rooted into the sides (inside the body surface)
    fin("Fish_Dorsal", [(0.78, 0.09), (0.9, 0.36), (0.96, 0.34), (1.02, 0.1)], thick=0.008)
    fin("Fish_Anal", [(0.4, -0.05), (0.44, -0.14), (0.5, -0.05)], thick=0.006)
    for sx in (-1, 1):
        pf = fin(f"Fish_Pectoral{sx}", [(1.12, -0.03), (0.86, -0.12), (0.88, -0.1), (1.1, -0.0)], x=sx * 0.055,
                 thick=0.006)
        pf.rotation_euler = (0, math.radians(sx * 35), 0)
        pf.location = (0, 0, 0)
    eye_m = W1.principled("W2_FishEye", (0.01, 0.01, 0.012), 0.02, **{"Coat Weight": 1.0})
    iris = W1.principled("W2_FishIris", (0.35, 0.3, 0.2), 0.2, metal=0.5)
    for sx in (-1, 1):   # eyes set into the head
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=0.022)
        me = bpy.data.meshes.new("Eye")
        bm.to_mesh(me)
        bm.free()
        e = W1.obj(f"Fish_Eye{sx}", me, coll, eye_m)
        e.location = (sx * 0.026, 1.36, 0.03)
        bm = bmesh.new()
        bmesh.ops.create_circle(bm, cap_ends=True, segments=24, radius=0.0225)
        me = bpy.data.meshes.new("Iris")
        bm.to_mesh(me)
        bm.free()
        ir = W1.obj(f"Fish_Iris{sx}", me, coll, iris)
        ir.location = (sx * 0.0405, 1.36, 0.03)
        ir.rotation_euler = (0, math.radians(90), 0)
    W1.empty("GRIP", (0, 0.07, 0), coll)
    W1.empty("TIP", (0, 2.08, 0.002), coll)
    apply_all(coll)
    return coll


def build_all():
    W1.make_textures()
    W1.build_bottle()
    W1.build_bottle(broken=True)
    W1.build_towel()
    W1.build_knife()
    build_golf_club()
    build_swordfish()
    build_pistol()
    build_harpoon_gun()
    build_sniper()
    build_coconut()
    W1.build_ammo()
