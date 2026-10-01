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


# ------------------------------------------------------------------ more weapons

def box_mesh(name, size, loc=(0, 0, 0), bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=3, affect="EDGES")
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def wood_material(name, light=(0.35, 0.17, 0.07), dark=(0.12, 0.05, 0.02), scale=(30, 3, 30)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    wv = nt.nodes.new("ShaderNodeTexWave")
    wv.inputs["Scale"].default_value = 5.0
    wv.inputs["Distortion"].default_value = 7.0
    wv.bands_direction = "Y"
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = scale
    nt.links.new(tc.outputs["Object"], mp.inputs[0])
    nt.links.new(mp.outputs[0], wv.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*dark, 1)
    ramp.color_ramp.elements[1].color = (*light, 1)
    nt.links.new(wv.outputs["Fac"], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.35
    b.inputs["Coat Weight"].default_value = 0.6
    return m


def build_golf_club():
    """Iron: rubber grip, chrome shaft, angled steel head with grooves."""
    coll = collection("Weapon_GolfClub")
    chrome = principled("W_Chrome", (0.9, 0.9, 0.92), rough=0.06, metal=1.0)
    rubber = principled("W_GripRubber", (0.03, 0.03, 0.035), rough=0.75)
    head_m = principled("W_ClubHead", (0.7, 0.7, 0.72), rough=0.22, metal=1.0)
    L = 0.95
    shaft = lathe("Shaft", [(0.0, 0.0), (0.0075, 0.0), (0.0065, 0.3), (0.0052, L - 0.05), (0.0058, L), (0.0, L)], segs=20)
    obj("Club_Shaft", shaft, coll, chrome)
    grip = lathe("Grip", [(0.0, -0.27), (0.0115, -0.27), (0.0125, -0.25), (0.0105, -0.02), (0.0085, 0.02), (0.0, 0.02)], segs=24)
    obj("Club_Grip", grip, coll, rubber)
    head = box_mesh("Head", (0.075, 0.06, 0.018), bevel=0.004)
    h = obj("Club_Head", head, coll, head_m)
    h.location = (0.03, L + 0.02, 0)
    h.rotation_euler = (math.radians(28), 0, 0)      # loft angle
    for k in range(7):                                # grooves on the face
        g = box_mesh(f"Groove{k}", (0.06, 0.0015, 0.0012))
        gob = obj(f"Club_Groove{k}", g, coll, rubber)
        gob.parent = h
        gob.location = (0, -0.022 + k * 0.007, 0.0095)
    empty("GRIP", (0, -0.12, 0), coll)
    empty("TIP", (0.03, L + 0.02, 0), coll)
    return coll


def build_swordfish():
    """Frozen swordfish, held by the tail. Ice crust + frost (realistic, a little funny)."""
    coll = collection("Weapon_Swordfish")
    fish = bpy.data.materials.get("W_FrozenFish") or bpy.data.materials.new("W_FrozenFish")
    fish.use_nodes = True
    nt = fish.node_tree
    b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")         # dark blue-grey back, silver belly
    ramp.color_ramp.elements[0].position = 0.4
    ramp.color_ramp.elements[0].color = (0.75, 0.78, 0.82, 1)
    ramp.color_ramp.elements[1].position = 0.6
    ramp.color_ramp.elements[1].color = (0.08, 0.1, 0.16, 1)
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = -0.05
    mr.inputs["From Max"].default_value = 0.05
    nt.links.new(sep.outputs[2], mr.inputs["Value"])
    nt.links.new(mr.outputs[0], ramp.inputs[0])
    frost = nt.nodes.new("ShaderNodeTexNoise")
    frost.inputs["Scale"].default_value = 60
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    nt.links.new(ramp.outputs[0], mix.inputs[6])
    mix.inputs[7].default_value = (0.9, 0.95, 1.0, 1)
    fm = nt.nodes.new("ShaderNodeMath")
    fm.operation = "MULTIPLY"
    fm.inputs[1].default_value = 0.35
    nt.links.new(frost.outputs["Fac"], fm.inputs[0])
    nt.links.new(fm.outputs[0], mix.inputs[0])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.45
    b.inputs["Metallic"].default_value = 0.3
    b.inputs["Coat Weight"].default_value = 1.0      # thin ice glaze
    b.inputs["Coat Roughness"].default_value = 0.05
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.3
    nt.links.new(frost.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs[0], b.inputs["Normal"])
    # body: lathe along +Y (tail at y=0, head toward +Y), flattened sideways
    L = 1.6
    prof = [(0.0, 0.0), (0.02, 0.05), (0.03, 0.15), (0.07, 0.4), (0.11, 0.75), (0.12, 1.0), (0.1, 1.2),
            (0.06, 1.35), (0.025, 1.42), (0.0, 1.45)]
    body = lathe("FishBody", prof, segs=32, cap_bottom=False)
    fb = obj("Fish_Body", body, coll, fish)
    fb.scale = (0.6, 1, 1)
    sword = lathe("Sword", [(0.0, 1.38), (0.02, 1.38), (0.012, 1.7), (0.004, 2.0), (0.0, 2.05)], segs=16)
    sw = obj("Fish_Sword", sword, coll, fish)
    sw.scale = (0.5, 1, 1)
    # tail fin (moon shape) + dorsal fin + side fins
    def fin(name, pts, loc, rot):
        bm = bmesh.new()
        vs = [bm.verts.new((0, y, z)) for y, z in pts]
        bm.faces.new(vs)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        f = obj(name, me, coll, fish)
        f.location = loc
        f.rotation_euler = rot
        s = f.modifiers.new("Solidify", "SOLIDIFY")
        s.thickness = 0.006
        return f
    fin("Fish_Tail", [(0.06, 0.0), (-0.12, 0.28), (-0.02, 0.05), (-0.02, -0.05), (-0.12, -0.28)], (0, 0, 0), (0, 0, 0))
    fin("Fish_Dorsal", [(0.0, 0.0), (0.12, 0.3), (0.2, 0.02)], (0, 0.85, 0.1), (0, 0, 0))
    for sx in (-1, 1):
        fin(f"Fish_Pectoral{sx}", [(0.0, 0.0), (-0.25, -0.05), (-0.05, -0.02)], (sx * 0.06, 1.1, -0.04),
            (0, 0, math.radians(sx * 20)))
    eye = principled("W_FishEye", (0.02, 0.02, 0.02), rough=0.05, **{"Coat Weight": 1.0})
    for sx in (-1, 1):
        e = obj(f"Fish_Eye{sx}", lathe("Eye", [(0, -0.01), (0.022, -0.01), (0.022, 0.0), (0, 0.004)], segs=16), coll, eye)
        e.location = (sx * 0.055, 1.3, 0.03)
        e.rotation_euler = (0, 0, math.radians(90 * sx))
    empty("GRIP", (0, 0.07, 0), coll)
    empty("TIP", (0, 2.05, 0), coll)
    return coll


def build_pistol():
    """Chrome pistol with wood grips, 80s action-movie style (generic, no brand)."""
    coll = collection("Weapon_Pistol")
    chrome = principled("W_Chrome", (0.9, 0.9, 0.92), rough=0.06, metal=1.0)
    dark = principled("W_DarkSteel", (0.05, 0.05, 0.055), rough=0.35, metal=1.0)
    wood = wood_material("W_PistolWood", (0.45, 0.2, 0.08), (0.16, 0.06, 0.02))
    obj("Pistol_Slide", box_mesh("Slide", (0.03, 0.2, 0.034), (0, 0.06, 0.035), bevel=0.003), coll, chrome)
    obj("Pistol_Frame", box_mesh("Frame", (0.028, 0.13, 0.022), (0, 0.04, 0.012), bevel=0.003), coll, chrome)
    gr = obj("Pistol_Grip", box_mesh("GripCore", (0.026, 0.05, 0.11), (0, 0, 0), bevel=0.004), coll, chrome)
    gr.location = (0, -0.025, -0.05)
    gr.rotation_euler = (math.radians(-14), 0, 0)
    for sx in (-1, 1):
        w = obj(f"Pistol_Wood{sx}", box_mesh("WoodPanel", (0.006, 0.042, 0.09), (0, 0, 0), bevel=0.0025), coll, wood)
        w.location = (sx * 0.0155, -0.025, -0.052)
        w.rotation_euler = (math.radians(-14), 0, 0)
    bar = lathe("Barrel", [(0.0, 0.13), (0.0065, 0.13), (0.0065, 0.165), (0.0, 0.165)], segs=20)
    obj("Pistol_Muzzle", bar, coll, dark).location = (0, 0, 0.04)
    obj("Pistol_Trigger", box_mesh("Trigger", (0.005, 0.008, 0.02), (0, 0.025, -0.012), bevel=0.0015), coll, dark)
    guard = lathe("Guard", [(0.022, -0.002), (0.025, -0.002), (0.025, 0.002), (0.022, 0.002)], segs=24, cap_bottom=False)
    g = obj("Pistol_TriggerGuard", guard, coll, chrome)
    g.rotation_euler = (0, 0, math.radians(90))
    g.location = (0, 0.03, -0.012)
    g.scale = (1, 1, 0.75)
    obj("Pistol_Hammer", box_mesh("Hammer", (0.008, 0.012, 0.016), (0, -0.04, 0.05), bevel=0.002), coll, dark)
    for k in range(8):   # slide serrations
        obj(f"Pistol_Serr{k}", box_mesh("Serr", (0.031, 0.002, 0.02), (0, -0.025 + k * 0.006, 0.036)), coll, dark)
    obj("Pistol_SightF", box_mesh("SightF", (0.004, 0.006, 0.006), (0, 0.15, 0.055)), coll, dark)
    obj("Pistol_SightR", box_mesh("SightR", (0.014, 0.006, 0.006), (0, -0.03, 0.055)), coll, dark)
    empty("GRIP", (0, -0.02, -0.03), coll)
    empty("MUZZLE", (0, 0.168, 0.04), coll)
    return coll


def build_harpoon_gun():
    """Yellow speargun with a rope reel and a steel harpoon."""
    coll = collection("Weapon_HarpoonGun")
    yellow = principled("W_HarpoonYellow", (1.0, 0.72, 0.02), rough=0.3, **{"Coat Weight": 0.6})
    black = principled("W_RubberBlack", (0.02, 0.02, 0.02), rough=0.6)
    steel = principled("W_Steel", (0.82, 0.82, 0.8), rough=0.18, metal=1.0)
    rope = principled("W_Rope", (0.85, 0.82, 0.7), rough=0.9)
    barrel = lathe("Barrel", [(0.0, -0.05), (0.016, -0.05), (0.016, 0.75), (0.0, 0.75)], segs=24)
    obj("Harpoon_Barrel", barrel, coll, yellow)
    gr = obj("Harpoon_Grip", box_mesh("Grip", (0.03, 0.05, 0.13), bevel=0.006), coll, black)
    gr.location = (0, 0.03, -0.07)
    gr.rotation_euler = (math.radians(-12), 0, 0)
    obj("Harpoon_Muzzle", lathe("MuzzleH", [(0.0, 0.72), (0.026, 0.72), (0.026, 0.79), (0.0, 0.79)], segs=24), coll, black)
    spear = lathe("Spear", [(0.0, 0.0), (0.0035, 0.0), (0.0035, 1.0), (0.0, 1.0)], segs=12)
    sp = obj("Harpoon_Spear", spear, coll, steel)
    sp.location = (0, 0, 0.025)
    tip = lathe("Tip", [(0.0, 0.0), (0.008, 0.0), (0.0, 0.06)], segs=12)
    obj("Harpoon_Tip", tip, coll, steel).location = (0, 1.0, 0.025)
    for sx in (-1, 1):     # barbs
        b = obj(f"Harpoon_Barb{sx}", box_mesh("Barb", (0.002, 0.03, 0.004)), coll, steel)
        b.location = (sx * 0.006, 0.99, 0.025)
        b.rotation_euler = (0, 0, math.radians(-25 * sx))
    # rubber bands
    for sx in (-1, 1):
        band = lathe("Band", [(0.0, 0.0), (0.006, 0.0), (0.006, 0.42), (0.0, 0.42)], segs=12)
        bo = obj(f"Harpoon_Band{sx}", band, coll, black)
        bo.location = (sx * 0.02, 0.33, 0.02)
        bo.rotation_euler = (0, 0, math.radians(sx * 2))
    # rope reel under the barrel
    reel = lathe("Reel", [(0.0, -0.015), (0.04, -0.015), (0.04, -0.012), (0.022, -0.012), (0.022, 0.012),
                          (0.04, 0.012), (0.04, 0.015), (0.0, 0.015)], segs=32)
    r = obj("Harpoon_Reel", reel, coll, black)
    r.rotation_euler = (0, 0, math.radians(90))
    r.location = (0, 0.15, -0.045)
    coil = lathe("Coil", [(0.022, -0.011), (0.034, -0.011), (0.034, 0.011), (0.022, 0.011)], segs=32)
    c = obj("Harpoon_Rope", coil, coll, rope)
    c.rotation_euler = (0, 0, math.radians(90))
    c.location = (0, 0.15, -0.045)
    empty("GRIP", (0, 0.03, -0.05), coll)
    empty("MUZZLE", (0, 1.06, 0.025), coll)
    return coll


def build_sniper():
    """Bolt-action sniper rifle with scope and bipod (generic)."""
    coll = collection("Weapon_Sniper")
    dark = principled("W_DarkSteel", (0.05, 0.05, 0.055), rough=0.35, metal=1.0)
    stock_m = principled("W_SniperStock", (0.12, 0.13, 0.1), rough=0.6)     # olive polymer
    lens = principled("W_ScopeLens", (0.02, 0.04, 0.08), rough=0.02, **{"Coat Weight": 1.0})
    obj("Sniper_Barrel", lathe("Barrel", [(0.0, 0.2), (0.011, 0.2), (0.009, 0.95), (0.0, 0.95)], segs=20), coll, dark)
    obj("Sniper_Brake", lathe("Brake", [(0.0, 0.95), (0.016, 0.95), (0.016, 1.02), (0.0, 1.02)], segs=16), coll, dark)
    obj("Sniper_Receiver", box_mesh("Receiver", (0.036, 0.24, 0.045), (0, 0.1, 0.0), bevel=0.004), coll, dark)
    stock = box_mesh("Stock", (0.042, 0.62, 0.07), (0, -0.05, -0.03), bevel=0.01)
    obj("Sniper_Stock", stock, coll, stock_m)
    obj("Sniper_Butt", box_mesh("Butt", (0.044, 0.04, 0.15), (0, -0.37, -0.06), bevel=0.01), coll, stock_m)
    obj("Sniper_Grip", box_mesh("SGrip", (0.034, 0.045, 0.1), (0, -0.04, -0.09), bevel=0.008), coll, stock_m)
    obj("Sniper_Mag", box_mesh("Mag", (0.03, 0.07, 0.06), (0, 0.1, -0.07), bevel=0.003), coll, dark)
    bolt = lathe("Bolt", [(0.0, 0.0), (0.004, 0.0), (0.004, 0.05), (0.0, 0.05)], segs=12)
    b = obj("Sniper_Bolt", bolt, coll, dark)
    b.rotation_euler = (0, 0, math.radians(90))
    b.location = (0.02, 0.03, 0.01)
    knob = lathe("Knob", [(0.0, -0.01), (0.009, -0.006), (0.009, 0.006), (0.0, 0.01)], segs=16)
    k = obj("Sniper_BoltKnob", knob, coll, dark)
    k.rotation_euler = (0, 0, math.radians(90))
    k.location = (0.075, 0.03, 0.01)
    # scope
    scope = lathe("Scope", [(0.0, -0.05), (0.024, -0.05), (0.024, -0.02), (0.016, 0.0), (0.016, 0.22), (0.026, 0.25),
                            (0.026, 0.3), (0.0, 0.3)], segs=32)
    obj("Sniper_Scope", scope, coll, dark).location = (0, 0.0, 0.075)
    for y in (-0.049, 0.299):
        l = lathe("Lens", [(0.0, 0.0), (0.022, 0.0), (0.0, 0.002)], segs=24)
        lo = obj("Sniper_Lens", l, coll, lens)
        lo.location = (0, y, 0.075)
    for y in (0.03, 0.18):
        obj("Sniper_Mount", box_mesh("Mount", (0.02, 0.02, 0.05), (0, y, 0.05)), coll, dark)
    # bipod (folded forward a little)
    for sx in (-1, 1):
        leg = lathe("Leg", [(0.0, 0.0), (0.005, 0.0), (0.005, 0.22), (0.0, 0.22)], segs=10)
        lg = obj(f"Sniper_BipodLeg{sx}", leg, coll, dark)
        lg.location = (sx * 0.012, 0.62, -0.01)
        lg.rotation_euler = (math.radians(-110), 0, math.radians(sx * 12))
    empty("GRIP", (0, -0.04, -0.06), coll)
    empty("MUZZLE", (0, 1.02, 0), coll)
    empty("SCOPE", (0, -0.12, 0.075), coll)
    return coll


def build_coconut():
    """Exploding coconut: hairy brown shell, 3 'eyes', a fuse on top (3 s)."""
    coll = collection("Weapon_Coconut")
    shell = bpy.data.materials.get("W_Coconut") or bpy.data.materials.new("W_Coconut")
    shell.use_nodes = True
    nt = shell.node_tree
    b = nt.nodes["Principled BSDF"]
    wv = nt.nodes.new("ShaderNodeTexWave")
    wv.wave_type = "RINGS"
    wv.inputs["Scale"].default_value = 3
    wv.inputs["Distortion"].default_value = 18
    wv.inputs["Detail"].default_value = 8
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.04, 0.018, 0.008, 1)
    ramp.color_ramp.elements[1].color = (0.16, 0.08, 0.035, 1)
    nt.links.new(wv.outputs["Fac"], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.8
    nt.links.new(wv.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs[0], b.inputs["Normal"])
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=0.06)
    for v in bm.verts:
        v.co.z *= 1.12
        v.co += v.co.normalized() * noise.noise(v.co * 40) * 0.003
    me = bpy.data.meshes.new("Coconut")
    bm.to_mesh(me)
    bm.free()
    obj("Coconut_Shell", me, coll, shell)
    dark = principled("W_CoconutEye", (0.03, 0.015, 0.01), rough=0.8)
    for k in range(3):
        a = math.tau * k / 3
        e = obj(f"Coconut_Eye{k}", lathe("CEye", [(0, 0), (0.007, 0), (0.007, 0.002), (0, 0.003)], segs=12), coll, dark)
        e.location = (math.cos(a) * 0.012, math.sin(a) * 0.012, 0.066)
        e.rotation_euler = (math.radians(-90), 0, 0)
    fuse_m = principled("W_Fuse", (0.6, 0.55, 0.45), rough=0.9)
    pts = [Vector((0, 0, 0.065)), Vector((0.005, 0.0, 0.085)), Vector((0.015, 0.004, 0.1)), Vector((0.028, 0.01, 0.108))]
    cu = bpy.data.curves.new("Fuse", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 0.0025
    sp = cu.splines.new("NURBS")
    sp.points.add(len(pts) - 1)
    for p, v in zip(sp.points, pts):
        p.co = (*v, 1)
    sp.use_endpoint_u = True
    sp.order_u = 3
    f = bpy.data.objects.new("Coconut_Fuse", cu)
    coll.objects.link(f)
    cu.materials.append(fuse_m)
    spark = principled("W_Spark", (1.0, 0.6, 0.1), rough=0.3, **{"Emission Color": (1.0, 0.55, 0.1, 1), "Emission Strength": 30.0})
    s = obj("Coconut_Spark", lathe("Spark", [(0, -0.005), (0.004, 0), (0, 0.005)], segs=8), coll, spark)
    s.location = pts[-1]
    empty("GRIP", (0, 0, 0), coll)
    return coll


def build_ammo():
    """Ammo pickups: bullet box, harpoon bundle, rifle rounds."""
    coll = collection("Pickup_Ammo")
    card = principled("W_AmmoBox", (0.65, 0.12, 0.08), rough=0.7)
    brass = principled("W_Brass", (0.85, 0.62, 0.25), rough=0.25, metal=1.0)
    lead = principled("W_Lead", (0.4, 0.4, 0.42), rough=0.4, metal=1.0)
    steel = principled("W_Steel", (0.82, 0.82, 0.8), rough=0.18, metal=1.0)
    obj("Ammo_PistolBox", box_mesh("Box", (0.09, 0.06, 0.035), (0, 0, 0.0175), bevel=0.002), coll, card)
    for k in range(5):
        c = obj(f"Ammo_Round{k}", lathe("Round", [(0, 0), (0.0045, 0), (0.0045, 0.06), (0.003, 0.068), (0, 0.075)],
                                          segs=16), coll, brass)
        c.rotation_euler = (math.radians(-90), 0, 0)
        c.location = (0.16 + k * 0.012, 0, 0.0)
        c.location.z = 0.005
    for k in range(3):
        sp = obj(f"Ammo_Harpoon{k}", lathe("Sp", [(0, 0), (0.0035, 0), (0.0035, 0.6), (0, 0.65)], segs=10), coll, steel)
        sp.location = (-0.15 + k * 0.012, -0.3, 0.004)
    return coll
