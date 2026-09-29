"""Shared helpers for building the Walt's Way Home characters and sets.

Characters are sculpted from metaball blobs (converted to meshes) so the
forms blend softly, the way Pixar-style characters do. Everything faces -Y,
Z is up, units are meters.
"""
import math

import bmesh
import bpy
from mathutils import Euler, Vector

META_SURFACE = 0.574  # surface radius of a lone metaball of radius 1


# ---------------------------------------------------------------- scene utils

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def link(ob, parent=None):
    bpy.context.scene.collection.objects.link(ob)
    if parent is not None:
        ob.parent = parent
    return ob


def empty(name, loc=(0, 0, 0)):
    ob = bpy.data.objects.new(name, None)
    ob.location = loc
    return link(ob)


def smooth(ob, subsurf=0):
    for p in ob.data.polygons:
        p.use_smooth = True
    if subsurf:
        m = ob.modifiers.new("Subsurf", "SUBSURF")
        m.levels = m.render_levels = subsurf
    return ob


# ---------------------------------------------------------------- metaballs

def B(co, r, neg=False, stiff=2.0):
    """Ball whose surface radius is roughly r."""
    return dict(type="BALL", co=co, radius=r / META_SURFACE, neg=neg, stiff=stiff)


def E(co, rx, ry, rz, rot=(0, 0, 0), neg=False, stiff=2.0):
    """Ellipsoid with rough surface radii rx, ry, rz and rotation in degrees."""
    m = max(rx, ry, rz)
    return dict(type="ELLIPSOID", co=co, radius=m / META_SURFACE,
                size=(rx / m, ry / m, rz / m), rot=rot, neg=neg, stiff=stiff)


def CH(p0, p1, r0, r1, n=None, **kw):
    """Chain of balls from p0 to p1, radius tapering r0 -> r1."""
    p0, p1 = Vector(p0), Vector(p1)
    if n is None:
        n = max(2, int((p1 - p0).length / (min(r0, r1) * 0.6)) + 1)
    return [B(tuple(p0.lerp(p1, i / (n - 1))), r0 + (r1 - r0) * i / (n - 1), **kw)
            for i in range(n)]


def CURVE(points, radii, per_seg=6, **kw):
    """Balls along a poly-line through points with per-point radii."""
    out = []
    for i in range(len(points) - 1):
        a, b = Vector(points[i]), Vector(points[i + 1])
        for j in range(per_seg):
            t = j / per_seg
            out.append(B(tuple(a.lerp(b, t)), radii[i] + (radii[i + 1] - radii[i]) * t, **kw))
    out.append(B(tuple(points[-1]), radii[-1], **kw))
    return out


def mirror(elems):
    """Return elems plus copies mirrored across X (skips elems on the center line)."""
    out = list(elems)
    for e in elems:
        if abs(e["co"][0]) > 1e-6:
            m = dict(e)
            m["co"] = (-e["co"][0], e["co"][1], e["co"][2])
            if "rot" in e:
                rx, ry, rz = e["rot"]
                m["rot"] = (rx, -ry, -rz)
            out.append(m)
    return out


def flatten(items):
    out = []
    for it in items:
        if isinstance(it, list):
            out.extend(flatten(it))
        else:
            out.append(it)
    return out


def meta(name, elems, mat, res=0.012, parent=None, subsurf=0):
    """Build a metaball from elems, convert it to a smooth mesh object."""
    elems = flatten(elems)
    mb = bpy.data.metaballs.new(name + "_mb")
    mb.resolution = mb.render_resolution = res
    mb.threshold = 0.6
    tmp = link(bpy.data.objects.new("MB_" + name, mb))
    for s in elems:
        e = mb.elements.new()
        e.type = s["type"]
        e.co = s["co"]
        e.radius = s["radius"]
        e.stiffness = s["stiff"]
        e.use_negative = s["neg"]
        if s["type"] == "ELLIPSOID":
            e.size_x, e.size_y, e.size_z = s["size"]
            e.rotation = Euler([math.radians(a) for a in s["rot"]]).to_quaternion()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.metaballs.remove(mb)
    me.name = name
    ob = link(bpy.data.objects.new(name, me), parent)
    ob.data.materials.append(mat)
    return smooth(ob, subsurf)


