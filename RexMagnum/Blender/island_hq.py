"""Rex Magnum - realistic island scene (approval renders for build step 5).

Same layout as island.py (heightmap + zones), with realistic materials and vegetation:
ocean with waves + clear shallow water, sand / wet sand / grass / jungle floor / rock with moss,
palm trees and broadleaf jungle trees (leaf textures drawn in code), ferns, bushes, boulders,
a detailed hotel + pool, a waterfall with foam and mist, mossy ruins, the crashed ship.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

import island as I

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "textures")


# ------------------------------------------------------------------ small helpers

def coll(name, hide=False):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    c.hide_render = hide
    return c


def link(ob, c="Island"):
    coll(c).objects.link(ob)
    return ob


def mesh_ob(name, bm, mat=None, c="Island", smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if mat:
        me.materials.append(mat)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return link(bpy.data.objects.new(name, me), c)


def box(name, size, loc, mat, rot=(0, 0, 0), c="Island", bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect="EDGES")
    ob = mesh_ob(name, bm, mat, c, smooth=False)
    ob.location = loc
    ob.rotation_euler = rot
    return ob


class N:
    """Node helper."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.out = self.new("ShaderNodeOutputMaterial")

    def new(self, t, **inputs):
        n = self.nt.nodes.new(t)
        for k, v in inputs.items():
            if k in n.inputs:
                n.inputs[k].default_value = v
            else:
                setattr(n, k, v)
        return n

    def link(self, a, b):
        self.nt.links.new(a, b)

    def math(self, op, a, b=None):
        m = self.new("ShaderNodeMath", operation=op)
        self._in(m.inputs[0], a)
        if b is not None:
            self._in(m.inputs[1], b)
        return m.outputs[0]

    def _in(self, sock, v):
        if isinstance(v, (int, float)):
            sock.default_value = v
        else:
            self.link(v, sock)

    def mix(self, fac, a, b):
        m = self.new("ShaderNodeMix", data_type="RGBA")
        self._in(m.inputs[0], fac)
        for s, v in ((m.inputs[6], a), (m.inputs[7], b)):
            if isinstance(v, tuple):
                s.default_value = (*v, 1) if len(v) == 3 else v
            else:
                self.link(v, s)
        return m.outputs[2]

    def mixf(self, fac, a, b):
        m = self.new("ShaderNodeMix", data_type="FLOAT")
        self._in(m.inputs[0], fac)
        self._in(m.inputs[2], a)
        self._in(m.inputs[3], b)
        return m.outputs[0]

    def smooth(self, v, lo, hi):
        m = self.new("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP")
        self._in(m.inputs["Value"], v)
        m.inputs["From Min"].default_value = lo
        m.inputs["From Max"].default_value = hi
        return m.outputs[0]

    def noise(self, vec, scale, detail=6, rough=0.55):
        n = self.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        self.link(vec, n.inputs["Vector"])
        return n.outputs["Fac"]


def simple(name, color, rough=0.5, metal=0.0, **kw):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    for k, v in kw.items():
        b.inputs[k].default_value = v
    return m


# ------------------------------------------------------------------ leaf textures (drawn in code)

