"""Island v2: realistic terrain + vegetation + ocean (island_hq) with the detailed, connected buildings from
island_assets placed in the world. Saves Island_v02.blend and renders approval views.

Run:  python build_island_v2.py [--trees 1.0] [--samples 96] [--res 1600x900] [--only name,name] [--no-render]
"""
import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import island_assets as IA  # noqa: E402
import island_hq as HQ  # noqa: E402

ROOT = os.path.dirname(HERE)


def place(coll, loc, rot_z=0.0, tilt=(0.0, 0.0)):
    T = Matrix.Translation(loc) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Rotation(tilt[0], 4, "X") @ \
        Matrix.Rotation(tilt[1], 4, "Y")
    for ob in coll.all_objects:
        if ob.parent is None:
            ob.matrix_world = T @ ob.matrix_world


def build(trees):
    bpy.ops.wm.read_homefile(use_empty=True)
    ha = HQ.build(tree_scale=trees, v2=True)
    cols, m = IA.build_all()
    hb = ha(40, -300)
    place(cols["hotel"], (40, -300, hb))
    place(cols["room304"], (40, -300, hb))
    pb = max(ha(40, -255), hb) + 0.3
    place(cols["pool"], (40, -255, pb))
    flatten_terrain(pb, hb)
    place(cols["dive"], (-30, -345, ha(-30, -345)))
    place(cols["tiki"], (110, -345, ha(110, -345)))
    place(cols["beach"], (40, -350, ha(40, -350)))
    y = -350.0                       # pier starts where the sand meets the sea
    while ha(40, y) > 0.4 and y > -480:
        y -= 1.0
    place(cols["pier"], (60, y + 4, 0.0))
    rz = max(ha(300 + dx, 70 + dy) for dx in (-20, 0, 20) for dy in (-22, 0, 20))
    place(cols["ruins"], (300, 70, rz))
    place(cols["cave"], (-160, 228, ha(-160, 222) - 0.4))
    place(cols["ship"], (40, 305, ha(40, 305) + 6.0), rot_z=0.3, tilt=(math.radians(4), math.radians(-3)))
    slime_leak(ha, m, cols["ship"])
    scorch_on_ground(ha, m, cols["ship"])
    # clear vegetation that would poke through buildings
    zones = [((40, -300), 60), ((40, -255), 62), ((-30, -345), 10), ((110, -345), 9), ((300, 70), 32),
             ((-160, 228), 18), ((40, 305), 40)]
    for ob in list(bpy.data.collections["Vegetation"].objects):
        if any(math.hypot(ob.location.x - c[0], ob.location.y - c[1]) < r for c, r in zones):
            if ob.name.startswith("ResortPalm") and math.hypot(ob.location.x - 40, ob.location.y - 300) > 0:
                # resort palms stay unless inside the hotel footprint or the deck
                if not (abs(ob.location.x - 40) < 47 and -312 < ob.location.y < -229):
                    continue
            bpy.data.objects.remove(ob)
    waterfall_v2(ha, m)
    import island as I_
    _, _, _, river = I_.heightmap()
    IA.jungle_falls(m, ha, river)
    gcol, gmk = IA.golf(m, ha)
    bpy.context.scene["golf_markers"] = json.dumps(gmk)
    cart = IA.golf_cart(m)
    place(cart, gmk["carts"][0])
    grass(ha, zones)
    return ha