# ---------------------------------------------------------------- mesh parts

def sphere(name, loc, r, mat, parent=None, rot=(0, 0, 0), scale=(1, 1, 1), seg=48):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=1.0)
    bm.to_mesh(me)
    bm.free()
    ob = link(bpy.data.objects.new(name, me), parent)
    ob.location = loc
    ob.rotation_euler = [math.radians(a) for a in rot]
    ob.scale = (r * scale[0], r * scale[1], r * scale[2])
    ob.data.materials.append(mat)
    return smooth(ob)


def eyelid(name, loc, r, mat, parent=None, open_amount=0.35, tilt=0.0, rot=(0, 0, 0)):
    """Upper eyelid: a slightly larger shell that hides the top of the eye.

    open_amount is how far down (in eye radii, from the top) the lid stays
    above the eye center; 1.0 = wide open, 0 = half closed, -1 = closed.
    """
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=1.0)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, open_amount * 0.9),
                           plane_no=(0, math.sin(tilt), -math.cos(tilt)), clear_outer=True)
    bm.to_mesh(me)
    bm.free()
    ob = link(bpy.data.objects.new(name, me), parent)
    ob.location = loc
    ob.rotation_euler = [math.radians(a) for a in rot]
    ob.scale = (r * 1.12,) * 3
    ob.data.materials.append(mat)
    sol = ob.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.08
    return smooth(ob)


def torus(name, loc, R, r, mat, rot=(90, 0, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc,
                                     rotation=[math.radians(a) for a in rot],
                                     major_segments=48, minor_segments=12)
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.append(mat)
    if parent:
        ob.parent = parent
    return smooth(ob)


def rod(name, p0, p1, r, mat, parent=None):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d.length, location=(p0 + p1) / 2,
                                        vertices=16)
    ob = bpy.context.object
    ob.name = name
    ob.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    ob.data.materials.append(mat)
    if parent:
        ob.parent = parent
    return smooth(ob)


