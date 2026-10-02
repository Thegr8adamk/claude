"""Weapons v2 -> game assets: one baked PBR material per weapon (Base Color, Normal, Metallic/Smoothness),
glass/liquid kept as separate see-through parts. Exports FBX + GLB to the Unity Weapons folder.

  blender -b --python weapons_bake.py -- [--out <Unity Weapons folder>] [--size 2048]
"""
import json
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import weapons as W1   # noqa: E402
import weapons2 as W2  # noqa: E402

ALL = ["Weapon_Bottle", "Weapon_BottleBroken", "Weapon_Towel", "Weapon_Knife", "Weapon_GolfClub", "Weapon_Swordfish",
       "Weapon_Pistol", "Weapon_HarpoonGun", "Weapon_Sniper", "Weapon_Coconut", "Pickup_Ammo"]
BIG = {"Weapon_Sniper", "Weapon_HarpoonGun", "Weapon_Swordfish", "Weapon_GolfClub", "Weapon_Towel"}


def ctx(ob, objs=None):
    return bpy.context.temp_override(object=ob, active_object=ob, selected_objects=objs or [ob],
                                     selected_editable_objects=objs or [ob])


def is_clear(mat):
    if mat is None or not mat.node_tree:
        return False
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED" and n.inputs["Transmission Weight"].default_value > 0.5:
            return True
    return False


def triangulate_ngons(objs):
    """N-gons (caps, bevelled gun parts) as triangles at export: Unity's own triangulation discarded the non-planar /
    concave ones ("A polygon of Mesh ... is self-intersecting and has been discarded"). Quads stay; custom normals
    kept. A modifier (applied by use_mesh_modifiers), so the source mesh is unchanged."""
    for o in objs:
        if o.type != "MESH" or any(m.type == "TRIANGULATE" for m in o.modifiers):
            continue
        if not any(len(p.vertices) > 4 for p in o.data.polygons):
            continue
        md = o.modifiers.new("TriangulateNgons", "TRIANGULATE")
        md.min_vertices = 5
        md.quad_method = "BEAUTY"
        md.ngon_method = "BEAUTY"
        md.keep_custom_normals = True


def join_opaque(coll, name):
    meshes = [o for o in coll.objects if o.type == "MESH" and not all(is_clear(m) for m in o.data.materials if m)
              and o.data.materials and o.name != name]
    if not meshes:
        return None
    # curves already converted; make each mesh single-user and real (no shared data), then join
    for o in meshes:
        o.data = o.data.copy()
    target = meshes[0]
    for o in bpy.context.view_layer.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = target
    with ctx(target, meshes):
        bpy.ops.object.join()
    target.name = name
    return target


def unwrap(ob):
    for o in bpy.context.view_layer.objects:
        o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    while len(ob.data.uv_layers) > 1:
        ob.data.uv_layers.remove(ob.data.uv_layers[-1])
    if not ob.data.uv_layers:
        ob.data.uv_layers.new(name="UVMap")
    with ctx(ob):
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004)
        bpy.ops.object.mode_set(mode="OBJECT")


def _img(name, size, non_color, alpha=False):
    im = bpy.data.images.get(name)
    if im:
        bpy.data.images.remove(im)
    im = bpy.data.images.new(name, size, size, alpha=alpha)
    im.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    return im


def bake_to(ob, img, kind, **kw):
    nodes = []
    for m in ob.data.materials:
        nt = m.node_tree
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = img
        for n in nt.nodes:
            n.select = False
        t.select = True
        nt.nodes.active = t
        nodes.append((nt, t))
    with ctx(ob):
        bpy.ops.object.bake(type=kind, margin=6, use_clear=True, **kw)
    for nt, t in nodes:
        nt.nodes.remove(t)


def bake_metallic(ob, img, socket="Metallic"):
    """Bake one Principled input (Metallic or Base Color) as emission, so metals keep their real color
    and worn-steel / paint-chip masks are kept."""
    saved = []
    for m in ob.data.materials:
        nt = m.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        old = [l.from_socket for l in out.inputs["Surface"].links]
        p = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        em = nt.nodes.new("ShaderNodeEmission")
        if p is not None and p.inputs[socket].is_linked:
            nt.links.new(p.inputs[socket].links[0].from_socket, em.inputs["Color"])
        else:
            v = p.inputs[socket].default_value if p else 0.0
            em.inputs["Color"].default_value = tuple(v) if socket == "Base Color" else (v, v, v, 1)
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        saved.append((nt, out, old, em))
    bake_to(ob, img, "EMIT")
    for nt, out, old, em in saved:
        nt.nodes.remove(em)
        if old:
            nt.links.new(old[0], out.inputs["Surface"])


