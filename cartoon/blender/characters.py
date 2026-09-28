"""Walt, Pip, Hazel and Bramble. Each build_* returns the character's root empty.

All characters stand on z=0 and face -Y.
"""
from lib import (B, CH, CURVE, E, add_eye, add_fur, box, cone, empty, eye, eyelid, fur,
                 hair_mat, meta, mirror, plaid, principled, rod, skin, sphere, torus)


def build_walt(loc=(0, 0, 0), use_fur=False):
    root = empty("Walt", loc)
    m_skin = skin("Walt_Skin", (0.88, 0.56, 0.42))
    m_shirt = plaid("Walt_Flannel")
    m_shorts = principled("Walt_Shorts", (0.52, 0.42, 0.27), 0.85, sheen=0.5,
                          bump=(250, 0.1))
    m_boot = principled("Walt_Boots", (0.2, 0.1, 0.045), 0.45, coat=0.2, bump=(120, 0.08))
    m_sole = principled("Walt_Sole", (0.05, 0.04, 0.035), 0.7)
    m_hat = principled("Walt_Hat", (0.58, 0.52, 0.36), 0.9, sheen=0.6, bump=(300, 0.1))
    m_band = principled("Walt_HatBand", (0.25, 0.18, 0.1), 0.7)
    m_pack = principled("Walt_Pack", (0.9, 0.33, 0.04), 0.6, sheen=0.3, bump=(200, 0.06))
    m_packdk = principled("Walt_PackDark", (0.35, 0.12, 0.02), 0.6)
    m_roll = principled("Walt_Bedroll", (0.12, 0.28, 0.16), 0.8, sheen=0.4, bump=(150, 0.1))
    m_gray = fur("Walt_GrayHair", (0.62, 0.61, 0.58), (0.85, 0.84, 0.8), scale=220, strength=0.6)
    m_frame = principled("Walt_Glasses", (0.12, 0.06, 0.03), 0.3, coat=0.5)
    m_lens = principled("Walt_Lens", (1, 1, 1), 0.0)
    m_lens.node_tree.nodes["Principled BSDF"].inputs["Transmission Weight"].default_value = 1.0
    m_eye = eye("Walt_Eye", iris=(0.28, 0.45, 0.2), iris_size=0.66, pupil_size=0.3)
    m_mouth = principled("Walt_Mouth", (0.25, 0.04, 0.04), 0.4)

    body = meta("Walt_Body", [
        E((0, -0.02, 1.47), 0.205, 0.2, 0.225),
        mirror([B((0.1, -0.1, 1.39), 0.1), E((0.205, 0.0, 1.47), 0.03, 0.05, 0.07)]),
        B((0, -0.12, 1.34), 0.09),
        B((0, 0.0, 1.3), 0.11),
        mirror(CH((0.445, -0.03, 1.0), (0.47, -0.06, 0.8), 0.068, 0.055)),
        mirror([E((0.475, -0.075, 0.735), 0.05, 0.062, 0.072), B((0.44, -0.12, 0.77), 0.03)]),
        mirror(CH((0.14, 0, 0.55), (0.14, 0, 0.16), 0.078, 0.062)),
    ], m_skin, parent=root)
    sphere("Walt_Nose", (0, -0.215, 1.465), 0.068, m_skin, root, scale=(1, 0.9, 0.92))

    meta("Walt_Shirt", [
        E((0, -0.05, 0.95), 0.36, 0.34, 0.36),
        E((0, 0.0, 1.16), 0.33, 0.25, 0.17),
        mirror([B((0.3, 0, 1.2), 0.12)]),
        mirror(CH((0.33, 0, 1.2), (0.43, -0.02, 1.02), 0.1, 0.085)),
        mirror([B((0.438, -0.025, 1.0), 0.085)]),                   # rolled cuff
        mirror([E((0.1, -0.12, 1.29), 0.09, 0.04, 0.05, rot=(0, 20, -25))]),  # collar
    ], m_shirt, parent=root)
    # Buttons down the belly.
    for i, z in enumerate((1.17, 1.05, 0.93, 0.81)):
        y = -0.25 if z > 1.1 else -0.395 + abs(z - 0.95) * 0.35
        sphere(f"Walt_Button{i}", (0, y, z), 0.014, m_band, root, scale=(1, 0.4, 1))

    meta("Walt_Shorts", [
        E((0, -0.01, 0.72), 0.31, 0.27, 0.17),
        mirror(CH((0.14, 0, 0.7), (0.145, -0.005, 0.5), 0.125, 0.105)),
        mirror([E((0.3, -0.01, 0.62), 0.03, 0.08, 0.06)]),          # side pockets
    ], m_shorts, parent=root)

    meta("Walt_Boots", [
        mirror([E((0.14, -0.05, 0.08), 0.085, 0.15, 0.075),
                *CH((0.14, 0, 0.08), (0.14, 0, 0.2), 0.082, 0.078)]),
    ], m_boot, parent=root)
    for s in (1, -1):
        torus(f"Walt_Sock{s}", (0.14 * s, 0, 0.22), 0.07, 0.02,
              principled("Walt_Sock", (0.9, 0.88, 0.82), 0.9, sheen=0.5), rot=(0, 0, 0),
              parent=root)

    meta("Walt_Hat", [
        E((0, -0.005, 1.625), 0.29, 0.29, 0.018, rot=(-6, 0, 0)),
        E((0, 0.005, 1.69), 0.2, 0.2, 0.085),
    ], m_hat, parent=root, res=0.008)
    torus("Walt_HatBand", (0, 0.0, 1.648), 0.2, 0.017, m_band, rot=(-4, 0, 0), parent=root)

    # Face: eyes, lids, brows, glasses, mustache.
    for s in (1, -1):
        add_eye(f"Walt_Eye{s}", (0.072 * s, -0.172, 1.535), 0.043, m_eye, root)
        eyelid(f"Walt_Lid{s}", (0.072 * s, -0.172, 1.535), 0.043, m_skin, root,
               open_amount=0.55, tilt=0.25)
        meta(f"Walt_Brow{s}", [E((0.08 * s, -0.19, 1.61), 0.055, 0.03, 0.022, rot=(0, 14 * s, 0))],
             m_gray, parent=root, res=0.005)
        torus(f"Walt_Rim{s}", (0.074 * s, -0.228, 1.532), 0.058, 0.0055, m_frame, parent=root)
        sphere(f"Walt_Lens{s}", (0.074 * s, -0.228, 1.532), 0.057, m_lens, root, scale=(1, 0.08, 1))
        rod(f"Walt_Arm{s}", (0.132 * s, -0.228, 1.535), (0.2 * s, -0.02, 1.52), 0.004, m_frame, root)
    rod("Walt_Bridge", (-0.018, -0.235, 1.54), (0.018, -0.235, 1.54), 0.004, m_frame, root)
    meta("Walt_Mustache", [
        mirror([B((0.045, -0.235, 1.412), 0.045), B((0.1, -0.205, 1.39), 0.04),
                B((0.135, -0.17, 1.365), 0.03)]),
        B((0, -0.24, 1.415), 0.04),
    ], m_gray, parent=root, res=0.006)
    meta("Walt_Mouth", [E((0, -0.205, 1.36), 0.06, 0.02, 0.018)], m_mouth, parent=root, res=0.005)
    meta("Walt_SideHair", mirror([E((0.19, 0.03, 1.55), 0.035, 0.1, 0.05)]), m_gray,
         parent=root, res=0.006)

    # Backpack with bedroll and straps.
    meta("Walt_Pack", [E((0, 0.32, 1.08), 0.27, 0.15, 0.32),
                       E((0, 0.45, 0.98), 0.18, 0.06, 0.15)], m_pack, parent=root)
    meta("Walt_Bedroll", CH((-0.26, 0.33, 1.43), (0.26, 0.33, 1.43), 0.085, 0.085),
         m_roll, parent=root)
    for s in (1, -1):
        meta(f"Walt_Strap{s}", CURVE([(0.17 * s, 0.22, 1.3), (0.18 * s, 0.05, 1.335),
                                      (0.19 * s, -0.12, 1.27)],
                                     [0.03] * 3, per_seg=8), m_packdk, parent=root, res=0.006)
    return root