def grass_material():
    m = bpy.data.materials.new("A_GrassBlade")
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    oi = nt.nodes.new("ShaderNodeObjectInfo")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].color = (0.05, 0.12, 0.02, 1)
    e[1].color = (0.32, 0.36, 0.1, 1)
    e.new(0.5).color = (0.1, 0.22, 0.04, 1)
    nt.links.new(oi.outputs["Random"], ramp.inputs[0])
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    tip = nt.nodes.new("ShaderNodeMapRange")       # dry tips
    tip.inputs["From Min"].default_value = 0.15
    tip.inputs["From Max"].default_value = 0.6
    nt.links.new(sep.outputs[2], tip.inputs["Value"])
    mx = nt.nodes.new("ShaderNodeMix")
    mx.data_type = "RGBA"
    nt.links.new(tip.outputs[0], mx.inputs[0])
    nt.links.new(ramp.outputs[0], mx.inputs[6])
    mx.inputs[7].default_value = (0.42, 0.4, 0.18, 1)
    nt.links.new(mx.outputs[2], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.6
    p.inputs["Subsurface Weight"].default_value = 0.25
    if "Transmission Weight" in p.inputs:
        p.inputs["Transmission Weight"].default_value = 0.1
    return m


def grass(ha, zones):
    """Instanced grass clumps (curved blades) on gentle grassy ground around every approval view."""
    import bmesh
    import random
    rng = random.Random(12)
    mat = grass_material()
    protos = []
    pc = HQ.coll("GrassProtos", hide=True)
    for v in range(6):
        bm = bmesh.new()
        for b in range(rng.randint(25, 45)):
            a = rng.uniform(0, math.tau)
            r = rng.uniform(0, 0.12)
            base = Vector((math.cos(a) * r, math.sin(a) * r, -0.02))
            lean = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(0.05, 0.25)
            hgt = rng.uniform(0.18, 0.5) * (1.3 if v >= 4 else 1.0)
            side = Vector((-math.sin(a + 1.2), math.cos(a + 1.2), 0))
            w = rng.uniform(0.006, 0.012)
            prev = None
            for j in range(4):
                t = j / 3
                c = base + lean * t * t + Vector((0, 0, hgt * t))
                ww = w * (1 - t * 0.9)
                pair = (bm.verts.new(c - side * ww), bm.verts.new(c + side * ww))
                if prev:
                    bm.faces.new((prev[0], prev[1], pair[1], pair[0]))
                prev = pair
        me = bpy.data.meshes.new(f"GrassClump{v}")
        bm.to_mesh(me)
        me.materials.append(mat)
        for poly in me.polygons:
            poly.use_smooth = True
        c = bpy.data.collections.new(f"GrassClump{v}")
        pc.children.link(c)
        ob = bpy.data.objects.new(f"GrassClump{v}", me)
        c.objects.link(ob)
        protos.append(c)
    gcol = HQ.coll("Grass")
    placed = 0
    areas = [(s_[1], s_[2]) for s_ in SHOTS_V2] + [((-30, -225), (130, -215)), ((-30, -320), (-30, -240)),
                                                   ((115, -320), (115, -240))]
    for loc, tgt in areas:
        a, b = Vector(loc[:2]), Vector(tgt[:2])
        L = (b - a).length
        for _ in range(16000):
            t = rng.random() ** 0.7
            spread = 4 + t * L * 0.6
            q = a.lerp(b, t) + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1))) * spread
            z = ha(q.x, q.y)
            if z < 1.4 or z > 140:
                continue
            # slope check
            if abs(ha(q.x + 2, q.y) - z) > 1.2 or abs(ha(q.x, q.y + 2) - z) > 1.2:
                continue
            if abs(q.x - 40) < 57 and -313 < q.y < -228:      # hotel + pool deck footprint
                continue
            if any(math.hypot(q.x - c[0], q.y - c[1]) < r for c, r in zones[2:]):
                continue
            inst = bpy.data.objects.new("Grass", None)
            inst.instance_type = "COLLECTION"
            inst.instance_collection = protos[rng.randrange(len(protos))]
            inst.location = (q.x, q.y, z)
            sc_ = rng.uniform(0.7, 1.6)
            inst.scale = (sc_, sc_, sc_ * rng.uniform(0.8, 1.3))
            inst.rotation_euler = (0, 0, rng.uniform(0, math.tau))
            gcol.objects.link(inst)
            placed += 1
    print("[IslandV2] grass clumps", placed, flush=True)


