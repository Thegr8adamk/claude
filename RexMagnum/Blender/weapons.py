"""Rex Magnum - weapons (realistic objects; generic, no real brands). Brief section 5.

Each weapon: a separate collection, a GRIP empty (where the hand holds it, +Y = up the hand,
-Y... see notes) and for guns a MUZZLE empty. Units: meters. Front of a weapon = +Y.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "textures")


# ------------------------------------------------------------------ helpers

def collection(name):
    c = bpy.data.collections.get(name)
    if c:
        for o in list(c.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def obj(name, me, coll, mat=None, smooth=True):
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    if mat:
        me.materials.append(mat)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return ob


def empty(name, loc, coll, size=0.03):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = size
    e.location = loc
    coll.objects.link(e)
    return e


def lathe(name, profile, segs=48, axis="Y", cap_bottom=True, cap_top=False):
    """profile: list of (radius, height) from bottom to top. Axis = the weapon length axis."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        ring = []
        for i in range(segs):
            a = math.tau * i / segs
            x, z = math.cos(a) * r, math.sin(a) * r
            ring.append(bm.verts.new((x, h, z) if axis == "Y" else (x, z, h)))
        rings.append(ring)
    for j in range(len(rings) - 1):
        for i in range(segs):
            bm.faces.new((rings[j][i], rings[j][(i + 1) % segs], rings[j + 1][(i + 1) % segs], rings[j + 1][i]))
    if cap_bottom:
        bm.faces.new(list(reversed(rings[0])))
    if cap_top:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def cyl_uv(me, axis="Y", u_scale=1.0, v_scale=1.0, v_offset=0.0):
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        us = []
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            u = (0.5 - math.atan2(co.z, co.x) / math.tau) * u_scale
            v = ((co.y if axis == "Y" else co.z) + v_offset) * v_scale
            uv.data[li].uv = (u, v)
            us.append(u)
        if max(us) - min(us) > 0.5 * u_scale:
            for li in p.loop_indices:
                if uv.data[li].uv[0] > 0.5 * u_scale:
                    uv.data[li].uv[0] -= u_scale


def principled(name, color, rough=0.5, metal=0.0, **kw):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    for k, v in kw.items():
        b.inputs[k].default_value = v
    return m


def image_material(name, image_path, rough=0.6, metal=0.0, alpha=False, **kw):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(image_path, check_existing=True)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    if alpha:
        nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    for k, v in kw.items():
        b.inputs[k].default_value = v
    return m


# ------------------------------------------------------------------ textures (drawn in code)

