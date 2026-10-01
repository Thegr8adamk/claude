"""Build all weapons + ammo pickups, export to Unity (FBX per weapon + GLB), render approval pictures.

  blender -b --python build_weapons.py -- [--render] [--out <Unity Weapons folder>]
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import weapons as W  # noqa: E402

ALL = ["Weapon_Bottle", "Weapon_BottleBroken", "Weapon_Towel", "Weapon_Knife", "Weapon_GolfClub", "Weapon_Swordfish",
       "Weapon_Pistol", "Weapon_HarpoonGun", "Weapon_Sniper", "Weapon_Coconut", "Pickup_Ammo"]

# Game colors for materials that are procedural in Blender (Unity gets a texture or a color)
FALLBACK = {
    "W_DarkWood": dict(tex="wood_dark.png"), "W_PistolWood": dict(tex="wood_light.png"),
    "W_Coconut": dict(tex="coconut.png"), "W_FrozenFish": dict(tex="fish.png", smooth=0.85, metal=0.2),
}


def game_textures(tex_dir):
    from PIL import Image, ImageFilter
    rng = np.random.default_rng(10)

    def grain(path, c0, c1, stretch=24):
        n = rng.random((64, 512 // stretch + 1))
        im = Image.fromarray((n * 255).astype(np.uint8)).resize((512, 512), Image.BICUBIC)
        a = np.asarray(im, dtype=np.float32) / 255
        a = 0.5 + 0.5 * np.sin(a * 20)
        col = np.array(c0)[None, None] * (1 - a[..., None]) + np.array(c1)[None, None] * a[..., None]
        Image.fromarray((col * 255).astype(np.uint8)).save(path)

    grain(os.path.join(tex_dir, "wood_dark.png"), (0.06, 0.025, 0.012), (0.22, 0.1, 0.045))
    grain(os.path.join(tex_dir, "wood_light.png"), (0.18, 0.07, 0.025), (0.5, 0.24, 0.1))
    grain(os.path.join(tex_dir, "coconut.png"), (0.05, 0.022, 0.01), (0.2, 0.1, 0.045), stretch=4)
    # fish: u goes around the body; dark blue back on top, silver belly, frost speckles
    u = np.linspace(0, 1, 512)[None, :]
    back = (np.cos(u * 2 * math.pi - math.pi / 2) > 0.1).astype(np.float32)
    back = np.repeat(back, 512, axis=0)
    back = np.asarray(Image.fromarray((back * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(20)),
                      dtype=np.float32) / 255
    col = np.array([0.75, 0.78, 0.82]) * (1 - back[..., None]) + np.array([0.1, 0.13, 0.22]) * back[..., None]
    frost = (rng.random((512, 512)) > 0.82)[..., None] * 0.35
    col = col * (1 - frost) + np.array([0.92, 0.96, 1.0]) * frost
    Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(os.path.join(tex_dir, "fish.png"))


def ensure_uvs(ob):
    me = ob.data
    if me.uv_layers:
        return
    # lathe parts run along Y: cylinder UVs; boxes: projection from the largest axis
    W.cyl_uv(me, u_scale=1.0, v_scale=4.0)


def main():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.join(HERE, "..", "Unity", "Assets", "RexMagnum", "Weapons")
    render = "--render" in a
    if "--out" in a:
        out = a[a.index("--out") + 1]
    out = os.path.abspath(out)
    tex_out = os.path.join(out, "Textures")
    os.makedirs(tex_out, exist_ok=True)
    bpy.ops.wm.read_homefile(use_empty=True)
    W.make_textures()
    game_textures(W.TEX)
    W.build_bottle()
    W.build_bottle(broken=True)
    W.build_towel()
    W.build_knife()
    W.build_golf_club()
    W.build_swordfish()
    W.build_pistol()
    W.build_harpoon_gun()
    W.build_sniper()
    W.build_coconut()
    W.build_ammo()
    # curves (fuse) -> mesh, UVs everywhere, an UPREF marker for orientation in Unity
    for ob in list(bpy.data.objects):
        if ob.type == "CURVE":
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), depsgraph=dg)
            new = bpy.data.objects.new(ob.name, me)
            for c in ob.users_collection:
                c.objects.link(new)
            new.matrix_world = ob.matrix_world.copy()
            bpy.data.objects.remove(ob)
            new.name = "Coconut_Fuse"
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            ensure_uvs(ob)
    for name in ALL:
        c = bpy.data.collections[name]
        g = c.objects.get("GRIP") or next((o for o in c.objects if o.name.startswith("GRIP")), None)
        if g is not None:
            W.empty("UPREF", g.location + Vector((0, 0, 0.1)), c)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Weapons_v01.blend"), compress=True)

    if render:
        W.render_weapons(os.path.join(HERE, "..", "Renders", "weapons"), ALL[:-1], samples=64, res=(900, 675))

    # towel: remove Solidify (shape keys must export); Unity uses a two-sided material
    t = bpy.data.objects["Weapon_Towel"]
    for m in list(t.modifiers):
        t.modifiers.remove(m)
    rows = {}
    for name in ALL:
        c = bpy.data.collections[name]
        objs = list(c.all_objects)
        for x in bpy.context.view_layer.objects:
            x.select_set(x in objs)
        bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True,
                                 object_types={"MESH", "EMPTY"}, apply_scale_options="FBX_SCALE_ALL",
                                 axis_forward="-Z", axis_up="Y", use_mesh_modifiers=(name != "Weapon_Towel"),
                                 mesh_smooth_type="FACE", bake_anim=False, path_mode="STRIP")
        bpy.ops.export_scene.gltf(filepath=os.path.join(out, name + ".glb"), use_selection=True,
                                  export_image_format="JPEG")
        for o in objs:
            if o.type != "MESH":
                continue
            for m in o.data.materials:
                if m is None or m.name in rows:
                    continue
                row = dict(name=m.name, baseMap="", color=[0.5, 0.5, 0.5], metallic=0.0, smoothness=0.5,
                           transparent=False, emission=0.0, twoSided=m.name.startswith("W_Towel"))
                nt = m.node_tree
                p = nt.nodes.get("Principled BSDF")
                if p:
                    row["color"] = list(p.inputs["Base Color"].default_value)[:3]
                    row["metallic"] = float(p.inputs["Metallic"].default_value)
                    row["smoothness"] = 1 - float(p.inputs["Roughness"].default_value)
                    row["transparent"] = float(p.inputs["Transmission Weight"].default_value) > 0.5
                    row["emission"] = float(p.inputs["Emission Strength"].default_value)
                for n in nt.nodes:
                    if n.type == "TEX_IMAGE" and n.image and n.image.filepath and "bump" not in n.image.name:
                        row["baseMap"] = os.path.basename(n.image.filepath)
                if m.name in FALLBACK:
                    fb = FALLBACK[m.name]
                    row["baseMap"] = fb["tex"]
                    row["smoothness"] = fb.get("smooth", row["smoothness"])
                    row["metallic"] = fb.get("metal", row["metallic"])
                rows[m.name] = row
    import shutil
    for r in rows.values():
        if r["baseMap"]:
            shutil.copy(os.path.join(W.TEX, r["baseMap"]), os.path.join(tex_out, r["baseMap"]))
    with open(os.path.join(out, "Weapons.json"), "w") as f:
        json.dump({"materials": list(rows.values()), "weapons": ALL}, f, indent=1)
    print("[W] DONE", len(ALL), "files,", len(rows), "materials")


if __name__ == "__main__":
    main()