def slime_leak(ha, m, ship_coll):
    """Glowing slime from the hull breach runs over the ground to the top of the big waterfall."""
    old = [o for o in ship_coll.objects if "SlimeLeak" in o.name]
    for o in old:
        bpy.data.objects.remove(o)
    k = IA.Kit.__new__(IA.Kit)
    k.name, k.c, k.n = "Asset_Ship", ship_coll, 900
    pts = []
    for i in range(24):
        t = i / 23
        x = 40 + math.sin(t * 5) * 1.5
        y = 278 - t * 16
        z = ha(x, y) + 0.35
        if i == 0:
            z = ha(40, 305) + 2.0
        pts.append((x, y, z))
    pts.insert(1, (40, 279, ha(40, 279) + 1.0))
    k.tube(pts, 1.1, m["slimeglow"], segs=12, base="SlimeLeak")
    for i in range(6):      # puddles along the way
        x, y = pts[4 + i * 3][:2]
        k.disc((x, y, ha(x, y) + 0.05), 2.2 + i * 0.2, 0.12, m["slimeglow"], segs=20, base="SlimePuddle")


def scorch_on_ground(ha, m, ship_coll):
    """Burn mark under the crashed ship: a disc mesh draped on the terrain (no floating plate)."""
    import bmesh
    for o in [o for o in ship_coll.objects if "Scorch" in o.name]:
        bpy.data.objects.remove(o)
    bm = bmesh.new()
    rings, segs, R = 14, 48, 38.0
    centre = bm.verts.new((40, 305, ha(40, 305) + 0.08))
    prev = None
    for r in range(1, rings + 1):
        rr = R * r / rings
        ring = []
        for k in range(segs):
            a = math.tau * k / segs
            x, y = 40 + math.cos(a) * rr, 305 + math.sin(a) * rr
            ring.append(bm.verts.new((x, y, ha(x, y) + 0.08)))
        for k in range(segs):
            if prev is None:
                bm.faces.new((centre, ring[k], ring[(k + 1) % segs]))
            else:
                bm.faces.new((prev[k], ring[k], ring[(k + 1) % segs], prev[(k + 1) % segs]))
        prev = ring
    me = bpy.data.meshes.new("ScorchGround")
    bm.to_mesh(me)
    me.materials.append(m["scorch"])
    ob = bpy.data.objects.new("Asset_Ship_ScorchGround", me)
    ship_coll.objects.link(ob)


def flatten_terrain(pb, hb):
    """Keep the ground below the pool deck and the hotel floor (no grass poking through the tiles)."""
    t = bpy.data.objects.get("TerrainHQ")
    if t is None:
        return
    for v in t.data.vertices:
        x, y = v.co.x, v.co.y
        if abs(x - 40) < 57 and -282 < y < -228:
            v.co.z = min(v.co.z, pb - 0.45)
        if abs(x - 40) < 47 and -313 < y < -287:
            v.co.z = min(v.co.z, hb - 0.05)
    t.data.update()


