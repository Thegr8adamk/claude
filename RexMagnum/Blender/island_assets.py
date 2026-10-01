"""Rex Magnum - detailed island assets (v2). Each builder makes one collection in LOCAL coordinates
(origin = ground point at the asset center, +Y = north, Z up). Parts rest on / against each other.

Assets: Hotel, PoolArea, DiveShop, TikiBar, BeachProps, Pier, Ruins, Cave, Ship, WaterfallRocks.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))


# ------------------------------------------------------------------ helpers

class Kit:
    def __init__(self, name):
        self.name = name
        c = bpy.data.collections.get(name)
        if c:
            for o in list(c.all_objects):
                bpy.data.objects.remove(o, do_unlink=True)
        else:
            c = bpy.data.collections.new(name)
            bpy.context.scene.collection.children.link(c)
        self.c = c
        self.n = 0

    def _ob(self, base, bm, mat, smooth=False):
        self.n += 1
        me = bpy.data.meshes.new(f"{base}{self.n}")
        bm.to_mesh(me)
        bm.free()
        if mat:
            me.materials.append(mat)
        for p in me.polygons:
            p.use_smooth = smooth
        ob = bpy.data.objects.new(f"{self.name}_{base}{self.n}", me)
        self.c.objects.link(ob)
        return ob

    def box(self, lo, hi, mat, base="Box", bevel=0.0):
        """Axis box from corner lo to corner hi (exact contact surfaces)."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        size = Vector(hi) - Vector(lo)
        bmesh.ops.scale(bm, vec=size, verts=bm.verts)
        bmesh.ops.translate(bm, vec=(Vector(hi) + Vector(lo)) / 2, verts=bm.verts)
        if bevel:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.45), segments=2,
                            affect="EDGES", profile=0.5)
        return self._ob(base, bm, mat)

    def boxc(self, center, size, mat, rot=0.0, base="Box", bevel=0.0):
        ob = self.box([-s / 2 for s in size], [s / 2 for s in size], mat, base, bevel)
        ob.location = center
        ob.rotation_euler = (0, 0, rot) if isinstance(rot, (int, float)) else rot
        return ob

    def cyl(self, p0, p1, r, mat, segs=16, r1=None, base="Cyl", caps=True):
        """Cylinder (or cone) from point p0 to p1."""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=caps, segments=segs, radius1=r, radius2=r if r1 is None else r1,
                              depth=d.length)
        rot = d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation((p0 + p1) / 2) @ rot, verts=bm.verts)
        return self._ob(base, bm, mat, smooth=True)

    def tube(self, pts, r, mat, segs=10, base="Tube"):
        pts = [Vector(p) for p in pts]
        bm = bmesh.new()
        rings = []
        prev = None
        for i, p in enumerate(pts):
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
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        return self._ob(base, bm, mat, smooth=True)

    def bamboo(self, p0, p1, r, mat, seg_len=0.4, segs=12, base="Bamboo"):
        """Bamboo pole: slightly tapered tube with bulging nodes every seg_len."""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        L = d.length
        prof = []
        n = max(1, int(L / seg_len))
        rng = random.Random(int(p0.x * 100 + p0.y * 37))
        for i in range(n):
            z0, z1 = i * L / n, (i + 1) * L / n
            rr = r * (1 - 0.15 * i / n)
            prof += [(rr, z0 + 0.01), (rr * 0.97, (z0 + z1) / 2), (rr, z1 - 0.025), (rr * 1.12, z1 - 0.01)]
        bm = bmesh.new()
        rings = []
        for rr, z in prof:
            rings.append([bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)) for a in
                          [math.tau * k / segs for k in range(segs)]])
        for j in range(len(rings) - 1):
            for k in range(segs):
                bm.faces.new((rings[j][k], rings[j][(k + 1) % segs], rings[j + 1][(k + 1) % segs], rings[j + 1][k]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        rot = d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation(p0) @ rot @ Matrix.Rotation(rng.uniform(0, 6), 4, "Z"),
                            verts=bm.verts)
        return self._ob(base, bm, mat, smooth=True)

    def disc(self, center, r, h, mat, segs=32, base="Disc", r_top=None):
        c = Vector(center)
        return self.cyl(c, c + Vector((0, 0, h)), r, mat, segs, r_top, base)

    def rock(self, center, size, mat, seed, base="Rock", detail=3, boxy=False):
        rng = random.Random(seed)
        bm = bmesh.new()
        if boxy:   # blocky stone: subdivided cube, slightly rounded
            bmesh.ops.create_cube(bm, size=1.6)
            bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=4, use_grid_fill=True)
            for v in bm.verts:
                v.co = v.co.lerp(v.co.normalized(), 0.25)
            bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(rng.uniform(0, 3), 3, "Z") @
                             Matrix.Rotation(rng.uniform(-0.3, 0.3), 3, "X"))
        else:
            bmesh.ops.create_icosphere(bm, subdivisions=detail, radius=1)
        off = Vector((rng.random() * 50, rng.random() * 50, rng.random() * 50))
        for v in bm.verts:
            k = 1 + 0.3 * noise.noise(v.co * 1.3 + off) + 0.12 * noise.noise(v.co * 4 + off)
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) * k
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        return self._ob(base, bm, mat, smooth=True)

    def text(self, body, size, loc, mat, extrude=0.05, rot=(math.pi / 2, 0, 0), base="Text"):
        cu = bpy.data.curves.new(f"{self.name}_txt", "FONT")
        cu.body = body
        cu.size = size
        cu.extrude = extrude
        cu.align_x = "CENTER"
        tmp = bpy.data.objects.new("tmp_txt", cu)
        self.c.objects.link(tmp)
        tmp.location = loc
        tmp.rotation_euler = rot
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg), depsgraph=dg)
        me.materials.clear()
        me.materials.append(mat)
        ob = bpy.data.objects.new(f"{self.name}_{base}", me)
        self.c.objects.link(ob)
        ob.matrix_world = tmp.matrix_world.copy()
        bpy.data.objects.remove(tmp)
        return ob


# ------------------------------------------------------------------ materials

def _mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m, None
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    return m, m.node_tree


def principled(name, color, rough=0.5, metal=0.0, **kw):
    m, nt = _mat(name)
    if nt is None:
        return m
    b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    for k, v in kw.items():
        b.inputs[k].default_value = v
    return m


def textured(name, kind, c0, c1, rough=0.6, scale=1.0, bump=0.3, metal=0.0, extra=None):
    """Procedural surfaces in world/object space: stucco, planks, thatch, stone blocks, bamboo, sand, tiles..."""
    m, nt = _mat(name)
    if nt is None:
        return m
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    nt.links.new(tc.outputs["Object"], mp.inputs[0])
    mp.inputs["Scale"].default_value = (scale, scale, scale)
    fac = None
    if kind in ("planks", "stone", "tiles"):
        # floors use (x, y); walls use (x + y, z) so planks / stone courses run horizontally on every face
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(mp.outputs[0], sep.inputs[0])
        xy = nt.nodes.new("ShaderNodeMath")
        xy.operation = "ADD"
        nt.links.new(sep.outputs[0], xy.inputs[0])
        nt.links.new(sep.outputs[1], xy.inputs[1])
        wall = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(xy.outputs[0], wall.inputs[0])
        nt.links.new(sep.outputs[2], wall.inputs[1])
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        gs = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Normal"], gs.inputs[0])
        ab = nt.nodes.new("ShaderNodeMath")
        ab.operation = "ABSOLUTE"
        nt.links.new(gs.outputs[2], ab.inputs[0])
        stp = nt.nodes.new("ShaderNodeMath")
        stp.operation = "GREATER_THAN"
        stp.inputs[1].default_value = 0.6
        nt.links.new(ab.outputs[0], stp.inputs[0])
        sw = nt.nodes.new("ShaderNodeMix")
        sw.data_type = "VECTOR"
        nt.links.new(stp.outputs[0], sw.inputs[0])
        nt.links.new(wall.outputs[0], sw.inputs[4])
        nt.links.new(mp.outputs[0], sw.inputs[5])
        vec = sw.outputs[1]
    else:
        vec = mp.outputs[0]
    if kind == "planks":
        br = nt.nodes.new("ShaderNodeTexBrick")
        br.inputs["Scale"].default_value = 1.0
        br.inputs["Brick Width"].default_value = 2.0
        br.inputs["Row Height"].default_value = 0.18
        br.inputs["Mortar Size"].default_value = 0.004
        br.inputs["Color1"].default_value = (*c0, 1)
        br.inputs["Color2"].default_value = (*c1, 1)
        br.inputs["Mortar"].default_value = (0.05, 0.035, 0.02, 1)
        nt.links.new(vec, br.inputs["Vector"])
        grain = nt.nodes.new("ShaderNodeTexWave")
        grain.inputs["Scale"].default_value = 8
        grain.inputs["Distortion"].default_value = 6
        nt.links.new(vec, grain.inputs["Vector"])
        mx = nt.nodes.new("ShaderNodeMix")
        mx.data_type = "RGBA"
        mx.blend_type = "MULTIPLY"
        mx.inputs[0].default_value = 0.35
        nt.links.new(br.outputs["Color"], mx.inputs[6])
        nt.links.new(grain.outputs["Color"], mx.inputs[7])
        nt.links.new(mx.outputs[2], p.inputs["Base Color"])
        fac = br.outputs["Fac"]
    elif kind in ("stone", "tiles"):
        br = nt.nodes.new("ShaderNodeTexBrick")
        br.inputs["Scale"].default_value = 1.0
        br.inputs["Brick Width"].default_value = 1.0 if kind == "tiles" else 1.4
        br.inputs["Row Height"].default_value = 1.0 if kind == "tiles" else 0.6
        br.inputs["Mortar Size"].default_value = 0.02 if kind == "tiles" else 0.03
        br.offset = 0.0 if kind == "tiles" else 0.5
        br.inputs["Color1"].default_value = (*c0, 1)
        br.inputs["Color2"].default_value = (*c1, 1)
        br.inputs["Mortar"].default_value = (c0[0] * 0.5, c0[1] * 0.5, c0[2] * 0.5, 1)
        nt.links.new(vec, br.inputs["Vector"])
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 6
        nz.inputs["Detail"].default_value = 10
        nt.links.new(vec, nz.inputs["Vector"])
        col = br.outputs["Color"]
        if kind == "stone":   # moss on top faces and in patches
            geo = nt.nodes.new("ShaderNodeNewGeometry")
            sep = nt.nodes.new("ShaderNodeSeparateXYZ")
            nt.links.new(geo.outputs["Normal"], sep.inputs[0])
            up = nt.nodes.new("ShaderNodeMapRange")
            up.inputs["From Min"].default_value = 0.4
            up.inputs["From Max"].default_value = 0.9
            nt.links.new(sep.outputs[2], up.inputs["Value"])
            pz = nt.nodes.new("ShaderNodeTexNoise")
            pz.inputs["Scale"].default_value = 0.6
            nt.links.new(vec, pz.inputs["Vector"])
            pm = nt.nodes.new("ShaderNodeMapRange")
            pm.inputs["From Min"].default_value = 0.5
            pm.inputs["From Max"].default_value = 0.62
            nt.links.new(pz.outputs["Fac"], pm.inputs["Value"])
            mm = nt.nodes.new("ShaderNodeMath")
            mm.operation = "MAXIMUM"
            nt.links.new(up.outputs[0], mm.inputs[0])
            nt.links.new(pm.outputs[0], mm.inputs[1])
            moss = nt.nodes.new("ShaderNodeMix")
            moss.data_type = "RGBA"
            nt.links.new(mm.outputs[0], moss.inputs[0])
            nt.links.new(col, moss.inputs[6])
            moss.inputs[7].default_value = (0.1, 0.2, 0.04, 1)
            col = moss.outputs[2]
        nt.links.new(col, p.inputs["Base Color"])
        add = nt.nodes.new("ShaderNodeMath")
        add.operation = "ADD"
        nt.links.new(br.outputs["Fac"], add.inputs[0])
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = 0.4
        nt.links.new(nz.outputs["Fac"], mul.inputs[0])
        nt.links.new(mul.outputs[0], add.inputs[1])
        fac = add.outputs[0]
    else:   # noise-based: stucco, thatch, bamboo, sand, rock, metal
        st = {"thatch": (1, 1, 30), "bamboo": (1, 1, 0.15), "rock": (1, 1, 1), "stucco": (1, 1, 1),
              "metal": (1, 1, 1), "sand": (1, 1, 1)}.get(kind, (1, 1, 1))
        mp.inputs["Scale"].default_value = (scale * st[0], scale * st[1], scale * st[2])
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = {"thatch": 6, "bamboo": 3, "rock": 1.5, "stucco": 3, "metal": 4,
                                            "sand": 30}.get(kind, 3)
        nz.inputs["Detail"].default_value = 12
        nt.links.new(mp.outputs[0], nz.inputs["Vector"])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (*c0, 1)
        ramp.color_ramp.elements[1].color = (*c1, 1)
        nt.links.new(nz.outputs["Fac"], ramp.inputs[0])
        nt.links.new(ramp.outputs[0], p.inputs["Base Color"])
        fac = nz.outputs["Fac"]
        if kind == "bamboo":   # node rings
            w = nt.nodes.new("ShaderNodeTexWave")
            w.wave_type = "BANDS"
            w.bands_direction = "Z"
            w.wave_profile = "SAW"
            w.inputs["Scale"].default_value = 2.5
            nt.links.new(tc.outputs["Object"], w.inputs["Vector"])
            fac = w.outputs["Fac"]
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if extra:
        for k, v in extra.items():
            p.inputs[k].default_value = v
    if fac is not None and bump > 0:
        b = nt.nodes.new("ShaderNodeBump")
        b.inputs["Strength"].default_value = bump
        b.inputs["Distance"].default_value = 0.02
        nt.links.new(fac, b.inputs["Height"])
        nt.links.new(b.outputs[0], p.inputs["Normal"])
    nt.links.new(p.outputs[0], out.inputs["Surface"])
    return m


