"""
Computer Quest: Tech Town - Blender asset builder.

Run with Blender's Python (bpy):
    python3 blender/build_assets.py            # pip install bpy
    blender -b -P blender/build_assets.py      # or with a Blender install

Produces blender/town_assets.glb, which tools/build.py embeds into the game HTML.

Conventions
- Blender units = meters = game units. Z is up. Every asset is modeled at the origin.
- Building fronts face -Y (becomes +Z / toward the camera in three.js after glTF export).
- Material names carry meaning for the game's material swapper:
    Brick_<hex>, Siding_<hex>, Stucco_<hex>, Stone_<hex>, Roof_<hex>, Concrete_<hex>
        -> tinted procedural textures with world-space UVs
    Window, Glass, Door_<hex>, Sign_<id>, Face, Water, Leaf_<hex>, Bark,
    Paint (recolored per car), Shirt / Pants / Hair / Skin (recolored per character),
    Glow_<hex> (emissive), Col_<hex> (plain color)
"""
import bpy, math, random, os, sys

random.seed(7)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'town_assets.glb')

bpy.ops.wm.read_factory_settings(use_empty=True)

# ------------------------------------------------------------------ materials
MATS = {}
def lin(h):
    h = h.lstrip('#')
    return tuple((int(h[i:i + 2], 16) / 255.0) ** 2.2 for i in (0, 2, 4))

def mat(name, color=None, rough=0.85, metal=0.0, emit=None):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    if color is None:
        part = name.split('_')[-1]
        color = part if (len(part) == 6 and all(c in '0123456789abcdefABCDEF' for c in part)) else 'cccccc'
    b.inputs['Base Color'].default_value = (*lin(color), 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit:
        b.inputs['Emission Color'].default_value = (*lin(emit), 1)
        b.inputs['Emission Strength'].default_value = 1.0
    MATS[name] = m
    return m

# ------------------------------------------------------------------ primitives
CUR = []

def _fin(o, m, bev=0.0, smooth=False):
    if bev > 0:
        md = o.modifiers.new('bev', 'BEVEL')
        md.width = bev
        md.segments = 1
        md.limit_method = 'ANGLE'
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=md.name)
    o.data.materials.clear()
    o.data.materials.append(m)
    if smooth:
        for p in o.data.polygons:
            p.use_smooth = True
    CUR.append(o)
    return o