def waterfall_v2(ha, m):
    """Replace the single flat sheet: 3 streams (2 layers each) that follow the cliff, wet ledge rocks, white
    water + foam clumps at the base, an irregular plunge pool with real water."""
    for nm in ("Waterfall", "Foam", "PlungePool"):
        ob = bpy.data.objects.get(nm)
        if ob:
            bpy.data.objects.remove(ob)
    k = IA.Kit("Asset_Waterfall")
    import bmesh
    import random
    rng = random.Random(5)
    top, bot = ha(40, 262) + 0.6, ha(40, 238)
    water_mat = fall_material()
    for (xc, w, seed) in ((26.0, 7.0, 1), (40.0, 15.0, 2), (53.0, 5.0, 3)):
        for layer in (0, 1):
            bm = bmesh.new()
            uv = bm.loops.layers.uv.new("UVMap")
            rows = []
            nj, ni = 60, 14
            for j in range(nj + 1):
                t = j / nj
                z = top - (top - bot) * t
                row = []
                for i in range(ni + 1):
                    u = i / ni
                    x = xc + (u - 0.5) * w * (1 + 0.35 * t) + math.sin(t * 7 + seed) * 0.6
                    y = 275.0
                    while y > 215 and ha(x, y) > z:
                        y -= 0.4
                    bulge = 0.5 * math.sin(u * 9 + j * 0.35 + seed) * math.sin(t * 3.1)
                    row.append(bm.verts.new((x, y - 1.2 - layer * 0.9 - 2.5 * t * t - bulge, z)))
                rows.append(row)
            for j in range(nj):
                for i in range(ni):
                    f = bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
                    for lp, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                        lp[uv].uv = (a / ni * w / 4 + seed * 3 + layer * 7, b / nj * (top - bot) / 4)
            k._ob("Stream", bm, water_mat, smooth=True)
        # white water clumps where the stream lands
        for c in range(26):
            x = xc + rng.uniform(-0.6, 0.6) * w
            y = 242 - rng.uniform(0, 6)
            k.rock((x, y, bot + 0.1), (rng.uniform(1, 2.5), rng.uniform(1, 2), rng.uniform(0.4, 1.0)),
                   m["foam"], seed=100 + c + seed * 50, base="WhiteWater", detail=2)
    # wet ledge rocks on the cliff and boulders around the pool
    for c in range(140):
        x = rng.uniform(-5, 85)
        z = rng.uniform(bot, top)
        y = 275.0
        while y > 215 and ha(x, y) > z:
            y -= 0.4
        big = rng.random() < 0.5
        k.rock((x, y + 1.0, z), (rng.uniform(3, 8) if big else rng.uniform(1.5, 4), rng.uniform(1.5, 3),
                                 rng.uniform(2, 5) if big else rng.uniform(0.8, 2)),
               m["rock"] if big else m["wetrock"], seed=300 + c, base="Ledge", detail=3)
    for c in range(30):
        a = rng.uniform(0, math.tau)
        x, y = 40 + math.cos(a) * rng.uniform(28, 36), 232 + math.sin(a) * rng.uniform(13, 18)
        k.rock((x, y, bot), (rng.uniform(1.5, 4), rng.uniform(1.5, 3.5), rng.uniform(1, 2.2)), m["wetrock"],
               seed=500 + c, base="Boulder")
    # plunge pool: irregular disc of slime-tinted clear water
    bm = bmesh.new()
    vs = []
    for i in range(48):
        a = math.tau * i / 48
        r = 1 + 0.12 * math.sin(a * 3 + 1) + 0.08 * math.sin(a * 7)
        vs.append(bm.verts.new((40 + math.cos(a) * 34 * r, 232 + math.sin(a) * 16 * r, bot + 0.35)))
    bm.faces.new(vs)
    k._ob("PlungePool", bm, IA.water("A_PlungeWater", (0.45, 0.85, 0.35), 0.35))