def cone(name, loc, r, depth, mat, rot=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cone_add(radius1=r, radius2=r * 0.05, depth=depth, location=loc,
                                    rotation=[math.radians(a) for a in rot], vertices=24)
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.append(mat)
    if parent:
        ob.parent = parent
    return smooth(ob, 1)


def box(name, loc, size, mat, rot=(0, 0, 0), parent=None, bevel=0.3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc,
                                    rotation=[math.radians(a) for a in rot])
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    ob.data.materials.append(mat)
    if parent:
        ob.parent = parent
    bv = ob.modifiers.new("Bevel", "BEVEL")
    bv.width = min(size) * bevel
    bv.segments = 4
    return smooth(ob)


# ---------------------------------------------------------------- materials

def _nodes(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    return m, nt, nt.nodes, nt.links, nt.nodes["Principled BSDF"]


def _bump(nt, bsdf, scale=60.0, strength=0.15, detail=8.0, coords="Object"):
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = scale
    nz.inputs["Detail"].default_value = detail
    bp = nt.nodes.new("ShaderNodeBump")
    bp.inputs["Strength"].default_value = strength
    nt.links.new(tc.outputs[coords], nz.inputs["Vector"])
    nt.links.new(nz.outputs["Fac"], bp.inputs["Height"])
    nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
    return nz


def principled(name, color, rough=0.5, sss=0.0, sheen=0.0, coat=0.0, metal=0.0,
               bump=None, spec=0.5):
    m, nt, n, l, b = _nodes(name)
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Specular IOR Level"].default_value = spec
    if sss:
        b.inputs["Subsurface Weight"].default_value = sss
        b.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
        b.inputs["Subsurface Scale"].default_value = 0.03
    if sheen:
        b.inputs["Sheen Weight"].default_value = sheen
        b.inputs["Sheen Tint"].default_value = (1, 1, 1, 1)
    if coat:
        b.inputs["Coat Weight"].default_value = coat
    if bump:
        _bump(nt, b, *bump)
    return m


def skin(name="Skin", color=(0.85, 0.52, 0.38)):
    m, nt, n, l, b = _nodes(name)
    b.inputs["Roughness"].default_value = 0.42
    b.inputs["Subsurface Weight"].default_value = 0.35
    b.inputs["Subsurface Radius"].default_value = (1.0, 0.4, 0.25)
    b.inputs["Subsurface Scale"].default_value = 0.05
    b.inputs["Coat Weight"].default_value = 0.08
    # Soft red variation (cheeks, nose, knuckles) from low-frequency noise.
    tc = n.new("ShaderNodeTexCoord")
    nz = n.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 6.0
    ramp = n.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*color, 1)
    ramp.color_ramp.elements[1].color = (color[0], color[1] * 0.78, color[2] * 0.8, 1)
    ramp.color_ramp.elements[0].position = 0.4
    ramp.color_ramp.elements[1].position = 0.7
    l.new(tc.outputs["Object"], nz.inputs["Vector"])
    l.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    # Painted blush (see paint_blush): warm red on cheeks, nose and ears.
    attr = n.new("ShaderNodeAttribute")
    attr.attribute_name = "blush"
    mix = n.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs[7].default_value = (color[0] * 0.95, color[1] * 0.42, color[2] * 0.42, 1)
    l.new(attr.outputs["Fac"], mix.inputs["Factor"])
    l.new(ramp.outputs["Color"], mix.inputs[6])
    l.new(mix.outputs[2], b.inputs["Base Color"])
    _bump(nt, b, scale=400.0, strength=0.04)
    return m


def paint_blush(ob, spots):
    """Store a 0-1 'blush' point attribute: spots = [(center, radius, strength)]."""
    me = ob.data
    at = me.attributes.get("blush") or me.attributes.new("blush", "FLOAT", "POINT")
    for v in me.vertices:
        p = v.co  # character-local: body meshes sit at the root's origin
        val = 0.0
        for c, r, k in spots:
            d = (p - Vector(c)).length / r
            if d < 1:
                val = max(val, k * (1 - d * d) ** 2)
        at.data[v.index].value = val
    return at


def fur(name, color, tip=None, scale=90.0, strength=0.35):
    """Fur-like surface for blob bodies: sheen, soft noise and strong fine bump."""
    m, nt, n, l, b = _nodes(name)
    b.inputs["Roughness"].default_value = 0.75
    b.inputs["Sheen Weight"].default_value = 0.35
    b.inputs["Sheen Roughness"].default_value = 0.35
    b.inputs["Sheen Tint"].default_value = (*color, 1)
    b.inputs["Specular IOR Level"].default_value = 0.3
    tc = n.new("ShaderNodeTexCoord")
    nz = n.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 18.0
    ramp = n.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (color[0] * 0.8, color[1] * 0.8, color[2] * 0.8, 1)
    ramp.color_ramp.elements[1].color = (*(tip or color), 1)
    l.new(tc.outputs["Object"], nz.inputs["Vector"])
    l.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    l.new(ramp.outputs["Color"], b.inputs["Base Color"])
    # Stretched noise reads like strands of fur.
    mp = n.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0, 1.0, 0.25)
    wv = n.new("ShaderNodeTexNoise")
    wv.inputs["Scale"].default_value = scale
    wv.inputs["Detail"].default_value = 4.0
    bp = n.new("ShaderNodeBump")
    bp.inputs["Strength"].default_value = strength
    l.new(tc.outputs["Object"], mp.inputs["Vector"])
    l.new(mp.outputs["Vector"], wv.inputs["Vector"])
    l.new(wv.outputs["Fac"], bp.inputs["Height"])
    l.new(bp.outputs["Normal"], b.inputs["Normal"])
    return m


