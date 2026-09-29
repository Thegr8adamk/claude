"""Stylized autumn forest set built from code.

build_forest() makes a patch of forest around the origin with a clear path
running along +Y. Trees are collection instances so large forests stay
cheap to render.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector, noise

from lib import CURVE, B, E, link, meta, principled, smooth


def _coll(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
    return c


def bark_mat():
    m = bpy.data.materials.new("Bark")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.85
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (6, 6, 0.8)
    wave = nt.nodes.new("ShaderNodeTexNoise")
    wave.inputs["Scale"].default_value = 4.0
    wave.inputs["Detail"].default_value = 10.0
    wave.inputs["Distortion"].default_value = 0.5
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.08, 0.05, 0.035, 1)
    ramp.color_ramp.elements[1].color = (0.3, 0.2, 0.13, 1)
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.6
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], wave.inputs["Vector"])
    nt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(wave.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def leaf_mat(name, c1, c2):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.6
    b.inputs["Subsurface Weight"].default_value = 0.25
    b.inputs["Subsurface Radius"].default_value = (0.6, 0.5, 0.2)
    b.inputs["Subsurface Scale"].default_value = 0.2
    b.inputs["Sheen Weight"].default_value = 0.5
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 1.2
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*c1, 1)
    ramp.color_ramp.elements[1].color = (*c2, 1)
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.inputs["Scale"].default_value = 14.0
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.8
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tc.outputs["Object"], vor.inputs["Vector"])
    nt.links.new(vor.outputs["Distance"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def blob(name, radius, mat, seed, lumpy=0.35, subdiv=4):
    """Lumpy foliage / rock blob from a noise-displaced icosphere."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    off = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in bm.verts:
        d = v.co.normalized()
        n = noise.noise(d * 1.6 + off) * lumpy + noise.noise(d * 4 + off) * lumpy * 0.3
        v.co = d * radius * (1 + n)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(mat)
    return smooth(ob)


def leaf_card(mat):
    """A small cupped leaf mesh used as a particle instance."""
    ob = bpy.data.objects.get("LeafCard")
    if ob:
        return ob
    me = bpy.data.meshes.new("LeafCard")
    verts = [(0, 0, 0), (0.35, 0.18, 0.04), (0.35, -0.18, 0.04), (0.75, 0.2, 0.06), (0.75, -0.2, 0.06),
             (1.0, 0, 0.02), (0.55, 0, -0.02)]
    faces = [(0, 1, 6), (0, 6, 2), (1, 3, 6), (6, 4, 2), (3, 5, 6), (6, 5, 4)]
    me.from_pydata(verts, [], faces)
    ob = bpy.data.objects.new("LeafCard", me)
    ob.data.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def add_leaves(blob_ob, mat, count=500, size=0.16):
    """Scatter leaf instances over a foliage blob so it reads as real foliage."""
    st = bpy.data.particles.new(blob_ob.name + "_Leaves")
    st.type = "EMITTER"
    st.count = count
    st.frame_start = st.frame_end = 1
    st.lifetime = 100000
    st.emit_from = "FACE"
    st.distribution = "RAND"
    st.physics_type = "NO"
    st.render_type = "OBJECT"
    st.instance_object = leaf_card(mat)
    st.particle_size = size
    st.size_random = 0.5
    st.use_rotations = True
    st.rotation_mode = "NOR"
    st.rotation_factor_random = 0.6
    st.phase_factor_random = 2.0
    mod = blob_ob.modifiers.new("Leaves", "PARTICLE_SYSTEM")
    old = mod.particle_system.settings
    mod.particle_system.settings = st
    bpy.data.particles.remove(old)
    mod.particle_system.seed = hash(blob_ob.name) % 1000


def make_tree(name, seed, bark, leaves, height=7.0, coll=None):
    """One stylized tree: curved trunk, a few branches, clusters of foliage."""
    rnd = random.Random(seed)
    pts, radii = [], []
    x = y = 0.0
    for i in range(6):
        z = height * 0.72 * i / 5
        x += rnd.uniform(-0.12, 0.12)
        y += rnd.uniform(-0.12, 0.12)
        pts.append((x, y, z))
        radii.append(0.26 * (1 - i / 8) + (0.14 if i == 0 else 0))
    trunk = meta(name + "_Trunk", [CURVE(pts, radii, per_seg=4),
                                   *[E((math.cos(a) * 0.25, math.sin(a) * 0.25, 0.08), 0.16, 0.16, 0.12)
                                     for a in (0.3, 2.3, 4.2)]], bark, res=0.06)
    parts = [trunk]
    top = Vector(pts[-1])
    for k in range(rnd.randint(5, 8)):
        a = rnd.uniform(0, math.tau)
        h = rnd.uniform(0.55, 1.05)
        r = rnd.uniform(1.0, 1.7) * height / 7
        c = top + Vector((math.cos(a) * r * 0.8, math.sin(a) * r * 0.8, (h - 0.7) * height * 0.35))
        f = blob(f"{name}_Leaf{k}", rnd.uniform(1.0, 1.5) * height / 7, leaves[k % len(leaves)],
                 seed * 10 + k)
        f.location = c
        f.scale = (1, 1, 0.8)
        link(f)
        add_leaves(f, leaves[k % len(leaves)])
        parts.append(f)
    top_blob = blob(f"{name}_LeafTop", 1.6 * height / 7, leaves[0], seed * 10 + 99)
    top_blob.location = top + Vector((0, 0, 0.6))
    link(top_blob)
    add_leaves(top_blob, leaves[0], count=700)
    parts.append(top_blob)
    coll = coll or _coll("TreeProtos")
    for p in parts:
        for c in p.users_collection:
            c.objects.unlink(p)
        coll.objects.link(p)
    return coll


