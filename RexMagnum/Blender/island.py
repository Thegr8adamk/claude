"""Rex Magnum - island rough layout (brief section 6, build step 5).

Makes:
  1. island heightmap (numpy) -> Unity Terrain RAW (16-bit, 1025x1025) + PNG preview
  2. a top-down map picture with the zones (PNG, for the owner)
  3. a rough 3D blockout in Blender (terrain, sea, hotel 5 floors, pool, beach, golf, jungle,
     slime river, waterfalls, ruins temple, cave, crashed ship) for the 3 view renders.
World: X east, Y north, Z up. Island about 900 m x 800 m. Sea level z = 0.
"""
import math
import os

import numpy as np

SIZE = 1000.0          # terrain square (m), centered on 0
RES = 1025             # Unity terrain heightmap resolution (2^n + 1)
MAX_H = 210.0          # max terrain height (m) for the RAW scale (the ship hill reaches ~204 m)

# zone anchor points (x, y) in meters
Z = {
    "hotel": (40, -300), "pool": (40, -255), "beach": (40, -345), "golf": (-280, -40),
    "jungle": (60, 60), "ruins": (300, 70), "cave": (-160, 230), "ship": (40, 300),
    "waterfall": (40, 250),
}


def _noise(shape, scale, seed):
    """Smooth value noise (bilinear-upsampled random grid), several octaves."""
    rng = np.random.default_rng(seed)
    out = np.zeros(shape)
    amp, tot = 1.0, 0.0
    for o in range(5):
        g = max(2, int(scale * 2 ** o))
        grid = rng.random((g + 1, g + 1))
        ys = np.linspace(0, g, shape[0])
        xs = np.linspace(0, g, shape[1])
        y0 = np.floor(ys).astype(int).clip(0, g - 1)
        x0 = np.floor(xs).astype(int).clip(0, g - 1)
        fy = (ys - y0)[:, None]
        fx = (xs - x0)[None, :]
        fy = fy * fy * (3 - 2 * fy)
        fx = fx * fx * (3 - 2 * fx)
        a = grid[y0][:, x0]
        b = grid[y0][:, x0 + 1]
        c = grid[y0 + 1][:, x0]
        d = grid[y0 + 1][:, x0 + 1]
        out += amp * (a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy)
        tot += amp
        amp *= 0.5
    return out / tot


def heightmap(res=RES, seed=304):
    xs = np.linspace(-SIZE / 2, SIZE / 2, res)
    X, Y = np.meshgrid(xs, xs)            # Y north (row 0 = south)
    n = _noise((res, res), 6, seed)
    # island mask: distorted ellipse
    r = np.sqrt((X / 450) ** 2 + ((Y - 10) / 380) ** 2) + (n - 0.5) * 0.35
    land = np.clip((1.0 - r) / 0.12, 0, 1)
    h = np.zeros_like(X)
    h += land * 4.0                                         # low coastal plain
    # north mountain: ship sits on top of the big waterfall cliff
    m = np.exp(-(((X - 40) / 170) ** 2 + ((Y - 320) / 110) ** 2))
    h += m * 120
    # cliff for the big waterfall (sharp step south of the ship)
    cliff = 1 / (1 + np.exp(-(Y - 255) / 2.0))
    h += cliff * np.exp(-((X - 40) / 120) ** 2) * 55
    # rocky ridges on the mountain (ridged noise)
    rn = _noise((res, res), 14, seed + 7)
    ridged = 1 - np.abs(rn * 2 - 1)
    wf_zone = np.exp(-(((X - 40) / 28) ** 2 + ((Y - 252) / 18) ** 2))   # smooth cliff where the water falls
    h += m * (ridged ** 3) * 26 * (1 - wf_zone)
    # east hills (ruins plateau)
    h += np.exp(-(((X - 300) / 110) ** 2 + ((Y - 70) / 90) ** 2)) * 28
    # center jungle: rolling hills
    h += land * (n ** 2) * 22
    # west golf course: flatten
    golf = np.exp(-(((X + 280) / 150) ** 2 + ((Y + 40) / 140) ** 2))
    h = h * (1 - golf * 0.8) + golf * 6
    # south: hotel terrace + beach slope
    hotel = np.exp(-(((X - 40) / 160) ** 2 + ((Y + 290) / 60) ** 2))
    h = h * (1 - hotel) + hotel * 3.0
    # green slime river: from the waterfall pool down through the jungle to the east coast
    t = np.linspace(0, 1, 500)
    rx = 40 + 330 * t + 40 * np.sin(t * 6)            # waterfall pool -> jungle -> east coast
    ry = 240 - 400 * t + 30 * np.sin(t * 4)
    river = np.zeros_like(X)
    for px, py in zip(rx[::4], ry[::4]):
        river = np.maximum(river, np.exp(-(((X - px) ** 2 + (Y - py) ** 2) / (2 * 9 ** 2))))
    h = h - river * 5.0 * land
    # sea floor outside
    h = np.where(land > 0, h, -6 * (1 - land))
    h = np.where(land <= 0, -8.0, h - (1 - land) * 8)
    h = carve_river(X, Y, h, rx, ry)
    h = flatten_road(X, Y, h, (rx, ry))
    return X, Y, h, (rx, ry)


