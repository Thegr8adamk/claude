"""Walt, Pip, Hazel and Bramble. Each build_* returns the character's root empty.

All characters stand on z=0 and face -Y. Faces are laid out by ray-casting the
sculpted head (see lib.surf), so eyes, noses and mouths sit on the surface.
"""
import math

from mathutils import Vector

from lib import (B, CH, CURVE, E, add_fur, box, bvh_of, cone, empty, eye, eyelid, fur, hair_mat,
                 meta, mirror, paint_blush, plaid, principled, remeta, rod, skin, smile_arc,
                 sphere, surf, torus)


def _eye_on(bvh, name, x, z, r, mat, parent, pop=0.38, look=(0, 0, 0)):
    """Place an eye so `pop` of its radius sticks out of the surface at (x, z)."""
    p = surf(bvh, x, z)
    c = (p.x, p.y + r * (1 - pop), p.z)
    return sphere(name, c, r, mat, parent, rot=look), c


def _mouth(bvh, body_elems, cx, cz, half_w, lift, depth, r, m_in, m_teeth, parent, name,
           tongue=None, teeth=True):
    """Carve a smile into the face and fill it with a mouth interior, teeth and tongue."""
    arc = smile_arc(bvh, cx, cz, half_w, lift)
    carve = [B((p.x, p.y - r * 0.35, p.z), r * (0.65 + 0.35 * math.cos(math.pi * (i / (len(arc) - 1) - 0.5))),
               neg=True) for i, p in enumerate(arc)]
    inner = [B((p.x * 0.9, p.y + depth, p.z), r * 0.9) for p in arc]
    meta(name + "_MouthIn", inner, m_in, parent=parent, res=r / 6)
    mid = arc[len(arc) // 2]
    if teeth:
        top = [B((p.x * 0.85, p.y + depth * 0.45, p.z + r * 0.55), r * 0.42) for p in arc[1:-1]]
        meta(name + "_Teeth", top, m_teeth, parent=parent, res=r / 8)
    if tongue:
        meta(name + "_Tongue", [E((mid.x, mid.y + depth * 0.5, mid.z - r * 0.35), half_w * 0.5,
                                  r * 0.6, r * 0.45)], tongue, parent=parent, res=r / 8)
    return carve


# ======================================================================= WALT

def build_walt(loc=(0, 0, 0), use_fur=False):
    root = empty("Walt", loc)
    tone = (0.86, 0.56, 0.4)
    m_skin = skin("Walt_Skin", tone)
    m_shirt = plaid("Walt_Flannel")
    m_shorts = principled("Walt_Shorts", (0.5, 0.4, 0.25), 0.85, sheen=0.5, bump=(250, 0.1))
    m_belt = principled("Walt_Belt", (0.18, 0.09, 0.04), 0.4, coat=0.3)
    m_buckle = principled("Walt_Buckle", (0.8, 0.65, 0.3), 0.25, metal=1.0)
    m_boot = principled("Walt_Boots", (0.24, 0.12, 0.05), 0.45, coat=0.2, bump=(120, 0.08))
    m_sock = principled("Walt_Sock", (0.9, 0.87, 0.8), 0.9, sheen=0.6, bump=(300, 0.2))
    m_hat = principled("Walt_Hat", (0.6, 0.53, 0.36), 0.9, sheen=0.6, bump=(300, 0.1))
    m_band = principled("Walt_HatBand", (0.3, 0.18, 0.08), 0.7)
    m_pack = principled("Walt_Pack", (0.92, 0.36, 0.05), 0.6, sheen=0.3, bump=(200, 0.06))
    m_packdk = principled("Walt_PackDark", (0.35, 0.13, 0.03), 0.6)
    m_roll = principled("Walt_Bedroll", (0.12, 0.3, 0.17), 0.8, sheen=0.4, bump=(150, 0.1))
    m_gray = fur("Walt_GrayHair", (0.66, 0.65, 0.62), (0.88, 0.87, 0.84), scale=220, strength=0.6)
    m_frame = principled("Walt_Glasses", (0.14, 0.07, 0.03), 0.3, coat=0.5)
    m_lens = principled("Walt_Lens", (1, 1, 1), 0.0)
    m_lens.node_tree.nodes["Principled BSDF"].inputs["Transmission Weight"].default_value = 1.0
    m_eye = eye("Walt_Eye", iris=(0.3, 0.5, 0.22), iris_size=0.7, pupil_size=0.32)
    m_mouth = principled("Walt_Mouth", (0.18, 0.03, 0.03), 0.5)
    m_teeth = principled("Walt_Teeth", (0.95, 0.92, 0.85), 0.25, coat=0.5, sss=0.2)
    m_tongue = principled("Walt_Tongue", (0.75, 0.25, 0.25), 0.35, sss=0.4)

    HC = Vector((0, -0.02, 1.38))  # head center
    body_elems = [
        E(tuple(HC), 0.25, 0.24, 0.27),                                          # big head
        mirror([B((0.125, -0.12, 1.27), 0.12)]),                                 # jowls
        mirror([B((0.13, -0.16, 1.36), 0.085)]),                                 # apple cheeks
        B((0, -0.15, 1.21), 0.1),                                                # chin
        mirror([E((0.25, 0.0, 1.37), 0.035, 0.06, 0.085, rot=(0, -10, 0))]),    # ears
        B((0, 0.0, 1.13), 0.12),                                                 # neck
        mirror(CH((0.425, -0.03, 0.87), (0.45, -0.07, 0.69), 0.064, 0.05)),      # forearms
        mirror([E((0.455, -0.09, 0.64), 0.042, 0.05, 0.055)]),                   # palms
        mirror(CH((0.44, -0.125, 0.66), (0.43, -0.155, 0.61), 0.02, 0.016)),     # thumbs
        mirror(CH((0.14, 0.0, 0.45), (0.14, 0.0, 0.16), 0.072, 0.055)),          # shins
        mirror([B((0.14, 0.025, 0.37), 0.074)]),                                 # calves
    ]
    for i, fy in enumerate((-0.125, -0.1, -0.075, -0.05)):                        # fingers
        ln = 0.06 if i in (1, 2) else 0.05
        body_elems.append(mirror(CH((0.46, fy, 0.6), (0.455, fy - 0.005, 0.6 - ln), 0.016, 0.013)))
    body = meta("Walt_Body", body_elems, m_skin, parent=root)
    bvh = bvh_of(body)
    carve = _mouth(bvh, body_elems, 0.0, 1.222, 0.08, 0.04, 0.03, 0.024, m_mouth, m_teeth, root,
                   "Walt", tongue=m_tongue)
    body = remeta(body, body_elems + carve, m_skin)
    bvh = bvh_of(body)
    paint_blush(body, [((0.13, -0.22, 1.33), 0.08, 0.7), ((-0.13, -0.22, 1.33), 0.08, 0.7),
                       ((0.26, 0.0, 1.37), 0.07, 0.6), ((-0.26, 0.0, 1.37), 0.07, 0.6)])

    nose_p = surf(bvh, 0, 1.33)
    nose = sphere("Walt_Nose", (0, nose_p.y - 0.035, 1.335), 0.078, m_skin, root, scale=(1, 0.88, 0.9))
    nose.data.attributes.new("blush", "FLOAT", "POINT").data.foreach_set(
        "value", [0.55] * len(nose.data.vertices))

    for s in (1, -1):
        e, c = _eye_on(bvh, f"Walt_Eye{s}", 0.095 * s, 1.435, 0.06, m_eye, root, pop=0.4)
        eyelid(f"Walt_Lid{s}", c, 0.06, m_skin, root, open_amount=0.82, tilt=0.15)
        rim_c = (c[0], c[1] - 0.085, c[2] - 0.005)
        torus(f"Walt_Rim{s}", rim_c, 0.075, 0.006, m_frame, parent=root)
        sphere(f"Walt_Lens{s}", rim_c, 0.074, m_lens, root, scale=(1, 0.06, 1))
        rod(f"Walt_Arm{s}", (rim_c[0] + 0.075 * s, rim_c[1], rim_c[2]), (0.25 * s, -0.02, 1.42),
            0.0045, m_frame, root)
        # Bushy brows, friendly angle (inner ends slightly raised).
        bp = surf(bvh, 0.1 * s, 1.535)
        brow = meta(f"Walt_Brow{s}", CURVE([(0.04 * s, bp.y - 0.01, 1.535), (0.1 * s, bp.y - 0.015, 1.545),
                                            (0.155 * s, bp.y + 0.01, 1.525)], [0.022, 0.028, 0.02],
                                           per_seg=5), m_gray, parent=root, res=0.005)
        if use_fur:
            add_fur(brow, hair_mat("Walt_BrowHair", (0.75, 0.74, 0.7)), count=500, length=0.022,
                    children=6, radius=0.0006, clump=0.4, rough=0.004)
    rod("Walt_Bridge", (-0.03, rim_c[1] - 0.005, rim_c[2] + 0.01), (0.03, rim_c[1] - 0.005, rim_c[2] + 0.01),
        0.0045, m_frame, root)
    # Walrus mustache sitting on the upper lip.
    mp = [surf(bvh, x, 1.275) for x in (0.0, 0.05, 0.1, 0.135)]
    must = meta("Walt_Mustache", [
        B((0, mp[0].y - 0.035, 1.28), 0.042),
        mirror([B((0.05, mp[1].y - 0.03, 1.278), 0.042), B((0.1, mp[2].y - 0.02, 1.272), 0.034),
                B((0.14, mp[3].y - 0.008, 1.278), 0.024), B((0.163, mp[3].y + 0.005, 1.298), 0.016)]),
    ], m_gray, parent=root, res=0.006)
    side = meta("Walt_SideHair", mirror([E((0.23, 0.05, 1.47), 0.04, 0.12, 0.06),
                                         E((0.18, 0.16, 1.43), 0.05, 0.08, 0.07)]) +
                [E((0, 0.2, 1.42), 0.14, 0.06, 0.08)], m_gray, parent=root, res=0.007)
    if use_fur:
        m_h = hair_mat("Walt_Hair", (0.78, 0.77, 0.73))
        add_fur(must, m_h, count=1500, length=0.03, children=8, radius=0.0007, clump=0.5,
                rough=0.004)
        add_fur(side, m_h, count=1500, length=0.03, children=8, radius=0.0007, clump=0.5,
                rough=0.006)

    meta("Walt_Shirt", [
        E((0, -0.05, 0.85), 0.36, 0.34, 0.34),
        E((0, 0.0, 1.03), 0.32, 0.24, 0.16),
        mirror([B((0.29, 0, 1.07), 0.115)]),
        mirror(CH((0.32, 0, 1.07), (0.415, -0.02, 0.89), 0.095, 0.08)),
        mirror([E((0.42, -0.025, 0.875), 0.085, 0.085, 0.04, rot=(0, -12, 0))]),  # rolled cuff
        mirror([E((0.09, -0.13, 1.16), 0.085, 0.035, 0.05, rot=(0, 25, -25))]),  # collar points
    ], m_shirt, parent=root)
    for i, z in enumerate((1.08, 0.97, 0.86, 0.75)):
        y = -0.3 if z > 1.0 else -0.4 + abs(z - 0.86) * 0.35
        sphere(f"Walt_Button{i}", (0, y, z), 0.014, m_band, root, scale=(1, 0.4, 1))

    meta("Walt_Shorts", [
        E((0, -0.01, 0.62), 0.31, 0.27, 0.15),
        mirror(CH((0.14, 0, 0.6), (0.145, -0.005, 0.42), 0.125, 0.105)),
        mirror([E((0.3, -0.01, 0.53), 0.03, 0.08, 0.06)]),
    ], m_shorts, parent=root)
    torus("Walt_Belt", (0, -0.01, 0.7), 0.3, 0.025, m_belt, rot=(0, 0, 0), parent=root).scale = (1, 0.9, 1)
    box("Walt_Buckle", (0, -0.3, 0.7), (0.07, 0.02, 0.05), m_buckle, parent=root)

    meta("Walt_Boots", [mirror([E((0.14, -0.05, 0.08), 0.085, 0.15, 0.075),
                                *CH((0.14, 0, 0.08), (0.14, 0, 0.2), 0.082, 0.078)])],
         m_boot, parent=root)
    for s in (1, -1):
        torus(f"Walt_Sock{s}", (0.14 * s, 0, 0.215), 0.068, 0.022, m_sock, rot=(0, 0, 0), parent=root)

    meta("Walt_Hat", [
        E((0, -0.01, 1.575), 0.34, 0.34, 0.02, rot=(-7, 0, 0)),
        E((0, 0.005, 1.645), 0.245, 0.245, 0.1),
    ], m_hat, parent=root, res=0.008)
    torus("Walt_HatBand", (0, -0.002, 1.6), 0.245, 0.018, m_band, rot=(-5, 0, 0), parent=root)

    meta("Walt_Pack", [E((0, 0.32, 0.98), 0.27, 0.15, 0.32),
                       E((0, 0.45, 0.88), 0.18, 0.06, 0.15)], m_pack, parent=root)
    meta("Walt_Bedroll", CH((-0.26, 0.33, 1.33), (0.26, 0.33, 1.33), 0.085, 0.085), m_roll, parent=root)
    for s in (1, -1):
        meta(f"Walt_Strap{s}", CURVE([(0.17 * s, 0.22, 1.2), (0.18 * s, 0.05, 1.23),
                                      (0.19 * s, -0.12, 1.17)], [0.03] * 3, per_seg=8),
             m_packdk, parent=root, res=0.006)
    return root


# ======================================================================= PIP

def build_pip(loc=(0, 0, 0), use_fur=False):
    root = empty("Pip", loc)
    orange = (0.66, 0.22, 0.05)
    m_fur = fur("Pip_Fur", orange, (0.88, 0.42, 0.12), scale=260, strength=0.5)
    m_belly = fur("Pip_Belly", (0.93, 0.8, 0.62), (1.0, 0.93, 0.82), scale=260, strength=0.4)
    m_eye = eye("Pip_Eye", iris=(0.28, 0.14, 0.05), iris_size=0.82, pupil_size=0.46)
    m_nose = principled("Pip_Nose", (0.1, 0.03, 0.035), 0.25, coat=0.6)
    m_tooth = principled("Pip_Tooth", (0.97, 0.92, 0.8), 0.25, coat=0.5)
    m_inner = principled("Pip_EarInner", (0.85, 0.48, 0.42), 0.6, sss=0.4)
    m_mouth = principled("Pip_Mouth", (0.2, 0.03, 0.04), 0.5)
    m_tongue = principled("Pip_Tongue", (0.85, 0.35, 0.38), 0.35, sss=0.4)

    body_elems = [
        E((0, 0.01, 0.12), 0.068, 0.064, 0.095),                        # small pear body
        B((0, -0.005, 0.275), 0.1),                                     # BIG head
        mirror([B((0.06, -0.05, 0.245), 0.05)]),                        # puffy cheeks
        B((0, -0.085, 0.245), 0.04),                                    # snout
        mirror([E((0.058, 0.0, 0.385), 0.024, 0.013, 0.042, rot=(0, -18, 0))]),  # ears
        mirror(CH((0.052, -0.03, 0.165), (0.035, -0.08, 0.13), 0.018, 0.014)),   # arms
        mirror([B((0.032, -0.09, 0.127), 0.017)]),                      # paws
        mirror([E((0.055, -0.005, 0.05), 0.042, 0.058, 0.047)]),        # haunches
        mirror([E((0.05, -0.058, 0.011), 0.022, 0.045, 0.012)]),        # feet
    ]
    body = meta("Pip_Body", body_elems, m_fur, parent=root, res=0.004)
    bvh = bvh_of(body)
    carve = _mouth(bvh, body_elems, 0.0, 0.222, 0.026, 0.01, 0.02, 0.011, m_mouth, m_tooth, root,
                   "Pip", teeth=False)
    body = remeta(body, body_elems + carve, m_fur, res=0.004)
    bvh = bvh_of(body)
    meta("Pip_Muzzle", mirror([B((0.022, surf(bvh, 0.022, 0.25).y + 0.012, 0.25), 0.024)]),
         m_belly, parent=root, res=0.003)
    meta("Pip_BellyPatch", [E((0, -0.048, 0.13), 0.048, 0.03, 0.075)], m_belly, parent=root, res=0.004)
    tail = meta("Pip_Tail", CURVE(
        [(0, 0.06, 0.05), (0, 0.15, 0.1), (0, 0.2, 0.23), (0, 0.19, 0.37), (0, 0.13, 0.46),
         (0, 0.07, 0.48)], [0.03, 0.05, 0.07, 0.08, 0.07, 0.045], per_seg=6),
        m_fur, parent=root, res=0.006)
    for s in (1, -1):
        e, c = _eye_on(bvh, f"Pip_Eye{s}", 0.047 * s, 0.295, 0.042, m_eye, root, pop=0.42,
                       look=(0, 0, 12 * s))
        eyelid(f"Pip_Lid{s}", c, 0.042, m_fur, root, open_amount=0.85, tilt=0.1)
        meta(f"Pip_EarIn{s}", [E((0.058 * s, -0.01, 0.387), 0.014, 0.005, 0.03, rot=(0, 18 * s, 0))],
             m_inner, parent=root, res=0.003)
        meta(f"Pip_Brow{s}", [E((0.05 * s, surf(bvh, 0.05 * s, 0.345).y - 0.004, 0.345), 0.018, 0.006, 0.006,
                                rot=(0, -10 * s, 0))], principled("Pip_BrowC", (0.4, 0.12, 0.03), 0.7),
             parent=root, res=0.002)
    np_ = surf(bvh, 0, 0.262)
    sphere("Pip_Nose", (0, np_.y + 0.002, 0.262), 0.013, m_nose, root, scale=(1.25, 0.8, 0.9))
    tp = surf(bvh, 0, 0.228)
    box("Pip_ToothL", (-0.0065, tp.y + 0.002, 0.214), (0.011, 0.005, 0.02), m_tooth, parent=root)
    box("Pip_ToothR", (0.0065, tp.y + 0.002, 0.218), (0.011, 0.005, 0.012), m_tooth, parent=root)  # chipped
    if use_fur:
        m_h = hair_mat("Pip_Hair", (0.75, 0.28, 0.07))
        add_fur(tail, m_h, count=5000, length=0.07, children=24, radius=0.0007, clump=0.5,
                rough=0.01)
        add_fur(body, m_h, count=8000, length=0.012, children=10, radius=0.0004, clump=0.25,
                rough=0.002)
    return root


# ======================================================================= HAZEL

def build_hazel(loc=(0, 0, 0), use_fur=False):
    root = empty("Hazel", loc)
    m_back = fur("Hazel_Feathers", (0.72, 0.42, 0.14), (0.9, 0.62, 0.3), scale=120, strength=0.25)
    m_face = fur("Hazel_Face", (0.95, 0.91, 0.84), (1.0, 0.98, 0.94), scale=160, strength=0.2)
    m_rim = fur("Hazel_Rim", (0.55, 0.32, 0.12), (0.72, 0.48, 0.24), scale=160, strength=0.2)
    m_wing = fur("Hazel_Wing", (0.55, 0.36, 0.17), (0.78, 0.6, 0.38), scale=90, strength=0.3)
    m_eye = eye("Hazel_Eye", iris=(0.12, 0.06, 0.03), iris_size=0.84, pupil_size=0.5)
    m_beak = principled("Hazel_Beak", (0.88, 0.78, 0.62), 0.35, coat=0.4)
    m_foot = principled("Hazel_Foot", (0.55, 0.5, 0.45), 0.6)
    m_claw = principled("Hazel_Claw", (0.08, 0.07, 0.07), 0.3, coat=0.5)
    m_spec = principled("Hazel_Specs", (0.25, 0.2, 0.18), 0.5, coat=0.3)

    body = meta("Hazel_Body", [
        E((0, 0.01, 0.22), 0.16, 0.15, 0.2),
        B((0, 0.0, 0.43), 0.165),                                       # big round head
        mirror([E((0.1, 0.01, 0.57), 0.05, 0.05, 0.03, rot=(0, -25, 0))]),
        mirror([E((0.05, 0.0, 0.05), 0.04, 0.05, 0.05)]),
    ], m_back, parent=root, res=0.006)
    meta("Hazel_Chest", [E((0, -0.07, 0.21), 0.125, 0.1, 0.16)], m_face, parent=root, res=0.006)
    meta("Hazel_FaceRim", mirror([E((0.062, -0.115, 0.45), 0.1, 0.035, 0.125, rot=(0, -22, 0))]),
         m_rim, parent=root, res=0.004)
    face = meta("Hazel_Face", mirror([E((0.06, -0.125, 0.45), 0.09, 0.035, 0.115, rot=(0, -22, 0))]),
                m_face, parent=root, res=0.004)
    bvh = bvh_of(face)
    for s in (1, -1):
        e, c = _eye_on(bvh, f"Hazel_Eye{s}", 0.062 * s, 0.455, 0.045, m_eye, root, pop=0.45)
        eyelid(f"Hazel_Lid{s}", c, 0.045, m_face, root, open_amount=0.2, tilt=0.15)
        # Little reading spectacles perched low on the eyes: very Hazel.
        torus(f"Hazel_Spec{s}", (c[0], c[1] - 0.04, c[2] - 0.012), 0.036, 0.0035, m_spec, parent=root)
        meta(f"Hazel_Brow{s}", [E((0.065 * s, c[1] - 0.02, 0.51), 0.045, 0.015, 0.012, rot=(0, 15 * s, 0)),
                                B((0.105 * s, c[1] - 0.01, 0.525), 0.014)], m_rim, parent=root, res=0.003)
        meta(f"Hazel_Wing{s}", [E((0.155 * s, 0.03, 0.25), 0.045, 0.13, 0.19, rot=(8, 0, 10 * s)),
                                E((0.145 * s, 0.1, 0.09), 0.03, 0.06, 0.1, rot=(20, 0, 5 * s))],
             m_wing, parent=root, res=0.005)
        for k, a in enumerate((-20, 0, 20)):
            dx = math.sin(math.radians(a)) * 0.03
            dy = -math.cos(math.radians(a)) * 0.03
            rod(f"Hazel_Toe{s}{k}", (0.05 * s, -0.02, 0.012), (0.05 * s + dx, -0.035 + dy, 0.008),
                0.007, m_foot, root)
            cone(f"Hazel_Claw{s}{k}", (0.05 * s + dx * 1.6, -0.04 + dy * 1.6, 0.006), 0.006,
                 0.016, m_claw, rot=(95, 0, a), parent=root)
    rod("Hazel_SpecBridge", (-0.026, c[1] - 0.042, c[2] - 0.005), (0.026, c[1] - 0.042, c[2] - 0.005),
        0.003, m_spec, root)
    bp = surf(bvh, 0, 0.41)
    cone("Hazel_Beak", (0, bp.y - 0.012, 0.41), 0.017, 0.05, m_beak, rot=(160, 0, 0), parent=root)
    if use_fur:
        add_fur(body, hair_mat("Hazel_Down", (0.82, 0.55, 0.25)), count=6000, length=0.012,
                children=8, radius=0.0006, clump=0.3, rough=0.003)
    return root


# ======================================================================= BRAMBLE

def build_bramble(loc=(0, 0, 0), use_fur=False):
    root = empty("Bramble", loc)
    m_fur = fur("Bramble_Fur", (0.035, 0.028, 0.024), (0.07, 0.055, 0.045), scale=70, strength=0.5)
    m_muzzle = fur("Bramble_Muzzle", (0.48, 0.32, 0.19), (0.66, 0.5, 0.33), scale=160, strength=0.3)
    m_chest = fur("Bramble_Chest", (0.87, 0.77, 0.6), (0.96, 0.9, 0.75), scale=120, strength=0.3)
    m_nose = principled("Bramble_Nose", (0.02, 0.018, 0.018), 0.2, coat=0.8)
    m_pad = principled("Bramble_Pad", (0.14, 0.09, 0.08), 0.6, bump=(80, 0.3))
    m_eye = eye("Bramble_Eye", iris=(0.45, 0.24, 0.08), iris_size=0.74, pupil_size=0.34)
    m_claw = principled("Bramble_Claw", (0.8, 0.75, 0.65), 0.3, coat=0.4)
    m_mouth = principled("Bramble_Mouth", (0.15, 0.03, 0.03), 0.5)
    m_teeth = principled("Bramble_Teeth", (0.95, 0.92, 0.84), 0.25, coat=0.5)
    m_tongue = principled("Bramble_Tongue", (0.8, 0.3, 0.32), 0.35, sss=0.4)

    body = meta("Bramble_Body", [
        mirror([E((0.26, 0.0, 0.28), 0.2, 0.22, 0.28)]),
        mirror([E((0.26, -0.1, 0.075), 0.15, 0.22, 0.08)]),
        E((0, -0.02, 0.9), 0.52, 0.46, 0.52),
        E((0, 0.02, 1.28), 0.42, 0.36, 0.28),
        B((0, -0.05, 1.68), 0.32),                                      # bigger head
        mirror([B((0.13, -0.2, 1.58), 0.12)]),                          # cheeks
        mirror([B((0.23, 0.0, 1.95), 0.095)]),                          # ears
        mirror(CH((0.42, 0.0, 1.32), (0.56, -0.12, 0.92), 0.145, 0.12)),
        mirror([B((0.57, -0.16, 0.84), 0.125)]),
    ], m_fur, parent=root, res=0.016)
    muz_elems = [E((0, -0.3, 1.6), 0.15, 0.13, 0.11), B((0, -0.28, 1.53), 0.08)]
    muz = meta("Bramble_Muzzle", muz_elems, m_muzzle, parent=root, res=0.006)
    mb = bvh_of(muz)
    carve = _mouth(mb, muz_elems, 0.0, 1.535, 0.075, 0.03, 0.035, 0.026, m_mouth, m_teeth, root,
                   "Bramble", tongue=m_tongue)
    muz = remeta(muz, muz_elems + carve, m_muzzle, res=0.006)
    mb = bvh_of(muz)
    np_ = surf(mb, 0, 1.64)
    sphere("Bramble_Nose", (0, np_.y + 0.005, 1.645), 0.06, m_nose, root, scale=(1, 0.65, 0.68))
    meta("Bramble_ChestMark", [E((0, -0.325, 1.22), 0.25, 0.05, 0.06),
                               mirror([E((0.19, -0.3, 1.26), 0.08, 0.05, 0.05, rot=(0, -30, 0))])],
         m_chest, parent=root, res=0.008)
    bvh = bvh_of(body)
    for s in (1, -1):
        e, c = _eye_on(bvh, f"Bramble_Eye{s}", 0.11 * s, 1.76, 0.055, m_eye, root, pop=0.4,
                       look=(0, 0, 8 * s))
        eyelid(f"Bramble_Lid{s}", c, 0.055, m_fur, root, open_amount=0.7, tilt=-0.3)
        # Worried-kind brows: inner ends raised.
        meta(f"Bramble_Brow{s}", [E((0.1 * s, c[1] - 0.03, 1.845), 0.045, 0.02, 0.015, rot=(0, 20 * s, 0))],
             m_muzzle, parent=root, res=0.004)
        meta(f"Bramble_EarIn{s}", [E((0.23 * s, -0.07, 1.95), 0.055, 0.02, 0.055)], m_muzzle,
             parent=root, res=0.005)
        meta(f"Bramble_Pad{s}", [E((0.58 * s, -0.27, 0.82), 0.06, 0.02, 0.055)], m_pad,
             parent=root, res=0.004)
        for k in range(4):
            cone(f"Bramble_ToeClaw{s}{k}", (0.26 * s + (k - 1.5) * 0.06, -0.295, 0.035), 0.014,
                 0.05, m_claw, rot=(100, 0, 0), parent=root)
    if use_fur:
        add_fur(body, hair_mat("Bramble_Hair", (0.03, 0.025, 0.022)), count=24000, length=0.05,
                children=8, radius=0.0022, clump=0.35, rough=0.012)
    return root