def leaf_textures():
    from PIL import Image, ImageDraw, ImageFilter
    os.makedirs(TEX, exist_ok=True)
    rng = random.Random(3)
    # broadleaf cluster card (many leaves with stems) - RGBA
    W = 1024
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for _ in range(70):
        cx, cy = rng.uniform(120, W - 120), rng.uniform(120, W - 120)
        L = rng.uniform(90, 170)
        a = rng.uniform(0, math.tau)
        w = L * rng.uniform(0.32, 0.45)
        g = rng.uniform(0.55, 1.0)
        col = (int(40 * g + 10), int(110 * g + 20), int(25 * g), 255)
        pts = []
        for k in range(24):
            t = k / 23
            r = math.sin(t * math.pi) * w * (1 - 0.3 * t)
            pts.append((t * L, r))
        pts += [(x, -y) for x, y in reversed(pts)]
        rot = [(cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)) for x, y in pts]
        d.polygon(rot, fill=col)
        d.line([(cx, cy), (cx + L * 0.95 * math.cos(a), cy + L * 0.95 * math.sin(a))],
               fill=(int(90 * g), int(150 * g), int(60 * g), 255), width=2)
    im.save(os.path.join(TEX, "leaf_broad.png"))
    # palm leaflet strip (one long thin leaf, vertical)
    im = Image.new("RGBA", (128, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pts = [(64 + math.sin(t / 1023 * math.pi) * 52 * (1 - 0.55 * t / 1023), t) for t in range(0, 1024, 16)]
    pts += [(128 - x, y) for x, y in reversed(pts)]
    d.polygon(pts, fill=(70, 120, 35, 255))
    d.line([(64, 0), (64, 1023)], fill=(150, 160, 80, 255), width=4)
    im = im.filter(ImageFilter.GaussianBlur(0.6))
    im.save(os.path.join(TEX, "leaf_palm.png"))
    # fern frond card
    im = Image.new("RGBA", (512, 1024), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.line([(256, 1023), (256, 0)], fill=(60, 90, 30, 255), width=6)
    for k in range(40):
        y = 1000 - k * 24
        L = 220 * math.sin((k + 1) / 41 * math.pi) + 10
        for s in (-1, 1):
            d.polygon([(256, y), (256 + s * L, y - 30), (256 + s * L * 0.9, y - 8), (256, y + 8)],
                      fill=(55 + k, 120 + k, 30, 255))
    im.save(os.path.join(TEX, "leaf_fern.png"))


def leaf_material(name, image, tint=(1, 1, 1)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    n = N(m)
    tex = n.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX, image), check_existing=True)
    obj_info = n.new("ShaderNodeObjectInfo")
    hue = n.new("ShaderNodeHueSaturation")
    n.link(tex.outputs["Color"], hue.inputs["Color"])
    vary = n.math("MULTIPLY", n.math("SUBTRACT", obj_info.outputs["Random"], 0.5), 0.08)
    n.link(n.math("ADD", vary, 0.5), hue.inputs["Hue"])
    hue.inputs["Saturation"].default_value = 1.05
    hue.inputs["Value"].default_value = 0.9
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(hue.outputs[0], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.45
    p.inputs["Subsurface Weight"].default_value = 0.0
    trans = n.new("ShaderNodeBsdfTranslucent")
    n.link(hue.outputs[0], trans.inputs["Color"])
    mixs = n.new("ShaderNodeMixShader")
    mixs.inputs[0].default_value = 0.3            # light shines through the leaves
    n.link(p.outputs[0], mixs.inputs[1])
    n.link(trans.outputs[0], mixs.inputs[2])
    tr = n.new("ShaderNodeBsdfTransparent")
    alpha_mix = n.new("ShaderNodeMixShader")
    n.link(tex.outputs["Alpha"], alpha_mix.inputs[0])
    n.link(tr.outputs[0], alpha_mix.inputs[1])
    n.link(mixs.outputs[0], alpha_mix.inputs[2])
    n.link(alpha_mix.outputs[0], n.out.inputs["Surface"])
    return m


# ------------------------------------------------------------------ terrain + ocean

def terrain_material():
    m = bpy.data.materials.new("TerrainHQ")
    n = N(m)
    geo = n.new("ShaderNodeNewGeometry")
    tc = n.new("ShaderNodeTexCoord")
    pos = geo.outputs["Position"]
    sep = n.new("ShaderNodeSeparateXYZ")
    n.link(pos, sep.inputs[0])
    nsep = n.new("ShaderNodeSeparateXYZ")
    n.link(geo.outputs["Normal"], nsep.inputs[0])
    z, up = sep.outputs[2], nsep.outputs[2]
    big = n.noise(pos, 0.02, 6)
    mid = n.noise(pos, 0.25, 8)
    fine = n.noise(pos, 6.0, 4)
    # wind ripples in the sand
    rip = n.new("ShaderNodeTexWave", wave_type="BANDS")
    rip.inputs["Scale"].default_value = 1.2
    rip.inputs["Distortion"].default_value = 4.0
    n.link(pos, rip.inputs["Vector"])
    zj = n.math("ADD", z, n.math("MULTIPLY", n.math("SUBTRACT", big, 0.5), 6.0))   # wobbly height bands
    sand = n.mix(mid, (0.78, 0.69, 0.5), (0.9, 0.83, 0.66))
    wet = n.mix(mid, (0.42, 0.35, 0.25), (0.52, 0.45, 0.32))
    grass = n.mix(n.math("MULTIPLY", mid, big), (0.12, 0.24, 0.05), (0.3, 0.42, 0.12))
    jungle = n.mix(mid, (0.05, 0.09, 0.03), (0.16, 0.12, 0.05))          # dark floor + leaf litter
    rock = n.mix(n.noise(pos, 0.6, 8), (0.09, 0.085, 0.08), (0.24, 0.22, 0.2))     # dark volcanic rock
    moss = (0.12, 0.2, 0.06)
    col = n.mix(n.smooth(z, 0.2, 0.9), wet, sand)
    col = n.mix(n.smooth(zj, 2.0, 3.5), col, grass)
    col = n.mix(n.smooth(zj, 7.0, 11.0), col, jungle)
    steep = n.smooth(up, 0.82, 0.62)
    rockc = n.mix(n.smooth(up, 0.55, 0.85), rock, moss) if False else rock
    rock_mask = n.math("MAXIMUM", steep, n.smooth(zj, 125, 150))
    col = n.mix(rock_mask, col, rockc)
    col = n.mix(n.math("MULTIPLY", rock_mask, n.smooth(fine, 0.55, 0.7)), col, moss)
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(col, p.inputs["Base Color"])
    rough = n.mixf(n.smooth(z, 0.2, 0.9), 0.35, 0.92)
    n.link(rough, p.inputs["Roughness"])
    # bump: ripples on sand, grit everywhere, strong on rock
    h = n.math("ADD", n.math("MULTIPLY", rip.outputs["Fac"], n.math("SUBTRACT", 1.0, n.smooth(zj, 1.5, 3.0))), 0)
    h = n.math("ADD", h, n.math("MULTIPLY", fine, 0.6))
    h = n.math("ADD", h, n.math("MULTIPLY", n.noise(pos, 1.5, 10), n.math("MULTIPLY", rock_mask, 3.0)))
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.35
    bump.inputs["Distance"].default_value = 0.08
    n.link(h, bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.link(p.outputs[0], n.out.inputs["Surface"])
    return m


def build_terrain(h, step=2):
    hs = h[::step, ::step]
    n = hs.shape[0]
    xs = np.linspace(-I.SIZE / 2, I.SIZE / 2, n)
    bm = bmesh.new()
    vs = [[bm.verts.new((xs[j], xs[i], hs[i, j])) for j in range(n)] for i in range(n)]
    for i in range(n - 1):
        for j in range(n - 1):
            bm.faces.new((vs[i][j], vs[i][j + 1], vs[i + 1][j + 1], vs[i + 1][j]))
    ob = mesh_ob("TerrainHQ", bm, terrain_material())
    return ob


def build_ocean():
    m = bpy.data.materials.new("OceanHQ")
    n = N(m)
    p = n.new("ShaderNodeBsdfPrincipled")
    p.inputs["Base Color"].default_value = (0.8, 0.95, 1.0, 1)
    p.inputs["Roughness"].default_value = 0.02
    p.inputs["IOR"].default_value = 1.333
    p.inputs["Transmission Weight"].default_value = 1.0
    n.link(p.outputs[0], n.out.inputs["Surface"])
    vol = n.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (0.2, 0.75, 0.75, 1)
    vol.inputs["Density"].default_value = 0.035
    vol.inputs["Absorption Color"].default_value = (0.15, 0.7, 0.75, 1)
    n.link(vol.outputs[0], n.out.inputs["Volume"])
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=10)
    ob = mesh_ob("Ocean", bm, m)
    md = ob.modifiers.new("Ocean", "OCEAN")
    md.geometry_mode = "GENERATE"
    md.resolution = 10
    md.spatial_size = 60
    md.repeat_x = 28
    md.repeat_y = 28
    md.wave_scale = 0.35
    md.choppiness = 0.9
    md.wind_velocity = 9
    md.use_normals = True
    ob.location = (-840, -840, -0.3)
    # flat far sea out to the horizon (same water look)
    bm2 = bmesh.new()
    bmesh.ops.create_grid(bm2, x_segments=1, y_segments=1, size=20000)
    far = mesh_ob("FarSea", bm2, m)
    far.location = (0, 0, -0.45)
    # deep water floor so the sea gets darker far out
    return ob


# ------------------------------------------------------------------ vegetation

def tube(bm, pts, radii, segs=8):
    rings = []
    prev = None
    for i, (p, r) in enumerate(zip(pts, radii)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        nrm = (prev if prev is not None else t.orthogonal()).normalized()
        nrm = (nrm - t * nrm.dot(t)).normalized()
        prev = nrm
        b = t.cross(nrm)
        rings.append([bm.verts.new(p + (nrm * math.cos(a) + b * math.sin(a)) * r)
                      for a in [math.tau * k / segs for k in range(segs)]])
    for j in range(len(rings) - 1):
        for k in range(segs):
            bm.faces.new((rings[j][k], rings[j][(k + 1) % segs], rings[j + 1][(k + 1) % segs], rings[j + 1][k]))


def bark_material(name, c0, c1, rings=False):
    m = bpy.data.materials.new(name)
    n = N(m)
    tc = n.new("ShaderNodeTexCoord")
    pos = tc.outputs["Object"]
    if rings:
        w = n.new("ShaderNodeTexWave", wave_type="BANDS", bands_direction="Z")
        w.inputs["Scale"].default_value = 3.0
        w.inputs["Distortion"].default_value = 1.5
        n.link(pos, w.inputs["Vector"])
        f = w.outputs["Fac"]
    else:
        mp = n.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (6, 6, 0.6)
        n.link(pos, mp.inputs[0])
        f = n.noise(mp.outputs[0], 3, 10)
    col = n.mix(f, c0, c1)
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(col, p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.85
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.8
    n.link(f, bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.link(p.outputs[0], n.out.inputs["Surface"])
    return m


def make_palm(seed, protos):
    rng = random.Random(seed)
    bm = bmesh.new()
    H = rng.uniform(9, 13)
    lean = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized() * rng.uniform(0.8, 2.2)
    pts = [Vector((0, 0, 0)) + lean * (t / 10) ** 2 + Vector((0, 0, H * t / 10)) for t in range(11)]
    tube(bm, pts, [0.28 - 0.1 * t / 10 + (0.06 if t == 0 else 0) for t in range(11)], segs=10)
    trunk = mesh_ob(f"PalmTrunk{seed}", bm, protos["palm_bark"], "Protos")
    top = pts[-1]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    nf = rng.randint(11, 15)
    for f in range(nf):
        a = f / nf * math.tau + rng.uniform(-0.15, 0.15)
        droop = rng.uniform(0.2, 0.9)
        L = rng.uniform(3.6, 4.8)
        d = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-math.sin(a), math.cos(a), 0))
        spine = [top + d * (L * t) + Vector((0, 0, 0.9 * math.sin(t * math.pi * 0.6) - droop * 2.2 * t * t))
                 for t in [k / 14 for k in range(15)]]
        for k in range(1, 14):            # leaflets on both sides
            for s in (-1, 1):
                base = spine[k]
                ll = 0.9 * math.sin((k / 14) * math.pi) + 0.25
                tip = base + (side * s * 0.85 + (spine[k + 1] - spine[k - 1]).normalized() * 0.5).normalized() * ll \
                    + Vector((0, 0, -0.35 * ll))
                w = (spine[k + 1] - spine[k]).normalized() * 0.06
                v = [bm.verts.new(base - w), bm.verts.new(base + w), bm.verts.new(tip + w * 0.4), bm.verts.new(tip - w * 0.4)]
                fc = bm.faces.new(v)
                for lp, c in zip(fc.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
                    lp[uv].uv = c
    fronds = mesh_ob(f"PalmFronds{seed}", bm, protos["palm_leaf"], "Protos", smooth=False)
    # coconuts
    bm = bmesh.new()
    for k in range(rng.randint(2, 5)):
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.13, matrix=Matrix.Translation(
            top + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), -0.25))))
    nuts = mesh_ob(f"PalmNuts{seed}", bm, protos["coconut"], "Protos")
    c = bpy.data.collections.new(f"Palm{seed}")
    for o in (trunk, fronds, nuts):
        c.objects.link(o)
    return c


def make_jungle_tree(seed, protos):
    rng = random.Random(seed + 100)
    bm = bmesh.new()
    H = rng.uniform(14, 22)
    pts = [Vector((math.sin(t * 0.5) * 0.4, math.cos(t * 0.3) * 0.3, H * t / 10)) for t in range(11)]
    tube(bm, pts, [0.7 * (1 - 0.6 * t / 10) + (0.5 if t == 0 else 0) for t in range(11)], segs=10)
    branches_end = []
    for b in range(rng.randint(4, 6)):
        a = rng.uniform(0, math.tau)
        start = pts[rng.randint(6, 9)]
        d = Vector((math.cos(a), math.sin(a), rng.uniform(0.4, 0.9))).normalized()
        L = rng.uniform(3, 6)
        bp = [start + d * (L * t / 5) for t in range(6)]
        tube(bm, bp, [0.3 * (1 - t / 6) + 0.05 for t in range(6)], segs=6)
        branches_end.append(bp[-1])
    branches_end.append(pts[-1])
    trunk = mesh_ob(f"JTrunk{seed}", bm, protos["bark"], "Protos")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for c in branches_end:                 # leaf cards in a crown around each branch end
        for k in range(28):
            p = c + Vector((rng.gauss(0, 2.2), rng.gauss(0, 2.2), rng.gauss(0.5, 1.2)))
            nrm = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.2, 1))).normalized()
            t1 = nrm.orthogonal().normalized()
            t2 = nrm.cross(t1)
            s = rng.uniform(1.6, 2.6)
            v = [bm.verts.new(p + (t1 * x + t2 * y) * s) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            fc = bm.faces.new(v)
            for lp, cc in zip(fc.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
                lp[uv].uv = cc
    leaves = mesh_ob(f"JLeaves{seed}", bm, protos["broad_leaf"], "Protos", smooth=False)
    c = bpy.data.collections.new(f"Jungle{seed}")
    c.objects.link(trunk)
    c.objects.link(leaves)
    return c


def make_fern(seed, protos):
    rng = random.Random(seed + 500)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for k in range(rng.randint(7, 11)):
        a = k / 9 * math.tau + rng.uniform(-0.2, 0.2)
        d = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-math.sin(a), math.cos(a), 0)) * 0.35
        L = rng.uniform(0.9, 1.5)
        base = Vector((0, 0, 0.05))
        mid = d * L * 0.5 + Vector((0, 0, L * 0.45))
        tip = d * L + Vector((0, 0, L * 0.15))
        v = [bm.verts.new(base - side * 0.2), bm.verts.new(base + side * 0.2), bm.verts.new(mid + side),
             bm.verts.new(mid - side), bm.verts.new(tip + side * 0.3), bm.verts.new(tip - side * 0.3)]
        f1 = bm.faces.new((v[0], v[1], v[2], v[3]))
        f2 = bm.faces.new((v[3], v[2], v[4], v[5]))
        for lp, cc in zip(f1.loops, ((0, 0), (1, 0), (1, 0.5), (0, 0.5))):
            lp[uv].uv = cc
        for lp, cc in zip(f2.loops, ((0, 0.5), (1, 0.5), (1, 1), (0, 1))):
            lp[uv].uv = cc
    ob = mesh_ob(f"Fern{seed}", bm, protos["fern"], "Protos", smooth=False)
    c = bpy.data.collections.new(f"FernC{seed}")
    c.objects.link(ob)
    return c