def box(x0, x1, y0, y1, z0, z1, m, bev=0.0, rot_z=0.0):
    """Axis-aligned box from bounds."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    o = bpy.context.active_object
    o.scale = (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))
    bpy.ops.object.transform_apply(scale=True)
    if rot_z:
        o.rotation_euler[2] = rot_z
    return _fin(o, m, bev)

def cbox(cx, cy, cz, sx, sy, sz, m, bev=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(cx, cy, cz), rotation=rot)
    o = bpy.context.active_object
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(scale=True)
    return _fin(o, m, bev)

def cyl(r, z0, z1, x, y, m, verts=12, smooth=True, r2=None, rot=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=z1 - z0, location=(x, y, (z0 + z1) / 2))
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=z1 - z0, location=(x, y, (z0 + z1) / 2))
    o = bpy.context.active_object
    if rot:
        o.rotation_euler = rot
    return _fin(o, m, smooth=smooth)

def sph(r, x, y, z, m, sx=1, sy=1, sz=1, seg=12, ring=8, smooth=True, ico=0):
    if ico:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=ico, radius=r, location=(x, y, z))
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=r, location=(x, y, z))
    o = bpy.context.active_object
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(scale=True)
    return _fin(o, m, smooth=smooth)

def plane(cx, cy, cz, w, h, m, face='-Y'):
    """Vertical plane with 0..1 UVs, texture upright, visible from `face` side."""
    rz = {'-Y': 0, '+X': math.pi / 2, '+Y': math.pi, '-X': -math.pi / 2}[face]
    bpy.ops.mesh.primitive_plane_add(size=1, location=(cx, cy, cz), rotation=(math.pi / 2, 0, rz))
    o = bpy.context.active_object
    o.scale = (w, h, 1)
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return _fin(o, m)

def prism_roof(w, d, z0, h, m, over=0.4, ridge='X'):
    """Gable roof: triangle prism. ridge along X (slopes face front/back) or Y."""
    import bmesh
    me = bpy.data.meshes.new('roof')
    bm = bmesh.new()
    if ridge == 'X':
        hw, hd = w / 2 + over, d / 2 + over
        pts = [(-hw, -hd, z0), (-hw, hd, z0), (-hw, 0, z0 + h), (hw, -hd, z0), (hw, hd, z0), (hw, 0, z0 + h)]
    else:
        hw, hd = w / 2 + over, d / 2 + over
        pts = [(-hw, -hd, z0), (hw, -hd, z0), (0, -hd, z0 + h), (-hw, hd, z0), (hw, hd, z0), (0, hd, z0 + h)]
    v = [bm.verts.new(p) for p in pts]
    bm.faces.new((v[0], v[2], v[1]))
    bm.faces.new((v[3], v[4], v[5]))
    bm.faces.new((v[0], v[3], v[5], v[2]))
    bm.faces.new((v[1], v[2], v[5], v[4]))
    bm.faces.new((v[0], v[1], v[4], v[3]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new('roof', me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    return _fin(o, m)

def start():
    CUR.clear()

def finish(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in CUR:
        o.select_set(True)
    bpy.context.view_layer.objects.active = CUR[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    CUR.clear()
    return o

# ------------------------------------------------------------------ shared materials
WIN = mat('Window', 'a9d4f2', rough=0.3)
GLASS = mat('Glass', '5f8fb8', rough=0.15, metal=0.2)
TRIM = mat('Col_f4f1ea')
DARK = mat('Col_2e3440')
METAL = mat('Col_8a939e', rough=0.4, metal=0.6)
CONC = mat('Concrete_c9c5bc')
GLOWW = mat('Glow_fff2b0', emit='fff2b0')

def win(x, y, zc, w, h, face='-Y', sill=True, shutters=None):
    """Framed window on a wall whose surface is at y (front) or x (sides)."""
    o = 0.03
    if face == '-Y':
        plane(x, y - o, zc, w, h, WIN, '-Y')
        box(x - w / 2 - .08, x + w / 2 + .08, y - .09, y, zc + h / 2, zc + h / 2 + .1, TRIM)
        if sill:
            box(x - w / 2 - .14, x + w / 2 + .14, y - .16, y, zc - h / 2 - .1, zc - h / 2, TRIM)
        box(x - w / 2 - .08, x - w / 2, y - .09, y, zc - h / 2, zc + h / 2, TRIM)
        box(x + w / 2, x + w / 2 + .08, y - .09, y, zc - h / 2, zc + h / 2, TRIM)
        if shutters:
            sm = mat('Col_' + shutters)
            box(x - w / 2 - .5, x - w / 2 - .1, y - .06, y, zc - h / 2, zc + h / 2, sm)
            box(x + w / 2 + .1, x + w / 2 + .5, y - .06, y, zc - h / 2, zc + h / 2, sm)
    else:
        s = 1 if face == '+X' else -1
        plane(x + s * o, y, zc, w, h, WIN, face)
        box(min(x, x + s * .09), max(x, x + s * .09), y - w / 2 - .08, y + w / 2 + .08, zc + h / 2, zc + h / 2 + .1, TRIM)
        if sill:
            box(min(x, x + s * .16), max(x, x + s * .16), y - w / 2 - .14, y + w / 2 + .14, zc - h / 2 - .1, zc - h / 2, TRIM)

def door(x, y, w, h, color='7a4a2a', glass=False):
    plane(x, y - .03, h / 2, w, h, GLASS if glass else mat('Door_' + color), '-Y')
    box(x - w / 2 - .12, x + w / 2 + .12, y - .12, y, h, h + .14, TRIM)
    box(x - w / 2 - .12, x - w / 2, y - .12, y, 0, h, TRIM)
    box(x + w / 2, x + w / 2 + .12, y - .12, y, 0, h, TRIM)

def sign(cid, x, y, zc, w, h, board='1f2d3d'):
    box(x - w / 2 - .12, x + w / 2 + .12, y - .14, y, zc - h / 2 - .12, zc + h / 2 + .12, mat('Col_' + board))
    plane(x, y - .16, zc, w, h, mat('Sign_' + cid), '-Y')

def steps(x, y, w, n=3, rise=.18, run=.4, m=None):
    m = m or CONC
    for i in range(n):
        box(x - w / 2, x + w / 2, y - run * (n - i), y, 0, rise * (i + 1), m)

def lamp_bulb(x, y, z):
    sph(.14, x, y, z, GLOWW, seg=8, ring=6)

# ================================================================== BUILDINGS
# Each returns (W, D). Front wall at y = -D/2.

def b_home():
    W, D, H = 10, 8, 4.2
    fy = -D / 2
    wall = mat('Siding_8fc1e3')
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, wall)
    box(-W / 2 - .1, W / 2 + .1, -D / 2 - .1, D / 2 + .1, 0, .45, mat('Stone_9b958a'))  # foundation
    prism_roof(W, D, H, 2.8, mat('Roof_8b3a3a'), over=.5, ridge='X')
    box(2.2, 3.2, -1.0, 0.2, H + .8, H + 3.4, mat('Brick_9c4a3a'))  # chimney
    box(2.05, 3.35, -1.15, .35, H + 3.4, H + 3.6, CONC)
    door(0, fy, 1.2, 2.3, 'c0392b')
    win(-3, fy, 2.3, 1.5, 1.4, shutters='2c5d8a')
    win(3, fy, 2.3, 1.5, 1.4, shutters='2c5d8a')
    for yy in (-1.8, 1.8):
        win(W / 2, yy, 2.3, 1.2, 1.3, face='+X')
        win(-W / 2, yy, 2.3, 1.2, 1.3, face='-X')
    # porch
    box(-2.0, 2.0, fy - 1.8, fy, 0, .3, mat('Col_b08a5e'))
    for px in (-1.8, 1.8):
        cyl(.1, .3, 2.8, px, fy - 1.6, TRIM, verts=8)
    box(-2.3, 2.3, fy - 2.1, fy, 2.8, 3.0, mat('Roof_8b3a3a'))
    steps(0, fy - 1.8, 1.6, n=2, rise=.15, run=.35)
    # flower boxes
    for bx in (-3, 3):
        box(bx - .8, bx + .8, fy - .35, fy, 1.35, 1.6, mat('Col_7a4a2a'))
        for k in range(5):
            sph(.13, bx - .6 + k * .3, fy - .2, 1.7, mat('Col_' + random.choice(['e84a5f', 'ffcc33', 'ff7aa2'])), seg=6, ring=4)
    sign('home', 0, fy, 3.7, 2.6, .55, '5b3a1e')
    return W, D

def b_mouse():
    W, D, H = 14, 10, 5.2
    fy = -D / 2
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, mat('Stucco_efe2c4'))
    box(-W / 2 - .15, W / 2 + .15, -D / 2 - .15, D / 2 + .15, H, H + .6, mat('Col_e07b39'))  # parapet band
    box(-W / 2 + .3, W / 2 - .3, -D / 2 + .3, D / 2 - .3, H + .6, H + .7, CONC)
    # glass front with mullions
    plane(0, fy - .03, 2.2, 8, 3.6, GLASS, '-Y')
    for mx in [-4 + i * 1.6 for i in range(6)]:
        box(mx - .06, mx + .06, fy - .1, fy, .4, 4.0, DARK)
    box(-4.06, 4.06, fy - .1, fy, 3.95, 4.1, DARK)
    box(-4.06, 4.06, fy - .1, fy, .35, .45, DARK)
    door(0, fy - .05, 1.6, 2.4, glass=True)
    # canopy
    box(-2.4, 2.4, fy - 2.2, fy, 3.1, 3.3, mat('Col_e07b39'))
    for px in (-2.2, 2.2):
        cyl(.08, 0, 3.1, px, fy - 2.0, METAL, verts=8)
    # colored stripes / side windows
    for sx in (-5.7, 5.7):
        win(sx, fy, 2.4, 1.6, 1.6)
    for yy in (-2.5, 0, 2.5):
        win(W / 2, yy, 2.6, 1.4, 1.4, face='+X')
        win(-W / 2, yy, 2.6, 1.4, 1.4, face='-X')
    # flagpole
    cyl(.07, 0, 8.5, -W / 2 - 1.2, fy - 1.0, METAL, verts=8)
    plane(-W / 2 - .45, fy - 1.0, 7.7, 1.4, .9, mat('Col_2d7ff9'), '-Y')
    plane(-W / 2 - .45, fy - .99, 7.7, 1.4, .9, mat('Col_2d7ff9'), '+Y')
    sign('mouse', 0, fy - .02, 4.55, 5.2, .75, 'ffffff')
    # rooftop units
    box(2, 4, 0, 2, H + .7, H + 1.6, METAL, bev=.05)
    box(-4.5, -3, -1, 1, H + .7, H + 1.3, METAL, bev=.05)
    return W, D

def b_keys():
    W, D, H = 12, 10, 8
    fy = -D / 2
    brick = mat('Brick_b5533c')
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, brick)
    box(-W / 2 - .15, W / 2 + .15, -D / 2 - .15, D / 2 + .15, H, H + .35, TRIM)  # cornice
    box(-W / 2 - .05, W / 2 + .05, -D / 2 - .05, D / 2 + .05, 3.95, 4.15, TRIM)  # floor band
    prism_roof(W, D, H + .35, 2.6, mat('Roof_4a5563'), over=.3, ridge='X')
    # cupola with clock
    box(-1, 1, -1, 1, H + 1.2, H + 3.4, TRIM)
    prism_roof(2, 2, H + 3.4, 1.4, mat('Roof_2f6f4f'), over=.2, ridge='Y')
    cyl(.55, fy + 3.95, fy + 4.0, 0, 0, TRIM, verts=16, rot=(math.pi / 2, 0, 0))
    plane(0, -1.03, H + 2.3, 1.1, 1.1, mat('Sign_clock'), '-Y')
    door(0, fy, 1.8, 2.6, '2c5d8a')
    steps(0, fy, 3.2, n=3)
    for wx in (-4, -2.2, 2.2, 4):
        win(wx, fy, 2.1, 1.2, 1.6)
        win(wx, fy, 6.0, 1.2, 1.6)
    win(0, fy, 6.0, 1.2, 1.6)
    for yy in (-2.8, 0, 2.8):
        for zz in (2.1, 6.0):
            win(W / 2, yy, zz, 1.2, 1.6, face='+X')
            win(-W / 2, yy, zz, 1.2, 1.6, face='-X')
    sign('keys', 0, fy - .02, 3.45, 3.8, .6, '1f3b5a')
    return W, D

def b_office():
    W, D, H = 12, 11, 17
    fy = -D / 2
    cm = mat('Concrete_d7d9dc')
    # glass core
    box(-W / 2 + .3, W / 2 - .3, -D / 2 + .3, D / 2 - .3, 0, H, GLASS)
    # concrete floor bands
    for f in range(6):
        z = f * 3.3
        box(-W / 2, W / 2, -D / 2, D / 2, z, z + .45, cm)
    # vertical fins (front + sides)
    for fx in [-W / 2 + i * 2.0 for i in range(7)]:
        box(fx - .15, fx + .15, fy - .25, fy + .3, 0, H, cm)
    for fyy in [-D / 2 + i * 2.2 for i in range(6)]:
        box(W / 2 - .3, W / 2 + .25, fyy - .15, fyy + .15, 0, H, cm)
        box(-W / 2 - .25, -W / 2 + .3, fyy - .15, fyy + .15, 0, H, cm)
    box(-W / 2 - .2, W / 2 + .2, -D / 2 - .2, D / 2 + .2, H, H + .6, cm)
    # lobby canopy + entrance
    box(-3, 3, fy - 2.4, fy, 3.2, 3.45, DARK)
    for px in (-2.8, 2.8):
        cyl(.1, 0, 3.2, px, fy - 2.2, METAL, verts=8)
    door(0, fy - .3, 2.2, 2.6, glass=True)
    sign('office', 0, fy - .3, 3.9, 4.2, .6, '1f2d3d')
    # rooftop
    box(-3, 1, -2, 2, H + .6, H + 2.2, METAL, bev=.06)
    cyl(.08, H + .6, H + 4.5, 3.5, 2, METAL, verts=6)
    lamp_bulb(3.5, 2, H + 4.6)
    return W, D

def b_web():
    W, D, H = 14, 11, 7
    fy = -D / 2
    stone = mat('Stone_e6dcc6')
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, stone)
    box(-W / 2 - .2, W / 2 + .2, -D / 2 - .2, D / 2 + .2, H, H + .5, TRIM)
    # portico
    pz = .9
    box(-5, 5, fy - 3.2, fy, 0, pz, stone)
    for i in range(4):
        box(-5 + i * 0, 5, fy - 3.2 - .45 * (i + 1), fy - 3.2 - .45 * i, 0, pz - .22 * (i + 1) if pz - .22 * (i + 1) > 0 else .01, CONC)
    for cx in (-4.2, -1.5, 1.5, 4.2):
        cyl(.42, pz, 6.3, cx, fy - 2.6, TRIM, verts=16)
        box(cx - .55, cx + .55, fy - 3.15, fy - 2.05, pz, pz + .2, TRIM)
        box(cx - .55, cx + .55, fy - 3.15, fy - 2.05, 6.3, 6.5, TRIM)
    box(-5.2, 5.2, fy - 3.3, fy, 6.5, 7.0, TRIM)
    # pediment
    import bmesh
    me = bpy.data.meshes.new('ped'); bm = bmesh.new()
    y0, y1 = fy - 3.3, fy
    pts = [(-5.4, y0, 7.0), (5.4, y0, 7.0), (0, y0, 9.2), (-5.4, y1, 7.0), (5.4, y1, 7.0), (0, y1, 9.2)]
    v = [bm.verts.new(p) for p in pts]
    bm.faces.new((v[0], v[1], v[2])); bm.faces.new((v[3], v[5], v[4]))
    bm.faces.new((v[0], v[2], v[5], v[3])); bm.faces.new((v[1], v[4], v[5], v[2])); bm.faces.new((v[0], v[3], v[4], v[1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('ped', me); bpy.context.collection.objects.link(o); _fin(o, stone)
    door(0, fy, 2.0, 3.2, '5b3a1e')
    for wx in (-5.4, 5.4):
        win(wx, fy, 3.4, 1.1, 3.0, sill=False)
    for yy in (-2.8, 0, 2.8):
        win(W / 2, yy, 3.4, 1.1, 3.0, face='+X')
        win(-W / 2, yy, 3.4, 1.1, 3.0, face='-X')
    sign('web', 0, fy - 3.32, 6.75, 4.6, .5, 'e6dcc6')
    # book statues / planters
    for sx in (-6.3, 6.3):
        box(sx - .6, sx + .6, fy - 1.6, fy - .4, 0, .7, stone)
        sph(.55, sx, fy - 1.0, 1.2, mat('Leaf_3f8f4a'), ico=1, smooth=False)
    return W, D

def b_mail():
    W, D, H = 12, 10, 6
    fy = -D / 2
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, mat('Brick_c9a27e'))
    box(-W / 2 - .2, W / 2 + .2, -D / 2 - .2, D / 2 + .2, H, H + .5, mat('Col_2b4c7e'))
    box(-W / 2 - .05, W / 2 + .05, -D / 2 - .05, D / 2 + .05, 0, .6, mat('Stone_8f8a80'))
    door(0, fy, 1.8, 2.6, glass=True)
    # slanted blue awning
    import bmesh
    cbox(0, fy - .8, 3.1, 4.4, 1.71, .08, mat('Col_2d6cdf'), rot=(0.3588, 0, 0))
    box(-2.2, 2.2, fy - 1.65, fy - 1.55, 2.4, 2.8, mat('Col_ffffff'))
    for wx in (-4, 4):
        win(wx, fy, 2.4, 2.0, 1.8)
    for yy in (-2.5, 2.5):
        win(W / 2, yy, 2.6, 1.6, 1.6, face='+X')
        win(-W / 2, yy, 2.6, 1.6, 1.6, face='-X')
    sign('mail', 0, fy - .02, 4.7, 5.0, .7, '2b4c7e')
    # mailbox out front
    bl = mat('Col_2455b8')
    box(3.2, 3.9, fy - 2.6, fy - 2.0, .3, 1.2, bl, bev=.05)
    cyl(.35, 1.0, 1.3, 3.55, fy - 2.3, bl, verts=12, rot=(0, math.pi / 2, 0))
    for lx in (3.25, 3.85):
        box(lx - .04, lx + .04, fy - 2.55, fy - 2.05, 0, .3, DARK)
    # flag pole
    cyl(.07, 0, 8, -W / 2 - 1, fy - 1, METAL, verts=8)
    plane(-W / 2 - .3, fy - 1, 7.3, 1.3, .8, mat('Col_d63a3a'), '-Y')
    plane(-W / 2 - .3, fy - .99, 7.3, 1.3, .8, mat('Col_d63a3a'), '+Y')
    return W, D

def shop(W, D, H, wall, band, sid, board):
    fy = -D / 2
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, wall)
    box(-W / 2 - .1, W / 2 + .1, -D / 2 - .1, D / 2 + .1, H, H + .3, band)
    plane(0, fy - .03, 1.9, W - 2, 3.0, GLASS, '-Y')
    for mx in (-W / 2 + 1, -1.3, 1.3, W / 2 - 1):
        box(mx - .07, mx + .07, fy - .1, fy, 0, 3.5, DARK)
    box(-W / 2 + .9, W / 2 - .9, fy - .1, fy, 3.35, 3.5, DARK)
    door(0, fy - .05, 1.8, 2.5, glass=True)
    box(-W / 2 - .1, W / 2 + .1, fy - 1.3, fy, 3.6, 3.8, band)
    sign(sid, 0, fy - .02, 4.6, W - 3, .9, board)
    return fy

def b_android():
    W, D, H = 11, 9, 5.8
    fy = shop(W, D, H, mat('Stucco_3b4252'), mat('Col_3ddc84'), 'android', '0f5132')
    # big phone model on roof
    box(-.9, .9, -.2, .2, H + .3, H + 3.6, mat('Col_1c2a24'), bev=.12)
    plane(0, -.22, H + 1.95, 1.5, 3.0, mat('Glow_3ddc84', emit='3ddc84'), '-Y')
    for yy in (-2, 2):
        win(W / 2, yy, 2.4, 1.6, 1.6, face='+X')
        win(-W / 2, yy, 2.4, 1.6, 1.6, face='-X')
    for px in (-4.2, 4.2):
        box(px - .5, px + .5, fy - 1.1, fy - .3, 0, .6, CONC)
        sph(.5, px, fy - .7, .95, mat('Leaf_2f8f4a'), ico=1, smooth=False)
    return W, D

def b_iphone():
    W, D, H = 11, 9, 6.2
    fy = shop(W, D, H, mat('Stucco_eef0f3'), mat('Col_c0c6ce'), 'iphone', 'f7f7f9')
    box(-W / 2 - .6, W / 2 + .6, -D / 2 - 1.2, D / 2 + .3, H + .3, H + .55, mat('Col_d9dde2'))
    box(-.9, .9, -.2, .2, H + .6, H + 3.8, mat('Col_202226'), bev=.2)
    plane(0, -.22, H + 2.2, 1.45, 2.9, mat('Glow_a28bff', emit='a28bff'), '-Y')
    for yy in (-2, 2):
        win(W / 2, yy, 2.4, 1.6, 1.6, face='+X')
        win(-W / 2, yy, 2.4, 1.6, 1.6, face='-X')
    for px in (-4.2, 4.2):
        cyl(.5, 0, .7, px, fy - .8, mat('Col_c0c6ce'), verts=16)
        cyl(.08, .7, 1.7, px, fy - .8, mat('Bark'), verts=6)
        sph(.45, px, fy - .8, 1.9, mat('Leaf_4caf50'), ico=1, smooth=False)
    return W, D

def b_final():
    W, D = 20, 14
    fy = -D / 2
    stone = mat('Stone_ece4d2')
    # wings
    box(-W / 2, -4, -D / 2 + 1, D / 2, 0, 7, stone)
    box(4, W / 2, -D / 2 + 1, D / 2, 0, 7, stone)
    box(-W / 2 - .2, -4, -D / 2 + .8, D / 2 + .2, 7, 7.4, TRIM)
    box(4, W / 2 + .2, -D / 2 + .8, D / 2 + .2, 7, 7.4, TRIM)
    # center block
    box(-4, 4, -D / 2, D / 2, 0, 10, stone)
    box(-4.3, 4.3, -D / 2 - .3, D / 2 + .3, 10, 10.6, TRIM)
    # portico
    box(-4.6, 4.6, fy - 3.4, fy, 0, 1.0, stone)
    for i in range(5):
        h = 1.0 - .2 * (i + 1)
        if h <= 0:
            break
        box(-4.6, 4.6, fy - 3.4 - .45 * (i + 1), fy - 3.4 - .45 * i, 0, h, CONC)
    for cx in (-3.8, -2.3, -.8, .8, 2.3, 3.8):
        cyl(.36, 1.0, 8.8, cx, fy - 2.7, TRIM, verts=16)
    box(-4.6, 4.6, fy - 3.4, fy, 8.8, 9.6, TRIM)
    import bmesh
    me = bpy.data.meshes.new('ped'); bm = bmesh.new()
    y0, y1 = fy - 3.4, fy
    pts = [(-4.8, y0, 9.6), (4.8, y0, 9.6), (0, y0, 11.6), (-4.8, y1, 9.6), (4.8, y1, 9.6), (0, y1, 11.6)]
    v = [bm.verts.new(p) for p in pts]
    bm.faces.new((v[0], v[1], v[2])); bm.faces.new((v[3], v[5], v[4]))
    bm.faces.new((v[0], v[2], v[5], v[3])); bm.faces.new((v[1], v[4], v[5], v[2])); bm.faces.new((v[0], v[3], v[4], v[1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('ped', me); bpy.context.collection.objects.link(o); _fin(o, stone)
    # dome
    cyl(3.2, 10.6, 12.6, 0, 1, stone, verts=24)
    for k in range(12):
        a = k / 12 * math.pi * 2
        cyl(.18, 10.6, 12.6, math.cos(a) * 3.25, 1 + math.sin(a) * 3.25, TRIM, verts=6)
    cyl(3.4, 12.6, 12.9, 0, 1, TRIM, verts=24)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=3.1, location=(0, 1, 12.9))
    d = bpy.context.active_object
    d.scale = (1, 1, .85)
    bpy.ops.object.transform_apply(scale=True)
    _fin(d, mat('Roof_4f8a8b'), smooth=True)
    cyl(.35, 15.4, 16.4, 0, 1, TRIM, verts=10)
    sph(.3, 0, 1, 16.6, mat('Col_d4af37', rough=.3, metal=.8), seg=10, ring=6)
    plane(0, fy - .02, 7.4, 1.6, 1.6, mat('Sign_clock'), '-Y')
    door(0, fy, 2.4, 3.6, '5b3a1e')
    for wx in (-7.5, -5.5, 5.5, 7.5):
        win(wx, -D / 2 + 1, 2.4, 1.2, 2.0)
        win(wx, -D / 2 + 1, 5.2, 1.2, 2.0)
    for yy in (-3, 0, 3):
        for zz in (2.4, 5.2):
            win(W / 2, yy, zz, 1.2, 2.0, face='+X')
            win(-W / 2, yy, zz, 1.2, 2.0, face='-X')
    sign('final', 0, fy - 3.42, 9.2, 5.4, .55, 'ece4d2')
    # flags
    for fx in (-9, 9):
        cyl(.07, 0, 9, fx, fy - 2, METAL, verts=8)
        plane(fx + .7, fy - 2, 8.3, 1.4, .9, mat('Col_2d7ff9'), '-Y')
        plane(fx + .7, fy - 1.99, 8.3, 1.4, .9, mat('Col_2d7ff9'), '+Y')
    return W, D

def b_security():
    W, D, H = 13, 10, 7.5
    fy = -D / 2
    navy = mat('Stucco_2b3f5c')
    box(-W / 2, W / 2, -D / 2, D / 2, 0, H, navy)
    box(-W / 2 - .15, W / 2 + .15, -D / 2 - .15, D / 2 + .15, H, H + .5, mat('Col_d9dde2'))
    box(-W / 2 - .05, W / 2 + .05, -D / 2 - .05, D / 2 + .05, 0, .5, mat('Stone_9b958a'))
    # glass entrance tower
    box(-2.6, 2.6, fy - .6, fy + .2, 0, H + 1.6, GLASS)
    box(-2.75, 2.75, fy - .75, fy + .2, H + 1.6, H + 1.9, mat('Col_d9dde2'))
    for mx in (-2.6, -1.3, 0, 1.3, 2.6):
        box(mx - .06, mx + .06, fy - .68, fy - .56, 0, H + 1.6, DARK)
    door(0, fy - .6, 2.0, 2.6, glass=True)
    steps(0, fy - .6, 3.4, n=2, rise=.15, run=.35)
    # big shield emblem
    cyl(1.05, 0, .12, 0, 0, mat('Col_d9dde2'), verts=6, rot=(math.pi / 2, 0, 0)); o = CUR[-1]; o.location = (0, fy - .72, H - .4)
    cyl(.85, 0, .14, 0, 0, mat('Glow_39c7ff', emit='39c7ff'), verts=6, rot=(math.pi / 2, 0, 0)); o = CUR[-1]; o.location = (0, fy - .76, H - .4)
    # side windows (two floors)
    for wx in (-5, -3.8, 3.8, 5):
        win(wx, fy, 2.0, .9, 1.5); win(wx, fy, 5.2, .9, 1.5)
    for yy in (-2.8, 0, 2.8):
        for zz in (2.0, 5.2):
            win(W / 2, yy, zz, 1.2, 1.5, face='+X'); win(-W / 2, yy, zz, 1.2, 1.5, face='-X')
    sign('security', 0, fy - .62, 3.55, 4.6, .6, '16324f')
    # rooftop antenna + satellite dish + blue light bar
    cyl(.07, H + .5, H + 5, 4.5, 2.5, METAL, verts=6)
    sph(.16, 4.5, 2.5, H + 5.1, mat('Glow_ff3b30', emit='ff3b30'), seg=8, ring=6)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1.1, location=(-4, 2, H + 1.6))
    d = bpy.context.active_object; d.scale = (1, 1, .35); d.rotation_euler = (.6, 0, 0); bpy.ops.object.transform_apply(scale=True, rotation=True)
    _fin(d, mat('Col_d9dde2'), smooth=True)
    cyl(.1, H + .5, H + 1.3, -4, 2, METAL, verts=6)
    box(-1.2, 1.2, fy - .5, fy + .2, H + 1.9, H + 2.15, mat('Glow_2d7ff9', emit='2d7ff9'))
    for px in (-5.2, 5.2):
        box(px - .5, px + .5, fy - 1.2, fy - .2, 0, .6, CONC)
        sph(.5, px, fy - .7, .95, mat('Leaf_2f8f4a'), ico=1, smooth=False)
    return W, D

# ================================================================== PROPS
def p_tree(name, kind):
    start()
    bark = mat('Bark', '6b4a2e')
    if kind == 'round':
        cyl(.22, 0, 2.2, 0, 0, bark, verts=7, r2=.15)
        leaf = mat('Leaf_3f9a4a')
        sph(1.5, 0, 0, 3.2, leaf, ico=1, smooth=False)
        sph(1.1, .8, .3, 2.7, leaf, ico=1, smooth=False)
        sph(1.0, -.7, -.3, 2.8, leaf, ico=1, smooth=False)
        sph(.9, .1, -.6, 4.0, mat('Leaf_58b85a'), ico=1, smooth=False)
    elif kind == 'pine':
        cyl(.2, 0, 1.2, 0, 0, bark, verts=7)
        leaf = mat('Leaf_2e7d4f')
        for i, (r, z0, z1) in enumerate([(1.6, 1.0, 3.0), (1.25, 2.3, 4.2), (.9, 3.5, 5.4)]):
            cyl(r, z0, z1, 0, 0, leaf, verts=8, r2=0.05, smooth=False)
    else:  # bush
        leaf = mat('Leaf_4caf50')
        sph(.7, 0, 0, .5, leaf, sz=.75, ico=1, smooth=False)
        sph(.5, .5, .2, .45, leaf, sz=.8, ico=1, smooth=False)
        sph(.45, -.45, -.1, .42, mat('Leaf_58b85a'), sz=.8, ico=1, smooth=False)
    finish(name)

def p_lamp():
    start()
    pole = mat('Col_2e3a46', rough=.4, metal=.5)
    cyl(.18, 0, .3, 0, 0, pole, verts=8)
    cyl(.07, .3, 4.2, 0, 0, pole, verts=8)
    box(-.05, .05, -.05, .9, 4.1, 4.2, pole)
    cyl(.25, 3.85, 4.1, 0, .9, pole, verts=8, r2=.12)
    lamp_bulb(0, .9, 3.82)
    finish('P_lamp')

def p_bench():
    start()
    wood = mat('Col_a0703f')
    iron = mat('Col_2e3440', rough=.5, metal=.5)
    for k in range(3):
        box(-1, 1, -.25 + k * .17, -.12 + k * .17, .45, .52, wood)
    for k in range(2):
        box(-1, 1, .3, .36, .65 + k * .2, .78 + k * .2, wood)
    for x in (-.85, .85):
        box(x - .05, x + .05, -.25, .3, 0, .45, iron)
        box(x - .05, x + .05, .28, .36, 0, 1.05, iron)
    finish('P_bench')

def p_car():
    start()
    paint = mat('Paint', 'e5484d', rough=.35, metal=.3)
    cbox(0, 0, .55, 1.8, 3.9, .6, paint, bev=.12)
    cbox(0, .25, 1.1, 1.55, 2.0, .55, paint, bev=.1)
    dg = mat('Col_1d2b3a', rough=.15)
    plane(0, -.77, 1.1, 1.35, .42, dg, '-Y')      # windshield (front = -Y)
    plane(0, 1.27, 1.1, 1.35, .42, dg, '+Y')
    plane(.785, .25, 1.1, 1.7, .4, dg, '+X')
    plane(-.785, .25, 1.1, 1.7, .4, dg, '-X')
    tire = mat('Col_1b1b1b', rough=.9)
    hub = mat('Col_c9ced6', rough=.3, metal=.7)
    for x in (-.85, .85):
        for y in (-1.25, 1.25):
            cyl(.38, -.13, .13, 0, 0, tire, verts=12, rot=(0, math.pi / 2, 0))
            o = CUR[-1]; o.location = (x, y, .38)
            cyl(.2, -.14, .14, 0, 0, hub, verts=8, rot=(0, math.pi / 2, 0))
            o = CUR[-1]; o.location = (x * 1.01, y, .38)
    for x in (-.6, .6):
        plane(x, -1.96, .65, .35, .18, mat('Glow_fff6d5', emit='fff6d5'), '-Y')
        plane(x, 1.96, .65, .35, .18, mat('Glow_ff3b30', emit='ff3b30'), '+Y')
    box(-.8, .8, -2.0, -1.9, .3, .42, mat('Col_9aa4af', metal=.5))
    finish('P_car')

def p_fountain():
    start()
    stone = mat('Stone_d8d2c4')
    cyl(3.0, 0, .7, 0, 0, stone, verts=24)
    cyl(2.7, .01, .6, 0, 0, mat('Water', '4aa3df', rough=.1), verts=24)
    cyl(.45, 0, 1.8, 0, 0, stone, verts=10)
    cyl(1.2, 1.8, 2.1, 0, 0, stone, verts=16, r2=1.4)
    cyl(1.15, 2.0, 2.12, 0, 0, mat('Water', '4aa3df', rough=.1), verts=16)
    cyl(.2, 2.1, 2.9, 0, 0, stone, verts=8)
    sph(.3, 0, 0, 3.0, stone, seg=10, ring=6)
    finish('P_fountain')

def p_small():
    start()  # hydrant
    red = mat('Col_d63a3a', rough=.5)
    cyl(.18, 0, .7, 0, 0, red, verts=10)
    sph(.18, 0, 0, .72, red, seg=8, ring=6)
    cyl(.07, .4, .5, 0, 0, red, verts=6, rot=(0, math.pi / 2, 0))
    o = CUR[-1]; o.scale = (1, 1, 4.5)
    finish('P_hydrant')
    start()  # trash can
    g = mat('Col_2f6f4f', rough=.5, metal=.3)
    cyl(.3, 0, .9, 0, 0, g, verts=12)
    cyl(.34, .9, 1.0, 0, 0, g, verts=12)
    finish('P_trash')
    start()  # planter with flowers
    cyl(.6, 0, .5, 0, 0, mat('Col_b5533c'), verts=12)
    cyl(.52, .45, .52, 0, 0, mat('Col_5b3a1e'), verts=12)
    for k in range(7):
        a = k / 7 * math.pi * 2
        sph(.14, math.cos(a) * .32, math.sin(a) * .32, .62, mat('Col_' + ['e84a5f', 'ffcc33', 'ff7aa2', 'a78bfa'][k % 4]), seg=6, ring=4)
    sph(.2, 0, 0, .65, mat('Leaf_4caf50'), ico=1, smooth=False)
    finish('P_planter')
    start()  # traffic sign / street sign post
    cyl(.05, 0, 2.6, 0, 0, METAL, verts=6)
    plane(0, -.06, 2.4, .9, .9, mat('Sign_stop'), '-Y')
    plane(0, .06, 2.4, .9, .9, mat('Col_9aa4af'), '+Y')
    finish('P_sign')
    start()  # picket fence segment 4m along X
    w = mat('Col_f4f1ea')
    for k in range(11):
        x = -2 + k * .4
        box(x - .05, x + .05, -.03, .03, 0, .9, w)
        cbox(x, 0, .95, .07, .06, .07, w, rot=(0, math.pi / 4, 0))
    box(-2.05, 2.05, .03, .08, .25, .35, w)
    box(-2.05, 2.05, .03, .08, .6, .7, w)
    finish('P_fence')

# ================================================================== CHARACTER (realistic proportions)
def loft(rings, m, sides=14, cap_top=True, cap_bottom=True):
    """Lofted body shape. rings = [(z, rx, ry, yoff), ...] bottom to top."""
    import bmesh
    me = bpy.data.meshes.new('loft'); bm = bmesh.new(); loops = []
    for z, rx, ry, yo in rings:
        loops.append([bm.verts.new((math.cos(a) * rx, yo + math.sin(a) * ry, z)) for a in [i / sides * math.pi * 2 for i in range(sides)]])
    for a, b in zip(loops, loops[1:]):
        for i in range(sides):
            bm.faces.new((a[i], a[(i + 1) % sides], b[(i + 1) % sides], b[i]))
    if cap_bottom: bm.faces.new(list(reversed(loops[0])))
    if cap_top: bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('loft', me); bpy.context.collection.objects.link(o)
    return _fin(o, m, smooth=True)

def limb(p0, p1, r0, r1, m, sides=10):
    """Tapered cylinder between two points (x,y,z)."""
    import mathutils
    v = mathutils.Vector(p1) - mathutils.Vector(p0); L = v.length
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1, depth=L, location=(0, 0, 0))
    o = bpy.context.active_object
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(v)
    o.location = (mathutils.Vector(p0) + mathutils.Vector(p1)) / 2
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return _fin(o, m, smooth=True)

def c_person():
    import bmesh
    skin = mat('Skin', 'd9a27f', rough=.55)
    shirt = mat('Shirt', '2d7ff9', rough=.85)
    pants = mat('Pants', '2f3b52', rough=.85)
    hair = mat('Hair', '3b2416', rough=.6)
    shoe = mat('Shoe', '262626', rough=.45)
    eyew = mat('EyeWhite', 'f4f1ea', rough=.3)
    iris = mat('Iris', '4a2e1a', rough=.2)
    lips = mat('Lips', 'b0645a', rough=.45)
    belt = mat('Belt', '2a1d14', rough=.5)
    root = bpy.data.objects.new('C_person', None)
    bpy.context.collection.objects.link(root)

    def part(name, pivot, build, parent=None):
        start(); build(); o = finish(name)
        bpy.context.scene.cursor.location = pivot
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        par = parent or root
        o.parent = par
        if parent is not None:
            o.matrix_parent_inverse = par.matrix_world.inverted()
        return o

    def torso():
        loft([(.84, .15, .10, 0), (.92, .165, .105, 0), (.98, .15, .095, 0)], pants)                       # pelvis
        loft([(.975, .152, .097, 0), (1.005, .152, .097, 0)], belt, cap_top=False, cap_bottom=False)       # belt
        loft([(.99, .145, .092, 0), (1.10, .158, .1, -.005), (1.24, .178, .112, -.01), (1.34, .19, .108, 0),
              (1.405, .19, .095, .005), (1.45, .11, .075, .005), (1.47, .06, .05, .005)], shirt)          # chest & shoulders
        limb((0, .005, 1.44), (0, .005, 1.57), .048, .044, skin)                                            # neck
        bpy.ops.mesh.primitive_torus_add(major_radius=.058, minor_radius=.014, major_segments=14, minor_segments=5, location=(0, .004, 1.465))
        _fin(bpy.context.active_object, shirt, smooth=True)                                                  # collar
    part('Torso', (0, 0, .9), torso)

    def head():
        bpy.ops.mesh.primitive_uv_sphere_add(segments=22, ring_count=16, radius=1, location=(0, 0, 0))
        o = bpy.context.active_object; me = o.data
        for v in me.vertices:
            x, y, z = v.co
            k = 1.0 if z > -.1 else 1 - (-.1 - z) * .32          # narrower jaw
            fy = -.012 if (z < -.2 and y < 0) else 0             # chin slightly forward
            v.co = (x * .094 * k, y * .108 + fy, z * .122)
        o.location = (0, .004, 1.645); bpy.ops.object.transform_apply(location=True)
        _fin(o, skin, smooth=True)
        limb((0, -.1, 1.655), (0, -.128, 1.615), .014, .006, skin, sides=8)                               # nose bridge/tip
        sph(.016, 0, -.118, 1.618, skin, sx=1.3, sy=.9, sz=.8, seg=10, ring=6)                           # nose tip
        for sx in (-1, 1):
            sph(.015, .036 * sx, -.086, 1.664, eyew, seg=12, ring=8)                                      # eyeball
            sph(.0085, .036 * sx, -.099, 1.664, iris, sx=1, sy=.5, sz=1, seg=10, ring=6)                  # iris
            cbox(.036 * sx, -.1, 1.688, .038, .008, .007, hair, rot=(0, -.12 * sx, 0))                    # eyebrow
            sph(.03, .095 * sx, .005, 1.645, skin, sx=.35, sy=.75, sz=1.15, seg=10, ring=6)               # ear
        sph(.021, 0, -.103, 1.582, lips, sx=1.35, sy=.45, sz=.42, seg=12, ring=6)                         # lips
        # short hair cap
        bpy.ops.mesh.primitive_uv_sphere_add(segments=22, ring_count=16, radius=1, location=(0, 0, 0))
        hc = bpy.context.active_object; bm = bmesh.new(); bm.from_mesh(hc.data)
        for v in bm.verts:
            x, y, z = v.co; v.co = (x * .101, y * .116 + .01, z * .128)
        kill = [v for v in bm.verts if (v.co.z < -.02 and v.co.y < -.045) or v.co.z < -.075 or (v.co.z < .035 and v.co.y < -.07)]
        bmesh.ops.delete(bm, geom=kill, context='VERTS'); bm.to_mesh(hc.data); bm.free()
        hc.location = (0, .004, 1.655); bpy.ops.object.transform_apply(location=True)
        _fin(hc, hair, smooth=True)
    H = part('Head', (0, 0, 1.56), head)

    def glasses():
        g = mat('Col_1f2d3d', rough=.3)
        for x in (-.036, .036):
            bpy.ops.mesh.primitive_torus_add(major_radius=.021, minor_radius=.0035, major_segments=14, minor_segments=4, location=(x, -.108, 1.664), rotation=(math.pi / 2, 0, 0))
            _fin(bpy.context.active_object, g)
        box(-.015, .015, -.11, -.106, 1.664, 1.668, g)
        for x in (-1, 1):
            box(.056 * x - .002, .056 * x + .002, -.105, .01, 1.664, 1.668, g)
    part('Glasses', (0, 0, 1.56), glasses, parent=H)
    part('HairBun', (0, 0, 1.56), lambda: sph(.05, 0, .085, 1.74, hair, seg=12, ring=8), parent=H)

    def long_hair():
        sph(.1, 0, .045, 1.53, hair, sx=1.05, sy=.55, sz=1.35, seg=16, ring=10)
        for x in (-1, 1):
            sph(.05, .085 * x, -.01, 1.56, hair, sx=.55, sy=.9, sz=1.9, seg=10, ring=8)
    part('HairLong', (0, 0, 1.56), long_hair, parent=H)

    for side, sx in (('L', 1), ('R', -1)):
        def arm(sx=sx):
            sph(.047, .192 * sx, 0, 1.395, shirt, seg=12, ring=8)                                          # shoulder
            limb((.205 * sx, 0, 1.4), (.225 * sx, .005, 1.13), .05, .044, shirt)                          # upper arm
            limb((.225 * sx, .005, 1.13), (.232 * sx, -.02, .88), .043, .036, shirt)                     # forearm (sleeve)
            limb((.232 * sx, -.02, .885), (.233 * sx, -.022, .86), .031, .029, skin)                     # wrist
            cbox(.234 * sx, -.022, .8, .03, .052, .1, skin, bev=.012)                                     # hand
            limb((.225 * sx, -.045, .85), (.22 * sx, -.055, .8), .011, .009, skin, sides=6)              # thumb
        part('Arm' + side, (.205 * sx, 0, 1.4), arm)

        def leg(sx=sx):
            limb((.095 * sx, 0, .9), (.1 * sx, -.005, .49), .085, .058, pants)                           # thigh
            limb((.1 * sx, -.005, .49), (.1 * sx, .005, .1), .057, .043, pants)                          # shin
            cbox(.1 * sx, -.035, .05, .1, .27, .1, shoe, bev=.03)                                          # shoe
            cbox(.1 * sx, -.035, .004, .105, .28, .008, mat('Col_e8e4da'), bev=.002)                        # sole
        part('Leg' + side, (.095 * sx, 0, .9), leg)
    return root

# ================================================================== BUILD ALL
builders = [('home', b_home), ('mouse', b_mouse), ('keys', b_keys), ('office', b_office), ('web', b_web),
            ('mail', b_mail), ('android', b_android), ('iphone', b_iphone), ('security', b_security), ('final', b_final)]
DIMS = {}
for cid, fn in builders:
    start()
    DIMS[cid] = fn()
    finish('B_' + cid)

p_tree('P_tree_round', 'round')
p_tree('P_tree_pine', 'pine')
p_tree('P_bush', 'bush')
p_lamp(); p_bench(); p_car(); p_fountain(); p_small()
c_person()

# report
tv = 0
for o in bpy.data.objects:
    if o.type == 'MESH':
        tv += len(o.data.vertices)
print('ASSETS', sorted(o.name for o in bpy.data.objects if o.parent is None), 'verts', tv)
print('DIMS', DIMS)

bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', use_selection=False, export_apply=True, export_yup=True)
print('WROTE', OUT, os.path.getsize(OUT))
