"""Rex Magnum - export island v2 to Unity.

Reads Island_v02.blend (built by build_island_v2.py) and writes Unity/Assets/RexMagnum/Island/:
  Buildings/<Asset>.fbx      one file per building zone, world coordinates, objects <Asset>_LOD0 / <Asset>_LOD1
  Vegetation/<Proto>.fbx     palms, jungle trees, ferns, boulders, grass clumps (local coordinates)
  Textures/*.png             baked tileable PBR tiles (base, normal, mask), terrain layers, water normal, falls
  island_splat0/1.png        terrain paint weights (RGBA + RG) for 6 terrain layers
  Island.json                materials, assets, vegetation instances, terrain + spawn data for IslandBuilder.cs
  AxisProbe.fbx              3 empties that let Unity check the Blender -> Unity axis mapping

Run:  python island_export.py [--res 1024] [--skip-bake]
UVs: box projection in metres / tile size, so every texture tile keeps its real-world size.
"""
import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import island as I  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Unity", "Assets", "RexMagnum", "Island")
TEX = os.path.join(OUT, "Textures")
BLD = os.path.join(OUT, "Buildings")
VEG = os.path.join(OUT, "Vegetation")

# material -> (tile size m, bake on a wall (vertical) plane?)
BAKE = {
    "A_Stucco": (4, True), "A_Planks": (4, True), "A_WeatheredWood": (4, True), "A_Bamboo": (2, True),
    "A_TempleStone": (4, True), "A_Rock": (6, True), "A_WetRock": (4, True), "A_Terracotta": (2, False),
    "A_DeckTiles": (4, False), "A_PoolTile": (2, False), "A_ShipHull": (8, False), "A_Teak": (2, False),
    "A_Trim": (4, True), "A_Aluminum": (2, True), "A_Cushion": (2, False), "A_SignBoard": (2, True),
    "A_Thatch": (4, False), "PalmBark": (2, True), "JungleBark": (2, True),
    "A_Carpet": (2, False), "A_Wallpaper": (2, True), "A_Sheets": (2, False), "A_Duvet": (2, False),
    "A_DoorWood": (2, True),
}
GLASS = {"A_Glass": 0.2, "A_RailGlass": 0.3, "A_BottleGlass": 0.6, "A_DomeGlass": 0.45}
WATER = {"A_PoolWater": "pool", "A_PlungeWater": "slime", "A_SlimeWater": "slime", "SlimeWater": "slime"}
FALL = {"A_Fall", "A_SmallFall"}
FOLIAGE_IMG = {"PalmLeaf": "leaf_palm.png", "BroadLeaf": "leaf_broad.png", "FernLeaf": "leaf_fern.png"}
TWO_SIDED = {"A_GrassBlade", "A_Vine", "A_Straw", "A_Curtain0", "A_Curtain1", "A_Curtain2", "A_Curtain3",
             "A_Curtain4", "A_Fall", "A_SmallFall"}
REMAP = {"TerrainHQ": "A_Rock"}
SKIP_MAT = {"Mist"}
LOD1_DROP = ("Straw", "Curtain", "Bed", "Chair", "Table", "Towel", "Lamp", "Stool", "Leg", "Bottle", "Slat",
             "Ladder", "Rung", "Vine", "Sandbag", "RailPost", "Screw", "WhiteWater", "Text", "Sign", "Room304")
ASSETS = ["Asset_Hotel", "Asset_Room304", "Asset_PoolArea", "Asset_DiveShop", "Asset_TikiBar", "Asset_BeachProps", "Asset_Pier",
          "Asset_Ruins", "Asset_Cave", "Asset_Ship", "Asset_Waterfall"]


# ------------------------------------------------------------------ texture baking

def _img(name, res, non_color=False):
    im = bpy.data.images.get(name)
    if im:
        bpy.data.images.remove(im)
    im = bpy.data.images.new(name, res, res, alpha=False, float_buffer=True)
    if non_color:
        im.colorspace_settings.name = "Non-Color"
    return im