def river_surface_z(h, rx, ry):
    """Water level along the river: ground + 0.25 m, never higher than the point upstream (water does not run
    uphill). Same rule as island_assets.river_profile."""
    n = h.shape[0] - 1
    out, last = [], None
    for x, y in zip(rx, ry):
        fx = min(max((x + SIZE / 2) / SIZE * n, 0), n - 1e-6)
        fy = min(max((y + SIZE / 2) / SIZE * n, 0), n - 1e-6)
        ix, iy, tx, ty = int(fx), int(fy), fx - int(fx), fy - int(fy)
        z = (h[iy, ix] * (1 - tx) * (1 - ty) + h[iy, ix + 1] * tx * (1 - ty) + h[iy + 1, ix] * (1 - tx) * ty
             + h[iy + 1, ix + 1] * tx * ty) + 0.25
        z = max(z, 0.2)
        last = z if last is None else min(z, last)
        out.append(last)
    return smooth_level(np.array(out))


def smooth_level(z, win=15):
    """Turn the stair steps of a running minimum into slopes: moving average, then never uphill again."""
    k = np.ones(win) / win
    zp = np.concatenate([np.full(win // 2, z[0]), z, np.full(win // 2, z[-1])])
    zs = np.convolve(zp, k, mode="valid")[:len(z)]
    return np.minimum.accumulate(np.minimum(zs, z + 0.0))


def carve_river(X, Y, h, rx, ry, half=8.0, bank=6.0, depth=0.8):
    """Cut a channel along the river so the water surface always lies in it: bed = water level - depth across the
    river, then a bank that rises back to the ground. Where the land bumps up above the water, this cuts a gorge."""
    zs = river_surface_z(h, rx, ry)
    bed = np.full_like(h, np.inf)
    for x, y, z in zip(rx[::2], ry[::2], zs[::2]):
        d = np.sqrt((X - x) ** 2 + (Y - y) ** 2)
        m = d < half + bank
        if not m.any():
            continue
        t = np.clip((d[m] - half * 0.8) / (half * 0.2 + bank), 0, 1)
        target = z - depth * (1 - t) + t * t * 6.0          # flat bed, then a bank that climbs 6 m
        bed[m] = np.minimum(bed[m], target)
    return np.minimum(h, bed)


def save_unity_raw(h, path):
    """16-bit little-endian RAW for Unity Terrain (height range: -8 .. MAX_H)."""
    v = np.clip((h + 8) / (MAX_H + 8), 0, 1)
    (v * 65535).astype("<u2").tofile(path)


def draw_map(h, river, path):
    from PIL import Image, ImageDraw, ImageFont
    res = h.shape[0]
    img = np.zeros((res, res, 3))
    sea = np.array([0.1, 0.35, 0.6])
    shallow = np.array([0.2, 0.65, 0.75])
    sand = np.array([0.93, 0.86, 0.66])
    grass = np.array([0.25, 0.55, 0.2])
    jungle = np.array([0.08, 0.35, 0.12])
    rock = np.array([0.45, 0.42, 0.38])
    img[:] = sea
    img[h > -3] = shallow
    img[(h > 0)] = sand
    img[(h > 2)] = grass
    img[(h > 8)] = jungle
    img[(h > 60)] = rock
    shade = np.clip(1 + np.gradient(h, axis=1) * 0.15 - np.gradient(h, axis=0) * 0.15, 0.6, 1.3)
    img = np.clip(img * shade[..., None], 0, 1)
    im = Image.fromarray((img[::-1] * 255).astype(np.uint8)).resize((1600, 1600))
    d = ImageDraw.Draw(im)

    def px(x, y):
        return ((x + SIZE / 2) / SIZE * 1600, (SIZE / 2 - y) / SIZE * 1600)

    # river + slime color
    pts = [px(x, y) for x, y in zip(*river)]
    d.line(pts, fill=(90, 230, 60), width=12)
    # cart road loop: resort -> golf -> cave -> ruins -> resort
    loop = [Z["hotel"], (-150, -200), Z["golf"], (-250, 120), Z["cave"], (40, 180), (200, 150), Z["ruins"],
            (250, -120), (150, -250), Z["hotel"]]
    d.line([px(*p) for p in loop], fill=(200, 170, 120), width=8, joint="curve")
    try:
        f = ImageFont.truetype("DejaVuSans-Bold.ttf", 30)
        fs = ImageFont.truetype("DejaVuSans.ttf", 22)
    except OSError:
        f = fs = ImageFont.load_default()
    labels = [("RESORT HOTEL (5 floors)", "hotel"), ("POOL", "pool"), ("BEACH: dive shop, tiki bar", "beach"),
              ("GOLF COURSE", "golf"), ("THICK JUNGLE", "jungle"), ("JUNGLE RUINS + blob factory", "ruins"),
              ("CAVE (behind waterfall)", "cave"), ("CRASHED ALIEN SHIP (King Glorp)", "ship"),
              ("BIG WATERFALL", "waterfall")]
    gx, gy = px(*Z["golf"])
    d.ellipse([gx - 150, gy - 130, gx + 150, gy + 130], fill=(120, 200, 80), outline=(70, 140, 50), width=4)
    for k, (x, y) in enumerate([(-330, -80), (-240, 20), (-300, 60), (-210, -120)]):
        a, b = px(x, y)
        d.ellipse([a - 14, b - 10, a + 14, b + 10], fill=(60, 170, 60))
        d.ellipse([a + 18, b + 4, a + 34, b + 14], fill=(240, 225, 170))
    for text, k in labels:
        x, y = px(*Z[k])
        d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(255, 80, 60), outline=(0, 0, 0), width=2)
        if Z[k][0] > 150:
            d.text((x - 14, y + 14), text, fill=(255, 255, 255), font=f, stroke_width=3, stroke_fill=(0, 0, 0),
                   anchor="ra")
        else:
            d.text((x + 14, y - 16), text, fill=(255, 255, 255), font=f, stroke_width=3, stroke_fill=(0, 0, 0))
    d.text((30, 30), "REX MAGNUM — ISLAND ROUGH MAP (north up, 1 km square)", fill=(255, 255, 255), font=f,
           stroke_width=3, stroke_fill=(0, 0, 0))
    d.text((30, 1540), "brown line = golf-cart road loop   ·   green line = slime river   ·   red dots = zones",
           fill=(255, 255, 255), font=fs, stroke_width=2, stroke_fill=(0, 0, 0))
    # scale bar 100 m
    x0, y0 = 1350, 1540
    d.line([(x0, y0), (x0 + 160, y0)], fill=(255, 255, 255), width=6)
    d.text((x0 + 40, y0 - 34), "100 m", fill=(255, 255, 255), font=fs, stroke_width=2, stroke_fill=(0, 0, 0))
    im.save(path)


# ------------------------------------------------------------------ Blender blockout

def blender_blockout(h, river, tree_count=2500):
    import bmesh
    import bpy
    from mathutils import Vector

    def mat(name, color, rough=0.8, **kw):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (*color, 1)
        b.inputs["Roughness"].default_value = rough
        for k, v in kw.items():
            b.inputs[k].default_value = v
        return m

    def link(ob, coll="Island"):
        c = bpy.data.collections.get(coll) or bpy.data.collections.new(coll)
        if c.name not in bpy.context.scene.collection.children:
            bpy.context.scene.collection.children.link(c)
        c.objects.link(ob)
        return ob

    def box(name, size, loc, m, rot=0.0):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(m)
        ob = link(bpy.data.objects.new(name, me))
        ob.location = loc
        ob.rotation_euler = (0, 0, rot)
        return ob

    def height_at(x, y):
        res = h.shape[0]
        i = int(np.clip((y + SIZE / 2) / SIZE * (res - 1), 0, res - 1))
        j = int(np.clip((x + SIZE / 2) / SIZE * (res - 1), 0, res - 1))
        return float(h[i, j])

    # ---- terrain mesh (downsampled), material by height and slope
    step = 4
    hs = h[::step, ::step]
    n = hs.shape[0]
    xs = np.linspace(-SIZE / 2, SIZE / 2, n)
    bm = bmesh.new()
    verts = [[bm.verts.new((xs[j], xs[i], hs[i, j])) for j in range(n)] for i in range(n)]
    for i in range(n - 1):
        for j in range(n - 1):
            bm.faces.new((verts[i][j], verts[i][j + 1], verts[i + 1][j + 1], verts[i + 1][j]))
    me = bpy.data.meshes.new("Terrain")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    terr = link(bpy.data.objects.new("Terrain", me))
    tm = bpy.data.materials.new("Terrain")
    tm.use_nodes = True
    nt = tm.node_tree
    b = nt.nodes["Principled BSDF"]
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs[0])
    nrm = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Normal"], nrm.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")         # by height
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0.0, (0.75, 0.68, 0.5, 1)       # wet sand
    e[1].position, e[1].color = 0.4, (0.15, 0.32, 0.08, 1)
    for pos, col in ((0.03, (0.93, 0.86, 0.66, 1)), (0.06, (0.3, 0.5, 0.15, 1)), (0.75, (0.35, 0.33, 0.3, 1))):
        el = e.new(pos)
        el.color = col
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = 0.0
    mr.inputs["From Max"].default_value = 140.0
    nt.links.new(sep.outputs[2], mr.inputs["Value"])
    nt.links.new(mr.outputs[0], ramp.inputs[0])
    rockmix = nt.nodes.new("ShaderNodeMix")
    rockmix.data_type = "RGBA"
    sl = nt.nodes.new("ShaderNodeMapRange")              # steep = rock
    sl.inputs["From Min"].default_value = 0.85
    sl.inputs["From Max"].default_value = 0.65
    nt.links.new(nrm.outputs[2], sl.inputs["Value"])
    nt.links.new(sl.outputs[0], rockmix.inputs[0])
    nt.links.new(ramp.outputs[0], rockmix.inputs[6])
    rockmix.inputs[7].default_value = (0.32, 0.3, 0.27, 1)
    nt.links.new(rockmix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    me.materials.append(tm)

    # ---- sea + shallow color
    sea = mat("Sea", (0.02, 0.18, 0.25), 0.02, **{"Transmission Weight": 0.6, "IOR": 1.33})
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=3000)
    me = bpy.data.meshes.new("Sea")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(sea)
    link(bpy.data.objects.new("Sea", me)).location = (0, 0, 0.0)

    # ---- slime river (glowing green ribbon) + waterfalls
    slime = mat("SlimeRiver", (0.25, 0.9, 0.15), 0.05, **{"Emission Color": (0.3, 1.0, 0.2, 1), "Emission Strength": 0.6})
    rx, ry = river
    bm = bmesh.new()
    prev = None
    for k in range(0, len(rx) - 1, 2):
        x, y = rx[k], ry[k]
        dx, dy = rx[k + 1] - x, ry[k + 1] - y
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L * 7, dx / L * 7
        z = max(height_at(x, y) + 0.6, 0.3)
        a = bm.verts.new((x - nx, y - ny, z))
        b2 = bm.verts.new((x + nx, y + ny, z))
        if prev:
            bm.faces.new((prev[0], prev[1], b2, a))
        prev = (a, b2)
    me = bpy.data.meshes.new("River")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(slime)
    link(bpy.data.objects.new("SlimeRiver", me))
    water = mat("Waterfall", (0.75, 0.95, 0.85), 0.1, **{"Emission Color": (0.6, 1.0, 0.5, 1), "Emission Strength": 0.8,
                                                         "Transmission Weight": 0.4})
    top = height_at(40, 275)
    bot = height_at(40, 232)
    wf = box("BigWaterfall", (40, 2, top - bot), (40, 254, (top + bot) / 2), water)
    wf.rotation_euler = (math.radians(-18), 0, 0)
    box("WaterfallPool", (60, 30, 0.6), (40, 228, bot + 0.3), slime)

    # ---- resort hotel: 5 floors + roof, balconies, lobby, room 304 balcony faces the pool (north)
    white = mat("HotelWhite", (0.92, 0.9, 0.85), 0.6)
    glass = mat("HotelGlass", (0.2, 0.35, 0.45), 0.05, **{"Metallic": 0.5})
    roof = mat("RoofTerracotta", (0.6, 0.25, 0.15), 0.7)
    base = height_at(40, -300)
    W_, D_, F = 90, 22, 3.6
    for fl in range(5):
        z = base + fl * F
        box(f"Hotel_Floor{fl + 1}", (W_, D_, F - 0.2), (40, -300, z + F / 2), white)
        box(f"Hotel_Glass{fl + 1}", (W_ - 2, D_ + 0.2, F * 0.55), (40, -300, z + F * 0.55), glass)
        if fl > 0:
            for k in range(14):
                box(f"Hotel_Balcony{fl}_{k}", (5, 1.6, 0.15), (40 - W_ / 2 + 4 + k * 6.4, -300 + D_ / 2 + 0.8, z), white)
    box("Hotel_Roof", (W_ + 2, D_ + 2, 0.6), (40, -300, base + 5 * F + 0.3), roof)
    box("Hotel_Penthouse", (30, 14, 3.4), (40, -300, base + 5 * F + 2.0), white)
    box("Hotel_Sign", (16, 0.4, 2.5), (40, -300 - D_ / 2 - 0.4, base + 5 * F - 1.4),
        mat("SignNeon", (1, 0.3, 0.6), 0.3, **{"Emission Color": (1, 0.3, 0.6, 1), "Emission Strength": 4.0}))

    # ---- pool area (north side, between hotel and jungle): main pool, swim-up bar, slides, hot tubs, stage
    pool_water = mat("PoolWater", (0.1, 0.7, 0.85), 0.02, **{"Transmission Weight": 0.5,
                                                             "Emission Color": (0.1, 0.6, 0.8, 1), "Emission Strength": 0.2})
    tiles = mat("PoolDeck", (0.85, 0.82, 0.74), 0.5)
    pb = height_at(40, -255)
    box("Pool_Deck", (110, 50, 0.4), (40, -255, pb), tiles)
    box("Pool_Main", (50, 20, 0.5), (30, -255, pb + 0.1), pool_water)
    box("Pool_Bar", (8, 4, 1.2), (52, -255, pb + 0.6), mat("Bamboo", (0.55, 0.4, 0.2), 0.7))
    for k in range(2):
        box(f"HotTub{k}", (4, 4, 0.5), (75 + k * 7, -245, pb + 0.25), pool_water)
    slide = mat("SlideRed", (0.85, 0.15, 0.1), 0.3)
    s = box("WaterSlide", (2, 24, 0.6), (-2, -248, pb + 4), slide)
    s.rotation_euler = (math.radians(14), 0, math.radians(20))
    box("Stage", (10, 6, 0.8), (85, -270, pb + 0.4), mat("Wood", (0.4, 0.25, 0.12), 0.6))
    umb = mat("Umbrella", (1.0, 0.85, 0.2), 0.6)
    for k in range(10):
        x = -15 + k * 9
        box(f"Umbrella{k}", (3, 3, 0.15), (x, -240, pb + 2.4), umb)
        box(f"Chair{k}", (0.8, 2, 0.3), (x + 1.5, -240, pb + 0.35), white)

    # ---- beach: dive shop + tiki bar
    hut = mat("Thatch", (0.6, 0.5, 0.3), 0.9)
    box("DiveShop", (10, 7, 3.5), (-30, -345, height_at(-30, -345) + 1.75), mat("DiveBlue", (0.2, 0.5, 0.8), 0.6))
    box("TikiBar", (8, 8, 3), (110, -345, height_at(110, -345) + 1.5), hut)

    # ---- golf course: greens + sand traps + pro shop + cart shed
    green = mat("GolfGreen", (0.25, 0.65, 0.18), 0.7)
    sandm = mat("SandTrap", (0.95, 0.88, 0.7), 0.9)
    for k, (x, y) in enumerate([(-330, -80), (-240, 20), (-300, 60), (-210, -120)]):
        gz = height_at(x, y)
        g = box(f"Green{k}", (26, 20, 0.3), (x, y, gz + 0.1), green)
        box(f"SandTrap{k}", (9, 6, 0.3), (x + 16, y - 6, gz + 0.12), sandm)
    box("ProShop", (12, 9, 4), (-200, -60, height_at(-200, -60) + 2), white)
    box("CartShed", (16, 8, 3.5), (-215, -80, height_at(-215, -80) + 1.75), roof)

    # ---- jungle ruins: stepped stone temple (blob factory inside)
    stone = mat("TempleStone", (0.45, 0.43, 0.36), 0.95)
    rz = height_at(300, 70)
    for k in range(6):
        s = 40 - k * 6
        box(f"Temple_Step{k}", (s, s, 3.5), (300, 70, rz + 1.75 + k * 3.5), stone)
    box("Temple_Shrine", (8, 8, 6), (300, 70, rz + 6 * 3.5 + 3), stone)
    glow = mat("FactoryGlow", (0.3, 1, 0.2), 0.3, **{"Emission Color": (0.3, 1.0, 0.2, 1), "Emission Strength": 6.0})
    box("Temple_Door", (6, 0.5, 6), (300, 50 - 0.3, rz + 3), glow)
    for k in range(4):
        p = box(f"SlimePipe{k}", (1.2, 14, 1.2), (290 + k * 6, 40, rz + 1.5), glow)
        p.rotation_euler = (0, 0, math.radians(10 * k - 15))

    # ---- cave entrance behind the small north-west waterfall
    cz = height_at(-160, 230)
    box("CaveMouth", (10, 4, 8), (-160, 228, cz + 4), mat("CaveDark", (0.01, 0.01, 0.01), 1.0))
    box("SmallWaterfall", (10, 1, 12), (-160, 224, cz + 6), water)

    # ---- crashed alien ship on top of the big waterfall
    hull = mat("ShipHull", (0.55, 0.6, 0.62), 0.25, **{"Metallic": 1.0})
    sz = height_at(40, 300)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=16, radius=30)
    bmesh.ops.scale(bm, vec=(1, 1, 0.22), verts=bm.verts)
    me = bpy.data.meshes.new("Ship")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(hull)
    ship = link(bpy.data.objects.new("CrashedShip", me))
    ship.location = (40, 300, sz + 3)
    ship.rotation_euler = (math.radians(14), math.radians(-9), 0.3)
    dome = box("ShipDome", (14, 14, 6), (40, 300, sz + 9), mat("ShipDome", (0.4, 1.0, 0.4), 0.05,
                                                                **{"Emission Color": (0.3, 1, 0.3, 1), "Emission Strength": 2.0}))
    dome.rotation_euler = ship.rotation_euler
    ooze = box("ShipOoze", (8, 30, 0.6), (40, 275, sz - 5), slime)
    ooze.rotation_euler = (math.radians(-35), 0, 0)

    # ---- trees: palms near the coast/resort, jungle trees inland (simple instances)
    trunk = mat("PalmTrunk", (0.35, 0.25, 0.15), 0.9)
    leaf = mat("Leaves", (0.1, 0.4, 0.08), 0.7, **{"Subsurface Weight": 0.2})
    coll_t = bpy.data.collections.new("TreeProtos")
    bpy.context.scene.collection.children.link(coll_t)
    coll_t.hide_render = True
    protos = []
    for kind in ("palm", "jungle"):
        bm = bmesh.new()
        if kind == "palm":
            bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.35, radius2=0.22, depth=12)
            bmesh.ops.translate(bm, vec=(0, 0, 6), verts=bm.verts)
        else:
            bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.6, radius2=0.4, depth=16)
            bmesh.ops.translate(bm, vec=(0, 0, 8), verts=bm.verts)
        me = bpy.data.meshes.new(kind + "_trunk")
        bm.to_mesh(me)
        bm.free()
        me.materials.append(trunk)
        t = bpy.data.objects.new(kind + "_trunk", me)
        coll_t.objects.link(t)
        bm = bmesh.new()
        if kind == "palm":
            for a in range(8):
                ang = a / 8 * math.tau
                v0 = bm.verts.new((0, 0, 12))
                v1 = bm.verts.new((math.cos(ang) * 5 - math.sin(ang) * 0.8, math.sin(ang) * 5 + math.cos(ang) * 0.8, 10.5))
                v2 = bm.verts.new((math.cos(ang) * 6, math.sin(ang) * 6, 9.5))
                v3 = bm.verts.new((math.cos(ang) * 5 + math.sin(ang) * 0.8, math.sin(ang) * 5 - math.cos(ang) * 0.8, 10.5))
                bm.faces.new((v0, v1, v2, v3))
        else:
            bmesh.ops.create_icosphere(bm, subdivisions=2, radius=7)
            bmesh.ops.scale(bm, vec=(1, 1, 0.7), verts=bm.verts)
            bmesh.ops.translate(bm, vec=(0, 0, 16), verts=bm.verts)
        me = bpy.data.meshes.new(kind + "_leaves")
        bm.to_mesh(me)
        bm.free()
        me.materials.append(leaf)
        l = bpy.data.objects.new(kind + "_leaves", me)
        coll_t.objects.link(l)
        c = bpy.data.collections.new("Tree_" + kind)
        c.objects.link(t)
        c.objects.link(l)
        protos.append(c)
    rng = np.random.default_rng(7)
    placed = 0
    tries = 0
    while placed < tree_count and tries < tree_count * 20:
        tries += 1
        x, y = rng.uniform(-450, 450), rng.uniform(-420, 420)
        z = height_at(x, y)
        if z < 1.5 or z > 110:
            continue
        # keep zones clear
        if any(math.hypot(x - Z[k][0], y - Z[k][1]) < r for k, r in
               (("hotel", 75), ("pool", 65), ("golf", 120), ("ruins", 40), ("ship", 45), ("waterfall", 30))):
            continue
        # keep the 3 view-picture sight lines open (camera -> target)
        if any(_seg_dist(x, y, a, b) < 22 for a, b in VIEW_LINES):
            continue
        jungle = z > 6
        proto = protos[1] if jungle and rng.random() < 0.75 else protos[0]
        inst = bpy.data.objects.new("Tree", None)
        inst.instance_type = "COLLECTION"
        inst.instance_collection = proto
        inst.location = (x, y, z - 0.3)
        s = rng.uniform(0.7, 1.3)
        inst.scale = (s, s, s)
        inst.rotation_euler = (rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), rng.uniform(0, math.tau))
        link(inst, "Trees")
        placed += 1
    print(f"[Island] blockout done, {placed} trees")
    return height_at