def water(name="WaterClear", tint=(0.35, 0.9, 0.95), density=0.25, rough=0.02, emission=None):
    m, nt = _mat(name)
    if nt is None:
        return m
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    p.inputs["Roughness"].default_value = rough
    p.inputs["IOR"].default_value = 1.333
    p.inputs["Transmission Weight"].default_value = 1.0
    if emission:
        p.inputs["Emission Color"].default_value = (*emission, 1)
        p.inputs["Emission Strength"].default_value = 0.4
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.8
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    b = nt.nodes.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = 0.12
    nt.links.new(nz.outputs["Fac"], b.inputs["Height"])
    nt.links.new(b.outputs[0], p.inputs["Normal"])
    nt.links.new(p.outputs[0], out.inputs["Surface"])
    v = nt.nodes.new("ShaderNodeVolumeAbsorption")
    v.inputs["Color"].default_value = (*tint, 1)
    v.inputs["Density"].default_value = density
    nt.links.new(v.outputs[0], out.inputs["Volume"])
    return m


def _apply(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), depsgraph=dg)
    ob.modifiers.clear()
    ob.data = me


def falling_water(name):
    """White streaks + clear gaps, stretched along Z (falling direction)."""
    m, nt = _mat(name)
    if nt is None:
        return m
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (2.5, 2.5, 0.06)
    nt.links.new(tc.outputs["Object"], mp.inputs[0])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 3
    nz.inputs["Detail"].default_value = 8
    nt.links.new(mp.outputs[0], nz.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    ramp.inputs["From Min"].default_value = 0.5
    ramp.inputs["From Max"].default_value = 0.78
    nt.links.new(nz.outputs["Fac"], ramp.inputs["Value"])
    white = nt.nodes.new("ShaderNodeBsdfPrincipled")
    white.inputs["Base Color"].default_value = (0.92, 0.96, 0.97, 1)
    white.inputs["Roughness"].default_value = 0.35
    white.inputs["Subsurface Weight"].default_value = 0.3
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    haze = nt.nodes.new("ShaderNodeMath")
    haze.operation = "MULTIPLY_ADD"
    haze.inputs[1].default_value = 0.75
    haze.inputs[2].default_value = 0.12
    nt.links.new(ramp.outputs[0], haze.inputs[0])
    nt.links.new(haze.outputs[0], mix.inputs[0])
    nt.links.new(clear.outputs[0], mix.inputs[1])
    nt.links.new(white.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    return m


def weather(m, grime=(0.32, 0.3, 0.26), streaks=0.55, crevice=0.6, base_dirt=0.5, scale=1.0):
    """Make a clean CG material look lived-in: dirt in crevices (AO), rain streaks running down (world Z),
    dirt splash near the ground, and broad colour blotches. Works on any material with a Principled BSDF."""
    if m.get("weathered"):
        return m
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    src = p.inputs["Base Color"].links[0].from_socket if p.inputs["Base Color"].links else None
    if src is None:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = p.inputs["Base Color"].default_value
        src = rgb.outputs[0]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    # rain streaks: noise stretched along Z
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (2.2 * scale, 2.2 * scale, 0.12 * scale)
    nt.links.new(tc.outputs["Object"], mp.inputs[0])
    st = nt.nodes.new("ShaderNodeTexNoise")
    st.inputs["Scale"].default_value = 2.0
    st.inputs["Detail"].default_value = 6
    nt.links.new(mp.outputs[0], st.inputs["Vector"])
    stm = nt.nodes.new("ShaderNodeMapRange")
    stm.inputs["From Min"].default_value = 0.5
    stm.inputs["From Max"].default_value = 0.75
    stm.inputs["To Max"].default_value = streaks
    nt.links.new(st.outputs["Fac"], stm.inputs["Value"])
    # crevice dirt from ambient occlusion
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.inputs["Distance"].default_value = 0.6
    ao.samples = 8
    aom = nt.nodes.new("ShaderNodeMapRange")
    aom.inputs["From Min"].default_value = 1.0
    aom.inputs["From Max"].default_value = 0.4
    aom.inputs["To Max"].default_value = crevice
    nt.links.new(ao.outputs["AO"], aom.inputs["Value"])
    # ground splash: object Z near 0
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    gm = nt.nodes.new("ShaderNodeMapRange")
    gm.inputs["From Min"].default_value = 0.0
    gm.inputs["From Max"].default_value = 0.7
    gm.inputs["To Min"].default_value = base_dirt
    gm.inputs["To Max"].default_value = 0.0
    nt.links.new(sep.outputs[2], gm.inputs["Value"])
    mx1 = nt.nodes.new("ShaderNodeMath")
    mx1.operation = "MAXIMUM"
    nt.links.new(stm.outputs[0], mx1.inputs[0])
    nt.links.new(aom.outputs[0], mx1.inputs[1])
    mx2 = nt.nodes.new("ShaderNodeMath")
    mx2.operation = "MAXIMUM"
    nt.links.new(mx1.outputs[0], mx2.inputs[0])
    nt.links.new(gm.outputs[0], mx2.inputs[1])
    # broad blotches (sun fading / patching) multiply the clean colour slightly
    bl = nt.nodes.new("ShaderNodeTexNoise")
    bl.inputs["Scale"].default_value = 0.35 * scale
    bl.inputs["Detail"].default_value = 3
    nt.links.new(tc.outputs["Object"], bl.inputs["Vector"])
    blm = nt.nodes.new("ShaderNodeMapRange")
    blm.inputs["To Min"].default_value = 0.82
    blm.inputs["To Max"].default_value = 1.08
    nt.links.new(bl.outputs["Fac"], blm.inputs["Value"])
    mulc = nt.nodes.new("ShaderNodeVectorMath")
    mulc.operation = "SCALE"
    nt.links.new(src, mulc.inputs[0])
    nt.links.new(blm.outputs[0], mulc.inputs["Scale"])
    dirty = nt.nodes.new("ShaderNodeMix")
    dirty.data_type = "RGBA"
    dirty.blend_type = "MULTIPLY"
    nt.links.new(mx2.outputs[0], dirty.inputs[0])
    nt.links.new(mulc.outputs[0], dirty.inputs[6])
    dirty.inputs[7].default_value = (*grime, 1)
    nt.links.new(dirty.outputs[2], p.inputs["Base Color"])
    # dirt is rougher
    if not p.inputs["Roughness"].links:
        r0 = p.inputs["Roughness"].default_value
        rr = nt.nodes.new("ShaderNodeMapRange")
        rr.inputs["To Min"].default_value = r0
        rr.inputs["To Max"].default_value = min(1.0, r0 + 0.35)
        nt.links.new(mx2.outputs[0], rr.inputs["Value"])
        nt.links.new(rr.outputs[0], p.inputs["Roughness"])
    m["weathered"] = True
    return m


def straw_material():
    m, nt = _mat("A_Straw")
    if nt is None:
        return m
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    at = nt.nodes.new("ShaderNodeAttribute")
    at.attribute_name = "StrawTone"
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].color = (0.16, 0.11, 0.05, 1)
    e[1].color = (0.62, 0.5, 0.3, 1)
    e.new(0.55).color = (0.42, 0.32, 0.17, 1)
    nt.links.new(at.outputs["Fac"], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.85
    p.inputs["Subsurface Weight"].default_value = 0.15
    tc = nt.nodes.new("ShaderNodeTexCoord")
    w = nt.nodes.new("ShaderNodeTexWave")
    w.inputs["Scale"].default_value = 40
    w.inputs["Distortion"].default_value = 3
    nt.links.new(tc.outputs["UV"], w.inputs["Vector"])
    b = nt.nodes.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = 0.5
    nt.links.new(w.outputs["Fac"], b.inputs["Height"])
    nt.links.new(b.outputs[0], p.inputs["Normal"])
    nt.links.new(p.outputs[0], out.inputs["Surface"])
    return m


def cone_quads(cx, cy, r0, z0, r1, z1, n=28):
    out = []
    for i in range(n):
        a, b = math.tau * i / n, math.tau * (i + 1) / n
        out.append(((cx + math.cos(b) * r0, cy + math.sin(b) * r0, z0), (cx + math.cos(a) * r0, cy + math.sin(a) * r0, z0),
                    (cx + math.cos(b) * r1, cy + math.sin(b) * r1, z1), (cx + math.cos(a) * r1, cy + math.sin(a) * r1, z1)))
    return out


def underlay():
    return principled("A_ThatchUnder", (0.1, 0.07, 0.035), 0.95)


def straw_roof(k, quads, seed=0, row=0.22, width=0.05, length=0.65, overhang=0.18, lift=0.06):
    """Cover roof faces with overlapping straw strips (like real palm-thatch).
    quads: list of (eaveA, eaveB, topA, topB) world points (topA == topB for triangles)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    tone = bm.verts.layers.float.new("StrawTone")
    uvl = bm.loops.layers.uv.new("UVMap")
    for ea, eb, ta, tb in quads:
        ea, eb, ta, tb = Vector(ea), Vector(eb), Vector(ta), Vector(tb)
        nrm = (eb - ea).cross(ta - ea).normalized()
        if nrm.z < 0:
            nrm = -nrm
        slope = ((ta + tb) / 2 - (ea + eb) / 2).length
        rows = max(1, int(slope / row))
        for r in range(rows + 1):
            v = r / rows
            a = ea.lerp(ta, v)
            b = eb.lerp(tb, v)
            span = (b - a).length
            n = max(1, int(span / (width * 0.7)))
            for i in range(n):
                u = (i + rng.random() * 0.6) / n
                top = a.lerp(b, u)
                eave = ea.lerp(eb, u)
                down = (eave - top)
                if down.length < 1e-4:
                    continue
                down.normalize()
                side = down.cross(nrm).normalized()
                L = length * rng.uniform(0.8, 1.2) + (overhang if r == 0 else 0)
                L = min(L, (top - eave).length + overhang) if r > 0 else overhang + rng.uniform(0, 0.1)
                if r == 0:
                    top = eave - down * 0.3
                    L = overhang + 0.3 + rng.uniform(0, 0.1)
                w = width * rng.uniform(0.6, 1.3)
                h = lift * (1 - v * 0.5) + rng.uniform(0, 0.025)
                twist = rng.uniform(-0.15, 0.15)
                d2 = (down + side * twist).normalized()
                t = rng.random()
                vs = []
                for s_ in range(4):
                    f = s_ / 3
                    c = top + d2 * L * f + nrm * (h - 0.05 * f * f) - Vector((0, 0, 0.04 * f * f))
                    ww = w * (1 - 0.6 * f)
                    vs.append((bm.verts.new(c - side * ww / 2), bm.verts.new(c + side * ww / 2)))
                for s_ in range(3):
                    fc = bm.faces.new((vs[s_][0], vs[s_][1], vs[s_ + 1][1], vs[s_ + 1][0]))
                    for lp, uvv in zip(fc.loops, ((0, s_ / 3), (1, s_ / 3), (1, (s_ + 1) / 3), (0, (s_ + 1) / 3))):
                        lp[uvl].uv = uvv
                for pair in vs:
                    for vv in pair:
                        vv[tone] = t
    ob = k._ob("Straw", bm, straw_material(), smooth=True)
    return ob


def M():
    """All shared materials."""
    return dict(
        stucco=textured("A_Stucco", "stucco", (0.84, 0.81, 0.74), (0.95, 0.93, 0.88), 0.85, 1.0, 0.15),
        trim=principled("A_Trim", (0.97, 0.96, 0.93), 0.5),
        glass=principled("A_Glass", (0.6, 0.72, 0.72), 0.02, **{"Transmission Weight": 1.0, "IOR": 1.5}),
        railglass=principled("A_RailGlass", (0.7, 0.85, 0.85), 0.02, **{"Transmission Weight": 0.9}),
        alu=principled("A_Aluminum", (0.75, 0.76, 0.78), 0.3, 0.9),
        darkmetal=principled("A_DarkMetal", (0.06, 0.06, 0.065), 0.4, 0.8),
        terracotta=textured("A_Terracotta", "tiles", (0.55, 0.22, 0.12), (0.45, 0.18, 0.1), 0.7, 4.0, 0.4),
        deck=textured("A_DeckTiles", "tiles", (0.86, 0.8, 0.7), (0.8, 0.74, 0.64), 0.55, 1.6, 0.15),
        pooltile=textured("A_PoolTile", "tiles", (0.12, 0.5, 0.75), (0.1, 0.42, 0.68), 0.15, 10.0, 0.1),
        poolwater=water("A_PoolWater", (0.4, 0.92, 0.97), 0.25),
        slimewater=water("A_SlimeWater", (0.35, 0.95, 0.25), 0.4, emission=(0.3, 1.0, 0.2)),
        wood=textured("A_Planks", "planks", (0.45, 0.3, 0.17), (0.38, 0.25, 0.14), 0.75, 1.0, 0.3),
        weathered=textured("A_WeatheredWood", "planks", (0.55, 0.5, 0.42), (0.48, 0.43, 0.36), 0.85, 1.0, 0.35),
        thatch=textured("A_Thatch", "thatch", (0.38, 0.28, 0.14), (0.68, 0.56, 0.33), 0.95, 4.0, 0.9),
        bamboo=textured("A_Bamboo", "bamboo", (0.42, 0.33, 0.14), (0.66, 0.55, 0.28), 0.4, 1.0, 0.2),
        stone=textured("A_TempleStone", "stone", (0.27, 0.26, 0.21), (0.21, 0.2, 0.16), 0.9, 0.9, 0.6),
        rock=textured("A_Rock", "rock", (0.03, 0.032, 0.03), (0.13, 0.13, 0.12), 0.85, 1.0, 1.5),
        wetrock=textured("A_WetRock", "rock", (0.03, 0.03, 0.03), (0.14, 0.13, 0.12), 0.15, 1.0, 1.0),
        cushion=principled("A_Cushion", (0.95, 0.95, 0.92), 0.85),
        stripe_red=principled("A_StripeRed", (0.9, 0.2, 0.15), 0.7),
        slide=principled("A_SlideFiberglass", (0.95, 0.35, 0.1), 0.25, **{"Coat Weight": 1.0}),
        neon=principled("A_Neon", (1.0, 0.3, 0.6), 0.3, **{"Emission Color": (1.0, 0.3, 0.6, 1), "Emission Strength": 12}),
        warmlight=principled("A_WarmLight", (1, 0.85, 0.6), 0.3, **{"Emission Color": (1, 0.8, 0.5, 1), "Emission Strength": 6}),
        poollight=principled("A_PoolLight", (0.6, 0.9, 1), 0.3, **{"Emission Color": (0.5, 0.9, 1, 1), "Emission Strength": 8}),
        slimeglow=principled("A_SlimeGlow", (0.3, 1, 0.2), 0.2, **{"Emission Color": (0.3, 1.0, 0.2, 1), "Emission Strength": 10}),
        hull=textured("A_ShipHull", "metal", (0.6, 0.63, 0.66), (0.85, 0.87, 0.88), 0.42, 2.0, 0.3, metal=1.0),
        scorch=textured("A_Scorch", "rock", (0.015, 0.013, 0.01), (0.09, 0.08, 0.06), 0.95, 0.5, 0.4),
        canvas_blue=principled("A_CanvasBlue", (0.1, 0.35, 0.65), 0.8),
        sign_board=principled("A_SignBoard", (0.95, 0.85, 0.3), 0.6),
        tank=principled("A_ScubaTank", (0.95, 0.75, 0.1), 0.3, 0.6),
        rope=principled("A_Rope", (0.75, 0.65, 0.45), 0.9),
        towel1=principled("A_Towel1", (0.98, 0.45, 0.35), 0.95),
        towel2=principled("A_Towel2", (0.15, 0.65, 0.65), 0.95),
        torchflame=principled("A_Flame", (1, 0.4, 0.05), 0.5, **{"Emission Color": (1, 0.35, 0.05, 1), "Emission Strength": 5}),
        sand=textured("A_Sand", "sand", (0.78, 0.69, 0.5), (0.9, 0.83, 0.66), 0.9, 1.0, 0.3),
        foam=principled("A_Foam", (0.96, 0.98, 0.98), 0.6),
        curtain=principled("A_Curtain", (0.85, 0.8, 0.7), 0.9, **{"Subsurface Weight": 0.3}),
        roomwall=principled("A_RoomWall", (0.6, 0.55, 0.48), 0.9),
        roomlight=principled("A_RoomLight", (1, 0.9, 0.75), 0.5, **{"Emission Color": (1, 0.82, 0.6, 1), "Emission Strength": 4}),
        teak=textured("A_Teak", "planks", (0.32, 0.2, 0.1), (0.4, 0.26, 0.13), 0.6, 3.0, 0.2),
    )


# ------------------------------------------------------------------ hotel

ROOM304_BAY = 3                    # 4th bay from the west end, floor 3 (index 2), pool side
ROOM304_DOOR = (-23.9, -22.4)     # balcony door gap (hotel-local x)


def image_material(name, file, emission=0.0, rough=0.5):
    m, nt = _mat(name)
    if nt is None:
        return m
    p = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage")
    path = os.path.join(HERE, "textures", file)
    t.image = bpy.data.images.load(path, check_existing=True)
    nt.links.new(t.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = rough
    if emission:
        nt.links.new(t.outputs["Color"], p.inputs["Emission Color"])
        p.inputs["Emission Strength"].default_value = emission
    m["image"] = file
    return m


def uv_plane(k, corners, mat, base):
    """Quad with 0..1 UVs (for posters, screens): corners = bottom-left, bottom-right, top-right, top-left."""
    bm = bmesh.new()
    vs = [bm.verts.new(c) for c in corners]
    f = bm.faces.new(vs)
    uv = bm.loops.layers.uv.new("UVMap")
    for lp, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        lp[uv].uv = t
    return k._ob(base, bm, mat)


def bottle(k, x, y, z, mat, lying=0.0, rot=0.0, base="Bottle"):
    """Brown long-neck beer bottle (0.23 m), standing or lying on its side."""
    parts = [((0, 0, 0), (0, 0, 0.15), 0.033, 0.033), ((0, 0, 0.15), (0, 0, 0.19), 0.033, 0.013),
             ((0, 0, 0.19), (0, 0, 0.235), 0.013, 0.014)]
    obs = []
    for a, b, r0, r1 in parts:
        obs.append(k.cyl(a, b, r0, mat, segs=14, r1=r1, base=base))
    for o in obs:
        o.rotation_euler = (0, lying, rot)
        o.location = (x, y, z + (0.033 if lying else 0))
    return obs


def room304(m):
    """Rex's room 304 (hotel-local coordinates, same transform as the hotel). Morning after: bed, empty beer
    bottles, pizza box, shirt on the floor, TV playing Maximum Justice, the film poster, bathroom with
    mirror, closet with a mirror door, stuck entry door, balcony door open (towel on the balcony rail)."""
    k = Kit("Asset_Room304")
    F = 3.6
    z0 = 2 * F + 0.35                                   # floor top
    zc = 3 * F                                          # ceiling (underside of the slab above)
    bay = 6.0
    xa, xb = -45 + ROOM304_BAY * bay + 0.35, -45 + (ROOM304_BAY + 1) * bay - 0.35
    yb, yw = 1.6, 11.0 - 0.27                           # corridor wall / window glass
    carpet = textured("A_Carpet", "sand", (0.2, 0.24, 0.3), (0.28, 0.32, 0.38), 0.95, 3.0, 0.15)
    paper = textured("A_Wallpaper", "stucco", (0.78, 0.74, 0.62), (0.86, 0.83, 0.72), 0.8, 2.0, 0.05)
    weather(paper, streaks=0.15, crevice=0.4, base_dirt=0.15, grime=(0.55, 0.5, 0.42))
    sheets = textured("A_Sheets", "stucco", (0.86, 0.86, 0.84), (0.96, 0.96, 0.95), 0.85, 6.0, 0.5)
    duvet = textured("A_Duvet", "stucco", (0.1, 0.22, 0.4), (0.16, 0.3, 0.5), 0.9, 4.0, 0.6)
    mirror = principled("A_Mirror", (0.95, 0.95, 0.95), 0.02, 1.0)
    porcelain = principled("A_Porcelain", (0.92, 0.92, 0.9), 0.15)
    brown = principled("A_BeerGlass", (0.18, 0.07, 0.01), 0.05, **{"Transmission Weight": 0.85})
    tvm = image_material("A_TVScreen", "tv_maximum_justice.png", emission=2.5, rough=0.2)
    poster = image_material("A_Poster", "poster_maximum_justice.png", rough=0.6)
    pizza = principled("A_Cardboard", (0.55, 0.42, 0.28), 0.9)
    shirt = principled("A_ShirtOnFloor", (0.15, 0.3, 0.6), 0.9)
    door = textured("A_DoorWood", "planks", (0.32, 0.2, 0.1), (0.38, 0.24, 0.12), 0.6, 1.0, 0.2)

    # floor, ceiling, wall finishes (sit on / against the hotel slab and piers)
    k.box((xa, yb, z0), (xb, yw, z0 + 0.02), carpet, "Carpet")
    k.box((xa, yb, zc - 0.02), (xb, yw, zc), m["trim"], "Ceiling")
    k.box((xa, yb, z0), (xa + 0.02, yw, zc), paper, "WallW")
    k.box((xb - 0.02, yb, z0), (xb, yw, zc), paper, "WallE")
    dx0, dx1 = -23.7, -22.7                             # entry door opening in the corridor wall
    k.box((xa, yb, z0), (dx0, yb + 0.02, zc), paper, "WallN")
    k.box((dx1, yb, z0), (xb, yb + 0.02, zc), paper, "WallN")
    k.box((dx0, yb, z0 + 2.1), (dx1, yb + 0.02, zc), paper, "WallN")
    for x in (dx0 - 0.06, dx1):
        k.box((x, yb, z0), (x + 0.06, yb + 0.06, z0 + 2.16), m["trim"], "DoorFrame")
    k.box((dx0 - 0.06, yb, z0 + 2.1), (dx1 + 0.06, yb + 0.06, z0 + 2.16), m["trim"], "DoorFrame")
    k.box((dx0 + 0.01, yb + 0.02, z0 + 0.01), (dx1 - 0.01, yb + 0.06, z0 + 2.09), door, "Door_Slab")
    k.cyl((dx1 - 0.12, yb + 0.06, z0 + 1.0), (dx1 - 0.12, yb + 0.12, z0 + 1.0), 0.025, m["alu"], base="Door_Knob")
    k.box((dx0 + 0.2, yb + 0.061, z0 + 1.3), (dx0 + 0.45, yb + 0.07, z0 + 1.45), m["sign_board"], "Door_Sign")
    for yy in (yb + 0.02, yw - 0.02):                    # skirting boards
        k.box((xa, min(yy, yy + 0.02), z0), (xb, max(yy, yy + 0.02) + 0.0, z0 + 0.1), m["trim"], "Skirting")

    # bathroom (south-west corner): partition walls, sink, mirror, toilet, tub
    bx1, by1 = -24.2, 4.2
    k.box((xa, by1, z0), (bx1, by1 + 0.1, zc), paper, "BathWall")
    k.box((bx1, yb, z0), (bx1 + 0.1, 1.95, zc), paper, "BathWall")
    k.box((bx1, 2.85, z0), (bx1 + 0.1, by1 + 0.1, zc), paper, "BathWall")
    k.box((bx1, 1.95, z0 + 2.1), (bx1 + 0.1, 2.85, zc), paper, "BathWall")
    tile = m["pooltile"]
    k.box((xa, yb, z0 + 0.02), (bx1, by1, z0 + 0.03), principled("A_BathTile", (0.85, 0.85, 0.82), 0.3), "BathFloor")
    k.box((xa + 0.02, 2.3, z0), (xa + 0.6, 3.7, z0 + 0.85), m["teak"], "Vanity")
    k.box((xa + 0.02, 2.25, z0 + 0.85), (xa + 0.62, 3.75, z0 + 0.88), porcelain, "Counter")
    k.disc((xa + 0.32, 3.0, z0 + 0.8), 0.2, 0.08, porcelain, base="Sink")
    uv_plane(k, [(xa + 0.025, 3.7, z0 + 1.05), (xa + 0.025, 2.3, z0 + 1.05), (xa + 0.025, 2.3, z0 + 2.1),
                 (xa + 0.025, 3.7, z0 + 2.1)], mirror, "BathMirror")
    k.box((-25.4, yb + 0.02, z0), (-24.7, yb + 0.6, z0 + 0.4), porcelain, "Toilet", bevel=0.05)
    k.box((-25.4, yb + 0.02, z0 + 0.4), (-24.7, yb + 0.2, z0 + 0.8), porcelain, "Cistern", bevel=0.03)
    k.box((xa + 0.02, by1 - 0.8, z0), (bx1 - 0.6, by1 - 0.02, z0 + 0.55), porcelain, "Tub", bevel=0.05)

    # closet with a full-length mirror door (taunt practice)
    k.box((-22.3, yb + 0.02, z0), (xb - 0.02, 3.2, z0 + 2.3), m["teak"], "Closet")
    uv_plane(k, [(-22.31, yb + 0.15, z0 + 0.1), (-22.31, 3.05, z0 + 0.1), (-22.31, 3.05, z0 + 2.2),
                 (-22.31, yb + 0.15, z0 + 2.2)], mirror, "ClosetMirror")

    # king bed against the east wall, rumpled duvet, pillows; nightstands + lamps
    bx0 = xb - 2.15
    k.box((bx0, 5.2, z0), (xb - 0.05, 7.3, z0 + 0.3), m["teak"], "BedBase")
    k.box((bx0 + 0.03, 5.23, z0 + 0.3), (xb - 0.08, 7.27, z0 + 0.58), sheets, "Mattress", bevel=0.06)
    k.box((xb - 0.08, 5.15, z0), (xb - 0.02, 7.35, z0 + 1.3), m["teak"], "Headboard")
    duv = k.box((bx0, 5.15, z0 + 0.5), (xb - 0.6, 7.35, z0 + 0.66), duvet, "Duvet", bevel=0.05)
    sub = duv.modifiers.new("Sub", "SUBSURF")
    sub.levels = sub.render_levels = 2
    tx = bpy.data.textures.get("T_Rumple") or bpy.data.textures.new("T_Rumple", "CLOUDS")
    tx.noise_scale = 0.35
    dsp = duv.modifiers.new("Rumple", "DISPLACE")
    dsp.texture, dsp.strength, dsp.texture_coords = tx, 0.12, "GLOBAL"
    for py in (5.6, 6.8):
        k.box((xb - 0.6, py - 0.35, z0 + 0.58), (xb - 0.15, py + 0.35, z0 + 0.74), sheets, "Pillow", bevel=0.06)
    for ny in (4.65, 7.35):
        k.box((xb - 0.55, ny, z0), (xb - 0.05, ny + 0.5, z0 + 0.55), m["teak"], "Nightstand")
        k.cyl((xb - 0.3, ny + 0.25, z0 + 0.55), (xb - 0.3, ny + 0.25, z0 + 0.95), 0.02, m["alu"], base="LampStem")
        k.cyl((xb - 0.3, ny + 0.25, z0 + 0.9), (xb - 0.3, ny + 0.25, z0 + 1.15), 0.17, m["roomlight"], r1=0.11,
              base="LampShade")
    # dresser + TV (playing the film) on the west wall, poster above the desk
    k.box((xa + 0.02, 5.4, z0), (xa + 0.55, 7.1, z0 + 0.8), m["teak"], "Dresser")
    k.box((xa + 0.15, 5.65, z0 + 0.8), (xa + 0.25, 6.85, z0 + 1.55), principled("A_TVBody", (0.03, 0.03, 0.03), 0.4),
          "TV")
    uv_plane(k, [(xa + 0.26, 5.7, z0 + 0.85), (xa + 0.26, 6.8, z0 + 0.85), (xa + 0.26, 6.8, z0 + 1.5),
                 (xa + 0.26, 5.7, z0 + 1.5)], tvm, "TVScreen")
    k.box((xa + 0.02, 7.95, z0 + 1.15), (xa + 0.05, 8.95, z0 + 2.65), m["darkmetal"], "PosterFrame")
    uv_plane(k, [(xa + 0.055, 8.0, z0 + 1.2), (xa + 0.055, 8.9, z0 + 1.2), (xa + 0.055, 8.9, z0 + 2.6),
                 (xa + 0.055, 8.0, z0 + 2.6)], poster, "Poster")
    # table + 2 chairs by the window, bottles, pizza box, shirt
    tx_, ty_ = -25.3, 9.6
    k.disc((tx_, ty_, z0), 0.25, 0.03, m["darkmetal"], base="TableBase")
    k.cyl((tx_, ty_, z0), (tx_, ty_, z0 + 0.72), 0.03, m["darkmetal"], base="TableStem")
    k.disc((tx_, ty_, z0 + 0.72), 0.45, 0.03, m["teak"], base="TableTop")
    for cx_, cy_ in ((tx_ - 0.7, ty_), (tx_ + 0.7, ty_ - 0.3)):
        for lx in (-0.2, 0.2):
            for ly in (-0.2, 0.2):
                k.cyl((cx_ + lx, cy_ + ly, z0), (cx_ + lx, cy_ + ly, z0 + 0.45), 0.015, m["alu"], base="ChairLeg")
        k.box((cx_ - 0.23, cy_ - 0.23, z0 + 0.45), (cx_ + 0.23, cy_ + 0.23, z0 + 0.5), m["teak"], "ChairSeat")
    rng = random.Random(304)
    for i in range(5):                                   # empties on the table
        a = i / 5 * math.tau
        bottle(k, tx_ + math.cos(a) * 0.25, ty_ + math.sin(a) * 0.25, z0 + 0.75, brown)
    for i in range(9):                                   # empties on the floor (some lying down)
        x, y = rng.uniform(xa + 0.8, xb - 2.4), rng.uniform(4.6, 9.8)
        bottle(k, x, y, z0 + 0.02, brown, lying=(math.pi / 2 if rng.random() < 0.6 else 0.0), rot=rng.uniform(0, 6.3),
               base=f"FloorBottle{i}x")          # separate physics props in Unity (kickable)
    k.box((-24.0, 8.0, z0 + 0.02), (-23.6, 8.4, z0 + 0.07), pizza, "PizzaBox")
    pz = k.box((-24.3, 8.5, z0 + 0.02), (-23.9, 8.9, z0 + 0.06), pizza, "PizzaBox")
    pz.rotation_euler = (0, 0, 0.4)
    sh = k.box((-23.8, 4.8, z0 + 0.02), (-23.2, 5.5, z0 + 0.06), shirt, "Shirt", bevel=0.02)
    sh.rotation_euler = (0, 0, -0.3)
    k.box((xa + 0.6, 4.4, z0), (xa + 1.1, 4.9, z0 + 0.85), principled("A_Fridge", (0.85, 0.85, 0.83), 0.3), "MiniFridge")
    return k.c


def room304_markers():
    """Gameplay points in hotel-local coordinates (Blender axes), used by the Unity builder."""
    F, bay = 3.6, 6.0
    z0 = 2 * F + 0.35
    xa, xb = -45 + ROOM304_BAY * bay + 0.35, -45 + (ROOM304_BAY + 1) * bay - 0.35
    return dict(
        doorHinge=(-23.7, 1.64, z0), bedLie=(xb - 1.15, 6.25, z0 + 0.6), wake=(-24.0, 6.3, z0 + 0.03),
        fullBottle=(-25.3, 9.6, z0 + 0.76), towel=(-23.0, 12.55, z0 + 0.6), closetMirror=(-22.32, 2.3, z0 + 1.15),
        bathMirror=(xa + 0.03, 3.0, z0 + 1.57), tv=(xa + 0.6, 6.25, z0 + 1.2), lamp1=(xb - 0.3, 4.9, z0 + 1.0),
        lamp2=(xb - 0.3, 7.6, z0 + 1.0), ceiling=(-24.0, 6.3, z0 + 3.1), balcony=(-23.1, 12.0, z0 - 0.3),
    )


def rooms(k, m, fl, z, F, x0, y0, y1, bay, B):
    """Each guest room: back wall tint, two curtain panels drawn by a random amount, ceiling, light on/off,
    bed edge seen through the glass. Balconies: chairs, small table, towels over the rail."""
    rng = random.Random(fl * 97)
    tints = [(0.62, 0.56, 0.47), (0.5, 0.56, 0.55), (0.66, 0.5, 0.42), (0.56, 0.55, 0.5), (0.7, 0.66, 0.58)]
    cols = [(0.86, 0.82, 0.72), (0.9, 0.88, 0.84), (0.55, 0.62, 0.62), (0.75, 0.6, 0.45), (0.8, 0.78, 0.7)]
    for i in range(B):
        xa, xb = x0 + i * bay + 0.35, x0 + (i + 1) * bay - 0.35
        for side, ywin, yback in ((-1, y0 + 0.27, -1.6), (1, y1 - 0.27, 1.6)):
            if fl == 2 and i == ROOM304_BAY and side == 1:
                continue                  # Rex's room is built in detail by room304()
            tint = principled(f"A_RoomTint{rng.randrange(5)}", tints[rng.randrange(5)], 0.9)
            k.box((xa, min(yback, yback - side * 0.02), z + 0.35), (xb, max(yback, yback - side * 0.02), z + F),
                  tint, "RoomBack")
            k.box((xa, min(ywin, yback), z + F - 0.02), (xb, max(ywin, yback), z + F), m["trim"], "Ceiling")
            if rng.random() < 0.3:
                xc = (xa + xb) / 2
                yc = (ywin + yback) / 2
                k.box((xc - 0.4, yc - 0.4, z + F - 0.04), (xc + 0.4, yc + 0.4, z + F - 0.02), m["roomlight"], "Lamp")
            # bed (seen through glass)
            yb = yback - side * 0.0
            k.box((xa + 0.4, min(yb, yb - side * 2.1), z + 0.35), (xa + 2.0, max(yb, yb - side * 2.1), z + 0.85),
                  m["cushion"], "Bed", bevel=0.05)
            # curtains: two panels hanging from a rail 0.15 m inside the glass
            cy_ = ywin - side * 0.15
            cm = principled(f"A_Curtain{rng.randrange(5)}", cols[rng.randrange(5)], 0.92,
                            **{"Subsurface Weight": 0.3})
            openness = rng.uniform(0.0, 0.85)
            pw = (xb - xa) * (1 - openness) / 2 + 0.15
            for (pa, pb) in ((xa, xa + pw), (xb - pw, xb)):
                # pleated panel: zig-zag strip
                bm = bmesh.new()
                nfold = max(2, int((pb - pa) / 0.12))
                top, bot = [], []
                for f in range(nfold + 1):
                    x = pa + (pb - pa) * f / nfold
                    dy = 0.04 * (1 if f % 2 else -1)
                    top.append(bm.verts.new((x, cy_ + dy, z + F - 0.1)))
                    bot.append(bm.verts.new((x, cy_ + dy, z + 0.4)))
                for f in range(nfold):
                    bm.faces.new((bot[f], bot[f + 1], top[f + 1], top[f]))
                k._ob("Curtain", bm, cm, smooth=True)
            k.box((xa, cy_ - 0.015, z + F - 0.12), (xb, cy_ + 0.015, z + F - 0.08), m["alu"], "CurtainRail")
        # balcony furniture (north side) on ~60% of rooms
        if rng.random() < 0.6:
            yb0 = y1 + 0.3
            xc = (xa + xb) / 2
            k.disc((xc, yb0 + 0.7, z + 0.25), 0.3, 0.02, m["alu"], base="TableTop").location.z = z + 0.95
            k.cyl((xc, yb0 + 0.7, z + 0.25), (xc, yb0 + 0.7, z + 0.95), 0.025, m["alu"], base="TableLeg")
            for dx in (-0.75, 0.75):
                cx_ = xc + dx
                for lx in (-0.2, 0.2):
                    for ly in (-0.2, 0.2):
                        k.cyl((cx_ + lx, yb0 + 0.7 + ly, z + 0.25), (cx_ + lx, yb0 + 0.7 + ly, z + 0.7), 0.015,
                              m["alu"], base="ChairLeg")
                k.box((cx_ - 0.23, yb0 + 0.47, z + 0.7), (cx_ + 0.23, yb0 + 0.93, z + 0.74), m["teak"], "ChairSeat")
                k.box((cx_ - 0.23, yb0 + 0.9, z + 0.74), (cx_ + 0.23, yb0 + 0.94, z + 1.15), m["teak"], "ChairBack")
        if rng.random() < 0.3:   # towel draped over the balcony rail (both sides of the top rail)
            xc = rng.uniform(xa + 0.4, xb - 1.2)
            tm = rng.choice([m["towel1"], m["towel2"], m["cushion"]])
            k.box((xc, y1 + 1.6, z + 0.7), (xc + 0.8, y1 + 1.66, z + 1.21), tm, "Towel")
            k.box((xc, y1 + 1.8, z + 0.7), (xc + 0.8, y1 + 1.86, z + 1.21), tm, "Towel")
            k.box((xc, y1 + 1.6, z + 1.21), (xc + 0.8, y1 + 1.86, z + 1.25), tm, "Towel")


def hotel(m):
    """Resort hotel: 90 m x 22 m, 5 floors (3.6 m), lobby on floor 1, balconies on the pool (north) side,
    penthouse + roof terrace + sniper spot. Rex's room 304 = floor 3, bay 4 (marked)."""
    k = Kit("Asset_Hotel")
    W, D, F, B = 90.0, 22.0, 3.6, 15
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    bay = W / B
    for fl in range(5):
        z = fl * F
        k.box((x0 - 0.3, y0 - 0.3, z), (x1 + 0.3, y1 + 0.3, z + 0.35), m["trim"], "Slab")
        # thin glazing behind the piers (set back 0.25 m); corridor core in the middle; rooms between
        k.box((x0 + 0.1, y0 + 0.25, z + 0.35), (x1 - 0.1, y0 + 0.27, z + F), m["glass"], "Glass")
        if fl == 2:   # room 304: sliding door stands open (gap), the door panel is slid behind the fixed pane
            gx0, gx1 = ROOM304_DOOR
            k.box((x0 + 0.1, y1 - 0.27, z + 0.35), (gx0, y1 - 0.25, z + F), m["glass"], "Glass")
            k.box((gx1, y1 - 0.27, z + 0.35), (x1 - 0.1, y1 - 0.25, z + F), m["glass"], "Glass")
            k.box((gx0 - 1.5, y1 - 0.33, z + 0.37), (gx0 + 0.05, y1 - 0.29, z + F - 0.4), m["glass"], "SlidDoor")
            k.box((gx1 - 0.04, y1 - 0.29, z + 0.35), (gx1, y1 - 0.23, z + F), m["alu"], "DoorJamb")
        else:
            k.box((x0 + 0.1, y1 - 0.27, z + 0.35), (x1 - 0.1, y1 - 0.25, z + F), m["glass"], "Glass")
        k.box((x0 + 0.1, -1.6, z + 0.35), (x1 - 0.1, 1.6, z + F), m["roomwall"], "Core")
        if fl > 0:
            rooms(k, m, fl, z, F, x0, y0, y1, bay, B)
        for i in range(B + 1):        # piers between rooms (stand on the slab, touch the slab above)
            x = x0 + i * bay
            k.box((x - 0.35, y0, z + 0.35), (x + 0.35, y1, z + F), m["stucco"], "Pier")
        if fl == 0:                   # lobby: tall glass with mullions + entrance canopy (south)
            for i in range(B * 2 + 1):
                x = x0 + i * bay / 2
                k.box((x - 0.04, y0 + 0.2, 0.35), (x + 0.04, y0 + 0.28, F), m["alu"], "Mullion")
            k.box((-9, y0 - 6.5, 3.2), (9, y0, 3.5), m["trim"], "Canopy")
            for sx in (-1, 1):
                k.cyl((sx * 8.4, y0 - 6.0, 0.0), (sx * 8.4, y0 - 6.0, 3.2), 0.22, m["trim"], base="CanopyCol")
            k.box((-3, y0 - 0.02, 0.35), (3, y0 + 0.25, 2.8), m["railglass"], "Doors")
            for s in range(3):
                k.box((-6, y0 - 1.5 - s * 0.4, 0), (6, y0 - 1.1 - s * 0.4 + 0.4, 0.12 * (3 - s)), m["trim"], "Step")
            k.text("ISLA GRANDE RESORT", 1.2, (0, y0 - 6.55, 3.55), m["neon"], 0.08, base="Sign")
        else:
            # windows: horizontal mullion + sill on the south side
            k.box((x0, y0 + 0.2, z + F * 0.42), (x1, y0 + 0.28, z + F * 0.46), m["alu"], "Transom")
            # pool side: balcony slabs, divider walls, glass railing with posts and top rail
            k.box((x0, y1, z), (x1, y1 + 1.8, z + 0.25), m["trim"], "Balcony")
            for i in range(B + 1):
                x = x0 + i * bay
                k.box((x - 0.1, y1, z + 0.25), (x + 0.1, y1 + 1.8, z + 2.6), m["stucco"], "Divider")
            k.box((x0, y1 + 1.7, z + 0.25), (x1, y1 + 1.74, z + 1.15), m["railglass"], "Rail")
            k.box((x0, y1 + 1.66, z + 1.15), (x1, y1 + 1.8, z + 1.21), m["alu"], "RailTop")
            for i in range(B * 3 + 1):
                x = x0 + i * bay / 3
                k.box((x - 0.03, y1 + 1.68, z + 0.25), (x + 0.03, y1 + 1.76, z + 1.15), m["alu"], "RailPost")
            # sliding-door frames on each balcony
            for i in range(B):
                xc = x0 + (i + 0.5) * bay
                k.box((xc - 0.03, y1 - 0.27, z + 0.35), (xc + 0.03, y1 - 0.2, z + F), m["alu"], "DoorFrame")
            k.box((x0, y1 - 0.27, z + 2.5), (x1, y1 - 0.2, z + 2.56), m["alu"], "DoorHead")
        if fl == 2:   # room 304 number plate on the balcony
            xc = x0 + 3.5 * bay
            k.text("304", 0.35, (xc, y1 + 1.81, z + 0.6), m["sign_board"], 0.02, rot=(math.pi / 2, 0, math.pi), base="Room304")
    top = 5 * F
    k.box((x0 - 0.3, y0 - 0.3, top), (x1 + 0.3, y1 + 0.3, top + 0.35), m["trim"], "Roof")
    for (a, b) in (((x0 - 0.3, y0 - 0.3), (x1 + 0.3, y0)), ((x0 - 0.3, y1), (x1 + 0.3, y1 + 0.3)),
                   ((x0 - 0.3, y0), (x0, y1)), ((x1, y0), (x1 + 0.3, y1))):
        k.box((a[0], a[1], top + 0.35), (b[0], b[1], top + 1.35), m["stucco"], "Parapet")
    # penthouse + terracotta hip roof + terrace pergola
    k.box((-15, -6, top + 0.35), (15, 6, top + 3.6), m["stucco"], "Penthouse")
    k.box((-14.8, -6.25, top + 0.6), (14.8, 6.25, top + 3.0), m["glass"], "PenthouseGlass")
    bm = bmesh.new()
    pts = [(-16, -7, 0), (16, -7, 0), (16, 7, 0), (-16, 7, 0), (-9, 0, 3.2), (9, 0, 3.2)]
    vs = [bm.verts.new((x, y, z + top + 3.6)) for x, y, z in pts]
    for f in ((0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4), (0, 3, 2, 1)):
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    k._ob("HipRoof", bm, m["terracotta"])
    for i in range(9):
        x = 18 + i * 2.6
        k.box((x - 0.1, -6, top + 0.35), (x + 0.1, -5.8, top + 3.2), m["weathered"], "PergolaPost")
        k.box((x - 0.1, 5.8, top + 0.35), (x + 0.1, 6, top + 3.2), m["weathered"], "PergolaPost")
        k.box((x - 0.06, -6, top + 3.2), (x + 0.06, 6, top + 3.4), m["weathered"], "PergolaBeam")
    k.box((17.9, -6, top + 3.0), (39, -5.8, top + 3.2), m["weathered"], "PergolaRail")
    k.box((17.9, 5.8, top + 3.0), (39, 6, top + 3.2), m["weathered"], "PergolaRail")
    # AC units + water tank on the roof
    for i in range(5):
        x = -40 + i * 4
        k.box((x, 4, top + 0.35), (x + 2.2, 6, top + 1.5), m["alu"], "ACUnit")
        k.disc((x + 1.1, 5, top + 1.5), 0.8, 0.05, m["darkmetal"], base="ACFan")
    k.disc((-30, -5, top + 0.35), 2.0, 3.0, m["alu"], base="WaterTank")
    # sniper spot: sandbag corner at the north-east roof corner
    for i in range(10):
        a = i / 9 * math.pi / 2
        c = (x1 - 3 + math.cos(a) * 2.5 - 2.5, y1 - 3 + math.sin(a) * 2.5 - 2.5, top + 0.35 + 0.22)
        k.rock(c, (0.55, 0.3, 0.2), principled("A_Sandbag", (0.62, 0.55, 0.4), 0.95), seed=i, base="Sandbag", detail=2)
    return k.c


# ------------------------------------------------------------------ pool area

def pool_area(m):
    """Deck 110 x 50 m (top at z=0). Main pool with steps, ladders, lights, swim-up bar, slide tower,
    2 hot tubs, stage, loungers + umbrellas."""
    k = Kit("Asset_PoolArea")
    deck = k.box((-55, -25, -0.4), (55, 25, 0.0), m["deck"], "Deck")
    # main pool: 50 x 20 m basin (tiled), 1.6 m deep, water surface 0.12 below the deck
    px0, px1, py0, py1, depth = -35, 15, -10, 10, 1.6
    k.box((px0, py0, -depth - 0.3), (px1, py1, -depth), m["pooltile"], "PoolFloor")
    for a, b in (((px0 - 0.3, py0 - 0.3), (px1 + 0.3, py0)), ((px0 - 0.3, py1), (px1 + 0.3, py1 + 0.3)),
                 ((px0 - 0.3, py0), (px0, py1)), ((px1, py0), (px1 + 0.3, py1))):
        k.box((a[0], a[1], -depth - 0.3), (b[0], b[1], -0.02), m["pooltile"], "PoolWall")
    k.box((px0, py0, -depth), (px1, py1, -0.12), m["poolwater"], "PoolWater")
    for a, b in (((px0 - 0.6, py0 - 0.6), (px1 + 0.6, py0)), ((px0 - 0.6, py1), (px1 + 0.6, py1 + 0.6)),
                 ((px0 - 0.6, py0), (px0, py1)), ((px1, py0), (px1 + 0.6, py1))):
        k.box((a[0], a[1], 0.0), (b[0], b[1], 0.08), m["trim"], "Coping")
    for s in range(4):   # entry steps at the west end (inside the water)
        k.box((px0, -4, -depth + s * 0.35 - 0.3), (px0 + 1.2 + (3 - s) * 0.4, 4, -depth + (s + 1) * 0.35),
              m["pooltile"], "PoolStep")
    for x in (-20, 0):   # ladders: rails bend over the coping into the water
        for dy in (-0.3, 0.3):
            k.tube([(x + dy, py1 + 0.4, 0.08), (x + dy, py1 + 0.4, 0.9), (x + dy, py1 - 0.1, 1.0),
                    (x + dy, py1 - 0.25, 0.6), (x + dy, py1 - 0.25, -1.2)], 0.025, m["alu"], base="Ladder")
        for s in range(3):
            k.cyl((x - 0.3, py1 - 0.25, -0.3 - s * 0.3), (x + 0.3, py1 - 0.25, -0.3 - s * 0.3), 0.02, m["alu"],
                  base="Rung")
    for i in range(8):   # underwater lights in the walls
        x = px0 + 3 + i * 6.5
        k.disc((x, py0 + 0.01, -0.9), 0.15, 0.02, m["poollight"], base="PoolLight").rotation_euler = (math.radians(-90), 0, 0)
    # swim-up bar in the pool (counter base stands on the pool floor), stools in the water, thatch roof on posts
    bx, by = 8, 0
    k.box((bx - 3, by - 1, -depth), (bx + 3, by + 1, 1.1), m["bamboo"], "BarCounter")
    k.box((bx - 3.2, by - 1.2, 1.1), (bx + 3.2, by + 1.2, 1.2), m["wood"], "BarTop")
    for i in range(5):
        k.cyl((bx - 2.4 + i * 1.2, by - 2.0, -depth), (bx - 2.4 + i * 1.2, by - 2.0, -0.55), 0.06, m["alu"], base="StoolPost")
        k.disc((bx - 2.4 + i * 1.2, by - 2.0, -0.55), 0.22, 0.06, m["trim"], base="StoolSeat")
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.bamboo((bx + sx * 3.4, by + sy * 1.4, -depth), (bx + sx * 3.4, by + sy * 1.4, 3.6), 0.12, m["bamboo"], 0.5)
    k.cyl((bx, by, 3.5), (bx, by, 5.4), 5.2, underlay(), segs=28, r1=0.2, base="BarRoof")
    straw_roof(k, cone_quads(bx, by, 5.2, 3.5, 0.2, 5.4), seed=4)
    # water slide: tower (deck to platform), stairs, slide tube spiralling down into the pool
    tx, ty, th = -40, 14, 7.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((tx + sx * 1.5, ty + sy * 1.5, 0), (tx + sx * 1.5, ty + sy * 1.5, th), 0.15, m["alu"], base="TowerLeg")
    k.box((tx - 1.8, ty - 1.8, th), (tx + 1.8, ty + 1.8, th + 0.2), m["deck"], "Platform")
    k.box((tx - 1.8, ty - 1.8, th + 0.2), (tx + 1.8, ty - 1.74, th + 1.2), m["railglass"], "PlatformRail")
    for s in range(20):
        z = s * th / 20
        k.box((tx + 1.6, ty + 1.8 - s * 0.32 - 0.3, z), (tx + 2.6, ty + 1.8 - s * 0.32, z + 0.06), m["alu"], "StairStep")
    path = []
    for i in range(40):
        t = i / 39
        a = t * math.tau * 1.25
        r = 4.5
        path.append((tx + 1.0 - r + r * math.cos(a), ty - 1.0 + r * math.sin(a) - t * 6, th + 0.4 - t * (th + 0.2)))
    path[-1] = (path[-1][0], -8.0, -0.2)   # ends in the pool water
    k.tube(path, 0.55, m["slide"], segs=16, base="SlideTube")
    for i in range(4, 36, 6):   # supports from the ground to the tube
        p = path[i]
        if p[2] > 0.8:
            k.cyl((p[0], p[1], 0), (p[0], p[1], p[2] - 0.5), 0.1, m["alu"], base="SlideSupport")
    # hot tubs (round, coping, water, steam bubbles)
    for i, (hx, hy) in enumerate(((28, 12), (34, 12))):
        k.disc((hx, hy, -1.0), 2.0, 1.0, m["pooltile"], base="TubShell")
        k.disc((hx, hy, -0.85), 1.85, 0.72, m["poolwater"], base="TubWater")
        k.disc((hx, hy, 0.0), 2.25, 0.1, m["trim"], base="TubCoping").scale = (1, 1, 1)
    # cut the pool + hot-tub holes out of the deck so the water is open to the sky
    holes = [k.box((px0 - 0.31, py0 - 0.31, -1), (px1 + 0.31, py1 + 0.31, 1), None, "Cut")]
    for hx, hy in ((28, 12), (34, 12)):
        holes.append(k.disc((hx, hy, -1), 2.01, 2, None, base="Cut"))
    for h in holes:
        bo = deck.modifiers.new("Hole", "BOOLEAN")
        bo.operation = "DIFFERENCE"
        bo.object = h
        bo.solver = "EXACT"
    _apply(deck)
    for h in holes:
        bpy.data.objects.remove(h)
    # stage with truss + lights (north-east corner)
    sx0, sy0 = 30, -18
    k.box((sx0 - 6, sy0 - 4, 0), (sx0 + 6, sy0 + 4, 0.8), m["wood"], "Stage")
    for dx in (-5.8, 5.8):
        for dy in (-3.8, 3.8):
            k.cyl((sx0 + dx, sy0 + dy, 0.8), (sx0 + dx, sy0 + dy, 5.0), 0.12, m["alu"], base="Truss")
    k.box((sx0 - 6, sy0 - 3.9, 4.9), (sx0 + 6, sy0 - 3.7, 5.1), m["alu"], "TrussBeam")
    k.box((sx0 - 6, sy0 + 3.7, 4.9), (sx0 + 6, sy0 + 3.9, 5.1), m["alu"], "TrussBeam")
    for i in range(6):
        k.cyl((sx0 - 5 + i * 2, sy0 - 3.8, 4.9), (sx0 - 5 + i * 2, sy0 - 3.5, 4.4), 0.18, m["darkmetal"], r1=0.25, base="StageLight")
    # loungers (frame legs on the deck, cushion on the frame) + umbrellas (pole into a base)
    rng = random.Random(1)
    for row, y in ((0, 14.5), (1, -14.5)):
        for i in range(10):
            x = -30 + i * 5
            for s in (-1, 1):
                lx = x + s * 0.9
                for dy in (-0.8, 0.8):
                    for dx in (-0.3, 0.3):
                        k.cyl((lx + dx, y + dy, 0), (lx + dx, y + dy, 0.35), 0.02, m["alu"], base="LoungerLeg")
                k.box((lx - 0.35, y - 0.95, 0.35), (lx + 0.35, y + 0.95, 0.42), m["alu"], "LoungerFrame")
                k.box((lx - 0.32, y - 0.9, 0.42), (lx + 0.32, y + 0.4, 0.52), m["cushion"], "Cushion", bevel=0.04)
                back = k.box((lx - 0.32, 0, 0), (lx + 0.32, 0.65, 0.1), m["cushion"], "Back", bevel=0.04)
                back.location = (0, y + 0.4, 0.47)
                back.rotation_euler = (math.radians(55 if row == 0 else -55), 0, 0)
                if rng.random() < 0.4:
                    k.box((lx - 0.3, y - 0.6, 0.52), (lx + 0.3, y + 0.2, 0.54), rng.choice([m["towel1"], m["towel2"]]), "Towel")
            k.disc((x, y, 0), 0.3, 0.12, m["darkmetal"], base="UmbBase")
            k.cyl((x, y, 0.12), (x, y, 2.7), 0.03, m["trim"], base="UmbPole")
            k.cyl((x, y, 2.4), (x, y, 2.85), 1.6, m["stripe_red"] if i % 2 else m["cushion"], segs=8, r1=0.04, base="UmbCanopy")
    return k.c


# ------------------------------------------------------------------ beach

def dive_shop(m):
    k = Kit("Asset_DiveShop")
    # stilts on the sand, floor, plank walls, windows, pitched thatch roof, sign, tanks, surfboards
    for x in (-5, 0, 5):
        for y in (-3.5, 3.5):
            k.cyl((x, y, 0), (x, y, 0.8), 0.15, m["weathered"], base="Stilt")
    k.box((-5.5, -4, 0.8), (5.5, 4, 1.0), m["weathered"], "Floor")
    k.box((-5, -3.5, 1.0), (5, 3.5, 4.0), m["wood"], "Walls")
    for x in (-3, 3):
        k.box((x - 1, -3.55, 2.0), (x + 1, -3.48, 3.2), m["glass"], "Window")
        k.box((x - 1.1, -3.6, 1.9), (x + 1.1, -3.45, 2.0), m["weathered"], "Sill")
    k.box((-0.8, -3.55, 1.0), (0.8, -3.48, 3.2), m["darkmetal"], "Door")
    bm = bmesh.new()
    pts = [(-6.2, -4.8, 4.0), (6.2, -4.8, 4.0), (6.2, 4.8, 4.0), (-6.2, 4.8, 4.0), (-6.2, 0, 6.3), (6.2, 0, 6.3)]
    v = [bm.verts.new(p) for p in pts]
    for f in ((0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4)):
        bm.faces.new([v[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    r = k._ob("Roof", bm, underlay())
    sol = r.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.25
    sol.offset = -1
    straw_roof(k, [((-6.2, -4.8, 4.0), (6.2, -4.8, 4.0), (-6.2, 0, 6.3), (6.2, 0, 6.3)),
                   ((6.2, 4.8, 4.0), (-6.2, 4.8, 4.0), (6.2, 0, 6.3), (-6.2, 0, 6.3))], seed=1)
    k.cyl((-6.3, 0, 6.32), (6.3, 0, 6.32), 0.22, straw_material(), segs=12, base="RidgeCap")
    k.box((-3, -4.2, 4.2), (3, -4.0, 5.0), m["sign_board"], "SignBoard")
    k.text("DIVE SHOP", 0.55, (0, -4.22, 4.4), m["darkmetal"], 0.03, base="SignText")
    for i in range(5):   # scuba tanks in a rack against the wall
        k.cyl((-4.5 + i * 0.4, -3.75, 1.0), (-4.5 + i * 0.4, -3.75, 1.8), 0.17, m["tank"], base="Tank")
    k.box((-4.8, -3.95, 1.5), (-2.7, -3.9, 1.6), m["weathered"], "Rack")
    for i, col in enumerate((m["stripe_red"], m["canvas_blue"], m["sign_board"])):   # surfboards leaning
        sb = k.boxc((3.0 + i * 0.7, -3.85, 1.0 + 1.1), (0.55, 0.08, 2.2), col, base="Surfboard", bevel=0.03)
        sb.rotation_euler = (math.radians(-12), 0, 0)
    return k.c


def tiki_bar(m):
    k = Kit("Asset_TikiBar")
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.bamboo((sx * 3.2, sy * 3.2, 0), (sx * 3.2, sy * 3.2, 3.3), 0.16, m["bamboo"], seg_len=0.55)
    # U-shaped bar counter: bamboo front, wood top
    for (a, b) in (((-2.6, -2.6), (2.6, -2.0)), ((-2.6, -2.0), (-2.0, 1.5)), ((2.0, -2.0), (2.6, 1.5))):
        k.box((a[0] + 0.06, a[1] + 0.06, 0), (b[0] - 0.06, b[1] - 0.06, 1.05), m["wood"], "Counter")
        # vertical bamboo slats on all outer faces of the counter
        for (q0, q1) in (((a[0], a[1]), (b[0], a[1])), ((a[0], b[1]), (b[0], b[1])), ((a[0], a[1]), (a[0], b[1])),
                         ((b[0], a[1]), (b[0], b[1]))):
            q0, q1 = Vector((*q0, 0)), Vector((*q1, 0))
            ns = int((q1 - q0).length / 0.085)
            for j in range(ns + 1):
                q = q0.lerp(q1, j / max(ns, 1))
                k.bamboo(q, q + Vector((0, 0, 1.05)), 0.042, m["bamboo"], seg_len=0.35, segs=8, base="Slat")
        k.box((a[0] - 0.1, a[1] - 0.1, 1.05), (b[0] + 0.1, b[1] + 0.1, 1.15), m["wood"], "CounterTop")
    for i in range(5):
        x = -2 + i * 1.0
        for lx, ly in ((-0.12, -0.12), (0.12, -0.12), (0, 0.13)):
            k.bamboo((x + lx, -3.3 + ly, 0), (x + lx * 0.5, -3.3 + ly * 0.5, 0.72), 0.025, m["bamboo"], 0.25, 8, "StoolLeg")
        k.disc((x, -3.3, 0.72), 0.22, 0.07, m["wood"], base="StoolSeat")
    # back shelf with bottles
    k.box((-1.8, 1.4, 0), (1.8, 1.8, 2.0), m["wood"], "Shelf")
    for i in range(10):
        k.cyl((-1.6 + i * 0.35, 1.5, 2.0), (-1.6 + i * 0.35, 1.5, 2.3), 0.04,
              principled("A_BottleGlass", (0.2, 0.5, 0.2) if i % 2 else (0.4, 0.2, 0.05), 0.05, **{"Transmission Weight": 0.8}),
              base="Bottle")
    # layered thatch roof (two cones) resting on the posts
    k.cyl((0, 0, 3.15), (0, 0, 5.0), 5.0, underlay(), segs=28, r1=0.6, base="RoofLow")
    straw_roof(k, cone_quads(0, 0, 5.0, 3.15, 0.6, 5.0), seed=2)
    k.cyl((0, 0, 4.6), (0, 0, 6.2), 1.4, underlay(), segs=20, r1=0.05, base="RoofTop")
    straw_roof(k, cone_quads(0, 0, 1.4, 4.6, 0.05, 6.2, n=16), seed=3)
    for sx, sy in ((-4.5, -4.5), (4.5, -4.5)):   # tiki torches stuck in the sand
        k.bamboo((sx, sy, 0), (sx, sy, 2.0), 0.05, m["bamboo"], 0.4, 10, "Torch")
        k.cyl((sx, sy, 2.0), (sx, sy, 2.3), 0.09, m["weathered"], r1=0.12, base="TorchCup")
        k.cyl((sx, sy, 2.3), (sx, sy, 2.65), 0.1, m["torchflame"], r1=0.0, base="Flame")
    return k.c


def beach_props(m):
    k = Kit("Asset_BeachProps")
    rng = random.Random(9)
    # lifeguard tower: 4 legs in the sand, platform, ladder, roof
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * 1.2, sy * 1.2, 0), (sx * 1.0, sy * 1.0, 2.5), 0.08, m["weathered"], base="TowerLeg")
    k.box((-1.4, -1.4, 2.5), (1.4, 1.4, 2.65), m["weathered"], "TowerDeck")
    k.box((-1.4, -1.4, 2.65), (1.4, -1.35, 3.6), m["stripe_red"], "TowerWall")
    k.box((-1.6, -1.6, 4.6), (1.6, 1.6, 4.75), m["stripe_red"], "TowerRoof")
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * 1.35, sy * 1.35, 2.65), (sx * 1.35, sy * 1.35, 4.6), 0.05, m["weathered"], base="RoofPost")
    for s in range(8):
        k.box((-0.4, -1.5 - s * 0.25, s * 0.31), (0.4, -1.3 - s * 0.25, s * 0.31 + 0.05), m["weathered"], "LadderStep")
    # beach umbrellas + towels on the sand, driftwood log, rocks
    for i in range(6):
        x, y = -30 + i * 12 + rng.uniform(-2, 2), 6 + rng.uniform(-2, 2)
        k.cyl((x, y, -0.3), (x, y, 2.3), 0.03, m["trim"], base="UmbPole")
        k.cyl((x, y, 2.0), (x, y, 2.5), 1.4, m["stripe_red"] if i % 2 else m["canvas_blue"], segs=8, r1=0.04, base="UmbCanopy")
        t = k.box((x + 0.6, y - 1, 0.0), (x + 1.5, y + 0.8, 0.02), m["towel1"] if i % 2 else m["towel2"], "BeachTowel")
    log = k.cyl((20, 2, 0.25), (26, 3.5, 0.3), 0.3, m["weathered"], segs=10, base="Driftwood")
    for i in range(14):
        k.rock((rng.uniform(-45, 45), rng.uniform(-8, -4), 0.1), (rng.uniform(0.4, 1.4), rng.uniform(0.4, 1.2),
                                                                rng.uniform(0.3, 0.7)), m["wetrock"], seed=i)
    return k.c


def pier(m, length=40.0):
    """Wooden pier from the beach into the sea; posts reach the sea floor."""
    k = Kit("Asset_Pier")
    for i in range(int(length / 4) + 1):
        y = -i * 4.0
        for sx in (-1.2, 1.2):
            k.cyl((sx, y, -3.0), (sx, y, 1.2), 0.15, m["weathered"], base="PierPost")
        k.box((-1.4, y - 0.15, 0.95), (1.4, y + 0.15, 1.1), m["weathered"], "Joist")
    k.box((-1.4, -length, 1.1), (1.4, 0, 1.2), m["wood"], "Deck")
    for sx in (-1.3, 1.3):
        k.box((sx - 0.05, -length, 1.2), (sx + 0.05, 0, 2.1), m["weathered"], "Rail")
    return k.c


# ------------------------------------------------------------------ ruins

def ruins(m):
    """Jungle temple: 6 stepped tiers of stone, real stairs, doorway with lintel (blob factory entrance),
    glowing slime pipes, fallen columns, carved heads, vines."""
    k = Kit("Asset_Ruins")
    rng = random.Random(4)
    tier_h = 3.5
    k.box((-21, -23, -8), (21, 21, 0.02), m["stone"], "Foundation", bevel=0.1)
    for i in range(6):
        s = 40 - i * 6
        k.box((-s / 2, -s / 2, i * tier_h), (s / 2, s / 2, (i + 1) * tier_h), m["stone"], "Tier", bevel=0.12)
        # cornice ledge on each tier (rests on the tier top edge)
        k.box((-s / 2 - 0.3, -s / 2 - 0.3, (i + 1) * tier_h - 0.35), (s / 2 + 0.3, s / 2 + 0.3, (i + 1) * tier_h),
              m["stone"], "Ledge", bevel=0.06)
    top = 6 * tier_h
    k.box((-4, -4, top), (4, 4, top + 5.5), m["stone"], "Shrine", bevel=0.15)
    k.box((-1.4, -4.05, top), (1.4, -3.6, top + 3.2), principled("A_ShrineDark", (0.01, 0.01, 0.01), 0.9), "ShrineDoor")
    # front stairs: real steps from the ground to the top, south face
    n = 60
    for s_ in range(n):
        z0 = s_ * top / n
        y = -20 + s_ * (20 - 4) / n
        k.box((-3, y - 0.6, 0), (3, y, z0 + top / n), m["stone"], "Stair")
    for sx in (-1, 1):
        sb = k.boxc((sx * 3.3, -12, top / 2 - 1), (0.6, 18.5, 0.8), m["stone"], base="StairWall")
        sb.rotation_euler = (math.atan2(top, 16), 0, 0)
    # blob factory doorway at ground level (west of the stairs) with lintel + glow
    k.box((-12, -20.6, 0), (-6, -20.0, 5.5), m["stone"], "DoorFrame")
    k.box((-11, -20.65, 0), (-7, -20.4, 4.3), m["slimeglow"], "FactoryGlow")
    k.box((-12.5, -20.8, 5.5), (-5.5, -19.8, 6.3), m["stone"], "Lintel", bevel=0.08)
    # slime pipes: from the doorway along the ground to the jungle (clamped, resting on supports)
    for p_ in range(3):
        y = -22 - p_ * 1.0
        pts = [(-9 + p_ * 0.8, -20.6, 0.6), (-9 + p_ * 0.8, y, 0.6), (-16, y - 2, 0.6), (-21, y - 3.5, -2.5)]
        k.tube(pts, 0.28, m["slimeglow"], segs=12, base="Pipe")
        for j in range(3):
            q = Vector(pts[1]).lerp(Vector(pts[2]), j / 3)
            k.box((q.x - 0.15, q.y - 0.4, 0), (q.x + 0.15, q.y + 0.4, 0.35), m["darkmetal"], "PipeStand")
    # carved heads on the stair base, fallen + standing columns
    for sx in (-1, 1):
        k.rock((sx * 4.2, -20.5, 1.2), (1.2, 1.0, 1.2), m["stone"], seed=10 + sx, base="StoneHead")
    for i in range(6):
        x, y = rng.uniform(-30, 30), rng.uniform(-30, 30)
        if abs(x) < 23 and abs(y) < 23:
            continue
        if rng.random() < 0.5:
            k.cyl((x, y, 0), (x, y, rng.uniform(2, 5)), 0.6, m["stone"], segs=12, base="Column")
        else:
            a = rng.uniform(0, math.tau)
            k.cyl((x, y, 0.55), (x + math.cos(a) * 4, y + math.sin(a) * 4, 0.55), 0.55, m["stone"], segs=12, base="FallenColumn")
    # vines hanging from the ledges
    vine = principled("A_Vine", (0.08, 0.25, 0.05), 0.7)
    for i in range(40):
        t = rng.randrange(6)
        s = 40 - t * 6
        side = rng.randrange(4)
        u = rng.uniform(-s / 2, s / 2)
        top_z = (t + 1) * tier_h
        x, y = ((u, -s / 2 - 0.35), (u, s / 2 + 0.35), (-s / 2 - 0.35, u), (s / 2 + 0.35, u))[side]
        L = rng.uniform(1.5, tier_h * 1.8)
        pts = [(x, y, top_z - 0.1 - L * j / 6 + 0.05 * math.sin(j)) for j in range(7)]
        k.tube(pts, 0.04, vine, segs=6, base="Vine")
    return k.c


# ------------------------------------------------------------------ cave

def cave(m):
    """Cave mouth in a rock face, behind a small waterfall: rock arch (voxel-merged boulders with a tunnel
    cut through), dark wet tunnel going in, plunge pool, waterfall curtain in front."""
    k = Kit("Asset_Cave")
    rng = random.Random(21)
    parts = []
    for i in range(14):    # rock mass around the mouth
        a = i / 13 * math.pi
        parts.append(k.rock((math.cos(a) * 7.5, rng.uniform(0, 3), 1 + math.sin(a) * 8),
                            (rng.uniform(3, 5), rng.uniform(3, 4.5), rng.uniform(2, 3.2)), m["rock"], seed=i, boxy=True))
    for i in range(10):
        parts.append(k.rock((rng.uniform(-14, 14), rng.uniform(2, 10), rng.uniform(0, 14)),
                            (rng.uniform(5, 8), rng.uniform(5, 7), rng.uniform(3, 5)), m["rock"], seed=50 + i, boxy=True))
    bm = bmesh.new()
    for p in parts:
        tmp = bmesh.new()
        tmp.from_mesh(p.data)
        tmp.transform(p.matrix_world)
        me_t = bpy.data.meshes.new("t")
        tmp.to_mesh(me_t)
        tmp.free()
        bm.from_mesh(me_t)
        bpy.data.meshes.remove(me_t)
        bpy.data.objects.remove(p)
    mass = k._ob("RockMass", bm, m["rock"], smooth=True)
    # tunnel: a rounded cutter into the rock (cut first, then remesh -> one clean closed surface)
    bmc = bmesh.new()
    bmesh.ops.create_cone(bmc, cap_ends=True, segments=24, radius1=3.0, radius2=2.4, depth=18)
    bmesh.ops.rotate(bmc, verts=bmc.verts, matrix=Matrix.Rotation(math.radians(-90), 3, "X"))
    bmesh.ops.scale(bmc, vec=(1.0, 1.0, 1.3), verts=bmc.verts)
    bmesh.ops.translate(bmc, vec=(0, 5.5, 2.4), verts=bmc.verts)
    cut = k._ob("TunnelCut", bmc, None)
    bo = mass.modifiers.new("Tunnel", "BOOLEAN")
    bo.operation = "DIFFERENCE"
    bo.object = cut
    bo.solver = "EXACT"
    bo.use_self = True
    rm = mass.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = 0.25
    for nm, size, strength in (("Crag", 3.0, 0.75), ("Chip", 0.8, 0.22)):
        tx = bpy.data.textures.get("T_" + nm) or bpy.data.textures.new("T_" + nm, "VORONOI")
        tx.noise_scale = size
        tx.distance_metric = "DISTANCE"
        tx.weight_1, tx.weight_2 = -1.0, 1.0      # F2-F1: sharp ridges and cracks
        d = mass.modifiers.new(nm, "DISPLACE")
        d.texture = tx
        d.texture_coords = "GLOBAL"
        d.strength = strength
        d.mid_level = 0.5
    _apply(mass)
    for p in mass.data.polygons:
        p.use_smooth = True
    bpy.data.objects.remove(cut)
    # tunnel floor (wet dark rock) + darkness further in
    k.box((-2.6, -3, -0.2), (2.6, 14, 0.05), m["wetrock"], "TunnelFloor")
    k.box((-3.5, 6, -0.5), (3.5, 7, 6), principled("A_Dark", (0, 0, 0), 1.0), "Darkness")
    # dark opening right behind the curtain (same shape as the tunnel) so the mouth reads as a deep cave
    dk = k.cyl((0, -0.7, 2.4), (0, 0.0, 2.4), 2.9, principled("A_Dark", (0, 0, 0), 1.0), segs=24, base="MouthDark")
    dk.scale = (1, 1, 1)
    for v in dk.data.vertices:
        v.co.z = 2.4 + (v.co.z - 2.4) * 1.25
        v.co.z = max(v.co.z, 0.0)
    # plunge pool + small waterfall curtain in front of the mouth (falls from the rock lip)
    k.box((-6, -6, -0.6), (6, -0.5, -0.1), m["slimewater"], "Pool")
    wf = falling_water("A_SmallFall")
    bm = bmesh.new()
    rows = []
    for j in range(21):
        t = j / 20
        rows.append([bm.verts.new(((i / 8 - 0.5) * 6.2, -1.2 - 1.5 * t * t, 8.0 * (1 - t))) for i in range(9)])
    for j in range(20):
        for i in range(8):
            bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    k._ob("Curtain", bm, wf, smooth=True)
    for i in range(8):   # rocks around the pool
        a = i / 8 * math.tau
        k.rock((math.cos(a) * 6.5, -3.2 + math.sin(a) * 3.5, 0), (1.2, 1.0, 0.7), m["wetrock"], seed=80 + i)
    return k.c


# ------------------------------------------------------------------ ship

def ship(m):
    """Crashed saucer: hull with panel rim, dome, broken landing legs, hull breach leaking glowing slime,
    debris and a scorched ground ring."""
    k = Kit("Asset_Ship")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=96, v_segments=32, radius=30)
    bmesh.ops.scale(bm, vec=(1, 1, 0.2), verts=bm.verts)
    hull = k._ob("Hull", bm, m["hull"], smooth=True)
    # rim ring + panel lights
    bm = bmesh.new()
    rows = []
    for j in range(4):
        z = (j - 1.5) * 0.6
        r = 30.5 - abs(j - 1.5) * 0.4
        rows.append([bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)) for a in [math.tau * i / 96 for i in range(96)]])
    for j in range(3):
        for i in range(96):
            bm.faces.new((rows[j][i], rows[j][(i + 1) % 96], rows[j + 1][(i + 1) % 96], rows[j + 1][i]))
    k._ob("Rim", bm, m["darkmetal"], smooth=True)
    lights = principled("A_ShipLights", (0.4, 1, 0.4), 0.2, **{"Emission Color": (0.4, 1, 0.4, 1), "Emission Strength": 15})
    for i in range(24):
        a = i / 24 * math.tau
        k.disc((math.cos(a) * 30.6, math.sin(a) * 30.6, -0.25), 0.6, 0.5, lights, segs=12, base="Light")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=9)
    bmesh.ops.scale(bm, vec=(1, 1, 0.6), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, 0, 4.5), verts=bm.verts)
    k._ob("Dome", bm, principled("A_DomeGlass", (0.4, 0.9, 0.5), 0.02, **{"Transmission Weight": 0.8,
                                                                          "Emission Color": (0.3, 1, 0.3, 1),
                                                                          "Emission Strength": 1.5}), smooth=True)
    # landing legs: two intact, one broken off (lying on the ground next to the hull)
    for i, a in enumerate((0.3, 2.4)):
        p = Vector((math.cos(a) * 18, math.sin(a) * 18, -5))
        k.cyl(p + Vector((0, 0, 5)), p, 0.8, m["darkmetal"], base="Leg")
        k.disc(p - Vector((0, 0, 0.3)), 2.0, 0.3, m["darkmetal"], base="Foot")
    k.cyl((-20, -25, 0.8), (-14, -30, 0.8), 0.8, m["darkmetal"], base="BrokenLeg")
    # breach on the front edge with slime pouring out (toward the waterfall, south)
    k.rock((0, -27, -1), (7, 3, 3), principled("A_Breach", (0.02, 0.03, 0.02), 0.6), seed=3, base="Breach")
    k.tube([(0, -27, -1), (0, -33, -4), (0, -38, -10), (0, -40, -18)], 1.6, m["slimeglow"], segs=12, base="SlimeLeak")
    rng = random.Random(8)
    for i in range(16):   # debris plates on the ground
        x, y = rng.uniform(-45, 45), rng.uniform(-45, 45)
        if math.hypot(x, y) < 32:
            continue
        d = k.boxc((x, y, -5.8), (rng.uniform(1, 4), rng.uniform(1, 3), 0.15), m["hull"], rot=rng.uniform(0, 3))
        d.rotation_euler = (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 3))
    k.disc((0, 0, -7.0), 36, 0.6, m["scorch"], segs=64, base="Scorch")
    return k.c