def _bake_plane(size, wall):
    bm = bmesh.new()
    if wall:
        co = [(0, 0, 10), (size, 0, 10), (size, 0, 10 + size), (0, 0, 10 + size)]
    else:
        co = [(0, 0, 10), (size, 0, 10), (size, size, 10), (0, size, 10)]
    vs = [bm.verts.new(c) for c in co]
    f = bm.faces.new(vs)
    uv = bm.loops.layers.uv.new("UVMap")
    for lp, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        lp[uv].uv = t
    if wall:
        f.normal_update()
        if f.normal.y > 0:     # face -Y so tangent space matches
            bmesh.ops.reverse_faces(bm, faces=[f])
    me = bpy.data.meshes.new("BakePlane")
    bm.to_mesh(me)
    ob = bpy.data.objects.new("BakePlane", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _to_np(im):
    a = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4)
    return a


def _tileable(a):
    """Cross-fade with a half-shifted copy: edges come from the copy (continuous), centre from the original."""
    n = a.shape[0]
    win = np.sin(np.pi * (np.arange(n) + 0.5) / n) ** 2
    w = np.outer(win, win)[..., None]
    b = np.roll(np.roll(a, n // 2, 0), n // 2, 1)
    return a * w + b * (1 - w)


def _save_png(a, path, srgb=True):
    from PIL import Image
    a = np.clip(a, 0, 1)
    if srgb:
        a = np.where(a <= 0.0031308, a * 12.92, 1.055 * np.power(a, 1 / 2.4) - 0.055)
    img = (a[::-1] * 255 + 0.5).astype(np.uint8)       # Blender rows start at the bottom
    Image.fromarray(img, "RGBA" if img.shape[2] == 4 else "RGB").save(path)


def bake_material(mat, size, wall, res, name=None):
    name = name or mat.name
    sc = bpy.context.scene
    ob = _bake_plane(size, wall)
    ob.data.materials.append(mat)
    nt = mat.node_tree
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output) \
        if any(n.type == "OUTPUT_MATERIAL" and n.is_active_output for n in nt.nodes) else \
        next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    surf_src = out.inputs["Surface"].links[0].from_socket if out.inputs["Surface"].links else None
    tex = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = tex
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(emit.outputs[0], out.inputs["Surface"])
    results = {}
    for key, sock in (("base", "Base Color"), ("rough", "Roughness")):
        inp = p.inputs[sock]
        for lk in list(emit.inputs["Color"].links):
            nt.links.remove(lk)
        if inp.links:
            nt.links.new(inp.links[0].from_socket, emit.inputs["Color"])
        else:
            v = inp.default_value
            emit.inputs["Color"].default_value = tuple(v) if hasattr(v, "__len__") else (v, v, v, 1)
        im = _img(f"bk_{name}_{key}", res, non_color=(key == "rough"))
        tex.image = im
        bpy.ops.object.bake(type="EMIT", margin=0)
        results[key] = _to_np(im)
    # normal with the real shader
    if surf_src is not None:
        nt.links.new(surf_src, out.inputs["Surface"])
    nt.nodes.remove(emit)
    im = _img(f"bk_{name}_nrm", res, non_color=True)
    tex.image = im
    bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", margin=0)
    results["nrm"] = _to_np(im)
    nt.nodes.remove(tex)
    bpy.data.objects.remove(ob)
    base = _tileable(results["base"])
    rough = _tileable(results["rough"])[..., 0]
    nrm = _tileable(results["nrm"])
    v = nrm[..., :3] * 2 - 1
    v /= np.linalg.norm(v, axis=-1, keepdims=True) + 1e-6
    nrm[..., :3] = v * 0.5 + 0.5
    nrm[..., 3] = 1
    metal = p.inputs["Metallic"].default_value
    mask = np.zeros_like(base)
    mask[..., 0] = metal
    mask[..., 1] = 1.0
    mask[..., 3] = 1.0 - rough
    files = {}
    for key, arr, srgb in (("base", base, True), ("nrm", nrm, False), ("mask", mask, False)):
        arr = arr.copy()
        arr[..., 3] = 1 if key != "mask" else arr[..., 3]
        f = f"{name}_{key}.png"
        _save_png(arr if key == "mask" else arr[..., :3], os.path.join(TEX, f), srgb=srgb)
        files[key] = f
    return files


def straw_texture():
    """Columns = colour tone (u), rows = along the strip with fibre lines."""
    from PIL import Image
    rng = np.random.default_rng(3)
    w, h = 256, 512
    u = np.linspace(0, 1, w)
    stops = [(0.0, (0.16, 0.11, 0.05)), (0.55, (0.42, 0.32, 0.17)), (1.0, (0.62, 0.5, 0.3))]
    col = np.zeros((w, 3))
    for c in range(3):
        col[:, c] = np.interp(u, [s[0] for s in stops], [s[1][c] for s in stops])
    fib = 0.8 + 0.4 * rng.random(w)
    fib = np.convolve(fib, np.ones(3) / 3, mode="same")
    img = col[None, :, :] * fib[None, :, None] * np.ones((h, 1, 1))
    img *= (0.9 + 0.1 * np.sin(np.linspace(0, 40, h)))[:, None, None]
    img[: h // 12] *= 0.7    # darker cut ends
    img = np.clip(img, 0, 1) ** (1 / 2.2)
    Image.fromarray((img * 255).astype(np.uint8)).save(os.path.join(TEX, "A_Straw_base.png"))
    return "A_Straw_base.png"


def water_textures(res=512):
    """Tileable water normal map (sum of periodic waves) and a waterfall streak texture."""
    from PIL import Image
    rng = np.random.default_rng(7)
    y, x = np.mgrid[0:res, 0:res] / res
    hgt = np.zeros((res, res))
    for _ in range(40):
        kx, ky = rng.integers(-9, 10, 2)
        if kx == 0 and ky == 0:
            continue
        amp = 1.0 / math.hypot(kx, ky) ** 1.3
        hgt += amp * np.sin(math.tau * (kx * x + ky * y) + rng.random() * math.tau)
    gy, gx = np.gradient(hgt)
    n = np.dstack([-gx * 18, -gy * 18, np.ones_like(hgt)])
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(TEX, "Water_nrm.png"))
    # falls: vertical streaks (periodic in u), foam blotches (periodic in v); alpha = streak mask
    u = np.arange(256) / 256
    s = np.zeros(256)
    for k in range(1, 40):
        s += rng.random() / k ** 0.7 * np.sin(math.tau * k * u + rng.random() * 6.3)
    s = (s - s.min()) / (s.max() - s.min())
    v = np.arange(1024) / 1024
    b = np.zeros(1024)
    for k in range(1, 30):
        b += rng.random() / k * np.sin(math.tau * k * v + rng.random() * 6.3)
    b = (b - b.min()) / (b.max() - b.min())
    m = np.clip((s[None, :] * 0.75 + b[:, None] * 0.35 - 0.35) * 2.2, 0, 1)
    rgba = np.dstack([0.9 + 0.08 * m, 0.95 + 0.04 * m, 0.94 + 0.05 * m, 0.15 + 0.85 * m])
    Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(TEX, "Fall_streaks.png"))


