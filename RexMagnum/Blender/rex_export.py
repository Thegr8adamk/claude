"""Rex Magnum - make the game version of Rex and export it.

Input: the full-detail Rex .blend (body + look + rig + face keys + animations).
Output (in --out):
  Rex.fbx            third-person model: body, clothes, hair cards, glasses, jewelry, rig, face keys, all actions
  Rex_FPArms.fbx     first-person arms: high detail, same rig and actions
  Rex.glb            web/phone copy
  Textures/*.png     baked skin (color, normal, roughness), hair card, fabric
  Rex_Materials.json material list for the Unity builder
The full-detail .blend is not changed (work happens on a copy in memory, then saved as *_game.blend).

Usage:
  blender -b "Rex_v08.blend" --python rex_export.py -- --out "<folder>" [--size 2048]
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

FACE_KEYS = ("Blink_L", "Blink_R", "Mouth_A", "Mouth_E", "Mouth_O", "Mouth_M", "Smile", "SmugGrin")
B = "mixamorig:"


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = dict(out=os.path.join(HERE, "..", "Export"), size=2048)
    for i, k in enumerate(a):
        if k == "--out":
            o["out"] = a[i + 1]
        if k == "--size":
            o["size"] = int(a[i + 1])
    o["out"] = os.path.abspath(o["out"])
    return o


def ctx(ob):
    return bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob],
                                     selected_editable_objects=[ob])


# ------------------------------------------------------------------ skin bake

def _img(name, size, non_color, alpha=False):
    im = bpy.data.images.get(name)
    if im:
        bpy.data.images.remove(im)
    im = bpy.data.images.new(name, size, size, alpha=alpha, float_buffer=False)
    im.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    return im


def _bake(ob, img, kind, mat, **kw):
    nt = mat.node_tree
    node = nt.nodes.new("ShaderNodeTexImage")
    node.image = img
    for n in nt.nodes:
        n.select = False
    node.select = True
    nt.nodes.active = node
    for o in bpy.context.view_layer.objects:
        o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    with ctx(ob):
        bpy.ops.object.bake(type=kind, margin=8, use_clear=True, **kw)
    nt.nodes.remove(node)


def _px(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def _save(img, arr, path):
    img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def bake_skin(body, tex_dir, size):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 16
    mat = body.data.materials[0]
    # hide everything else, so nothing blocks the bake
    hidden = []
    for o in bpy.data.objects:
        if o != body and not o.hide_render:
            o.hide_render = True
            hidden.append(o)
    sub = body.modifiers.get("Subsurf")
    if sub:
        sub.show_render = False

    bc = _img("Rex_Skin_BC", size, False)
    _bake(body, bc, "DIFFUSE", mat, pass_filter={"COLOR"})
    rough = _img("Rex_Skin_R", size, True)
    _bake(body, rough, "ROUGHNESS", mat)
    nrm = _img("Rex_Skin_N", size, True)
    sc.render.bake.normal_space = "TANGENT"
    _bake(body, nrm, "NORMAL", mat)

    # Face hair mask (R stubble, G brows, B bald top) -> image, via a temporary emission material
    me = body.data
    attr = me.color_attributes.get("Rex_FaceHairMask")
    if attr is not None:
        vals = np.empty(len(attr.data) * 4, dtype=np.float32)
        attr.data.foreach_get("color", vals)
        vals = vals.reshape(-1, 4)
        scalp = {v.index for v in me.vertices for g in v.groups
                 if body.vertex_groups[g.group].name == "scalp" and g.weight > 0.5}
        top = max(me.vertices[i].co.z for i in scalp) if scalp else 0
        for i in scalp:
            vals[i, 2] = 1.0 if me.vertices[i].co.z > top - (top - min(me.vertices[j].co.z for j in scalp)) * 0.45 else 0.0
        attr.data.foreach_set("color", vals.ravel())
        tmp = bpy.data.materials.new("Rex_TmpMask")
        tmp.use_nodes = True
        nt = tmp.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        em = nt.nodes.new("ShaderNodeEmission")
        ca = nt.nodes.new("ShaderNodeVertexColor")
        ca.layer_name = "Rex_FaceHairMask"
        nt.links.new(ca.outputs[0], em.inputs[0])
        nt.links.new(em.outputs[0], out.inputs[0])
        me.materials[0] = tmp
        mask = _img("Rex_Skin_Mask", size, True)
        _bake(body, mask, "EMIT", tmp)
        me.materials[0] = mat
        m = _px(mask)
    else:
        m = np.zeros((size, size, 4), dtype=np.float32)

    # Paint stubble and brows into the color + roughness (game skin)
    rng = np.random.default_rng(58)
    c = _px(bc)
    r = _px(rough)
    speck = (rng.random((size, size)) > 0.7).astype(np.float32)
    speck = 0.6 * speck + 0.4 * np.roll(speck, 1, axis=1)
    stub = np.clip(m[..., 0], 0, 1) * speck
    hair_col = np.array([0.09, 0.075, 0.06])
    gray = np.array([0.35, 0.33, 0.31])
    mixc = np.where((rng.random((size, size)) > 0.6)[..., None], gray, hair_col)
    c[..., :3] = c[..., :3] * (1 - 0.4 * stub[..., None]) + mixc * 0.4 * stub[..., None]
    brow = np.clip(m[..., 1] * 1.5, 0, 1) * (0.6 + 0.4 * rng.random((size, size)))
    c[..., :3] = c[..., :3] * (1 - 0.7 * brow[..., None]) + np.array([0.16, 0.11, 0.06]) * 0.7 * brow[..., None]
    r[..., :3] = np.clip(r[..., :3] + 0.15 * stub[..., None], 0, 1)
    r[..., :3] = r[..., :3] * (1 - 0.45 * m[..., 2:3])          # shiny bald top (sun glint)
    # URP mask: Metallic (R) = 0, Smoothness (A) = 1 - roughness
    ms = np.zeros_like(r)
    ms[..., 3] = 1 - r[..., 0]
    os.makedirs(tex_dir, exist_ok=True)
    _save(bc, c, os.path.join(tex_dir, "Rex_Skin_BC.png"))
    msimg = _img("Rex_Skin_MS", size, True, alpha=True)
    _save(msimg, ms, os.path.join(tex_dir, "Rex_Skin_MS.png"))
    _save(nrm, _px(nrm), os.path.join(tex_dir, "Rex_Skin_N.png"))
    for o in hidden:
        o.hide_render = False
    if sub:
        sub.show_render = True
    print("[Rex] skin baked")


# ------------------------------------------------------------------ geometry prep

def body_to_game(body):
    """Delete helper geometry, bake MPFB targets, keep only the face keys."""
    me = body.data
    kb = me.shape_keys.key_blocks
    n = len(me.vertices)
    for k in FACE_KEYS:
        kb[k].value = 0.0
    basis = np.empty(n * 3, dtype=np.float32)
    kb[0].data.foreach_get("co", basis)
    # current mix without face keys
    mix = basis.copy()
    for k in kb[1:]:
        if k.name in FACE_KEYS or k.value == 0:
            continue
        a = np.empty(n * 3, dtype=np.float32)
        k.data.foreach_get("co", a)
        mix += (a - basis) * k.value
    deltas = {}
    for k in FACE_KEYS:
        a = np.empty(n * 3, dtype=np.float32)
        kb[k].data.foreach_get("co", a)
        deltas[k] = a - basis
    body.shape_key_clear()
    me.vertices.foreach_set("co", mix)
    body.shape_key_add(name="Basis")
    for k in FACE_KEYS:
        sk = body.shape_key_add(name=k)
        sk.data.foreach_set("co", mix + deltas[k])
        sk.value = 0.0
    # delete helpers (not in the 'body' group)
    gi = body.vertex_groups["body"].index
    keep = np.zeros(n, dtype=bool)
    for v in me.vertices:
        for g in v.groups:
            if g.group == gi and g.weight > 0.5:
                keep[v.index] = True
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in range(n) if not keep[i]], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    for name in ("Hide helpers", "Subsurf"):
        md = body.modifiers.get(name)
        if md:
            body.modifiers.remove(md)
    # remove non-bone vertex groups (helpers, joints)
    for g in list(body.vertex_groups):
        if not g.name.startswith(B):
            body.vertex_groups.remove(g)
    print("[Rex] body:", len(me.vertices), "verts,", len(me.shape_keys.key_blocks) - 1, "face keys")


def apply_non_armature(ob):
    """Bake Smooth/Solidify/Subsurf/Bevel into the mesh; keep the Armature modifier."""
    arm_mods = [m for m in ob.modifiers if m.type == "ARMATURE"]
    for m in arm_mods:
        m.show_viewport = False
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = ob.data
    ob.data = me
    for m in list(ob.modifiers):
        if m.type != "ARMATURE":
            ob.modifiers.remove(m)
    for m in arm_mods:
        m.show_viewport = True
    if old.users == 0:
        bpy.data.meshes.remove(old)


def curve_to_mesh(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), depsgraph=dg)
    new = bpy.data.objects.new(ob.name + "_m", me)
    for c in ob.users_collection:
        c.objects.link(new)
    new.parent, new.parent_type, new.parent_bone = ob.parent, ob.parent_type, ob.parent_bone
    new.matrix_world = ob.matrix_world.copy()
    name = ob.name
    bpy.data.objects.remove(ob)
    new.name = name
    return new


def join_children(root, name):
    """Join all mesh children of an empty into one mesh object with the empty's parenting."""
    bm = bmesh.new()
    inv = root.matrix_world.inverted()
    mats = []
    kids = [c for c in root.children if c.type == "MESH"]
    for c in kids:
        tmp = bmesh.new()
        tmp.from_mesh(c.data)
        tmp.transform(inv @ c.matrix_world)
        for m in c.data.materials:
            if m not in mats:
                mats.append(m)
        idx = {i: mats.index(m) for i, m in enumerate(c.data.materials)}
        for f in tmp.faces:
            f.material_index = idx.get(f.material_index, 0)
        me_tmp = bpy.data.meshes.new("tmp")
        tmp.to_mesh(me_tmp)
        tmp.free()
        bm.from_mesh(me_tmp)
        bpy.data.meshes.remove(me_tmp)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    for c in root.users_collection:
        c.objects.link(ob)
    ob.parent, ob.parent_type, ob.parent_bone = root.parent, root.parent_type, root.parent_bone
    ob.matrix_world = root.matrix_world.copy()
    for c in kids:
        bpy.data.objects.remove(c)
    bpy.data.objects.remove(root)
    return ob