def weather_all(m):
    weather(m["stucco"], streaks=0.5, crevice=0.55, base_dirt=0.45)
    weather(m["trim"], streaks=0.35, crevice=0.5, base_dirt=0.3)
    weather(m["deck"], streaks=0.0, crevice=0.45, base_dirt=0.0, grime=(0.45, 0.42, 0.36))
    weather(m["terracotta"], streaks=0.3, crevice=0.6, base_dirt=0.0, grime=(0.25, 0.25, 0.22))
    weather(m["stone"], streaks=0.6, crevice=0.8, base_dirt=0.6, grime=(0.15, 0.17, 0.1))
    weather(m["wood"], streaks=0.3, crevice=0.6, base_dirt=0.5)
    weather(m["weathered"], streaks=0.45, crevice=0.6, base_dirt=0.5)
    weather(m["bamboo"], streaks=0.2, crevice=0.5, base_dirt=0.4)
    weather(m["hull"], streaks=0.7, crevice=0.7, base_dirt=0.8, grime=(0.12, 0.1, 0.08))
    weather(m["alu"], streaks=0.25, crevice=0.4, base_dirt=0.2)
    weather(m["sign_board"], streaks=0.4, crevice=0.4, base_dirt=0.0)
    weather(m["cushion"], streaks=0.0, crevice=0.35, base_dirt=0.0, grime=(0.7, 0.68, 0.62))


def build_all():
    m = M()
    weather_all(m)
    return dict(hotel=hotel(m), room304=room304(m), pool=pool_area(m), dive=dive_shop(m), tiki=tiki_bar(m), beach=beach_props(m),
                pier=pier(m), ruins=ruins(m), cave=cave(m), ship=ship(m)), m
