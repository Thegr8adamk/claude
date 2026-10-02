"""Rex Magnum - re-bake only some island materials into Island.json + Textures (no full re-export, FBX kept).

    python rebake_materials.py                       # the default list below + the 6 terrain layers
    python rebake_materials.py --names A_Marble,A_Green --no-terrain

Materials whose FBX UVs were made with the default 2 m box tile (they were not in BAKE when the FBX was
exported) get "uvTile": 2 so IslandBuilder scales the texture back to its real-world tile size.
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import island_export as E  # noqa: E402

NEW = ["A_Fairway", "A_Green", "A_BunkerSand", "A_CasinoCarpet", "A_CorridorCarpet", "A_CorridorPaper", "A_Marble",
       "A_StairStone", "A_SpaWood", "A_AlienFloor", "A_JungleFall", "A_CaveRock"]       # FBX UVs: 2 m box tiles
OLD = ["A_Rock", "A_Trim", "A_Aluminum", "A_Cushion", "A_SignBoard"]                    # FBX UVs already = BAKE tile


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--res", type=int, default=1024)
    ap.add_argument("--names", default="")
    ap.add_argument("--no-terrain", action="store_true")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    names = [n for n in a.names.split(",") if n] or NEW + OLD
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Island_v02.blend"))
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 1
    sc.render.bake.use_selected_to_active = False
    for cn in ("Vegetation", "Grass"):
        c = bpy.data.collections.get(cn)
        if c:
            c.hide_render = True
    import island_assets as IA
    IA.M()
    if bpy.data.materials.get("A_CaveRock") is None:      # lives in the CaveTunnels asset (made by --rebuild)
        IA.cave_rock_material()
    rows = E.material_rows(set(names), a.res, skip_bake=False)
    for r in rows:
        if r["name"] in NEW and r["name"] not in E.UV_OWN:
            r["uvTile"] = 2.0
    path = os.path.join(E.OUT, "Island.json")
    with open(path) as f:
        data = json.load(f)
    by = {r["name"]: r for r in rows}
    data["materials"] = [by.pop(r["name"], r) for r in data["materials"]] + list(by.values())
    if not a.no_terrain:
        layers = []
        for name, c0, c1, scale, bump, tile, opt in E.TERRAIN_LAYERS:
            print(f"[Rebake] terrain layer {name}", flush=True)
            files = E.bake_material(E.terrain_layer_material(name, c0, c1, scale, bump, **opt), tile, False, a.res, name)
            layers.append(dict(name=name, diffuse=f"Textures/{files['base']}", normal=f"Textures/{files['nrm']}",
                               mask=f"Textures/{files['mask']}", tile=tile))
        data["terrain"]["layers"] = layers
    with open(path, "w") as f:
        json.dump(data, f, indent=1)
    print(f"[Rebake] done: {[r['name'] for r in rows]}", flush=True)


if __name__ == "__main__":
    main()