def hair_cards(hair_ob, tex_dir, keep_every=8, width=0.006, max_segs=4):
    """Hair curves -> flat ribbon cards (game hair). Card texture: strands with alpha."""
    hc = hair_ob.data
    pos = np.empty(len(hc.points) * 3, dtype=np.float32)
    hc.position_data.foreach_get("vector", pos)
    pos = pos.reshape(-1, 3)
    offs = [c.first_point_index for c in hc.curves] + [len(hc.points)]
    mw = hair_ob.matrix_world
    head_c = Vector(pos.mean(axis=0))
    roots_z = pos[offs[:-1], 2]
    z_cut = roots_z.min() + (roots_z.max() - roots_z.min()) * 0.62   # no cards on the bald top
    verts, faces, uvs = [], [], []
    for ci in range(0, len(hc.curves), keep_every):
        a, b = offs[ci], offs[ci + 1]
        if pos[a, 2] > z_cut:
            continue
        pts = [Vector(p) for p in pos[a:b]]
        if len(pts) - 1 > max_segs:                     # fewer points per card
            step = (len(pts) - 1) / max_segs
            pts = [pts[round(i * step)] for i in range(max_segs + 1)]
        n = len(pts)
        u0 = (ci // keep_every % 8) / 8.0
        base = len(verts)
        for i, p in enumerate(pts):
            t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
            outward = (p - head_c).normalized()
            side = t.cross(outward).normalized() * (width * (1 - 0.6 * i / (n - 1)))
            verts += [p - side, p + side]
            v = i / (n - 1)
            uvs += [(u0, v), (u0 + 1 / 8, v)]
        for i in range(n - 1):
            faces.append((base + 2 * i, base + 2 * i + 1, base + 2 * i + 3, base + 2 * i + 2))
    me = bpy.data.meshes.new("Rex_HairCards")
    me.from_pydata([tuple(v) for v in verts], [], faces)
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            uv.data[li].uv = uvs[me.loops[li].vertex_index]
    for p in me.polygons:
        p.use_smooth = True
    # Card texture: blond strands with alpha
    size = 512
    rng = np.random.default_rng(3)
    img = np.zeros((size, size, 4), dtype=np.float32)
    for _ in range(900):
        x = rng.integers(0, size)
        w = rng.integers(1, 3)
        shade = rng.uniform(0.55, 1.0)
        gray = rng.random() < 0.3
        col = np.array([0.62, 0.5, 0.3]) * shade if not gray else np.array([0.7, 0.68, 0.64]) * shade
        length = rng.uniform(0.6, 1.0)
        img[: int(size * length), x:x + w, :3] = col
        img[: int(size * length), x:x + w, 3] = np.linspace(1.0, 0.3, int(size * length))[:, None]
    im = bpy.data.images.new("Rex_HairCard_BC", size, size, alpha=True)
    path = os.path.join(tex_dir, "Rex_HairCard_BC.png")
    _save(im, img, path)
    mat = bpy.data.materials.new("Rex_HairCard")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    me.materials.append(mat)
    ob = bpy.data.objects.new("Rex_HairCards", me)
    for c in hair_ob.users_collection:
        c.objects.link(ob)
    ob.parent, ob.parent_type, ob.parent_bone = hair_ob.parent, hair_ob.parent_type, hair_ob.parent_bone
    ob.matrix_world = mw.copy()
    print("[Rex] hair cards:", len(faces), "quads")
    return ob


def fp_arms(body, shirt, arm):
    """Separate high-detail first-person arms: hands, forearms, upper arms + short sleeves."""
    arm_bones = {B + n for n in ("LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftHand", "RightHand")}
    arm_bones |= {g.name for g in body.vertex_groups if "Hand" in g.name}

    def make(src, name, thresh):
        ob = src.copy()
        ob.data = src.data.copy()
        ob.name = name
        for c in src.users_collection:
            c.objects.link(ob)
        me = ob.data
        idx = {g.index for g in ob.vertex_groups if g.name in arm_bones}
        keep = []
        for v in me.vertices:
            w = sum(g.weight for g in v.groups if g.group in idx)
            keep.append(w >= thresh)
        if me.shape_keys:
            ob.shape_key_clear()
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i, k in enumerate(keep) if not k], context="VERTS")
        bm.to_mesh(me)
        bm.free()
        return ob

    a = make(body, "Rex_FPArms_Skin", 0.5)
    sub = a.modifiers.new("Subsurf", "SUBSURF")
    sub.levels = 1
    sub.render_levels = 1
    a.modifiers.move(a.modifiers.find("Subsurf"), 0)     # subdivide before skinning
    apply_non_armature(a)
    s = make(shirt, "Rex_FPArms_Sleeves", 0.6)
    bone_anchors(a, arm, [a, s])
    return [a, s]


