"""Rex Magnum - blob aliens (cartoon, original design).

One builder for all blob types (brief section 4):
  gumdrop body melting into a slime puddle, no legs, no mouth, clear jelly body with a
  small pink brain inside, big white cartoon eyes in slime sockets, long tentacles
  (thick at the body, thin tip, S-bend, curled end) that grow out like melted wax.
The body, puddle, sockets, drips and tentacles are joined into ONE smooth mesh with a
voxel remesh (no gaps, no seams). Eyes and brain are separate objects inside/on it.
Units: meters, Z up, front = -Y (same as Rex).
"""
import math
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

# ------------------------------------------------------------------ specs

BLOB_DATA = {}   # filled by build_blob: tentacle paths, eyes, size (used by blob_rig.py)

BLOB_TYPES = {
    # name: height, body radius, colors (light, mid, dark), eyes, eye layout, tentacles, thickness
    "Tourist": dict(height=1.25, radius=0.42, colors=((0.45, 0.95, 0.25), (0.16, 0.7, 0.12), (0.05, 0.35, 0.06)),
                    eyes=3, eye_r=0.115, stalks=False, tentacles=4, t_len=1.15, t_root=0.11, puddle=1.7,
                    squash=1.0),
    "Lifeguard": dict(height=1.9, radius=0.3, colors=((1.0, 0.35, 0.3), (0.85, 0.08, 0.06), (0.4, 0.02, 0.03)),
                      eyes=2, eye_r=0.1, stalks=True, tentacles=2, t_len=1.3, t_root=0.09, puddle=1.5, squash=1.0),
    "Bouncer": dict(height=2.15, radius=0.75, colors=((0.7, 0.45, 1.0), (0.42, 0.15, 0.8), (0.18, 0.05, 0.38)),
                    eyes=1, eye_r=0.24, stalks=False, tentacles=4, t_len=1.4, t_root=0.19, puddle=1.45, squash=1.0),
    "KingGlorp": dict(height=4.2, radius=1.6, colors=((1.0, 0.88, 0.35), (0.95, 0.66, 0.08), (0.55, 0.32, 0.02)),
                      eyes=5, eye_r=0.26, stalks=False, tentacles=6, t_len=3.0, t_root=0.38, puddle=1.5, squash=1.0,
                      crown=True),
}


# ------------------------------------------------------------------ helpers

def _link(ob, coll):
    coll.objects.link(ob)
    return ob


def _collection(name):
    c = bpy.data.collections.get(name)
    if c:
        for o in list(c.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def _mesh_from(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def gumdrop_profile(h, r, puddle):
    """(radius, z) points from the puddle edge up to the top of the head."""
    pts = []
    # puddle: thin, wide, melting lip
    pr = r * puddle
    pts += [(pr, 0.0), (pr * 0.97, 0.025), (pr * 0.85, 0.045), (r * 1.15, 0.06)]
    # body: wide bottom, gumdrop shoulders, round top
    n = 26
    for i in range(1, n + 1):
        t = i / n
        z = 0.06 + t * (h - 0.06)
        # gumdrop: radius stays wide low, then rounds to 0 at the top
        k = 1 - t
        rad = r * (1.05 - 0.15 * t) * math.sqrt(max(0.0, 1 - (max(0.0, t - 0.35) / 0.65) ** 2.2))
        if t < 0.15:
            rad = r * (1.15 - t)                     # slight sag (melting)
        pts.append((max(rad, 0.0), z))
    pts[-1] = (0.0, h)
    return pts


def lathe(name, profile, segs=64, wobble=0.03, seed=0):
    bm = bmesh.new()
    rings = []
    for (rad, z) in profile:
        ring = []
        for i in range(segs):
            a = math.tau * i / segs
            w = 1 + wobble * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, z * 3 + seed)))
            ring.append(bm.verts.new((math.cos(a) * rad * w, math.sin(a) * rad * w, z)))
        rings.append(ring)
    for j in range(len(rings) - 1):
        for i in range(segs):
            a, b = rings[j][i], rings[j][(i + 1) % segs]
            c, d = rings[j + 1][(i + 1) % segs], rings[j + 1][i]
            bm.faces.new((a, b, c, d))
    bm.faces.new(list(reversed(rings[0])))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _mesh_from(name, bm)