def ground_mat():
    m = bpy.data.materials.new("ForestFloor")
    m.use_nodes = True
    nt = m.node_tree
    n, L = nt.nodes, nt.links.new
    b = n["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.9
    tc = n.new("ShaderNodeTexCoord")
    nz = n.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.35
    nz.inputs["Detail"].default_value = 6
    ramp = n.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    cr.elements[0].position, cr.elements[0].color = 0.42, (0.06, 0.12, 0.025, 1)   # moss
    cr.elements[1].position, cr.elements[1].color = 0.58, (0.32, 0.12, 0.03, 1)    # red leaves
    e = cr.elements.new(0.72)
    e.color = (0.45, 0.24, 0.04, 1)                                                 # gold leaves
    L(tc.outputs["Object"], nz.inputs["Vector"])
    L(nz.outputs["Fac"], ramp.inputs["Fac"])
    # Dirt path along x ~ 0 with a wobbly edge.
    sep = n.new("ShaderNodeSeparateXYZ")
    absx = n.new("ShaderNodeMath"); absx.operation = "ABSOLUTE"
    wob = n.new("ShaderNodeTexNoise"); wob.inputs["Scale"].default_value = 0.3
    edge = n.new("ShaderNodeMath"); edge.operation = "MULTIPLY_ADD"
    edge.inputs[1].default_value = 1.2
    path = n.new("ShaderNodeMapRange")
    path.inputs["From Min"].default_value = 1.1
    path.inputs["From Max"].default_value = 1.8
    L(tc.outputs["Object"], sep.inputs["Vector"])
    L(sep.outputs["X"], absx.inputs[0])
    L(tc.outputs["Object"], wob.inputs["Vector"])
    L(wob.outputs["Fac"], edge.inputs[0])
    L(absx.outputs[0], edge.inputs[2])
    L(edge.outputs[0], path.inputs["Value"])
    mix = n.new("ShaderNodeMix"); mix.data_type = "RGBA"
    mix.inputs[6].default_value = (0.2, 0.12, 0.06, 1)                              # dirt
    L(path.outputs["Result"], mix.inputs["Factor"])
    L(ramp.outputs["Color"], mix.inputs[7])
    L(mix.outputs[2], b.inputs["Base Color"])
    bump = n.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.5
    fine = n.new("ShaderNodeTexVoronoi"); fine.inputs["Scale"].default_value = 18
    L(tc.outputs["Object"], fine.inputs["Vector"])
    L(fine.outputs["Distance"], bump.inputs["Height"])
    L(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def ground(size=80, path=True):
    me = bpy.data.meshes.new("Ground")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=220, y_segments=220, size=size / 2)
    for v in bm.verts:
        h = noise.noise(Vector((v.co.x * 0.08, v.co.y * 0.08, 0))) * 1.2
        h += noise.noise(Vector((v.co.x * 0.4, v.co.y * 0.4, 5))) * 0.12
        if path:
            h *= min(1.0, abs(v.co.x) / 4.0) ** 1.5
        v.co.z = h
    bm.to_mesh(me)
    bm.free()
    ob = link(bpy.data.objects.new("Ground", me))
    ob.data.materials.append(ground_mat())
    return smooth(ob)


def ground_height(x, y, path=True):
    h = noise.noise(Vector((x * 0.08, y * 0.08, 0))) * 1.2
    h += noise.noise(Vector((x * 0.4, y * 0.4, 5))) * 0.12
    if path:
        h *= min(1.0, abs(x) / 4.0) ** 1.5
    return h


def sky(scn, sun_elev=25, sun_rot=40, strength=0.12, night=False):
    w = bpy.data.worlds.new("Sky")
    scn.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes["Background"]
    if night:
        bg.inputs[0].default_value = (0.02, 0.035, 0.08, 1)
        bg.inputs[1].default_value = 0.6
    else:
        s = nt.nodes.new("ShaderNodeTexSky")
        s.sun_elevation = math.radians(sun_elev)
        s.sun_rotation = math.radians(sun_rot)
        nt.links.new(s.outputs[0], bg.inputs[0])
        bg.inputs[1].default_value = strength
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(90 - sun_elev), 0,
                                                    math.radians(sun_rot + 90)))
    sun = bpy.context.object
    sun.data.energy = 0.3 if night else 3.0
    sun.data.color = (0.6, 0.7, 1.0) if night else (1.0, 0.85, 0.65)
    sun.data.angle = math.radians(2)
    return sun