# ------------------------------------------------------------------ terrain layers + splat

def terrain_layer_material(name, c0, c1, scale, bump, ripples=False, litter=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = scale
    nz.inputs["Detail"].default_value = 10
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*c0, 1)
    ramp.color_ramp.elements[1].color = (*c1, 1)
    nt.links.new(nz.outputs["Fac"], ramp.inputs[0])
    col = ramp.outputs[0]
    h = nz.outputs["Fac"]
    if litter:   # leaf litter: voronoi cells of brown / dark
        vo = nt.nodes.new("ShaderNodeTexVoronoi")
        vo.inputs["Scale"].default_value = 9
        nt.links.new(tc.outputs["Object"], vo.inputs["Vector"])
        mx = nt.nodes.new("ShaderNodeMix")
        mx.data_type = "RGBA"
        mx.blend_type = "MULTIPLY"
        mx.inputs[0].default_value = 0.6
        nt.links.new(col, mx.inputs[6])
        nt.links.new(vo.outputs["Color"], mx.inputs[7])
        col = mx.outputs[2]
        h = vo.outputs["Distance"]
    if ripples:
        w = nt.nodes.new("ShaderNodeTexWave")
        w.inputs["Scale"].default_value = 1.2
        w.inputs["Distortion"].default_value = 4
        nt.links.new(tc.outputs["Object"], w.inputs["Vector"])
        h = w.outputs["Fac"]
    nt.links.new(col, p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.9
    b = nt.nodes.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = bump
    nt.links.new(h, b.inputs["Height"])
    nt.links.new(b.outputs[0], p.inputs["Normal"])
    return m


TERRAIN_LAYERS = [   # name, colours, noise scale, bump, tile m, options
    ("T_Sand", (0.78, 0.69, 0.5), (0.9, 0.83, 0.66), 3, 0.35, 4, dict(ripples=True)),
    ("T_WetSand", (0.42, 0.35, 0.25), (0.52, 0.45, 0.32), 3, 0.2, 4, {}),
    ("T_Grass", (0.12, 0.24, 0.05), (0.3, 0.42, 0.12), 2, 0.4, 4, {}),
    ("T_JungleFloor", (0.05, 0.09, 0.03), (0.16, 0.12, 0.05), 2, 0.6, 4, dict(litter=True)),
    ("T_Rock", (0.09, 0.085, 0.08), (0.24, 0.22, 0.2), 1.2, 1.0, 8, {}),
    ("T_Moss", (0.08, 0.15, 0.04), (0.14, 0.24, 0.07), 3, 0.5, 4, {}),
]


def smooth(v, lo, hi):
    t = np.clip((v - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def splat_maps(h):
    """Same rules as the Cycles terrain shader -> 6 weights per heightmap cell."""
    res = h.shape[0]
    cell = I.SIZE / (res - 1)
    gy, gx = np.gradient(h, cell)
    up = 1 / np.sqrt(1 + gx ** 2 + gy ** 2)
    big = I._noise(h.shape, 10, 91)
    fine = I._noise(h.shape, 120, 92)
    zj = h + (big - 0.5) * 6
    W = np.zeros(h.shape + (6,))
    W[..., 1] = 1                                         # wet sand
    def mix(f, k):
        f = f[..., None]
        one = np.zeros(6)
        one[k] = 1
        return W * (1 - f) + one * f
    W = mix(smooth(h, 0.2, 0.9), 0)                      # dry sand above the water line
    W = mix(smooth(zj, 2.0, 3.5), 2)                     # grass
    W = mix(smooth(zj, 7.0, 11.0), 3)                    # jungle floor
    rock = np.maximum(smooth(-up, -0.82, -0.62), smooth(zj, 125, 150))
    W = mix(rock, 4)
    W = mix(rock * smooth(fine, 0.55, 0.7), 5)           # moss on rock
    W /= W.sum(-1, keepdims=True)
    return W


def save_splat(W):
    from PIL import Image
    a = (np.clip(W, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(a[..., 0:4], "RGBA").save(os.path.join(OUT, "island_splat0.png"))
    b = np.zeros(W.shape[:2] + (4,), np.uint8)
    b[..., 0:2] = a[..., 4:6]
    b[..., 3] = 255
    Image.fromarray(b, "RGBA").save(os.path.join(OUT, "island_splat1.png"))


# ------------------------------------------------------------------ meshes

def box_uv(me, tiles):
    """Box-project UVs in metres / tile size (per material). Keeps existing UVs on straw/streams/leaves."""
    if me.uv_layers:
        if me.uv_layers[0].name != "UVMap":
            me.uv_layers[0].name = "UVMap"
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[1])
        return False
    uv = me.uv_layers.new(name="UVMap").data
    for poly in me.polygons:
        n = poly.normal
        s = tiles[poly.material_index] if poly.material_index < len(tiles) else 2.0
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if ax == 2:
                u, v = co.x, co.y
            elif ax == 0:
                u, v = co.y, co.z
            else:
                u, v = co.x, co.z
            uv[li].uv = (u / s, v / s)
    return True


def straw_uv(me):
    """u = colour tone (so one texture gives every strip its own shade), v = along the strip."""
    at = me.attributes.get("StrawTone")
    if at is None or not me.uv_layers:
        return
    uv = me.uv_layers[0].data
    for li, lp in enumerate(me.loops):
        t = at.data[lp.vertex_index].value
        u, v = uv[li].uv
        uv[li].uv = (t * 0.92 + u * 0.08, v)


def mat_name(m):
    return REMAP.get(m.name, m.name) if m else "A_Rock"


def joined_mesh(objs, name, mats_out, lod1=False):
    """Evaluate (modifiers), move to world space, box-UV, merge into one mesh with a material list."""
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    order = []
    for ob in objs:
        if ob.type != "MESH" or ob.hide_render:
            continue
        if lod1:
            if any(k in ob.name for k in LOD1_DROP):
                continue
            if max(ob.dimensions) < 0.6:
                continue
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), depsgraph=dg)
        me.transform(ob.matrix_world)
        names = [mat_name(m) for m in me.materials] or ["A_Rock"]
        if lod1:
            names = ["A_Thatch" if n == "A_ThatchUnder" else n for n in names]
        if any(n in SKIP_MAT for n in names):
            bpy.data.meshes.remove(me)
            continue
        if "Straw" in ob.name:
            straw_uv(me)
        tiles = [BAKE.get(n, (2.0, True))[0] for n in names]
        box_uv(me, tiles)
        idx = []
        for n in names:
            if n not in order:
                order.append(n)
            idx.append(order.index(n))
        for poly in me.polygons:
            poly.material_index = idx[min(poly.material_index, len(idx) - 1)]
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    out = bpy.data.meshes.new(name)
    bm.to_mesh(out)
    bm.free()
    for n in order:
        out.materials.append(bpy.data.materials.get(n) or bpy.data.materials.new(n))
        mats_out.add(n)
    return out


def export_fbx(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL", object_types={"MESH", "EMPTY"},
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP")


def temp_collection():
    c = bpy.data.collections.get("ExportTmp")
    if c is None:
        c = bpy.data.collections.new("ExportTmp")
        bpy.context.scene.collection.children.link(c)
    return c


def export_assets(mats_used):
    tmp = temp_collection()
    rows = []
    for an in ASSETS:
        coll = bpy.data.collections.get(an)
        if coll is None:
            continue
        name = an.replace("Asset_", "")
        objs = list(coll.all_objects)
        lod0 = bpy.data.objects.new(f"{name}_LOD0", joined_mesh(objs, f"{name}_LOD0", mats_used))
        lod1 = bpy.data.objects.new(f"{name}_LOD1", joined_mesh(objs, f"{name}_LOD1", mats_used, lod1=True))
        tmp.objects.link(lod0)
        tmp.objects.link(lod1)
        export_fbx([lod0, lod1], os.path.join(BLD, f"{name}.fbx"))
        tris = sum(len(p.vertices) - 2 for p in lod0.data.polygons)
        rows.append(dict(name=name, fbx=f"Buildings/{name}.fbx", tris=tris))
        print(f"[Export] {name}: {tris} tris", flush=True)
        for o in (lod0, lod1):
            me = o.data
            bpy.data.objects.remove(o)
            bpy.data.meshes.remove(me)
    # slime river (world mesh)
    river = bpy.data.objects.get("SlimeRiver")
    if river:
        ob = bpy.data.objects.new("River_LOD0", joined_mesh([river], "River_LOD0", mats_used))
        tmp.objects.link(ob)
        export_fbx([ob], os.path.join(BLD, "River.fbx"))
        rows.append(dict(name="River", fbx="Buildings/River.fbx", tris=len(ob.data.polygons)))
        bpy.data.objects.remove(ob)
    return rows


def export_vegetation(mats_used):
    tmp = temp_collection()
    protos = []
    names = [c.name for c in bpy.data.collections
             if c.name.startswith(("Palm", "Jungle", "FernC", "BoulderC", "GrassClump")) and c.objects]
    for n in sorted(names):
        coll = bpy.data.collections[n]
        ob = bpy.data.objects.new(n, joined_mesh(list(coll.objects), n, mats_used))
        tmp.objects.link(ob)
        export_fbx([ob], os.path.join(VEG, f"{n}.fbx"))
        kind = ("palm" if n.startswith("Palm") else "tree" if n.startswith("Jungle") else
                "fern" if n.startswith("Fern") else "rock" if n.startswith("Boulder") else "grass")
        protos.append(dict(name=n, fbx=f"Vegetation/{n}.fbx", kind=kind))
        bpy.data.objects.remove(ob)
    index = {p["name"]: i for i, p in enumerate(protos)}
    inst = []
    for o in bpy.data.collections["Vegetation"].objects:
        c = o.instance_collection
        if c is None or c.name not in index:
            continue
        inst.extend([index[c.name], round(o.location.x, 2), round(o.location.y, 2), round(o.location.z, 2),
                     round(o.rotation_euler.z, 3), round(o.scale.x, 3)])     # flat: 6 numbers per plant
    return protos, inst


def axis_probe():
    tmp = temp_collection()
    es = []
    for n, p in (("PX", (10, 0, 0)), ("PY", (0, 10, 0)), ("PZ", (0, 0, 10))):
        e = bpy.data.objects.new(n, None)
        e.location = p
        tmp.objects.link(e)
        es.append(e)
    export_fbx(es, os.path.join(OUT, "AxisProbe.fbx"))


# ------------------------------------------------------------------ material table

def principled_row(m):
    p = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m.node_tree else None
    row = dict(name=m.name, kind="lit", color=[0.6, 0.6, 0.6, 1], metallic=0.0, smoothness=0.4)
    if p is None:
        return row
    c = p.inputs["Base Color"].default_value
    row["color"] = [round(c[0], 4), round(c[1], 4), round(c[2], 4), 1]
    row["metallic"] = round(p.inputs["Metallic"].default_value, 3)
    row["smoothness"] = round(1 - p.inputs["Roughness"].default_value, 3)
    es = p.inputs["Emission Strength"].default_value
    ec = p.inputs["Emission Color"].default_value
    if es > 0 and max(ec[:3]) > 0:
        row["emission"] = [round(ec[0] * es, 3), round(ec[1] * es, 3), round(ec[2] * es, 3)]
    return row


def material_rows(mats_used, res, skip_bake):
    rows = []
    for n in sorted(mats_used):
        m = bpy.data.materials.get(n)
        if n in BAKE:
            size, wall = BAKE[n]
            if skip_bake and os.path.exists(os.path.join(TEX, f"{n}_base.png")):
                files = dict(base=f"{n}_base.png", nrm=f"{n}_nrm.png", mask=f"{n}_mask.png")
            else:
                print(f"[Export] baking {n} ({size} m tile)", flush=True)
                files = bake_material(m, size, wall, res)
            p = next(x for x in m.node_tree.nodes if x.type == "BSDF_PRINCIPLED")
            rows.append(dict(name=n, kind="lit", baseMap=f"Textures/{files['base']}",
                             normalMap=f"Textures/{files['nrm']}", maskMap=f"Textures/{files['mask']}",
                             color=[1, 1, 1, 1], metallic=round(p.inputs["Metallic"].default_value, 3),
                             smoothness=1.0, tile=size))
        elif n in GLASS:
            r = principled_row(m)
            r.update(kind="glass", alpha=GLASS[n], smoothness=0.95)
            rows.append(r)
        elif n in WATER:
            rows.append(dict(name=n, kind="water", water=WATER[n]))
        elif n in FALL:
            rows.append(dict(name=n, kind="fall", baseMap="Textures/Fall_streaks.png", flow=1 if n == "A_Fall" else -1))
        elif n in FOLIAGE_IMG:
            rows.append(dict(name=n, kind="foliage", baseMap=f"Textures/{FOLIAGE_IMG[n]}", color=[1, 1, 1, 1],
                             smoothness=0.35))
        elif m is not None and m.get("image"):
            import shutil
            f = m["image"]
            shutil.copy(os.path.join(HERE, "textures", f), os.path.join(TEX, f))
            r = principled_row(m)
            r.update(baseMap=f"Textures/{f}", color=[1, 1, 1, 1])
            if "emission" in r:
                r["emissionMap"] = f"Textures/{f}"
                r["emission"] = [1.5, 1.5, 1.5]
            rows.append(r)
        elif n == "A_Straw":
            rows.append(dict(name=n, kind="lit", baseMap="Textures/A_Straw_base.png", color=[1, 1, 1, 1],
                             smoothness=0.1, twoSided=True))
        elif m is not None:
            r = principled_row(m)
            if n == "A_GrassBlade":
                r.update(color=[0.16, 0.28, 0.06, 1], smoothness=0.3)
            rows.append(r)
        if rows and n in TWO_SIDED:
            rows[-1]["twoSided"] = True
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--res", type=int, default=1024)
    ap.add_argument("--blend", default=os.path.join(HERE, "Island_v02.blend"))
    ap.add_argument("--skip-bake", action="store_true")
    a = ap.parse_args()
    for d in (OUT, TEX, BLD, VEG):
        os.makedirs(d, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=a.blend)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 1
    sc.render.bake.use_selected_to_active = False
    # the thatch tile material is only used by LOD1 roofs; make sure it exists
    import island_assets as IA
    IA.M()
    # hide the huge scenery while baking (bakes only use the plane, but keep the depsgraph light)
    for cn in ("Vegetation", "Grass"):
        c = bpy.data.collections.get(cn)
        if c:
            c.hide_render = True
    mats_used = set()
    assets = export_assets(mats_used)
    protos, inst = export_vegetation(mats_used)
    mats_used.add("A_Thatch")
    axis_probe()
    straw_texture()
    import shutil
    for f in FOLIAGE_IMG.values():
        shutil.copy(os.path.join(HERE, "textures", f), os.path.join(TEX, f))
    water_textures()
    rows = material_rows(mats_used, a.res, a.skip_bake)
    # terrain
    X, Y, h, river = I.heightmap()
    # same flattening as build_island_v2.flatten_terrain: ground stays below the pool deck + hotel floor
    def H(x, y):
        return float(h[int((y + 500) / 1000 * 1024), int((x + 500) / 1000 * 1024)])
    hb = H(40, -300)
    pb0 = max(H(40, -255), hb) + 0.3
    xs = np.linspace(-I.SIZE / 2, I.SIZE / 2, h.shape[1])
    gx, gy = np.meshgrid(xs, xs)
    deck = (np.abs(gx - 40) < 57) & (gy > -282) & (gy < -228)
    h[deck] = np.minimum(h[deck], pb0 - 0.45)
    hot = (np.abs(gx - 40) < 47) & (gy > -313) & (gy < -287)
    h[hot] = np.minimum(h[hot], hb - 0.05)
    I.save_unity_raw(h, os.path.join(OUT, "island_heightmap_1025.raw"))
    save_splat(splat_maps(h))
    layers = []
    for name, c0, c1, scale, bump, tile, opt in TERRAIN_LAYERS:
        if a.skip_bake and os.path.exists(os.path.join(TEX, f"{name}_base.png")):
            files = dict(base=f"{name}_base.png", nrm=f"{name}_nrm.png", mask=f"{name}_mask.png")
        else:
            print(f"[Export] baking terrain layer {name}", flush=True)
            files = bake_material(terrain_layer_material(name, c0, c1, scale, bump, **opt), tile, False, a.res, name)
        layers.append(dict(name=name, diffuse=f"Textures/{files['base']}", normal=f"Textures/{files['nrm']}",
                           mask=f"Textures/{files['mask']}", tile=tile))
    pb = pb0
    data = dict(
        materials=rows, assets=assets,
        vegetation=dict(protos=protos, instances=inst),
        terrain=dict(size=I.SIZE, minH=-8.0, maxH=I.MAX_H, res=I.RES, raw="island_heightmap_1025.raw",
                     splat=["island_splat0.png", "island_splat1.png"], layers=layers),
        spawn=dict(pos=[44.0, -262.0, pb + 0.05], yaw=0.0),
        zones={k: list(v) for k, v in I.Z.items()},
        note="Blender coordinates (x east, y north, z up). Unity = (x, z, y).",
    )
    with open(os.path.join(OUT, "Island.json"), "w") as f:
        json.dump(data, f, indent=1)
    print(f"[Export] done: {len(assets)} buildings, {len(protos)} plant types, {len(inst) // 6} plants, "
          f"{len(rows)} materials", flush=True)


if __name__ == "__main__":
    main()