def fall_material():
    m = bpy.data.materials.get("A_Fall")
    if m:
        return m
    m = bpy.data.materials.new("A_Fall")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (3.0, 0.25, 1)
    nt.links.new(tc.outputs["UV"], mp.inputs[0])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 4
    nz.inputs["Detail"].default_value = 10
    nz.inputs["Roughness"].default_value = 0.6
    nt.links.new(mp.outputs[0], nz.inputs["Vector"])
    # more white toward the bottom (UV v small at top? v grows downward) -> aerated water
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["UV"], sep.inputs[0])
    lo = nt.nodes.new("ShaderNodeMapRange")
    lo.inputs["From Min"].default_value = 0.0
    lo.inputs["From Max"].default_value = 12.0
    lo.inputs["To Min"].default_value = 0.58
    lo.inputs["To Max"].default_value = 0.36
    nt.links.new(sep.outputs[1], lo.inputs["Value"])
    mr = nt.nodes.new("ShaderNodeMapRange")
    nt.links.new(nz.outputs["Fac"], mr.inputs["Value"])
    nt.links.new(lo.outputs[0], mr.inputs["From Min"])
    mr.inputs["From Max"].default_value = 0.72
    white = nt.nodes.new("ShaderNodeBsdfPrincipled")
    white.inputs["Base Color"].default_value = (0.9, 0.95, 0.94, 1)
    white.inputs["Roughness"].default_value = 0.3
    white.inputs["Subsurface Weight"].default_value = 0.4
    white.inputs["Emission Color"].default_value = (0.85, 0.95, 0.9, 1)
    white.inputs["Emission Strength"].default_value = 0.08
    b = nt.nodes.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = 0.4
    nt.links.new(nz.outputs["Fac"], b.inputs["Height"])
    nt.links.new(b.outputs[0], white.inputs["Normal"])
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    haze = nt.nodes.new("ShaderNodeMath")
    haze.operation = "MULTIPLY_ADD"
    haze.inputs[1].default_value = 0.85
    haze.inputs[2].default_value = 0.03
    nt.links.new(mr.outputs[0], haze.inputs[0])
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(haze.outputs[0], mix.inputs[0])
    nt.links.new(clear.outputs[0], mix.inputs[1])
    nt.links.new(white.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    m.blend_method = "HASHED" if hasattr(m, "blend_method") else None
    return m


SHOTS_V2 = [
    ("v2_hotel_pool.png", (70, -246, None), (28, -290, 6), 22),
    ("v2_beach_tiki.png", (124, -362, None), (108, -343, 2.6), 24),
    ("v2_beach_dive.png", (-14, -364, None), (-30, -345, 3.0), 24),
    ("v2_hotel_from_beach.png", (75, -420, 28), (40, -268, 4), 24),
    ("v2_waterfall.png", (15, 110, 30), (40, 252, 40), 30),
    ("v2_cave.png", (-152, 200, None), (-160, 230, 3), 26),
    ("v2_ruins.png", (300, 30, 3), (300, 70, 11), 20),
    ("v2_ship.png", (-12, 324, 8), (40, 305, 5), 24),
]


def render(ha, out_dir, samples, res, only):
    cam = HQ.setup_render(samples, res)
    sc = bpy.context.scene
    os.makedirs(out_dir, exist_ok=True)
    for name, loc, tgt, lens in SHOTS_V2:
        if only and not any(o in name for o in only):
            continue
        x, y, z = loc
        gz = ha(x, y)
        z = gz + (2.2 if z is None else z)          # heights are above the ground
        cam.location = (x, y, max(z, 1.8))
        tz = tgt[2] + max(ha(tgt[0], tgt[1]), 0.0)
        cam.data.lens = lens
        cam.rotation_euler = (Vector((tgt[0], tgt[1], tz)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        print("[IslandV2] rendered", name, flush=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--trees", type=float, default=1.0)
    ap.add_argument("--samples", type=int, default=96)
    ap.add_argument("--res", default="1600x900")
    ap.add_argument("--only", default="")
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--save", default=os.path.join(HERE, "Island_v02.blend"))
    ap.add_argument("--from-blend", action="store_true", help="render the saved Island_v02.blend (no rebuild)")
    a = ap.parse_args()
    if a.from_blend:
        import numpy as np
        import island as I
        bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Island_v02.blend"))
        _, _, hm, _ = I.heightmap()
        ha = lambda x, y: float(hm[int(np.clip((y + I.SIZE / 2) / I.SIZE * (hm.shape[0] - 1), 0, hm.shape[0] - 1)),
                                   int(np.clip((x + I.SIZE / 2) / I.SIZE * (hm.shape[1] - 1), 0, hm.shape[1] - 1))])
    else:
        ha = build(a.trees)
        if a.save:
            bpy.ops.wm.save_as_mainfile(filepath=a.save)
    if not a.no_render:
        w, h = (int(v) for v in a.res.split("x"))
        render(ha, os.path.join(ROOT, "Renders", "island"), a.samples, (w, h), [o for o in a.only.split(",") if o])