def mist(density=0.015, color=(0.9, 0.85, 0.75), size=60):
    bpy.ops.mesh.primitive_cube_add(size=size, location=(0, 0, size / 2 - 1))
    ob = bpy.context.object
    ob.name = "Mist"
    m = bpy.data.materials.new("Mist")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.remove(nt.nodes["Principled BSDF"])
    v = nt.nodes.new("ShaderNodeVolumePrincipled")
    v.inputs["Density"].default_value = density
    v.inputs["Color"].default_value = (*color, 1)
    v.inputs["Anisotropy"].default_value = 0.5
    nt.links.new(v.outputs[0], nt.nodes["Material Output"].inputs["Volume"])
    ob.data.materials.append(m)
    ob.visible_shadow = False
    return ob


def build_forest(seed=1, radius=30, count=320, clearing=2.6, palette="autumn", path=True,
                 y_range=(-25, 70)):
    rnd = random.Random(seed)
    bark = bark_mat()
    if palette == "autumn":
        leaves = [leaf_mat("LeafGold", (0.75, 0.42, 0.05), (0.95, 0.65, 0.12)),
                  leaf_mat("LeafRed", (0.55, 0.1, 0.03), (0.85, 0.25, 0.05)),
                  leaf_mat("LeafGreen", (0.18, 0.32, 0.06), (0.42, 0.5, 0.1))]
    else:
        leaves = [leaf_mat("LeafGreen", (0.12, 0.28, 0.05), (0.3, 0.45, 0.08))]
    protos = []
    for i in range(5):
        protos.append(make_tree(f"Tree{i}", seed * 100 + i, bark, leaves,
                                height=rnd.uniform(6, 9), coll=_coll(f"TreeProto{i}")))
    ground(size=180, path=path)
    placed = []
    tries = 0
    while len(placed) < count and tries < count * 30:
        tries += 1
        x, y = rnd.uniform(-radius, radius), rnd.uniform(*y_range)
        if path and abs(x) < clearing and y < 40:  # far trees close off the view down the path
            continue
        if any((x - px) ** 2 + (y - py) ** 2 < 4.4 for px, py in placed):
            continue
        placed.append((x, y))
        inst = bpy.data.objects.new(f"TreeInst{len(placed)}", None)
        inst.instance_type = "COLLECTION"
        inst.instance_collection = rnd.choice(protos)
        inst.location = (x, y, ground_height(x, y, path) - 0.1)
        inst.rotation_euler.z = rnd.uniform(0, math.tau)
        s = rnd.uniform(0.8, 1.25)
        inst.scale = (s, s, s)
        link(inst)
    # Rocks and mushrooms near the path.
    rock = principled("Rock", (0.35, 0.34, 0.3), 0.8, bump=(8, 0.6))
    moss = principled("Moss", (0.14, 0.25, 0.04), 0.9, sheen=0.8, bump=(40, 0.4))
    cap = principled("MushCap", (0.8, 0.12, 0.05), 0.35, coat=0.3, sss=0.2)
    stem = principled("MushStem", (0.92, 0.88, 0.78), 0.6, sss=0.3)
    for i in range(24):
        x = rnd.choice((-1, 1)) * rnd.uniform(clearing * 0.6, clearing + 4)
        y = rnd.uniform(-radius, radius)
        r = blob(f"Rock{i}", rnd.uniform(0.2, 0.7), rnd.choice((rock, moss)), i + 500, lumpy=0.25,
                 subdiv=3)
        r.location = (x, y, ground_height(x, y, path))
        r.scale.z = 0.6
        link(r)
    for i in range(30):
        x = rnd.choice((-1, 1)) * rnd.uniform(clearing * 0.7, clearing + 3)
        y = rnd.uniform(-radius, radius)
        z = ground_height(x, y, path)
        s = rnd.uniform(0.6, 1.4)
        meta(f"MushStem{i}", [B((x, y, z + 0.05 * s), 0.025 * s), B((x, y, z + 0.1 * s), 0.02 * s)],
             stem, res=0.006)
        meta(f"MushCap{i}", [E((x, y, z + 0.13 * s), 0.06 * s, 0.06 * s, 0.035 * s)], cap, res=0.006)
    bush_mats = [leaf_mat("BushGreen", (0.08, 0.2, 0.03), (0.22, 0.38, 0.06)), leaves[0], leaves[1]]
    for i in range(70):
        x = rnd.choice((-1, 1)) * rnd.uniform(clearing * 0.9, clearing + 9)
        y = rnd.uniform(-radius, radius)
        r = rnd.uniform(0.35, 0.9)
        bsh = blob(f"Bush{i}", r, rnd.choice(bush_mats), i + 900, lumpy=0.3, subdiv=3)
        bsh.location = (x, y, ground_height(x, y, path) + r * 0.25)
        bsh.scale.z = 0.75
        link(bsh)
        add_leaves(bsh, bsh.data.materials[0], count=int(260 * r), size=0.09)
    return placed