def make_textures():
    from PIL import Image, ImageDraw, ImageFont
    os.makedirs(TEX, exist_ok=True)
    # Beer label: made-up local brand
    W, H = 1024, 512
    im = Image.new("RGB", (W, H), (236, 224, 190))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, 60], fill=(170, 30, 30))
    d.rectangle([0, H - 60, W, H], fill=(170, 30, 30))
    d.ellipse([W // 2 - 170, 90, W // 2 + 170, 420], outline=(170, 30, 30), width=10)
    try:
        f1 = ImageFont.truetype("DejaVuSans-Bold.ttf", 78)
        f2 = ImageFont.truetype("DejaVuSans-Bold.ttf", 36)
    except OSError:
        f1 = f2 = ImageFont.load_default()
    d.text((W // 2, 210), "ISLA", fill=(30, 30, 30), font=f1, anchor="mm")
    d.text((W // 2, 300), "LAGER", fill=(170, 30, 30), font=f2, anchor="mm")
    # small palm tree
    d.line([(W // 2, 395), (W // 2 + 8, 340)], fill=(60, 40, 20), width=7)
    for a in range(5):
        ang = math.radians(200 + a * 35)
        d.line([(W // 2 + 8, 340), (W // 2 + 8 + 45 * math.cos(ang), 340 + 30 * math.sin(ang))], fill=(30, 120, 50), width=6)
    im.save(os.path.join(TEX, "bottle_label.png"))
    # Beach towel: coral / yellow / teal / white stripes along the length
    W, H = 512, 1024
    im = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(im)
    cols = [(250, 112, 92), (255, 255, 250), (252, 205, 60), (255, 255, 250), (32, 168, 164), (255, 255, 250)]
    widths = [140, 24, 110, 24, 140, 24]
    y = 0
    k = 0
    while y < H:
        d.rectangle([0, y, W, y + widths[k % 6]], fill=cols[k % 6])
        y += widths[k % 6]
        k += 1
    im.save(os.path.join(TEX, "towel_stripes.png"))
    # Terry cloth bump (tiny loops)
    rng = np.random.default_rng(5)
    n = rng.random((512, 512))
    Image.fromarray((n * 255).astype(np.uint8)).save(os.path.join(TEX, "towel_bump.png"))


# ------------------------------------------------------------------ weapons

def build_bottle(broken=False):
    """Brown glass beer bottle, held by the neck. Broken version: jagged neck (stab weapon)."""
    name = "Weapon_BottleBroken" if broken else "Weapon_Bottle"
    coll = collection(name)
    glass = principled("W_BrownGlass", (0.25, 0.09, 0.02), rough=0.03, **{"Transmission Weight": 1.0, "IOR": 1.5})
    beer = principled("W_Beer", (0.85, 0.45, 0.05), rough=0.05, **{"Transmission Weight": 1.0, "IOR": 1.34})
    label = image_material("W_BottleLabel", os.path.join(TEX, "bottle_label.png"), rough=0.55)
    cap = principled("W_BottleCap", (0.75, 0.72, 0.65), rough=0.25, metal=1.0)
    # profile (r, y): 0.24 m tall longneck; the hand holds the neck, so y=0 is the grip
    prof = [(0.0, -0.17), (0.028, -0.17), (0.031, -0.165), (0.031, -0.02), (0.029, 0.0), (0.02, 0.03),
            (0.0135, 0.055), (0.0125, 0.07), (0.0135, 0.072), (0.0135, 0.076)]
    if broken:
        # keep the neck and the shoulder; the body is gone (jagged edge at the bottom)
        prof = [(0.03, -0.065)] + [p for p in prof if p[1] > -0.06]
    outer = lathe("Bottle", [(r, y) for r, y in prof], segs=48)
    inner = [(max(r - 0.0025, 0.0), y) for r, y in prof]
    ob = obj(name, outer, coll, glass)
    sol = ob.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.0025
    if broken:
        # jagged broken edge at the top
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        low = min(v.co.y for v in bm.verts)
        for v in bm.verts:
            if v.co.y < low + 0.001:
                a = math.atan2(v.co.z, v.co.x)
                v.co.y -= 0.035 * abs(math.sin(a * 2.5)) + 0.015 * abs(math.sin(a * 7.3))
        bm.to_mesh(ob.data)
        bm.free()
    else:
        lab = lathe("Label", [(0.0313, -0.14), (0.0313, -0.06)], segs=48, cap_bottom=False)
        cyl_uv(lab, u_scale=1.0, v_scale=1 / 0.08, v_offset=0.14)
        obj("Bottle_Label", lab, coll, label)
        c = lathe("Cap", [(0.0, 0.074), (0.0145, 0.074), (0.0145, 0.083), (0.0, 0.084)], segs=24, cap_bottom=False)
        obj("Bottle_Cap", c, coll, cap)
        lq = lathe("Beer", [(0.0, -0.167), (0.0285, -0.167), (0.0285, -0.02), (0.02, 0.01)], segs=32)
        obj("Bottle_Beer", lq, coll, beer)
    empty("GRIP", (0, 0.02, 0), coll)
    empty("TIP", (0, -0.17, 0), coll)       # the part that hits
    return coll


def build_towel():
    """Beach towel. Carry: flat with fringe (bones along the length). Whip: twisted roll (shape key 'Whip')."""
    coll = collection("Weapon_Towel")
    mat = image_material("W_Towel", os.path.join(TEX, "towel_stripes.png"), rough=0.95, **{"Sheen Weight": 0.6})
    nt = mat.node_tree
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.4
    bt = nt.nodes.new("ShaderNodeTexImage")
    bt.image = bpy.data.images.load(os.path.join(TEX, "towel_bump.png"), check_existing=True)
    bt.image.colorspace_settings.name = "Non-Color"
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (12, 24, 1)
    uvn = nt.nodes.new("ShaderNodeUVMap")
    nt.links.new(uvn.outputs[0], mp.inputs[0])
    nt.links.new(mp.outputs[0], bt.inputs[0])
    nt.links.new(bt.outputs["Color"], bump.inputs["Height"])
    nt.links.new(bump.outputs[0], nt.nodes["Principled BSDF"].inputs["Normal"])
    L, Wd = 1.5, 0.75
    nx, ny = 12, 60
    bm = bmesh.new()
    grid = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = (i / nx - 0.5) * Wd
            y = -j / ny * L                      # hangs down from the hand (grip at y=0)
            row.append(bm.verts.new((x, y, 0.0)))
        grid.append(row)
    uvl = bm.loops.layers.uv.new("UVMap")
    for j in range(ny):
        for i in range(nx):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            for lp, (u, v) in zip(f.loops, ((i / nx, j / ny), ((i + 1) / nx, j / ny),
                                             ((i + 1) / nx, (j + 1) / ny), (i / nx, (j + 1) / ny))):
                lp[uvl].uv = (u, v)
    me = bpy.data.meshes.new("Towel")
    bm.to_mesh(me)
    bm.free()
    towel = obj("Weapon_Towel", me, coll, mat)
    sol = towel.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.006
    # Fringe: short white threads at both ends
    white = principled("W_TowelFringe", (0.95, 0.94, 0.9), rough=0.95)
    bm = bmesh.new()
    for yend, sgn in ((0.0, 1), (-L, -1)):
        for k in range(48):
            x = (k / 47 - 0.5) * Wd * 0.97
            v0 = bm.verts.new((x - 0.002, yend, 0))
            v1 = bm.verts.new((x + 0.002, yend, 0))
            v2 = bm.verts.new((x + 0.002, yend + sgn * 0.045, 0))
            v3 = bm.verts.new((x - 0.002, yend + sgn * 0.045, 0))
            bm.faces.new((v0, v1, v2, v3))
    fme = bpy.data.meshes.new("Fringe")
    bm.to_mesh(fme)
    bm.free()
    obj("Towel_Fringe", fme, coll, white, smooth=False)
    # Whip shape key: roll into a twisted tube (spiral stripes), tip stays flat with fringe
    towel.shape_key_add(name="Basis")
    sk = towel.shape_key_add(name="Whip")
    sk.value = 0.0
    for i, v in enumerate(me.vertices):
        x, y = v.co.x, v.co.y
        t = -y / L
        u = x / Wd + 0.5                   # 0..1 across
        flat_tip = max(0.0, (t - 0.85) / 0.15)
        r = 0.035 * (1 - 0.4 * t)
        ang = u * math.tau * 1.6 + t * math.tau * 3.0      # twist along the length
        rolled = Vector((math.cos(ang) * r * (1 - u * 0.5), y, math.sin(ang) * r * (1 - u * 0.5)))
        sk.data[i].co = rolled.lerp(Vector((x * 0.6, y, 0)), flat_tip)
    empty("GRIP", (0, -0.03, 0), coll)
    empty("TIP", (0, -L, 0), coll)
    # Bones (8) along the length for swing/snap/carry animations (in Unity: driven by the weapon script)
    return coll


def build_knife():
    """Steak knife from the buffet: serrated steel blade, dark wood handle, 3 rivets."""
    coll = collection("Weapon_Knife")
    steel = principled("W_Steel", (0.82, 0.82, 0.8), rough=0.18, metal=1.0, **{"Anisotropic": 0.6})
    wood = bpy.data.materials.get("W_DarkWood") or bpy.data.materials.new("W_DarkWood")
    wood.use_nodes = True
    nt = wood.node_tree
    b = nt.nodes["Principled BSDF"]
    wv = nt.nodes.new("ShaderNodeTexWave")
    wv.inputs["Scale"].default_value = 6.0
    wv.inputs["Distortion"].default_value = 6.0
    wv.bands_direction = "Y"
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (40, 4, 40)
    nt.links.new(tc.outputs["Object"], mp.inputs[0])
    nt.links.new(mp.outputs[0], wv.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.06, 0.025, 0.012, 1)
    ramp.color_ramp.elements[1].color = (0.2, 0.09, 0.04, 1)
    nt.links.new(wv.outputs["Fac"], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.35
    b.inputs["Coat Weight"].default_value = 0.5
    # Blade: 2D outline extruded thin, edge serrated
    pts = []
    n = 40
    blade_len = 0.13
    for i in range(n + 1):           # spine (top edge)
        t = i / n
        pts.append((0.0 + 0.002 * t, t * blade_len, 0.011 - 0.009 * t ** 3))
    for i in range(n, -1, -1):       # cutting edge (bottom), serrated, curving up to the tip
        t = i / n
        serr = 0.0012 * (1 if (i % 2) else 0)
        z = -0.011 + 0.012 * t ** 2.5 + serr
        pts.append((0.0, t * blade_len, z))
    bm = bmesh.new()
    top = [bm.verts.new((0.0012, y, z)) for _, y, z in pts]
    bot = [bm.verts.new((-0.0012, y, z)) for _, y, z in pts]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    m = len(pts)
    for i in range(m):
        bm.faces.new((top[i], top[(i + 1) % m], bot[(i + 1) % m], bot[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # thin the cutting edge
    for v in bm.verts:
        if v.co.z < -0.006:
            v.co.x *= 0.35
    me = bpy.data.meshes.new("Blade")
    bm.to_mesh(me)
    bm.free()
    blade = obj("Knife_Blade", me, coll, steel, smooth=False)
    # Handle
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(0.014, 0.11, 0.022), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, -0.055, -0.001), verts=bm.verts)
    hm = bpy.data.meshes.new("Handle")
    bm.to_mesh(hm)
    bm.free()
    handle = obj("Knife_Handle", hm, coll, wood)
    bev = handle.modifiers.new("Bevel", "BEVEL")
    bev.width = 0.005
    bev.segments = 4
    for k in range(3):
        y = -0.02 - k * 0.035
        r = lathe("Rivet", [(0.0, -0.0075), (0.0028, -0.0075), (0.0028, 0.0075), (0.0, 0.0075)], segs=16)
        rv = obj(f"Knife_Rivet{k}", r, coll, steel)
        rv.rotation_euler = (0, 0, math.pi / 2)
        rv.location = (0, y, 0)
    # bolster
    bo = lathe("Bolster", [(0.0, 0.0), (0.0085, 0.0), (0.0095, 0.006), (0.006, 0.012), (0.0, 0.012)], segs=24)
    bol = obj("Knife_Bolster", bo, coll, steel)
    bol.scale = (0.75, 1, 1.25)
    empty("GRIP", (0, -0.055, 0), coll)
    empty("TIP", (0, blade_len, 0), coll)
    return coll


def render_weapons(out_dir, names, samples=96, res=(1200, 900)):
    import rex_render
    rex_render.setup(samples=samples, res=res)
    sc = bpy.context.scene
    sc.cycles.transmission_bounces = 12
    sc.cycles.max_bounces = 16
    os.makedirs(out_dir, exist_ok=True)
    for name in names:
        c = bpy.data.collections[name]
        for other in bpy.context.scene.collection.children:
            other.hide_render = other != c
        pts = [o.matrix_world @ Vector(b) for o in c.all_objects if o.type == "MESH" for b in o.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        mid = (lo + hi) / 2
        size = (hi - lo).length
        fl = bpy.data.objects.get("R_Floor")
        if fl:
            fl.location = (0, 0, lo.z - 0.002)
        for lname, off in (("R_Key", (-1.2, -1.0, 1.4)), ("R_Fill", (1.4, -0.8, 0.6)), ("R_Rim", (0.6, 1.4, 1.2))):
            l = bpy.data.objects[lname]
            l.location = mid + Vector(off) * size * 1.4
            rex_render._look(l, mid)
            l.data.energy = {"R_Key": 220, "R_Fill": 45, "R_Rim": 140}[lname] * (size * 1.4) ** 2 * 0.5
            l.data.size = size * 0.8
        d = size * 2.0
        rex_render.shot(out_dir, f"{name.lower()}_front.png", mid + Vector((d * 0.9, -d * 0.35, d * 0.25)), mid, 70)
        rex_render.shot(out_dir, f"{name.lower()}_side.png", mid + Vector((d, 0, 0.02)), mid, 70)
        if name == "Weapon_Towel":       # second look: twisted whip roll
            t = bpy.data.objects["Weapon_Towel"]
            t.data.shape_keys.key_blocks["Whip"].value = 1.0
            rex_render.shot(out_dir, "weapon_towel_whip.png", mid + Vector((d * 0.9, -d * 0.35, d * 0.25)), mid, 70)
            t.data.shape_keys.key_blocks["Whip"].value = 0.0
    for other in bpy.context.scene.collection.children:
        other.hide_render = False