def build_pip(loc=(0, 0, 0), use_fur=False):
    root = empty("Pip", loc)
    orange = (0.62, 0.2, 0.05)
    m_fur = fur("Pip_Fur", orange, (0.85, 0.38, 0.12), scale=260, strength=0.5)
    m_belly = fur("Pip_Belly", (0.9, 0.78, 0.6), (1.0, 0.92, 0.8), scale=260, strength=0.4)
    m_eye = eye("Pip_Eye", iris=(0.2, 0.1, 0.04), iris_size=0.8, pupil_size=0.45)
    m_nose = principled("Pip_Nose", (0.08, 0.03, 0.03), 0.25, coat=0.6)
    m_tooth = principled("Pip_Tooth", (0.95, 0.9, 0.78), 0.3, coat=0.5)
    m_inner = principled("Pip_EarInner", (0.8, 0.45, 0.4), 0.6, sss=0.4)

    body = meta("Pip_Body", [
        E((0, 0.01, 0.13), 0.085, 0.08, 0.11),                      # pear body
        B((0, -0.01, 0.27), 0.088),                                 # head
        mirror([B((0.052, -0.06, 0.245), 0.046)]),                  # cheeks
        B((0, -0.088, 0.255), 0.036),                               # snout
        mirror([E((0.05, 0.0, 0.365), 0.022, 0.012, 0.038, rot=(0, -15, 0)),
                E((0.056, 0.0, 0.405), 0.012, 0.01, 0.02, rot=(0, -25, 0))]),  # ears + tufts
        mirror(CH((0.062, -0.035, 0.18), (0.04, -0.085, 0.14), 0.02, 0.015)),  # arms
        mirror([B((0.037, -0.095, 0.137), 0.017)]),                 # paws
        mirror([E((0.06, -0.01, 0.055), 0.045, 0.062, 0.05)]),      # haunches
        mirror([E((0.055, -0.06, 0.012), 0.024, 0.048, 0.013)]),    # feet
    ], m_fur, parent=root, res=0.004)
    meta("Pip_BellyPatch", [E((0, -0.058, 0.14), 0.058, 0.035, 0.085),
                            B((0, -0.09, 0.235), 0.03)], m_belly, parent=root, res=0.004)
    tail = meta("Pip_Tail", CURVE(
        [(0, 0.07, 0.06), (0, 0.16, 0.11), (0, 0.21, 0.24), (0, 0.19, 0.38), (0, 0.13, 0.46),
         (0, 0.08, 0.48)],
        [0.035, 0.055, 0.075, 0.085, 0.075, 0.05], per_seg=6), m_fur, parent=root, res=0.006)
    for s in (1, -1):
        add_eye(f"Pip_Eye{s}", (0.041 * s, -0.074, 0.292), 0.031, m_eye, root, look=(0, 0, 14 * s))
        meta(f"Pip_EarIn{s}", [E((0.05 * s, -0.009, 0.366), 0.013, 0.005, 0.026, rot=(0, 15 * s, 0))],
             m_inner, parent=root, res=0.003)
    sphere("Pip_Nose", (0, -0.123, 0.267), 0.012, m_nose, root, scale=(1.2, 0.8, 0.9))
    box("Pip_ToothL", (-0.0065, -0.117, 0.228), (0.011, 0.005, 0.02), m_tooth, parent=root)
    box("Pip_ToothR", (0.0065, -0.117, 0.232), (0.011, 0.005, 0.012), m_tooth, parent=root)  # chipped
    if use_fur:
        m_h = hair_mat("Pip_Hair", (0.72, 0.27, 0.07))
        add_fur(tail, m_h, count=4000, length=0.06, children=24, radius=0.0007, clump=0.5,
                rough=0.008)
        add_fur(body, m_h, count=6000, length=0.009, children=10, radius=0.0004, clump=0.2,
                rough=0.002)
    return root