def plaid(name, base=(0.55, 0.04, 0.03), mid=(0.22, 0.02, 0.02), dark=(0.025, 0.02, 0.02),
          freq=9.0):
    m, nt, n, l, b = _nodes(name)
    b.inputs["Roughness"].default_value = 0.8
    b.inputs["Sheen Weight"].default_value = 0.6
    tc = n.new("ShaderNodeTexCoord")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Object"], sep.inputs["Vector"])

    def band(axis):
        mul = n.new("ShaderNodeMath"); mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = freq
        fr = n.new("ShaderNodeMath"); fr.operation = "FRACT"
        lt = n.new("ShaderNodeMath"); lt.operation = "LESS_THAN"
        lt.inputs[1].default_value = 0.42
        l.new(sep.outputs[axis], mul.inputs[0])
        l.new(mul.outputs[0], fr.inputs[0])
        l.new(fr.outputs[0], lt.inputs[0])
        return lt

    ax = band("X")
    az = band("Z")
    add = n.new("ShaderNodeMath"); add.operation = "ADD"
    half = n.new("ShaderNodeMath"); half.operation = "MULTIPLY"
    half.inputs[1].default_value = 0.5
    l.new(ax.outputs[0], add.inputs[0])
    l.new(az.outputs[0], add.inputs[1])
    l.new(add.outputs[0], half.inputs[0])
    ramp = n.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    els[0].color = (*base, 1)
    els[1].position = 0.45
    els[1].color = (*mid, 1)
    e3 = els.new(0.95)
    e3.color = (*dark, 1)
    l.new(half.outputs[0], ramp.inputs["Fac"])
    l.new(ramp.outputs["Color"], b.inputs["Base Color"])
    _bump(nt, b, scale=300.0, strength=0.08)
    return m


def eye(name, iris=(0.25, 0.5, 0.8), iris_size=0.62, pupil_size=0.3, sclera=(0.95, 0.94, 0.9)):
    """Eye for a unit sphere that looks down its local -Y axis."""
    m, nt, n, l, b = _nodes(name)
    b.inputs["Roughness"].default_value = 0.04
    b.inputs["Coat Weight"].default_value = 1.0
    b.inputs["Coat Roughness"].default_value = 0.0
    b.inputs["Subsurface Weight"].default_value = 0.1
    tc = n.new("ShaderNodeTexCoord")
    mp = n.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1, 0, 1)
    ln = n.new("ShaderNodeVectorMath"); ln.operation = "LENGTH"
    l.new(tc.outputs["Object"], mp.inputs["Vector"])
    l.new(mp.outputs["Vector"], ln.inputs[0])
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Object"], sep.inputs["Vector"])
    front = n.new("ShaderNodeMath"); front.operation = "LESS_THAN"
    front.inputs[1].default_value = 0.0
    l.new(sep.outputs["Y"], front.inputs[0])
    # Distance from axis, pushed to 1 (sclera) on the back half.
    inv = n.new("ShaderNodeMath"); inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    l.new(front.outputs[0], inv.inputs[1])
    mx = n.new("ShaderNodeMath"); mx.operation = "MAXIMUM"
    l.new(ln.outputs["Value"], mx.inputs[0])
    l.new(inv.outputs[0], mx.inputs[1])
    ramp = n.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    cr.elements[0].position = pupil_size
    cr.elements[0].color = (0.005, 0.005, 0.005, 1)
    cr.elements[1].position = pupil_size + 0.03
    cr.elements[1].color = (iris[0] * 0.5, iris[1] * 0.5, iris[2] * 0.5, 1)
    e = cr.elements.new(iris_size - 0.08); e.color = (*iris, 1)
    e = cr.elements.new(iris_size - 0.015); e.color = (iris[0] * 0.35, iris[1] * 0.35, iris[2] * 0.35, 1)
    e = cr.elements.new(iris_size + 0.02); e.color = (*sclera, 1)
    l.new(mx.outputs[0], ramp.inputs["Fac"])
    l.new(ramp.outputs["Color"], b.inputs["Base Color"])
    return m