def tube(name, pts, radii, segs=16):
    """Tube along points with a radius per point (closed ends)."""
    bm = bmesh.new()
    rings = []
    prev_n = None
    for i, (p, r) in enumerate(zip(pts, radii)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if prev_n is None:
            prev_n = t.orthogonal().normalized()
        n = (prev_n - t * prev_n.dot(t)).normalized()
        prev_n = n
        b = t.cross(n)
        ring = [bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * r)
                for a in [math.tau * k / segs for k in range(segs)]]
        rings.append(ring)
    for j in range(len(rings) - 1):
        for k in range(segs):
            bm.faces.new((rings[j][k], rings[j][(k + 1) % segs], rings[j + 1][(k + 1) % segs], rings[j + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _mesh_from(name, bm)


def sphere_mesh(name, r, loc, scale=(1, 1, 1), segs=24):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=segs // 2, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return _mesh_from(name, bm)


def tentacle_path(root, out_dir, length, curl_side, seed):
    """S-bend that rises, dips, then ends in a curl (points from root to tip)."""
    rnd = random.Random(seed)
    up = Vector((0, 0, 1))
    side = out_dir.cross(up).normalized()
    pts = []
    n = 34
    for i in range(n + 1):
        t = i / n
        d = t * length * 0.8
        s_bend = math.sin(t * math.tau * 0.9) * 0.18 * length * t
        drop = -0.35 * length * t * t + 0.18 * length * math.sin(t * math.pi)
        pts.append(root + out_dir * d + side * s_bend * curl_side + up * drop)
    # curl at the tip (spiral, getting smaller)
    tip = pts[-1]
    tdir = (pts[-1] - pts[-2]).normalized()
    cn = tdir.cross(side).normalized()
    r0 = 0.09 * length
    for k in range(1, 22):
        a = k / 21 * math.tau * 1.15
        rr = r0 * (1 - 0.65 * k / 21)
        pts.append(tip + tdir * math.sin(a) * rr + cn * (1 - math.cos(a)) * rr * curl_side +
                   side * 0.005 * k)
    return pts


# ------------------------------------------------------------------ materials

def jelly_material(name, colors):
    """Clear gummy jelly: transmission + green absorption, 2-3 shades, wet shine."""
    light, mid, dark = colors
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    p.inputs["Transmission Weight"].default_value = 0.92
    p.inputs["Roughness"].default_value = 0.04
    p.inputs["IOR"].default_value = 1.38
    p.inputs["Coat Weight"].default_value = 0.6
    p.inputs["Coat Roughness"].default_value = 0.02
    # shade mix: dark at the bottom (puddle), light at the top, noise blotches
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 3.5
    nz.inputs["Detail"].default_value = 3.0
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    zr = nt.nodes.new("ShaderNodeMapRange")
    zr.inputs["From Min"].default_value = 0.0
    zr.inputs["From Max"].default_value = 1.0
    nt.links.new(sep.outputs[2], zr.inputs["Value"])
    add = nt.nodes.new("ShaderNodeMath")
    add.operation = "ADD"
    nt.links.new(zr.outputs[0], add.inputs[0])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = 0.35
    nt.links.new(nz.outputs["Fac"], mul.inputs[0])
    nt.links.new(mul.outputs[0], add.inputs[1])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0.15, (*dark, 1)
    e[1].position, e[1].color = 0.95, (*light, 1)
    mid_el = e.new(0.55)
    mid_el.color = (*mid, 1)
    nt.links.new(add.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], p.inputs["Base Color"])
    # a little subsurface so thin parts glow like candy
    p.inputs["Subsurface Weight"].default_value = 0.15
    # inner volume: tinted absorption (looks thick and gummy)
    vol = nt.nodes.new("ShaderNodeVolumeAbsorption")
    vol.inputs["Color"].default_value = (*mid, 1)
    vol.inputs["Density"].default_value = 0.45
    # wet surface: fine bump
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.08
    nz2 = nt.nodes.new("ShaderNodeTexNoise")
    nz2.inputs["Scale"].default_value = 40.0
    nt.links.new(tc.outputs["Object"], nz2.inputs["Vector"])
    nt.links.new(nz2.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs[0], p.inputs["Normal"])
    nt.links.new(p.outputs[0], out.inputs["Surface"])
    nt.links.new(vol.outputs[0], out.inputs["Volume"])
    return m


def brain_material():
    m = bpy.data.materials.get("Blob_Brain") or bpy.data.materials.new("Blob_Brain")
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (1.0, 0.25, 0.5, 1)
    p.inputs["Roughness"].default_value = 0.35
    p.inputs["Subsurface Weight"].default_value = 0.4
    p.inputs["Emission Color"].default_value = (1.0, 0.35, 0.55, 1)
    p.inputs["Emission Strength"].default_value = 2.0
    return m


def eye_materials():
    w = bpy.data.materials.get("Blob_EyeWhite") or bpy.data.materials.new("Blob_EyeWhite")
    w.use_nodes = True
    p = w.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (0.95, 0.95, 0.93, 1)
    p.inputs["Roughness"].default_value = 0.08
    p.inputs["Coat Weight"].default_value = 1.0
    k = bpy.data.materials.get("Blob_Pupil") or bpy.data.materials.new("Blob_Pupil")
    k.use_nodes = True
    p = k.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (0.01, 0.01, 0.012, 1)
    p.inputs["Roughness"].default_value = 0.05
    p.inputs["Coat Weight"].default_value = 1.0
    return w, k


# ------------------------------------------------------------------ builder

def brain_mesh(name, size, seed):
    """Small pink brain: two lobes with folds (no stem)."""
    bm = bmesh.new()
    for sx in (-1, 1):
        res = bmesh.ops.create_icosphere(bm, subdivisions=3, radius=size)
        vs = res["verts"]
        for v in vs:
            c = v.co
            c.x = c.x * 0.55 + sx * size * 0.52
            c.y *= 0.95
            c.z *= 0.72
            fold = noise.noise(Vector((c.x * 9 / size, c.y * 9 / size, c.z * 9 / size + seed)))
            c += c.normalized() * fold * size * 0.08
    me = _mesh_from(name, bm)
    for p in me.polygons:
        p.use_smooth = True
    return me


def build_blob(kind="Tourist", seed=7, voxel=None):
    spec = BLOB_TYPES[kind]
    rnd = random.Random(seed)
    coll = _collection("Blob_" + kind)
    h, r = spec["height"], spec["radius"]
    jelly = jelly_material("Blob_Jelly_" + kind, spec["colors"])

    parts = []
    body = bpy.data.objects.new("tmp_body", lathe("tmp_body", gumdrop_profile(h, r, spec["puddle"]), seed=seed))
    _link(body, coll)
    parts.append(body)

    # Eye sockets (slime bumps) on the front, layout per eye count
    eyes = []
    n = spec["eyes"]
    er = spec["eye_r"]
    if n == 1:
        layout = [(0.0, 0.72)]
    elif n == 2:
        layout = [(-0.5, 0.0), (0.5, 0.0)]
    elif n == 3:
        layout = [(-0.55, 0.7), (0.55, 0.7), (0.0, 0.83)]
    else:
        layout = [((i - (n - 1) / 2) * 0.42, 0.74 + 0.07 * math.cos((i - (n - 1) / 2) * 0.9)) for i in range(n)]
    for i, (lx, lz) in enumerate(layout):
        if spec["stalks"]:
            # eye stalks: short tubes up from the head, eyes on top
            base = Vector((lx * r * 0.6, -r * 0.15, h - 0.08))
            top = base + Vector((lx * 0.12, -0.08, 0.35))
            stalk = [base.lerp(top, k / 10) + Vector((0, 0, 0.03 * math.sin(k / 10 * math.pi))) for k in range(11)]
            ob = bpy.data.objects.new(f"tmp_stalk{i}",
                                      tube(f"tmp_stalk{i}", stalk, [er * 0.55 * (1 - 0.3 * k / 10) for k in range(11)]))
            _link(ob, coll)
            parts.append(ob)
            center = top + Vector((0, -0.02, er * 0.6))
        else:
            z = h * lz
            # find the body surface at this height in the front
            prof = gumdrop_profile(h, r, spec["puddle"])
            rad_here = min(prof, key=lambda pr: abs(pr[1] - z))[0]
            ang = lx * 0.9
            surf = Vector((math.sin(ang) * rad_here, -math.cos(ang) * rad_here, z))
            outward = (surf - Vector((0, 0, z))).normalized()
            sock_c = surf + outward * er * 0.1
            center = surf + outward * er * 0.95          # eye bulges out of its slime socket
            sock = bpy.data.objects.new(f"tmp_socket{i}", sphere_mesh(f"tmp_socket{i}", er * 1.2, sock_c))
            _link(sock, coll)
            parts.append(sock)
        eyes.append((center, er))

    # Tentacles: from the lower-middle body, evenly around (front ones a bit forward)
    T = spec["tentacles"]
    tent_paths = []
    for i in range(T):
        a = math.tau * (i + 0.5) / T + rnd.uniform(-0.15, 0.15)
        out = Vector((math.sin(a), -math.cos(a), 0.0))
        zr = h * rnd.uniform(0.32, 0.45)
        prof = gumdrop_profile(h, r, spec["puddle"])
        rad_here = min(prof, key=lambda pr: abs(pr[1] - zr))[0]
        root = out * rad_here * 0.6 + Vector((0, 0, zr))
        pts = tentacle_path(root, (out + Vector((0, 0, 0.25))).normalized(), spec["t_len"] * rnd.uniform(0.85, 1.1),
                            1 if i % 2 else -1, seed + i)
        m = len(pts)
        radii = [spec["t_root"] * (1 - 0.86 * (k / (m - 1)) ** 0.8) + 0.008 for k in range(m)]
        radii[0] = spec["t_root"] * 1.6          # flare where it grows out of the body (melted wax)
        radii[1] = spec["t_root"] * 1.3
        tent_paths.append([p.copy() for p in pts])
        ob = bpy.data.objects.new(f"tmp_tent{i}", tube(f"tmp_tent{i}", pts, radii))
        _link(ob, coll)
        parts.append(ob)

    # Drips on the body + small blobs on the puddle
    for i in range(7):
        a = rnd.uniform(0, math.tau)
        z = h * rnd.uniform(0.25, 0.7)
        prof = gumdrop_profile(h, r, spec["puddle"])
        rad_here = min(prof, key=lambda pr: abs(pr[1] - z))[0]
        p = Vector((math.sin(a) * rad_here, -math.cos(a) * rad_here, z))
        dr = r * rnd.uniform(0.05, 0.09)
        drip = [p + Vector((0, 0, -k * dr * 0.5)) for k in range(6)]
        radii = [dr * (0.7 + 0.3 * math.sin(k / 5 * math.pi)) for k in range(5)] + [dr * 1.1]
        ob = bpy.data.objects.new(f"tmp_drip{i}", tube(f"tmp_drip{i}", drip, radii, segs=10))
        _link(ob, coll)
        parts.append(ob)

    if spec.get("crown"):
        for i in range(7):
            a = math.tau * i / 7
            base = Vector((math.sin(a) * r * 0.45, -math.cos(a) * r * 0.45, h * 0.93))
            tip = base + Vector((math.sin(a) * 0.15, -math.cos(a) * 0.15, 0.6))
            sp = [base.lerp(tip, k / 8) for k in range(9)]
            ob = bpy.data.objects.new(f"tmp_spike{i}", tube(f"tmp_spike{i}", sp, [0.16 * (1 - k / 8) + 0.01
                                                                                 for k in range(9)]))
            _link(ob, coll)
            parts.append(ob)

    # ---- Join everything into ONE smooth mesh (voxel remesh = no seams)
    bm = bmesh.new()
    for ob in parts:
        bm.from_mesh(ob.data)
    me = _mesh_from("Blob_" + kind, bm)
    for ob in parts:
        old = ob.data
        bpy.data.objects.remove(ob)
        bpy.data.meshes.remove(old)
    blob = _link(bpy.data.objects.new("Blob_" + kind, me), coll)
    rm = blob.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = voxel or max(0.01, h / 110)
    rm.adaptivity = 0.0
    sm = blob.modifiers.new("Smooth", "SMOOTH")
    sm.factor = 0.8
    sm.iterations = 12
    dg = bpy.context.evaluated_depsgraph_get()
    new = bpy.data.meshes.new_from_object(blob.evaluated_get(dg), depsgraph=dg)
    blob.modifiers.clear()
    old = blob.data
    blob.data = new
    bpy.data.meshes.remove(old)
    new.name = "Blob_" + kind
    # keep only the biggest piece (drops off small loose bits)
    bm = bmesh.new()
    bm.from_mesh(new)
    bm.verts.ensure_lookup_table()
    seen, islands = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, isl = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            isl.append(x)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        islands.append(isl)
    islands.sort(key=len, reverse=True)
    loose = [v for isl in islands[1:] for v in isl]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(new)
    bm.free()
    for p in new.polygons:
        p.use_smooth = True
    new.materials.append(jelly)

    # ---- Brain inside (upper body)
    brain = _link(bpy.data.objects.new("Blob_" + kind + "_Brain", brain_mesh("Brain", r * 0.3, seed)), coll)
    brain.location = (0, 0.06 * r, h * 0.58)
    brain.data.materials.append(brain_material())

    # ---- Eyes: white ball + black pupil + shine, looking forward (a bit cross-eyed for comedy)
    white, pupil = eye_materials()
    for i, (c, er_) in enumerate(eyes):
        e = _link(bpy.data.objects.new(f"Blob_{kind}_Eye{i}", sphere_mesh("Eye", er_, (0, 0, 0), segs=32)), coll)
        e.location = c
        e.data.materials.append(white)
        for p in e.data.polygons:
            p.use_smooth = True
        look = Vector((-c.x * 0.25, -1.0, 0.05)).normalized()
        pu = _link(bpy.data.objects.new(f"Blob_{kind}_Pupil{i}",
                                        sphere_mesh("Pupil", er_ * 0.45, (0, 0, 0), scale=(1, 0.45, 1), segs=24)), coll)
        pu.data.materials.append(pupil)
        for p in pu.data.polygons:
            p.use_smooth = True
        pu.rotation_mode = "QUATERNION"
        pu.rotation_quaternion = look.to_track_quat("-Y", "Z")
        pu.location = c + look * er_ * 0.84
        # join the pupil into the eye mesh (one object per eye: easy to rig and to roll)
        bpy.context.view_layer.update()
        bm = bmesh.new()
        bm.from_mesh(e.data)
        tmp = bmesh.new()
        tmp.from_mesh(pu.data)
        tmp.transform(e.matrix_world.inverted() @ pu.matrix_world)
        for f in tmp.faces:
            f.material_index = 1
        me_tmp = bpy.data.meshes.new("tmp")
        tmp.to_mesh(me_tmp)
        tmp.free()
        bm.from_mesh(me_tmp)
        bm.to_mesh(e.data)
        bm.free()
        bpy.data.meshes.remove(me_tmp)
        e.data.materials.append(pupil)
        old = pu.data
        bpy.data.objects.remove(pu)
        bpy.data.meshes.remove(old)
    BLOB_DATA[kind] = dict(h=h, r=r, tentacles=tent_paths, eyes=[(c.copy(), er_) for c, er_ in eyes],
                           brain=brain.location.copy(), stalks=spec["stalks"])
    print(f"[Blob] {kind}: {len(new.polygons)} faces (source)")
    return blob


def render_approval(kind, out_dir, samples=128, res=(900, 1200)):
    import os
    import rex_render
    rex_render.setup(samples=samples, res=res)
    sc = bpy.context.scene
    sc.cycles.transmission_bounces = 12
    sc.cycles.volume_bounces = 2
    sc.cycles.max_bounces = 16
    h = BLOB_TYPES[kind]["height"]
    span = max(h, BLOB_TYPES[kind]["t_len"] * 1.4)
    d = span * 3.2
    mid = (0, 0, h * 0.45)
    os.makedirs(out_dir, exist_ok=True)
    rex_render.shot(out_dir, f"blob_{kind.lower()}_front.png", (0, -d, h * 0.55), mid, 70)
    rex_render.shot(out_dir, f"blob_{kind.lower()}_side.png", (d, 0, h * 0.55), mid, 70)
    rex_render.shot(out_dir, f"blob_{kind.lower()}_three_quarter.png", (-d * 0.7, -d * 0.7, h * 0.75), mid, 70)