def build_hazel(loc=(0, 0, 0), use_fur=False):
    root = empty("Hazel", loc)
    m_back = fur("Hazel_Feathers", (0.68, 0.4, 0.15), (0.82, 0.58, 0.3), scale=120, strength=0.25)
    m_face = fur("Hazel_Face", (0.93, 0.9, 0.84), (1.0, 0.97, 0.93), scale=160, strength=0.2)
    m_rim = fur("Hazel_Rim", (0.55, 0.36, 0.18), (0.7, 0.5, 0.3), scale=160, strength=0.2)
    m_wing = fur("Hazel_Wing", (0.5, 0.33, 0.17), (0.7, 0.55, 0.38), scale=90, strength=0.3)
    m_eye = eye("Hazel_Eye", iris=(0.06, 0.035, 0.02), iris_size=0.85, pupil_size=0.5)
    m_beak = principled("Hazel_Beak", (0.85, 0.75, 0.6), 0.35, coat=0.4)
    m_foot = principled("Hazel_Foot", (0.55, 0.5, 0.45), 0.6)
    m_claw = principled("Hazel_Claw", (0.08, 0.07, 0.07), 0.3, coat=0.5)

    meta("Hazel_Body", [
        E((0, 0.01, 0.23), 0.165, 0.155, 0.22),
        B((0, 0.0, 0.43), 0.15),
        mirror([E((0.09, 0.01, 0.56), 0.05, 0.05, 0.03, rot=(0, -25, 0))]),  # head corners
        mirror([E((0.05, 0.0, 0.05), 0.04, 0.05, 0.05)]),           # feathered legs
    ], m_back, parent=root, res=0.006)
    meta("Hazel_Chest", [E((0, -0.07, 0.22), 0.13, 0.1, 0.17)], m_face, parent=root, res=0.006)
    # Heart-shaped face disk with a darker rim.
    meta("Hazel_FaceRim", mirror([E((0.058, -0.105, 0.45), 0.092, 0.035, 0.115, rot=(0, -22, 0))]),
         m_rim, parent=root, res=0.004)
    meta("Hazel_Face", mirror([E((0.056, -0.115, 0.45), 0.083, 0.035, 0.105, rot=(0, -22, 0))]),
         m_face, parent=root, res=0.004)
    for s in (1, -1):
        add_eye(f"Hazel_Eye{s}", (0.055 * s, -0.135, 0.452), 0.034, m_eye, root)
        eyelid(f"Hazel_Lid{s}", (0.055 * s, -0.135, 0.452), 0.034, m_face, root,
               open_amount=0.1, tilt=0.2)
        meta(f"Hazel_Brow{s}", [E((0.06 * s, -0.15, 0.495), 0.04, 0.015, 0.012, rot=(0, 18 * s, 0)),
                                B((0.095 * s, -0.14, 0.51), 0.012)], m_rim, parent=root, res=0.003)
        meta(f"Hazel_Wing{s}", [E((0.16 * s, 0.03, 0.26), 0.045, 0.13, 0.19, rot=(8, 0, 10 * s)),
                                E((0.15 * s, 0.1, 0.1), 0.03, 0.06, 0.1, rot=(20, 0, 5 * s))],
             m_wing, parent=root, res=0.005)
        for k, a in enumerate((-20, 0, 20)):
            import math
            dx = math.sin(math.radians(a)) * 0.03
            dy = -math.cos(math.radians(a)) * 0.03
            rod(f"Hazel_Toe{s}{k}", (0.05 * s, -0.02, 0.012), (0.05 * s + dx, -0.02 + dy - 0.015, 0.008),
                0.007, m_foot, root)
            cone(f"Hazel_Claw{s}{k}", (0.05 * s + dx * 1.6, -0.02 + dy * 1.6 - 0.02, 0.006), 0.006,
                 0.016, m_claw, rot=(95, 0, a), parent=root)
    cone("Hazel_Beak", (0, -0.155, 0.415), 0.016, 0.05, m_beak, rot=(160, 0, 0), parent=root)
    return root