def add_eye(name, loc, r, mat, parent, look=(0, 0, 0)):
    """Eye sphere at loc. look = (pitch_down_deg, 0, yaw_deg)."""
    return sphere(name, loc, r, mat, parent, rot=look)


# ---------------------------------------------------------------- fur particles

def add_fur(ob, mat, count=4000, length=0.03, children=20, radius=0.0015,
            kink=0.0, clump=0.3, rough=0.004, density_vgroup=None):
    """Hair-particle fur on a mesh object. Rendered as Cycles curves."""
    ob.data.materials.append(mat)
    st = bpy.data.particles.new(ob.name + "_Fur")
    st.type = "HAIR"
    st.count = count
    st.hair_length = length
    st.use_advanced_hair = False
    st.child_type = "INTERPOLATED"
    st.child_percent = max(1, children // 4)
    st.rendered_child_count = children
    st.clump_factor = clump
    st.roughness_2 = rough
    st.roughness_endpoint = rough * 0.5
    st.kink = "CURL" if kink else "NO"
    st.kink_amplitude = kink
    st.root_radius = 1.0
    st.tip_radius = 0.0
    st.radius_scale = radius
    st.display_step = 3
    st.render_step = 4
    st.factor_random = 0.0  # random velocity is in meters, not a ratio
    st.brownian_factor = 0.0
    st.hair_length = length  # set last: other settings can rescale it
    # Attach the finished settings; hair made before this uses the defaults.
    mod = ob.modifiers.new("Fur", "PARTICLE_SYSTEM")
    ps = mod.particle_system
    old = ps.settings
    ps.settings = st
    bpy.data.particles.remove(old)
    st.material_slot = mat.name
    if density_vgroup:
        ps.vertex_group_density = density_vgroup
    return ps


def hair_mat(name, color, melanin=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        if nd.type == "BSDF_PRINCIPLED":
            nt.nodes.remove(nd)
    h = nt.nodes.new("ShaderNodeBsdfHairPrincipled")
    h.parametrization = "COLOR"
    h.inputs["Color"].default_value = (*color, 1)
    h.inputs["Roughness"].default_value = 0.45
    h.inputs["Radial Roughness"].default_value = 0.6
    h.inputs["Coat"].default_value = 0.2
    out = nt.nodes["Material Output"]
    nt.links.new(h.outputs[0], out.inputs["Surface"])
    return m


# ---------------------------------------------------------------- face placement

def bvh_of(ob):
    from mathutils.bvhtree import BVHTree
    me = ob.data
    return BVHTree.FromPolygons([v.co.copy() for v in me.vertices],
                                [tuple(p.vertices) for p in me.polygons])


def surf(bvh, x, z, y0=-3.0):
    """First surface point hit by a ray fired along +Y at (x, z), or None."""
    loc, _n, _i, _d = bvh.ray_cast(Vector((x, y0, z)), Vector((0, 1, 0)))
    return loc


def smile_arc(bvh, cx, cz, half_w, lift, n=9):
    """Points on the face surface along a smile curve (ends lifted by `lift`)."""
    pts = []
    for i in range(n):
        t = -1 + 2 * i / (n - 1)
        p = surf(bvh, cx + t * half_w, cz + lift * t * t)
        if p is not None:
            pts.append(p)
    return pts


def remeta(ob, elems, mat, **kw):
    """Rebuild a meta object in place (same name) with new elements."""
    name, parent = ob.name, ob.parent
    me = ob.data
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return meta(name, elems, mat, parent=parent, **kw)