def make_boulder(seed, protos):
    rng = random.Random(seed + 900)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1)
    off = Vector((rng.random() * 10, rng.random() * 10, rng.random() * 10))
    for v in bm.verts:
        v.co *= 1 + 0.35 * noise.noise(v.co * 1.4 + off) + 0.1 * noise.noise(v.co * 5 + off)
        v.co.z *= 0.6
    ob = mesh_ob(f"Boulder{seed}", bm, protos["rock"], "Protos")
    c = bpy.data.collections.new(f"BoulderC{seed}")
    c.objects.link(ob)
    return c


def scatter(height_at, protos_list, count, rng, zmin, zmax, clear, name, scale=(0.8, 1.2), sink=0.2, avoid_lines=True):
    placed = 0
    tries = 0
    while placed < count and tries < count * 30:
        tries += 1
        x, y = rng.uniform(-470, 470), rng.uniform(-440, 440)
        z = height_at(x, y)
        if z < zmin or z > zmax:
            continue
        if any(math.hypot(x - I.Z[k][0], y - I.Z[k][1]) < r for k, r in clear):
            continue
        if avoid_lines and any(I._seg_dist(x, y, a, b) < 14 for a, b in VIEWS_CLEAR):
            continue
        if _river_dist(x, y) < 9.5:           # nothing grows in the river channel
            continue
        inst = bpy.data.objects.new(name, None)
        inst.instance_type = "COLLECTION"
        inst.instance_collection = protos_list[rng.randrange(len(protos_list))]
        inst.location = (x, y, z - sink)
        s = rng.uniform(*scale)
        inst.scale = (s, s, s)
        inst.rotation_euler = (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), rng.uniform(0, math.tau))
        link(inst, "Vegetation")
        placed += 1
    return placed