def build_bramble(loc=(0, 0, 0), use_fur=False):
    root = empty("Bramble", loc)
    m_fur = fur("Bramble_Fur", (0.03, 0.026, 0.024), (0.06, 0.05, 0.045), scale=70, strength=0.5)
    m_muzzle = fur("Bramble_Muzzle", (0.42, 0.28, 0.17), (0.6, 0.45, 0.3), scale=160, strength=0.3)
    m_chest = fur("Bramble_Chest", (0.85, 0.75, 0.58), (0.95, 0.88, 0.72), scale=120, strength=0.3)
    m_nose = principled("Bramble_Nose", (0.02, 0.018, 0.018), 0.2, coat=0.8)
    m_pad = principled("Bramble_Pad", (0.12, 0.08, 0.07), 0.6, bump=(80, 0.3))
    m_eye = eye("Bramble_Eye", iris=(0.35, 0.18, 0.06), iris_size=0.72, pupil_size=0.33)
    m_claw = principled("Bramble_Claw", (0.8, 0.75, 0.65), 0.3, coat=0.4)

    body = meta("Bramble_Body", [
        mirror([E((0.26, 0.0, 0.3), 0.2, 0.22, 0.3)]),              # legs
        mirror([E((0.26, -0.1, 0.075), 0.15, 0.22, 0.08)]),         # feet
        E((0, -0.02, 0.95), 0.52, 0.46, 0.55),                      # belly
        E((0, 0.02, 1.36), 0.42, 0.36, 0.3),                        # chest
        B((0, -0.05, 1.72), 0.27),                                  # head
        mirror([B((0.11, -0.2, 1.63), 0.1)]),                       # cheeks
        mirror([B((0.2, 0.0, 1.95), 0.085)]),                       # ears
        mirror(CH((0.42, 0.0, 1.4), (0.56, -0.12, 0.98), 0.145, 0.12)),  # arms
        mirror([B((0.57, -0.16, 0.9), 0.125)]),                     # paws
    ], m_fur, parent=root, res=0.016)
    meta("Bramble_Muzzle", [E((0, -0.28, 1.64), 0.13, 0.12, 0.095), B((0, -0.26, 1.58), 0.07)],
         m_muzzle, parent=root, res=0.006)
    meta("Bramble_ChestMark", [E((0, -0.325, 1.3), 0.25, 0.05, 0.06, rot=(0, 0, 0)),
                               mirror([E((0.19, -0.3, 1.34), 0.08, 0.05, 0.05, rot=(0, -30, 0))])],
         m_chest, parent=root, res=0.008)
    sphere("Bramble_Nose", (0, -0.395, 1.685), 0.055, m_nose, root, scale=(1, 0.6, 0.65))
    for s in (1, -1):
        add_eye(f"Bramble_Eye{s}", (0.1 * s, -0.262, 1.8), 0.042, m_eye, root, look=(0, 0, 10 * s))
        eyelid(f"Bramble_Lid{s}", (0.1 * s, -0.262, 1.8), 0.042, m_fur, root,
               open_amount=0.6, tilt=-0.35)
        meta(f"Bramble_EarIn{s}", [E((0.2 * s, -0.06, 1.95), 0.05, 0.02, 0.05)], m_muzzle,
             parent=root, res=0.005)
        meta(f"Bramble_Pad{s}", [E((0.58 * s, -0.27, 0.88), 0.06, 0.02, 0.055)], m_pad,
             parent=root, res=0.004)
        for k in range(4):
            x = (0.18 + k * 0.05) * s + (0.02 if s > 0 else -0.02) * 0
            cone(f"Bramble_ToeClaw{s}{k}", (0.26 * s + (k - 1.5) * 0.06, -0.295, 0.035), 0.014,
                 0.05, m_claw, rot=(100, 0, 0), parent=root)
    if use_fur:
        add_fur(body, hair_mat("Bramble_Hair", (0.028, 0.024, 0.022)), count=20000, length=0.045,
                children=8, radius=0.0022, clump=0.35, rough=0.01)
    return root