VIEW_LINES = [((40, -395), (40, -270)), ((20, 120), (40, 262)), ((215, -40), (300, 70))]


ROAD_LOOP = [(-40, -300), (-150, -200), Z["golf"], (-250, 120), Z["cave"], (40, 180), (200, 150), Z["ruins"],
             (250, -120), (150, -250), (115, -300), (80, -326), (0, -326)]
# golf-cart road (brief section 6): closed loop resort -> golf -> cave -> ruins -> resort; at the resort it runs
# round the south (beach) side of the hotel, clear of the hotel and the pool deck
ROAD_HALF = 3.0                                 # 6 m wide dirt road


def road_path(step=2.0):
    """Closed Catmull-Rom loop through ROAD_LOOP, sampled about every `step` metres: list of (x, y)."""
    P = [(float(x) + 0.0, float(y) + 0.0) for x, y in ROAD_LOOP]
    n = len(P)
    out = []
    for i in range(n):
        p0, p1, p2, p3 = P[i - 1], P[i], P[(i + 1) % n], P[(i + 2) % n]
        L = math.hypot(p2[0] - p1[0], p2[1] - p1[1])
        k = max(2, int(L / step))
        for j in range(k):
            t = j / k
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[c]) + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t2
                                    + (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t3) for c in (0, 1)))
    return out