def px(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def save(img, path, arr=None):
    if arr is not None:
        img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def bake_weapon(name, tex_dir, size):
    coll = bpy.data.collections[name]
    game = join_opaque(coll, name + "_Game")
    if game is None:
        return None
    unwrap(game)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 8
    sc.render.bake.normal_space = "TANGENT"
    # hide everything else while baking
    hidden = [o for o in bpy.data.objects if o != game and not o.hide_render]
    for o in hidden:
        o.hide_render = True
    bc = _img(name + "_BC", size, False)
    bake_metallic(game, bc, "Base Color")
    rough = _img(name + "_R", size, True)
    bake_to(game, rough, "ROUGHNESS")
    metal = _img(name + "_M", size, True)
    bake_metallic(game, metal)
    nrm = _img(name + "_N", size, True)
    bake_to(game, nrm, "NORMAL")
    for o in hidden:
        o.hide_render = False
    r, m = px(rough), px(metal)
    ms = np.zeros_like(r)
    ms[..., 0] = ms[..., 1] = ms[..., 2] = m[..., 0]
    ms[..., 3] = 1.0 - r[..., 0]
    msimg = _img(name + "_MS", size, True, alpha=True)
    save(bc, os.path.join(tex_dir, name + "_BC.png"))
    save(nrm, os.path.join(tex_dir, name + "_N.png"))
    save(msimg, os.path.join(tex_dir, name + "_MS.png"), ms)
    # replace the materials with one game material that uses the baked maps (what Unity gets)
    gm = bpy.data.materials.new(name + "_Mat")
    gm.use_nodes = True
    b = gm.node_tree.nodes["Principled BSDF"]
    t = gm.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(tex_dir, name + "_BC.png"), check_existing=True)
    gm.node_tree.links.new(t.outputs[0], b.inputs["Base Color"])
    game.data.materials.clear()
    game.data.materials.append(gm)
    print(f"[W2] baked {name} ({size}px), {len(game.data.polygons)} faces")
    return gm.name


def main():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.join(HERE, "..", "Unity", "Assets", "RexMagnum", "Weapons")
    size = 2048
    if "--out" in a:
        out = a[a.index("--out") + 1]
    if "--size" in a:
        size = int(a[a.index("--size") + 1])
    out = os.path.abspath(out)
    tex_dir = os.path.join(out, "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    bpy.ops.wm.read_homefile(use_empty=True)
    W2.build_all()
    for ob in list(bpy.data.objects):          # curves (fuse, straps) -> meshes
        if ob.type == "CURVE":
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), depsgraph=dg)
            new = bpy.data.objects.new(ob.name + "_m", me)
            for c in ob.users_collection:
                c.objects.link(new)
            new.matrix_world = ob.matrix_world.copy()
            bpy.data.objects.remove(ob)
    for name in ALL:
        c = bpy.data.collections[name]
        g = next((o for o in c.objects if o.name.startswith("GRIP")), None)
        if g is not None:
            W1.empty("UPREF", g.location + W1.Vector((0, 0, 0.1)), c)
    # towel keeps its own image material + shape key (no join); apply its modifiers first is not possible with
    # shape keys, so remove Solidify (two-sided in Unity)
    t = bpy.data.objects["Weapon_Towel"]
    for m in list(t.modifiers):
        t.modifiers.remove(m)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Weapons_v02.blend"), compress=True)
    rows = []
    for name in ALL:
        if name == "Weapon_Towel":
            rows.append(dict(name="W_Towel", baseMap="towel_stripes.png", normalMap="", maskMap="", color=[1, 1, 1],
                             metallic=0, smoothness=0.1, transparent=False, emission=0, twoSided=True))
            rows.append(dict(name="W_TowelFringe", baseMap="", normalMap="", maskMap="", color=[0.95, 0.94, 0.9],
                             metallic=0, smoothness=0.1, transparent=False, emission=0, twoSided=True))
            import shutil
            shutil.copy(os.path.join(W1.TEX, "towel_stripes.png"), os.path.join(tex_dir, "towel_stripes.png"))
        else:
            mname = bake_weapon(name, tex_dir, size if name in BIG else size // 2)
            if mname:
                rows.append(dict(name=mname, baseMap=name + "_BC.png", normalMap=name + "_N.png",
                                 maskMap=name + "_MS.png", color=[1, 1, 1], metallic=0, smoothness=0.5,
                                 transparent=False, emission=0, twoSided=False))
        # see-through parts keep simple materials
        for o in bpy.data.collections[name].objects:
            if o.type != "MESH":
                continue
            for m in o.data.materials:
                if m and is_clear(m) and not any(r["name"] == m.name for r in rows):
                    p = m.node_tree.nodes.get("Principled BSDF")
                    rows.append(dict(name=m.name, baseMap="", normalMap="", maskMap="",
                                     color=list(p.inputs["Base Color"].default_value)[:3], metallic=0,
                                     smoothness=0.97, transparent=True, emission=0, twoSided=False))
        # spark keeps emission
        if name == "Weapon_Coconut":
            rows.append(dict(name="W2_Spark", baseMap="", normalMap="", maskMap="", color=[1, 0.6, 0.1], metallic=0,
                             smoothness=0.5, transparent=False, emission=8, twoSided=False))
    for name in ALL:
        c = bpy.data.collections[name]
        objs = list(c.all_objects)
        triangulate_ngons(objs)
        for x in bpy.context.view_layer.objects:
            x.select_set(x in objs)
        bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True,
                                 object_types={"MESH", "EMPTY"}, apply_scale_options="FBX_SCALE_ALL",
                                 axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, mesh_smooth_type="FACE",
                                 use_tspace=True, bake_anim=False, path_mode="STRIP")
        bpy.ops.export_scene.gltf(filepath=os.path.join(out, name + ".glb"), use_selection=True,
                                  export_image_format="JPEG")
    with open(os.path.join(out, "Weapons.json"), "w") as f:
        json.dump({"materials": rows, "weapons": ALL}, f, indent=1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Weapons_v02_game.blend"), compress=True)
    print("[W2] DONE")


if __name__ == "__main__":
    main()