def bone_anchors(ob, arm, meshes, size=0.001):
    """Unity's "Strip Bones" drops every bone no vertex is weighted to: the arms have no leg skin, so the legs
    vanished and the Humanoid avatar failed ('LeftUpperLeg' not found). One 1 mm triangle per unweighted bone,
    at the bone's head and fully weighted to it, keeps the whole skeleton in any import setting."""
    used = set()
    for m in meshes:
        names = {g.index: g.name for g in m.vertex_groups}
        for v in m.data.vertices:
            for g in v.groups:
                if g.weight > 0.0 and g.group in names:
                    used.add(names[g.group])
    missing = [b for b in arm.data.bones if b.name not in used]
    if not missing:
        return 0
    to_local = ob.matrix_world.inverted() @ arm.matrix_world
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    dl = bm.verts.layers.deform.verify()
    for b in missing:
        g = ob.vertex_groups.get(b.name) or ob.vertex_groups.new(name=b.name)
        p = to_local @ b.head_local
        tri = [bm.verts.new(p + Vector(d) * size) for d in ((0, 0, 0), (1, 0, 0), (0, 0, 1))]
        for v in tri:
            v[dl][g.index] = 1.0
        bm.faces.new(tri)
    bm.to_mesh(ob.data)
    bm.free()
    print("[Rex] FP arms: anchored", len(missing), "unweighted bones")
    return len(missing)