def road_river_crossings(river, road=None):
    """Points where the road crosses the slime river (bridge spots): list of ((x, y), road direction (dx, dy))."""
    road = road or road_path()
    rx, ry = river
    out = []
    n = len(road)
    for i in range(n):
        x0, y0 = road[i]
        x1, y1 = road[(i + 1) % n]
        for k in range(0, len(rx) - 1):
            # segment intersection test
            ax, ay, bx, by = rx[k], ry[k], rx[k + 1], ry[k + 1]
            d = (x1 - x0) * (by - ay) - (y1 - y0) * (bx - ax)
            if abs(d) < 1e-9:
                continue
            t = ((ax - x0) * (by - ay) - (ay - y0) * (bx - ax)) / d
            u = ((ax - x0) * (y1 - y0) - (ay - y0) * (x1 - x0)) / d
            if 0 <= t <= 1 and 0 <= u <= 1:
                L = math.hypot(x1 - x0, y1 - y0) or 1
                out.append(((x0 + t * (x1 - x0), y0 + t * (y1 - y0)), ((x1 - x0) / L, (y1 - y0) / L)))
    return out


def flatten_road(X, Y, h, river_xy):
    """Level the ground across the road (cart can drive): inside the road the height is the smoothed centre-line
    height; a 4 m shoulder blends back. Not near the river channel (bridges cross there)."""
    road = road_path()
    n = h.shape[0] - 1
    def hat(x, y):
        fx = min(max((x + SIZE / 2) / SIZE * n, 0), n - 1e-6)
        fy = min(max((y + SIZE / 2) / SIZE * n, 0), n - 1e-6)
        ix, iy, tx, ty = int(fx), int(fy), fx - int(fx), fy - int(fy)
        return (h[iy, ix] * (1 - tx) * (1 - ty) + h[iy, ix + 1] * tx * (1 - ty) + h[iy + 1, ix] * (1 - tx) * ty
                + h[iy + 1, ix + 1] * tx * ty)
    zc = np.array([hat(x, y) for x, y in road])
    k = np.ones(9) / 9                                    # smooth the centre line along the road (no bumps)
    zc = np.convolve(np.concatenate([zc[-4:], zc, zc[:4]]), k, mode="valid")
    rx, ry = river_xy
    rpts = np.stack([rx, ry], 1)
    cell = SIZE / n
    out = h.copy()
    wsum = np.zeros_like(h)
    acc = np.zeros_like(h)
    for (x, y), z in zip(road[::1], zc):
        if np.min(np.hypot(rpts[:, 0] - x, rpts[:, 1] - y)) < 16:
            continue
        x0, x1 = int(max((x - 8 + SIZE / 2) / cell, 0)), int(min((x + 8 + SIZE / 2) / cell, n)) + 1
        y0, y1 = int(max((y - 8 + SIZE / 2) / cell, 0)), int(min((y + 8 + SIZE / 2) / cell, n)) + 1
        d = np.hypot(X[y0:y1, x0:x1] - x, Y[y0:y1, x0:x1] - y)
        w = np.clip(1 - (d - ROAD_HALF) / 4.0, 0, 1)
        w = w * w * (3 - 2 * w)
        upd = w > wsum[y0:y1, x0:x1]
        wsum[y0:y1, x0:x1] = np.where(upd, w, wsum[y0:y1, x0:x1])
        acc[y0:y1, x0:x1] = np.where(upd, z, acc[y0:y1, x0:x1])
    land = h > 0.5
    out = np.where(land & (wsum > 0), h * (1 - wsum) + acc * wsum, h)
    return out