_RIVER_KD = []


def _river_dist(x, y):
    """Distance (m) from a point to the slime river path."""
    if not _RIVER_KD:
        from mathutils import kdtree
        _, _, _, (rx, ry) = I.heightmap()
        kd = kdtree.KDTree(len(rx))
        for i, (a, b) in enumerate(zip(rx, ry)):
            kd.insert((float(a), float(b), 0.0), i)
        kd.balance()
        _RIVER_KD.append(kd)
    return _RIVER_KD[0].find((x, y, 0.0))[2]


def river_banks(height_at, ferns, rocks, rng, tree_scale=1.0):
    """Thick ferns and a few mossy boulders on both river banks (the channel sides were bare ground), from just
    outside the water to the top of the bank."""
    _, _, _, (rx, ry) = I.heightmap()
    placed = 0
    for i in range(0, len(rx) - 1, 1):
        x, y = float(rx[i]), float(ry[i])
        dx, dy = float(rx[i + 1]) - x, float(ry[i + 1]) - y
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L, dx / L
        for _ in range(int(3 * tree_scale)):
            side = rng.choice((-1, 1))
            o = side * rng.uniform(9.5, 15.0)
            px, py = x + nx * o + rng.uniform(-0.6, 0.6), y + ny * o + rng.uniform(-0.6, 0.6)
            if _river_dist(px, py) < 9.5:
                continue
            z = height_at(px, py)
            if z < 1.5:                      # river mouth / beach
                continue
            boulder = rng.random() < 0.06
            inst = bpy.data.objects.new("Boulder" if boulder else "Fern", None)
            inst.instance_type = "COLLECTION"
            inst.instance_collection = rng.choice(rocks if boulder else ferns)
            inst.location = (px, py, z - (0.4 if boulder else 0.05))
            sc = rng.uniform(0.5, 1.4) if boulder else rng.uniform(1.0, 2.0)
            inst.scale = (sc, sc, sc)
            inst.rotation_euler = (rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), rng.uniform(0, math.tau))
            link(inst, "Vegetation")
            placed += 1
    return placed


VIEWS_CLEAR = [((75, -420), (40, -268)), ((15, 110), (40, 250)), ((215, -40), (300, 70))]


# ------------------------------------------------------------------ buildings