# ------------------------------------------------------------------ export

def export(o):
    tex_dir = os.path.join(o["out"], "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    body = bpy.data.objects["Rex"]
    arm = bpy.data.objects["Rex_Rig"]
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()

    bake_skin(body, tex_dir, o["size"])
    skin = bpy.data.materials.new("Rex_SkinGame")
    skin.use_nodes = True
    sb = skin.node_tree.nodes["Principled BSDF"]
    t = skin.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(tex_dir, "Rex_Skin_BC.png"), check_existing=True)
    skin.node_tree.links.new(t.outputs[0], sb.inputs["Base Color"])
    body.data.materials[0] = skin
    body_to_game(body)

    for ob in list(bpy.data.objects):
        if ob.type == "MESH" and ob.name.startswith("Rex_") and any(m.type != "ARMATURE" for m in ob.modifiers):
            apply_non_armature(ob)
    for ob in list(bpy.data.objects):
        if ob.type == "CURVE":
            curve_to_mesh(ob)
    for root_name, name in (("Rex_GoldChain", "Rex_Chain"), ("Rex_Sunglasses", "Rex_Glasses")):
        root = bpy.data.objects.get(root_name)
        if root:
            join_children(root, name)
    hair = bpy.data.objects.get("Rex_Hair")
    if hair:
        hair_cards(hair, tex_dir)
    for ob in list(bpy.data.objects):
        if ob.type == "CURVES":              # stubble/brows are in the skin texture now
            bpy.data.objects.remove(ob)
    for ob in list(bpy.data.objects):
        if ob.name.startswith(("R_", "RM_")) or ob.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(ob)
    arms = fp_arms(body, bpy.data.objects["Rex_Shirt"], arm)
    # Lighter game meshes (third person). The .blend source keeps full detail.
    for name, ratio in (("Rex_Chain", 0.3), ("Rex_TeethUpper", 0.25), ("Rex_TeethLower", 0.25),
                        ("Rex_Glasses", 0.5), ("Rex_Socks", 0.4), ("Rex_Strap", 0.4), ("Rex_WatchBand", 0.5),
                        ("Rex_PinkyRing", 0.5)):
        for ob in [x for x in bpy.data.objects if x.type == "MESH" and x.name.startswith(name)]:
            md = ob.modifiers.new("Decimate", "DECIMATE")
            md.ratio = ratio
            apply_non_armature(ob)

    acts = [a for a in bpy.data.actions if a.name.startswith("Rex_")]

    def fbx(path, objs):
        for x in bpy.context.view_layer.objects:
            x.select_set(False)
        for x in objs:
            x.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False,
            mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False, primary_bone_axis="Y",
            secondary_bone_axis="X", armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_actions=True,
            bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5,
            path_mode="STRIP")
        print("[Rex] exported", os.path.basename(path))

    tp_objs = [arm] + [x for x in bpy.data.objects if x.type == "MESH" and not x.name.startswith("Rex_FPArms")]
    fp_objs = [arm] + arms + [bpy.data.objects[n] for n in ("Rex_PinkyRing", "Rex_WatchBand", "Rex_WatchFace")
                             if n in bpy.data.objects]
    fbx(os.path.join(o["out"], "Rex.fbx"), tp_objs)
    fbx(os.path.join(o["out"], "Rex_FPArms.fbx"), fp_objs)
    for x in bpy.context.view_layer.objects:
        x.select_set(x in tp_objs)
    bpy.ops.export_scene.gltf(filepath=os.path.join(o["out"], "Rex.glb"), use_selection=True,
                              export_animations=True, export_morph=True, export_skins=True,
                              export_image_format="JPEG")
    print("[Rex] exported Rex.glb")

    # material table for Unity
    rows = []
    for m in bpy.data.materials:
        if not m.users or not m.name.startswith("Rex_"):
            continue
        row = dict(name=m.name, baseMap="", normalMap="", maskMap="", color=[1, 1, 1], metallic=0.0,
                   smoothness=0.5, alphaClip=False, emission=[0, 0, 0])
        if m.name == "Rex_SkinGame":
            row.update(baseMap="Rex_Skin_BC.png", normalMap="Rex_Skin_N.png", maskMap="Rex_Skin_MS.png")
        elif m.name == "Rex_HairCard":
            row.update(baseMap="Rex_HairCard_BC.png", alphaClip=True, smoothness=0.4)
        elif m.name == "Rex_ShirtFlamingo":
            row.update(baseMap="rex_shirt_flamingo.png", smoothness=0.3)
        elif m.node_tree and "Principled BSDF" in m.node_tree.nodes:
            p = m.node_tree.nodes["Principled BSDF"]
            row.update(color=list(p.inputs["Base Color"].default_value)[:3],
                       metallic=float(p.inputs["Metallic"].default_value),
                       smoothness=1.0 - float(p.inputs["Roughness"].default_value))
        rows.append(row)
    import shutil
    shutil.copy(os.path.join(HERE, "textures", "rex_shirt_flamingo.png"), os.path.join(tex_dir, "rex_shirt_flamingo.png"))
    with open(os.path.join(o["out"], "Rex_Materials.json"), "w") as f:
        json.dump({"materials": rows,
                   "faceKeys": list(FACE_KEYS),
                   "animations": [a.name for a in acts]}, f, indent=1)
    for key in list(bpy.data.shape_keys):
        if key.user is None or key.users == 0:
            bpy.data.shape_keys.remove(key) if hasattr(bpy.data.shape_keys, "remove") else None
    bpy.data.orphans_purge(do_recursive=True)
    try:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(o["out"], "Rex_game.blend"), compress=True)
    except RuntimeError as e:
        print("[Rex] note: game .blend not saved:", e)
    print("[Rex] DONE")


if __name__ == "__main__":
    import bpy as _b  # noqa
    import addon_utils
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)
    export(args())