def _seg_dist(x, y, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(x - ax - t * dx, y - ay - t * dy)


def render_views(height_at, out_dir, samples=64, res=(1600, 900)):
    import bpy
    from mathutils import Vector
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.exposure = -2.6
    w = bpy.data.worlds.new("Sky")
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes["Background"]
    sky = nt.nodes.new("ShaderNodeTexSky")
    for t in ("MULTIPLE_SCATTERING", "SINGLE_SCATTERING", "NISHITA", "HOSEK_WILKIE"):
        try:
            sky.sky_type = t
            break
        except TypeError:
            continue
    try:
        sky.sun_elevation = math.radians(35)
        sky.sun_rotation = math.radians(200)
    except AttributeError:
        pass
    nt.links.new(sky.outputs[0], bg.inputs[0])
    bg.inputs[1].default_value = 0.35
    sc.world = w
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 4.0
    sun.data.angle = math.radians(1.0)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(200))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.clip_end = 5000
    sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(out_dir, exist_ok=True)
    shots = [
        ("island_view_hotel_from_beach.png", (40, -395, 6), (40, -270, 12), 24),
        ("island_view_waterfall.png", (20, 120, 70), (40, 262, 90), 28),
        ("island_view_ruins.png", (215, -40, 45), (300, 70, 30), 28),
        ("island_overview.png", (-500, -650, 320), (20, 20, 0), 35),
    ]
    for name, loc, tgt, lens in shots:
        loc = Vector(loc)
        loc.z = max(loc.z, height_at(loc.x, loc.y) + 12)
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(tgt) - loc).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        print("[Island] rendered", name)