def hotel_and_pool(height_at):
    stucco = bpy.data.materials.new("Stucco")
    n = N(stucco)
    tc = n.new("ShaderNodeTexCoord")
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(n.mix(n.noise(tc.outputs["Object"], 3, 8), (0.86, 0.83, 0.76), (0.95, 0.93, 0.88)), p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.8
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.15
    n.link(n.noise(tc.outputs["Object"], 40, 6), bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.link(p.outputs[0], n.out.inputs["Surface"])
    glass = simple("WindowGlass", (0.05, 0.1, 0.12), 0.02, 0.0, **{"Coat Weight": 1.0})
    frame = simple("WindowFrame", (0.2, 0.2, 0.22), 0.4, 0.8)
    rail = simple("RailGlass", (0.6, 0.8, 0.8), 0.02, **{"Transmission Weight": 0.9})
    roof = bpy.data.materials.new("Terracotta")
    n = N(roof)
    br = n.new("ShaderNodeTexBrick")
    br.inputs["Scale"].default_value = 30
    br.inputs["Color1"].default_value = (0.55, 0.22, 0.12, 1)
    br.inputs["Color2"].default_value = (0.42, 0.17, 0.09, 1)
    br.inputs["Mortar"].default_value = (0.25, 0.1, 0.06, 1)
    tc = n.new("ShaderNodeTexCoord")
    n.link(tc.outputs["Object"], br.inputs["Vector"])
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(br.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.7
    n.link(p.outputs[0], n.out.inputs["Surface"])
    base = height_at(40, -300)
    W_, D_, F = 90.0, 22.0, 3.6
    cx, cy = 40.0, -300.0
    for fl in range(5):
        z = base + fl * F
        box(f"H_Slab{fl}", (W_ + 0.6, D_ + 2.6, 0.35), (cx, cy, z), stucco)
        box(f"H_Core{fl}", (W_, D_ - 1.0, F - 0.35), (cx, cy, z + F / 2), glass)
        # piers between rooms + window frames
        for k in range(16):
            x = cx - W_ / 2 + k * (W_ / 15)
            box(f"H_Pier{fl}_{k}", (0.8, D_ + 0.2, F - 0.35), (x, cy, z + F / 2), stucco)
        for side in (-1, 1):
            box(f"H_Head{fl}{side}", (W_, 0.3, 0.6), (cx, cy + side * (D_ / 2 + 0.05), z + F - 0.5), stucco)
            box(f"H_Mullion{fl}{side}", (W_, 0.12, 0.08), (cx, cy + side * (D_ / 2 + 0.06), z + F * 0.45), frame)
        if fl > 0:   # balconies with glass railings on the pool side (north)
            box(f"H_Balc{fl}", (W_, 1.8, 0.25), (cx, cy + D_ / 2 + 0.9, z), stucco)
            box(f"H_Rail{fl}", (W_, 0.05, 1.0), (cx, cy + D_ / 2 + 1.75, z + 0.6), rail)
            box(f"H_RailTop{fl}", (W_, 0.08, 0.06), (cx, cy + D_ / 2 + 1.75, z + 1.1), frame)
    top = base + 5 * F
    box("H_Parapet", (W_ + 0.6, D_ + 2.6, 1.0), (cx, cy, top + 0.5), stucco)
    roof_ob = box("H_PenthouseRoof", (34, 18, 0.5), (cx, cy, top + 4.2), roof)
    box("H_Penthouse", (30, 14, 3.4), (cx, cy, top + 2.0), stucco)
    box("H_PenthouseGlass", (30.2, 14.2, 2.2), (cx, cy, top + 2.0), glass)
    neon = simple("Neon", (1, 0.3, 0.6), 0.3, **{"Emission Color": (1, 0.3, 0.6, 1), "Emission Strength": 6.0})
    box("H_Sign", (18, 0.3, 2.2), (cx, cy - D_ / 2 - 1.4, top - 1.3), neon)
    # pool deck, pool, loungers, umbrellas
    pb = height_at(40, -255)
    deck = bpy.data.materials.new("DeckTiles")
    n = N(deck)
    br = n.new("ShaderNodeTexBrick")
    br.inputs["Scale"].default_value = 0.6
    br.inputs["Color1"].default_value = (0.86, 0.8, 0.7, 1)
    br.inputs["Color2"].default_value = (0.8, 0.74, 0.64, 1)
    br.inputs["Mortar"].default_value = (0.6, 0.56, 0.5, 1)
    br.inputs["Mortar Size"].default_value = 0.01
    tc = n.new("ShaderNodeTexCoord")
    n.link(tc.outputs["Object"], br.inputs["Vector"])
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(br.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.55
    n.link(p.outputs[0], n.out.inputs["Surface"])
    pool_tile = simple("PoolTile", (0.1, 0.45, 0.7), 0.2)
    water = bpy.data.materials.new("PoolWater")
    n = N(water)
    p = n.new("ShaderNodeBsdfPrincipled")
    p.inputs["Roughness"].default_value = 0.01
    p.inputs["IOR"].default_value = 1.333
    p.inputs["Transmission Weight"].default_value = 1.0
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.15
    tc = n.new("ShaderNodeTexCoord")
    n.link(n.noise(tc.outputs["Object"], 1.5, 4), bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.link(p.outputs[0], n.out.inputs["Surface"])
    vol = n.new("ShaderNodeVolumeAbsorption")
    vol.inputs["Color"].default_value = (0.4, 0.9, 0.95, 1)
    vol.inputs["Density"].default_value = 0.3
    n.link(vol.outputs[0], n.out.inputs["Volume"])
    box("P_Deck", (110, 50, 0.4), (40, -255, pb), deck)
    box("P_Basin", (50, 20, 1.6), (30, -255, pb - 0.9), pool_tile)
    box("P_Water", (49.6, 19.6, 1.3), (30, -255, pb - 0.75), water)
    box("P_Coping", (51, 0.5, 0.15), (30, -245, pb + 0.25), stucco)
    box("P_Coping2", (51, 0.5, 0.15), (30, -265, pb + 0.25), stucco)
    bamboo = bark_material("Bamboo", (0.55, 0.42, 0.2), (0.7, 0.55, 0.28), rings=True)
    thatch = bpy.data.materials.new("Thatch")
    n = N(thatch)
    tc = n.new("ShaderNodeTexCoord")
    mp = n.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (30, 30, 1)
    n.link(tc.outputs["Object"], mp.inputs[0])
    f = n.noise(mp.outputs[0], 2, 12)
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(n.mix(f, (0.4, 0.3, 0.15), (0.7, 0.58, 0.35)), p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.95
    n.link(p.outputs[0], n.out.inputs["Surface"])
    box("P_SwimUpBar", (8, 4, 1.2), (52, -255, pb + 0.2), bamboo)
    roofbar = box("P_BarRoof", (11, 7, 0.6), (52, -255, pb + 3.6), thatch)
    for k in range(4):
        box(f"P_BarPost{k}", (0.3, 0.3, 3.4), (52 + (k % 2 * 2 - 1) * 4.5, -255 + (k // 2 * 2 - 1) * 2.5, pb + 1.7), bamboo)
    lounger = simple("LoungerFabric", (0.95, 0.95, 0.93), 0.8)
    umbrella = bpy.data.materials.new("UmbrellaStripes")
    n = N(umbrella)
    tc = n.new("ShaderNodeTexCoord")
    grad = n.new("ShaderNodeTexGradient", gradient_type="RADIAL")
    n.link(tc.outputs["Object"], grad.inputs["Vector"])
    stripes = n.math("SNAP", n.math("MULTIPLY", grad.outputs["Fac"], 16.0), 1.0)
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(n.mix(n.math("MODULO", stripes, 2.0), (0.95, 0.95, 0.9), (0.95, 0.35, 0.2)), p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.8
    n.link(p.outputs[0], n.out.inputs["Surface"])
    pole = simple("Pole", (0.85, 0.85, 0.85), 0.3, 0.8)
    for k in range(12):
        x = -14 + k * 8.5
        for row, y in enumerate((-240.5, -269.5)):
            bm = bmesh.new()
            bmesh.ops.create_cone(bm, cap_ends=False, segments=16, radius1=1.6, radius2=0.05, depth=0.5)
            u = mesh_ob(f"Umb{k}{row}", bm, umbrella)
            u.location = (x, y, pb + 2.6)
            box(f"UmbPole{k}{row}", (0.05, 0.05, 2.5), (x, y, pb + 1.3), pole)
            for s in (-1, 1):
                l = box(f"Lounger{k}{row}{s}", (0.7, 1.9, 0.12), (x + s * 0.9, y, pb + 0.45), lounger, bevel=0.05)
                l.rotation_euler = (math.radians(8), 0, 0)
    # beach huts
    dive = simple("DiveShopBlue", (0.15, 0.4, 0.65), 0.6)
    box("DiveShop", (10, 7, 3.5), (-30, -345, height_at(-30, -345) + 1.75), dive)
    box("DiveShopRoof", (12, 9, 0.7), (-30, -345, height_at(-30, -345) + 3.8), thatch)
    box("TikiBar", (7, 7, 1.2), (110, -345, height_at(110, -345) + 0.6), bamboo)
    box("TikiRoof", (10, 10, 0.8), (110, -345, height_at(110, -345) + 3.6), thatch)
    for k in range(4):
        box(f"TikiPost{k}", (0.3, 0.3, 3.4), (110 + (k % 2 * 2 - 1) * 3.2, -345 + (k // 2 * 2 - 1) * 3.2,
                                              height_at(110, -345) + 1.7), bamboo)


def waterfall_ruins_ship(height_at, river, structures=True):
    # waterfall sheet: stretched noise, white foam, slight green (slime runs down the waterfall)
    wf = bpy.data.materials.new("WaterfallHQ")
    n = N(wf)
    tc = n.new("ShaderNodeTexCoord")
    mp = n.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0, 1.0, 0.06)
    n.link(tc.outputs["Object"], mp.inputs[0])
    wave = n.new("ShaderNodeTexWave", wave_type="BANDS", bands_direction="X")
    wave.inputs["Scale"].default_value = 1.2
    wave.inputs["Distortion"].default_value = 9.0
    wave.inputs["Detail"].default_value = 8.0
    n.link(mp.outputs[0], wave.inputs["Vector"])
    streak = n.math("ADD", n.math("MULTIPLY", wave.outputs["Fac"], 0.7), n.math("MULTIPLY", n.noise(tc.outputs["Object"], 0.4, 6), 0.3))
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(n.mix(n.smooth(streak, 0.35, 0.75), (0.25, 0.45, 0.3), (0.97, 0.99, 0.97)), p.inputs["Base Color"])
    wb = n.new("ShaderNodeBump")
    wb.inputs["Strength"].default_value = 0.6
    n.link(streak, wb.inputs["Height"])
    n.link(wb.outputs[0], p.inputs["Normal"])
    p.inputs["Roughness"].default_value = 0.35
    p.inputs["Emission Color"].default_value = (0.9, 1.0, 0.9, 1)
    p.inputs["Emission Strength"].default_value = 0.15
    tr = n.new("ShaderNodeBsdfTransparent")
    mx = n.new("ShaderNodeMixShader")
    n.link(n.smooth(streak, 0.15, 0.35), mx.inputs[0])
    n.link(tr.outputs[0], mx.inputs[1])
    n.link(p.outputs[0], mx.inputs[2])
    n.link(mx.outputs[0], n.out.inputs["Surface"])
    top, bot = height_at(40, 262) + 0.5, height_at(40, 238)
    bm = bmesh.new()
    rows = []
    for j in range(41):
        t = j / 40
        z = top - (top - bot) * t
        row = []
        for i in range(13):
            x = 40 + (i / 12 - 0.5) * 30 * (1 + 0.3 * t)
            # find the cliff face at this height, put the water 1.5 m in front of it
            y = 275.0
            while y > 215 and height_at(x, y) > z:
                y -= 0.5
            row.append(bm.verts.new((x, y - 1.5 - 3 * t, z)))
        rows.append(row)
    for j in range(40):
        for i in range(12):
            bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    mesh_ob("Waterfall", bm, wf)
    # foam + mist at the bottom
    foam = simple("Foam", (0.95, 0.97, 0.97), 0.6)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1)
    for v in bm.verts:
        v.co *= 1 + 0.4 * noise.noise(v.co * 3)
    f = mesh_ob("Foam", bm, foam)
    f.location = (40, 246, bot + 0.2)
    f.scale = (22, 8, 1.2)
    mist = bpy.data.materials.new("Mist")
    n = N(mist)
    v = n.new("ShaderNodeVolumePrincipled")
    # soft mist: density fades out toward the edges (no visible box)
    tcm = n.new("ShaderNodeTexCoord")
    grad = n.new("ShaderNodeTexGradient", gradient_type="SPHERICAL")
    n.link(tcm.outputs["Generated"], grad.inputs["Vector"])
    dens = n.math("MULTIPLY", grad.outputs["Fac"], 0.03)
    n.link(dens, v.inputs["Density"])
    v.inputs["Color"].default_value = (1, 1, 1, 1)
    n.link(v.outputs[0], n.out.inputs["Volume"])
    bmm = bmesh.new()
    bmesh.ops.create_icosphere(bmm, subdivisions=3, radius=1)
    m = mesh_ob("Mist", bmm, mist)
    m.location = (40, 244, bot + 6)
    m.scale = (40, 18, 16)
    # plunge pool (slime green, glowing a little)
    slime = simple("SlimeWater", (0.3, 0.9, 0.2), 0.05, **{"Transmission Weight": 0.6,
                                                         "Emission Color": (0.3, 1, 0.2, 1), "Emission Strength": 0.4})
    box("PlungePool", (70, 34, 0.5), (40, 232, bot + 0.2), slime)
    # slime river: level water that steps down the slope, foam on the rapids (see island_assets.river_surface)
    import island_assets as IA
    IA.river_surface(height_at, river)
    if not structures:
        return
    # ruins: stone blocks + moss on top faces + vines
    stone = bpy.data.materials.new("TempleStoneHQ")
    n = N(stone)
    tc = n.new("ShaderNodeTexCoord")
    geo = n.new("ShaderNodeNewGeometry")
    nz = n.new("ShaderNodeSeparateXYZ")
    n.link(geo.outputs["Normal"], nz.inputs[0])
    br = n.new("ShaderNodeTexBrick")
    br.inputs["Scale"].default_value = 0.35
    br.inputs["Color1"].default_value = (0.24, 0.23, 0.19, 1)
    br.inputs["Color2"].default_value = (0.18, 0.17, 0.14, 1)
    br.inputs["Mortar"].default_value = (0.15, 0.14, 0.12, 1)
    n.link(tc.outputs["Object"], br.inputs["Vector"])
    moss = n.math("MULTIPLY", n.smooth(nz.outputs[2], 0.5, 0.9), n.smooth(n.noise(tc.outputs["Object"], 0.3, 6), 0.35, 0.6))
    moss = n.math("MAXIMUM", moss, n.math("MULTIPLY", n.smooth(n.noise(tc.outputs["Object"], 0.15, 6), 0.45, 0.6), 0.9))
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(n.mix(moss, br.outputs["Color"], (0.12, 0.22, 0.05)), p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.9
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.6
    n.link(n.math("ADD", br.outputs["Fac"], n.noise(tc.outputs["Object"], 3, 10)), bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.link(p.outputs[0], n.out.inputs["Surface"])
    rz = height_at(300, 70)
    rng = random.Random(4)
    for k in range(6):
        s = 40 - k * 6
        b = box(f"Temple{k}", (s, s, 3.5), (300, 70, rz + 1.75 + k * 3.5), stone, bevel=0.15)
        b.rotation_euler = (0, 0, math.radians(rng.uniform(-1, 1)))
    box("Shrine", (8, 8, 6), (300, 70, rz + 24), stone, bevel=0.2)
    box("Stairs", (6, 22, 0.4), (300, 52, rz + 9), stone, rot=(math.radians(52), 0, 0))
    glow = simple("FactoryGlowHQ", (0.3, 1, 0.2), 0.3, **{"Emission Color": (0.3, 1.0, 0.2, 1), "Emission Strength": 8.0})
    box("FactoryDoor", (5, 0.4, 6), (300, 49.6, rz + 3), glow)
    for k in range(6):   # fallen blocks
        box(f"Rubble{k}", (rng.uniform(1, 3), rng.uniform(1, 3), rng.uniform(0.8, 2)),
            (300 + rng.uniform(-30, 30), 70 + rng.uniform(-30, 30), rz + 0.5), stone,
            rot=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 3)), bevel=0.1)
    # crashed ship
    hull = bpy.data.materials.new("ShipHullHQ")
    n = N(hull)
    tc = n.new("ShaderNodeTexCoord")
    br = n.new("ShaderNodeTexBrick")
    br.inputs["Scale"].default_value = 0.2
    br.inputs["Mortar Size"].default_value = 0.005
    n.link(tc.outputs["Object"], br.inputs["Vector"])
    p = n.new("ShaderNodeBsdfPrincipled")
    n.link(n.mix(n.noise(tc.outputs["Object"], 0.5, 8), (0.45, 0.48, 0.5), (0.7, 0.72, 0.74)), p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 1.0
    p.inputs["Roughness"].default_value = 0.3
    bump = n.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.4
    n.link(br.outputs["Fac"], bump.inputs["Height"])
    n.link(bump.outputs[0], p.inputs["Normal"])
    n.link(p.outputs[0], n.out.inputs["Surface"])
    sz = height_at(40, 300)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=24, radius=30)
    bmesh.ops.scale(bm, vec=(1, 1, 0.22), verts=bm.verts)
    ship = mesh_ob("Ship", bm, hull)
    ship.location = (40, 305, sz + 3)
    ship.rotation_euler = (math.radians(14), math.radians(-9), 0.3)
    lights = simple("ShipLights", (0.4, 1, 0.4), 0.2, **{"Emission Color": (0.4, 1, 0.4, 1), "Emission Strength": 10})
    for k in range(16):
        a = k / 16 * math.tau
        l = box(f"ShipLight{k}", (1.2, 1.2, 0.6), (0, 0, 0), lights)
        l.parent = ship
        l.location = (math.cos(a) * 29, math.sin(a) * 29, 0)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=8)
    dome = mesh_ob("ShipDome", bm, simple("DomeGlass", (0.4, 0.9, 0.5), 0.02, **{"Transmission Weight": 0.8,
                                                                                "Emission Color": (0.3, 1, 0.3, 1),
                                                                                "Emission Strength": 1.0}))
    dome.parent = ship
    dome.location = (0, 0, 5)
    dome.scale = (1, 1, 0.6)


# ------------------------------------------------------------------ build + render

def build(tree_scale=1.0, v2=False):
    leaf_textures()
    X, Y, h, river = I.heightmap()
    def ha(x, y):
        """Bilinear terrain height (smooth: no steps between heightmap cells)."""
        n = h.shape[0] - 1
        fx = min(max((x + I.SIZE / 2) / I.SIZE * n, 0.0), n - 1e-6)
        fy = min(max((y + I.SIZE / 2) / I.SIZE * n, 0.0), n - 1e-6)
        ix, iy = int(fx), int(fy)
        tx, ty = fx - ix, fy - iy
        return float(h[iy, ix] * (1 - tx) * (1 - ty) + h[iy, ix + 1] * tx * (1 - ty) +
                     h[iy + 1, ix] * (1 - tx) * ty + h[iy + 1, ix + 1] * tx * ty)
    build_terrain(h)
    build_ocean()
    coll("Protos", hide=True)
    protos = dict(palm_bark=bark_material("PalmBark", (0.3, 0.24, 0.17), (0.5, 0.42, 0.3), rings=True),
                  bark=bark_material("JungleBark", (0.18, 0.14, 0.1), (0.35, 0.3, 0.24)),
                  palm_leaf=leaf_material("PalmLeaf", "leaf_palm.png"),
                  broad_leaf=leaf_material("BroadLeaf", "leaf_broad.png"),
                  fern=leaf_material("FernLeaf", "leaf_fern.png"),
                  coconut=simple("CoconutRaw", (0.25, 0.3, 0.1), 0.6),
                  rock=bpy.data.materials.get("TerrainHQ"))
    palms = [make_palm(s, protos) for s in range(6)]
    jungle = [make_jungle_tree(s, protos) for s in range(6)]
    ferns = [make_fern(s, protos) for s in range(4)]
    rocks = [make_boulder(s, protos) for s in range(5)]
    rng = random.Random(11)
    clear = (("hotel", 70), ("pool", 62), ("golf", 115), ("ruins", 38), ("ship", 45), ("waterfall", 26))
    if v2:
        clear = clear + (("cave", 24),)
    n1 = scatter(ha, palms, int(900 * tree_scale), rng, 0.6, 9, clear, "Palm", (0.8, 1.15), 0.3)
    n2 = scatter(ha, jungle, int(3200 * tree_scale), rng, 6, 150, clear, "Jungle", (0.7, 1.3), 0.5)
    n3 = scatter(ha, ferns, int(14000 * tree_scale), rng, 2.5, 150, clear[:5], "Fern", (0.8, 1.8), 0.05, False)
    n4 = scatter(ha, rocks, int(500 * tree_scale), rng, -2, 4, clear[:3], "Boulder", (0.6, 2.5), 0.4, False)
    n5 = river_banks(ha, ferns, rocks, rng, tree_scale)
    # palms around the hotel and pool (resort landscaping)
    for k in range(26):
        x = -20 + (k % 13) * 10
        y = -232 if k < 13 else -278
        inst = bpy.data.objects.new("ResortPalm", None)
        inst.instance_type = "COLLECTION"
        inst.instance_collection = palms[k % len(palms)]
        inst.location = (x + rng.uniform(-1.5, 1.5), y, ha(x, y))
        inst.rotation_euler = (0, 0, rng.uniform(0, math.tau))
        link(inst, "Vegetation")
    if not v2:
        hotel_and_pool(ha)
    waterfall_ruins_ship(ha, river, structures=not v2)
    print(f"[IslandHQ] palms {n1}, jungle trees {n2}, ferns {n3}, boulders {n4}, river bank plants {n5}")
    return ha


def setup_render(samples, res):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 8
    sc.cycles.transparent_max_bounces = 16
    sc.cycles.transmission_bounces = 8
    sc.cycles.volume_bounces = 1
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = "AgX"
    try:
        sc.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    sc.view_settings.exposure = -2.4
    w = bpy.data.worlds.new("SkyHQ")
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes["Background"]
    sky = nt.nodes.new("ShaderNodeTexSky")
    for t in ("MULTIPLE_SCATTERING", "NISHITA", "SINGLE_SCATTERING", "HOSEK_WILKIE"):
        try:
            sky.sky_type = t
            break
        except TypeError:
            continue
    for attr, val in (("sun_elevation", math.radians(32)), ("sun_rotation", math.radians(215)),
                      ("air_density", 1.0), ("dust_density", 2.0), ("altitude", 10.0)):
        if hasattr(sky, attr):
            setattr(sky, attr, val)
    nt.links.new(sky.outputs[0], bg.inputs[0])
    bg.inputs[1].default_value = 0.25
    sc.world = w
    sun = bpy.data.objects.new("SunHQ", bpy.data.lights.new("SunHQ", "SUN"))
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.93, 0.82)
    sun.data.angle = math.radians(0.8)
    sun.rotation_euler = (math.radians(58), 0, math.radians(215 + 180 - 90))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("CamHQ", bpy.data.cameras.new("CamHQ"))
    cam.data.clip_end = 6000
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


SHOTS = [
    ("island_hq_hotel_from_beach.png", (75, -420, 28), (40, -268, 4), 24),
    ("island_hq_waterfall.png", (15, 110, 95), (40, 252, 120), 30),
    ("island_hq_ruins.png", (215, -40, 45), (300, 70, 26), 28),
    ("island_hq_overview.png", (-520, -700, 300), (20, 20, 0), 32),
]


def render(ha, out_dir, samples=96, res=(1600, 900), only=None):
    cam = setup_render(samples, res)
    os.makedirs(out_dir, exist_ok=True)
    sc = bpy.context.scene
    for name, loc, tgt, lens in SHOTS:
        if only and name not in only:
            continue
        loc = Vector(loc)
        loc.z = max(loc.z, ha(loc.x, loc.y) + 2.5)
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(tgt) - loc).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        print("[IslandHQ] rendered", name)
