"""Rex Magnum - keyframed animations for the Mixamo/Unity rig.

One Blender Action per animation. Poses are written with world-space directions
(character faces -Y, left side is +X), so they do not depend on bone roll.
All loops are in place (the Unity controller moves the character).

Third person uses the full-body actions. First person uses the same actions on the
separate arms model (the arm/hand bones are the same).
"""
import math

import bpy
from mathutils import Matrix, Vector

import rex_rig as R

FPS = 30
B = R.B
FWD = Vector((0, -1, 0))
UP = Vector((0, 0, 1))
LEFT = Vector((1, 0, 0))


def d(x, y, z):
    return Vector((x, y, z)).normalized()


def ang_dir(forward_deg, side=0.0, sx=1):
    """Direction pointing down, swung forward by forward_deg (negative = back)."""
    a = math.radians(forward_deg)
    return Vector((side * sx, -math.sin(a), -math.cos(a))).normalized()


class Poser:
    def __init__(self, arm):
        self.arm = arm
        for pb in arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
        self.reset()
        self.hips_rest = self.world("Hips")
        self.foot_rest = {s: self._foot_pitch(s) for s in ("Left", "Right")}

    def reset(self):
        for pb in self.arm.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)
        bpy.context.view_layer.update()

    def world(self, bone, tail=False):
        return R.bone_world(self.arm, bone, tail)

    def _foot_pitch(self, side):
        h, t = self.world(side + "Foot"), self.world(side + "Foot", True)
        v = (t - h).normalized()
        return math.degrees(math.asin(max(-1, min(1, -v.z))))

    # ---- body parts
    def aim(self, bone, direction):
        R.aim_bone(self.arm, bone, direction)

    def turn(self, bone, axis, deg):
        R.rotate_world(self.arm, bone, axis, deg)

    def hips(self, offset=(0, 0, 0), pitch=0.0, yaw=0.0, roll=0.0):
        pb = self.arm.pose.bones[B + "Hips"]
        mw3 = self.arm.matrix_world.to_3x3()
        delta = mw3.inverted() @ Vector(offset)
        m = pb.matrix.copy()
        m.translation += delta
        pb.matrix = m
        pb.scale = (1, 1, 1)
        bpy.context.view_layer.update()
        if pitch:
            self.turn("Hips", LEFT, pitch)
        if yaw:
            self.turn("Hips", UP, yaw)
        if roll:
            self.turn("Hips", FWD, roll)

    def spine(self, bend=0.0, twist=0.0, side=0.0):
        """bend > 0 leans forward. Spread over Spine, Spine1, Spine2."""
        for b, k in (("Spine", 0.3), ("Spine1", 0.35), ("Spine2", 0.35)):
            if bend:
                self.turn(b, LEFT, -bend * k)
            if twist:
                self.turn(b, UP, twist * k)
            if side:
                self.turn(b, FWD, side * k)

    def head(self, nod=0.0, turn=0.0, tilt=0.0):
        for b, k in (("Neck", 0.4), ("Head", 0.6)):
            if nod:
                self.turn(b, LEFT, -nod * k)
            if turn:
                self.turn(b, UP, turn * k)
            if tilt:
                self.turn(b, FWD, tilt * k)

    def leg(self, side, thigh, knee, ankle=0.0, spread=0.06):
        sx = 1 if side == "Left" else -1
        self.aim(side + "UpLeg", ang_dir(thigh, spread, sx))
        self.aim(side + "Leg", ang_dir(thigh - knee, spread * 0.5, sx))
        p = math.radians(self.foot_rest[side] - ankle)
        self.aim(side + "Foot", Vector((0, -math.cos(p), -math.sin(p))))

    def arm_dirs(self, side, upper, fore, hand=None):
        self.aim(side + "Arm", upper)
        self.aim(side + "ForeArm", fore)
        if hand is not None:
            self.aim(side + "Hand", hand)

    def arm_swing(self, side, swing, elbow=20.0, out=0.18):
        """Natural arm hang; swing > 0 = forward."""
        sx = 1 if side == "Left" else -1
        up = ang_dir(swing, out, sx)
        fore = ang_dir(swing + elbow, out * 0.6, sx)
        self.arm_dirs(side, up, fore, fore)

    def fist(self, side, amount=1.0, thumb=1.0):
        if amount > 0:
            R.fist(self.arm, side, amount, thumb)

    def middle_finger(self, side):
        """All fingers in a fist except the middle finger."""
        _, s, n = R.hand_frame(self.arm, side)
        sign = (1 if side == "Left" else -1) * R.FIST_SIGN
        for f in ("Index", "Ring", "Pinky"):
            for seg, a in ((1, 80), (2, 100), (3, 60)):
                _, s, _ = R.hand_frame(self.arm, side)
                self.turn(f"{side}Hand{f}{seg}", s, a * sign)
        for seg, a in ((1, 25), (2, 30), (3, 30)):
            self.turn(f"{side}HandThumb{seg}", n, a * sign)

    # ---- keys
    def start_action(self):
        self.prev_q = {}

    def key(self, frame):
        for pb in self.arm.pose.bones:
            q = pb.rotation_quaternion.copy()
            q.normalize()
            p = self.prev_q.get(pb.name)
            if p is not None and p.dot(q) < 0:      # same hemisphere as the last key: no flips
                q.negate()
            pb.rotation_quaternion = q
            self.prev_q[pb.name] = q.copy()
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
        hp = self.arm.pose.bones[B + "Hips"]
        hp.keyframe_insert("location", frame=frame, group=hp.name)


def make_action(arm, poser, name, seconds, pose_fn, loop=True, step=2):
    act = bpy.data.actions.get(name)
    if act:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    frames = max(2, round(seconds * FPS))
    poser.start_action()
    for f in list(range(0, frames, step)) + [frames]:
        t = f / frames
        poser.reset()
        pose_fn(poser, t)
        poser.key(f + 1)
    act.frame_range = (1, frames + 1)
    if loop:
        act.use_cyclic = True
    # Smooth the curves (Bezier) and make loops seamless
    for fc in _fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
        if loop:
            fc.modifiers.new("CYCLES")
    print(f"[Rex] action {name}: {frames} frames")
    return act


def _fcurves(act):
    if hasattr(act, "fcurves") and act.fcurves is not None and len(act.fcurves):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags:
                out.extend(bag.fcurves)
    return out


# ------------------------------------------------------------------ poses

def s(t, phase=0.0):
    return math.sin(math.tau * (t + phase))


def ease(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def seg(t, a, b):
    return ease((t - a) / (b - a)) if b > a else 1.0


def p_idle_belly(P, t):
    br = s(t)
    P.hips((0, 0.01, -0.005 + 0.003 * br), pitch=-4)
    P.spine(bend=4 + 1.5 * br, side=1.0 * s(t, 0.25))
    P.head(nod=-3 + br, turn=4 * s(t, 0.1))
    P.leg("Left", 2, 4, spread=0.1)
    P.leg("Right", 1, 3, spread=0.1)
    P.arm_swing("Left", 4 + br, 22, 0.22)
    P.arm_swing("Right", 4 + br, 22, 0.22)
    P.fist("Left", 0.35, 0.3)
    P.fist("Right", 0.35, 0.3)


def p_idle_hero(P, t):
    br = s(t)
    P.hips((0, 0, 0.003 * br), pitch=4)
    P.spine(bend=-7 - br, twist=0)
    P.head(nod=-6, turn=3 * s(t, 0.3))
    P.leg("Left", 0, 2, spread=0.14)
    P.leg("Right", 0, 2, spread=0.14)
    P.arm_swing("Left", -2, 30, 0.32)
    P.arm_swing("Right", -2, 30, 0.32)
    P.fist("Left", 0.8)
    P.fist("Right", 0.8)


def gait(P, t, stride, knee_max, bob, arm, lean, run=False):
    for side, ph in (("Left", 0.0), ("Right", 0.5)):
        c = math.cos(math.tau * (t + ph))
        sn = math.sin(math.tau * (t + ph))
        thigh = stride * c
        # knee bends in the swing phase (leg moving forward)
        swing = max(0.0, sn)
        knee = 8 + knee_max * swing ** 1.5 + (14 if run else 4) * max(0.0, -sn) * 0.5
        ankle = -12 * c + (18 if run else 8) * swing
        P.leg(side, thigh, knee, ankle, spread=0.07)
    P.hips((0, 0, -bob * 0.5 + bob * abs(math.sin(math.tau * t))), yaw=6 * s(t), roll=2 * s(t, 0.25))
    P.spine(bend=lean, twist=-8 * s(t) * (1.3 if run else 1.0))
    P.head(nod=-lean * 0.6, turn=4 * s(t))
    for side, ph in (("Left", 0.5), ("Right", 0.0)):
        c = math.cos(math.tau * (t + ph))
        P.arm_swing(side, arm * c + (20 if run else 4), 70 if run else 22, 0.2)
        P.fist(side, 0.75 if run else 0.4, 0.5)


def p_walk(P, t):
    gait(P, t, 24, 55, 0.025, 22, 4)


def p_run(P, t):
    gait(P, t, 38, 95, 0.05, 45, 12, run=True)


def p_jump(P, t):
    crouch = seg(t, 0.0, 0.18) * (1 - seg(t, 0.18, 0.3)) + seg(t, 0.82, 0.9) * (1 - seg(t, 0.9, 1.0))
    air = seg(t, 0.25, 0.4) * (1 - seg(t, 0.75, 0.85))
    P.hips((0, 0, -0.18 * crouch), pitch=6 * crouch)
    P.spine(bend=18 * crouch - 6 * air)
    P.head(nod=-8 * air)
    for side in ("Left", "Right"):
        P.leg(side, 35 * crouch + 25 * air, 70 * crouch + 45 * air, 25 * crouch, 0.08)
        P.arm_swing(side, -30 * crouch + 60 * air, 30, 0.35 * air + 0.2)
        P.fist(side, 0.5)


def guard(P, side, amount=1.0):
    sx = 1 if side == "Left" else -1
    P.arm_dirs(side, d(0.25 * sx, -0.35, -0.9).lerp(d(0.2 * sx, -0.3, -1.0), 1 - amount),
               d(-0.15 * sx, -0.75, 0.65).lerp(d(0.1 * sx, -0.4, -1.0), 1 - amount))
    P.fist(side, 1.0)


def p_punch_jab(P, t):
    hit = seg(t, 0.1, 0.32) * (1 - seg(t, 0.45, 0.9))
    P.hips(pitch=0, yaw=-14 * hit)
    P.spine(bend=6 + 4 * hit, twist=-22 * hit)
    P.leg("Left", 12, 18, spread=0.12)
    P.leg("Right", -10, 12, spread=0.1)
    guard(P, "Left")
    # right jab (Rex leads with his right: he is not a real fighter)
    up = d(-0.1, -0.5, -0.4).lerp(d(-0.03, -1.0, 0.08), hit)
    fore = d(0.2, -0.8, 0.6).lerp(d(0.0, -1.0, 0.05), hit)
    P.arm_dirs("Right", up, fore, fore)
    P.fist("Right", 1.0)


def p_punch_heavy(P, t):
    wind = seg(t, 0.0, 0.35) * (1 - seg(t, 0.35, 0.5))
    hit = seg(t, 0.4, 0.55) * (1 - seg(t, 0.7, 1.0))
    P.hips(yaw=18 * wind - 30 * hit, pitch=4 * hit)
    P.spine(bend=4 + 12 * hit, twist=25 * wind - 40 * hit)
    P.leg("Left", 15 * hit, 25, spread=0.13)
    P.leg("Right", -15 * hit, 15, 10 * hit, spread=0.1)
    guard(P, "Left", 1 - hit * 0.5)
    up = d(-0.6, 0.35, -0.3).lerp(d(-0.05, -1.0, 0.15), hit)
    fore = d(-0.2, -0.5, 0.8).lerp(d(0.05, -1.0, 0.25), hit)
    if wind > hit:
        up = d(-0.1, -0.5, -0.4).lerp(d(-0.6, 0.35, -0.3), wind)
        fore = d(0.2, -0.8, 0.6).lerp(d(-0.2, -0.5, 0.8), wind)
    P.arm_dirs("Right", up, fore, fore)
    P.fist("Right", 1.0)


def p_swing(P, t):
    """Bottle (held by the neck in the right hand): side swing."""
    wind = seg(t, 0.0, 0.35) * (1 - seg(t, 0.35, 0.5))
    hit = seg(t, 0.38, 0.58) * (1 - seg(t, 0.75, 1.0))
    P.hips(yaw=25 * wind - 25 * hit)
    P.spine(bend=6, twist=35 * wind - 45 * hit)
    P.leg("Left", 10, 15, spread=0.12)
    P.leg("Right", -8, 12, spread=0.1)
    P.arm_swing("Left", 10, 40, 0.25)
    P.fist("Left", 0.6)
    a = d(-0.95, 0.1, 0.2).lerp(d(0.6, -0.8, 0.05), hit)
    if wind > hit:
        a = d(-0.3, -0.3, -0.8).lerp(d(-0.95, 0.1, 0.2), wind)
    P.arm_dirs("Right", a, a.lerp(d(0, -1, 0.3), 0.3), a)
    P.fist("Right", 0.9)


def p_whip(P, t):
    """Towel: wind-up over the shoulder, then snap forward."""
    wind = seg(t, 0.0, 0.3) * (1 - seg(t, 0.3, 0.42))
    snap = seg(t, 0.36, 0.48) * (1 - seg(t, 0.65, 1.0))
    P.spine(bend=4 + 10 * snap, twist=15 * wind - 25 * snap)
    P.leg("Left", 10, 15, spread=0.12)
    P.leg("Right", -8, 12, spread=0.1)
    P.arm_swing("Left", 8, 30, 0.25)
    P.fist("Left", 0.5)
    up = d(-0.4, 0.4, 0.8).lerp(d(-0.1, -1.0, 0.1), snap)
    fore = d(0.1, 0.5, 0.8).lerp(d(0.0, -1.0, -0.2), snap)
    if wind < 0.01 and snap < 0.01:
        up, fore = d(-0.2, -0.2, -1), d(-0.1, -0.6, -0.8)
    P.arm_dirs("Right", up, fore, fore)
    P.fist("Right", 0.9)


def p_taunt(P, t):
    """Middle finger up, held in front, with a little pump and a smug lean."""
    up_t = seg(t, 0.0, 0.25) * (1 - seg(t, 0.8, 1.0))
    pump = 0.08 * math.sin(math.tau * 3 * t) * up_t
    P.hips(pitch=-4 * up_t)
    P.spine(bend=-6 * up_t, twist=-10 * up_t)
    P.head(nod=-8 * up_t, tilt=6 * up_t)
    P.leg("Left", 2, 4, spread=0.13)
    P.leg("Right", 2, 4, spread=0.13)
    P.arm_swing("Left", 0, 25, 0.25)
    P.fist("Left", 0.5)
    up = d(-0.2, -0.3, -0.9).lerp(d(-0.15, -0.9, 0.0 + pump), up_t)
    fore = d(-0.1, -0.6, -0.8).lerp(d(0.05, -0.35, 0.95), up_t)
    P.arm_dirs("Right", up, fore, fore)
    if up_t > 0.05:
        P.middle_finger("Right")
    else:
        P.fist("Right", 0.4)


def p_photo(P, t):
    """Selfie: right arm up and out in front, phone facing Rex, grin."""
    up_t = seg(t, 0.0, 0.25) * (1 - seg(t, 0.85, 1.0))
    P.spine(bend=-3 * up_t, side=-4 * up_t)
    P.head(nod=-6 * up_t, turn=-12 * up_t, tilt=-8 * up_t)
    P.leg("Left", 2, 4, spread=0.13)
    P.leg("Right", 2, 4, spread=0.13)
    up = d(-0.2, -0.3, -0.9).lerp(d(-0.45, -0.75, 0.45), up_t)
    fore = d(-0.1, -0.6, -0.8).lerp(d(-0.2, -0.8, 0.55), up_t)
    P.arm_dirs("Right", up, fore, fore)
    P.fist("Right", 0.55, 0.2)
    # free hand: thumbs up toward the camera
    P.arm_dirs("Left", d(0.3, -0.3, -0.9).lerp(d(0.4, -0.5, -0.4), up_t),
               d(0.2, -0.5, -0.8).lerp(d(0.0, -0.6, 0.8), up_t))
    P.fist("Left", 1.0, 0.0)


def p_hit(P, t):
    k = seg(t, 0.0, 0.15) * (1 - seg(t, 0.3, 1.0))
    P.hips((0, 0.04 * k, -0.02 * k), pitch=-6 * k)
    P.spine(bend=-14 * k, twist=8 * k)
    P.head(nod=-15 * k, tilt=8 * k)
    P.leg("Left", 2 - 8 * k, 6 + 10 * k, spread=0.12)
    P.leg("Right", 2 + 6 * k, 6, spread=0.12)
    P.arm_swing("Left", 30 * k, 40, 0.3 + 0.2 * k)
    P.arm_swing("Right", 30 * k, 40, 0.3 + 0.2 * k)
    P.fist("Left", 0.3)
    P.fist("Right", 0.3)


def p_slip(P, t):
    """Comedy fall on slime: feet go up, lands on the back, then lies still."""
    fly = seg(t, 0.05, 0.35)
    land = seg(t, 0.35, 0.5)
    P.hips((0, 0.25 * fly + 0.1 * land, -0.25 * fly - 0.55 * land), pitch=-70 * fly - 15 * land)
    P.spine(bend=-10 * fly + 8 * land)
    P.head(nod=25 * land - 10 * fly)
    P.leg("Left", 70 * fly - 30 * land, 20 + 20 * land, 0, 0.12)
    P.leg("Right", 50 * fly - 20 * land, 30 + 20 * land, 0, 0.12)
    P.arm_swing("Left", 120 * fly - 60 * land, 30, 0.6)
    P.arm_swing("Right", 100 * fly - 60 * land, 30, 0.6)


def p_death(P, t):
    """Knees give out, falls on his back."""
    knees = seg(t, 0.0, 0.35)
    fall = seg(t, 0.3, 0.75)
    P.hips((0, 0.35 * fall, -0.45 * knees - 0.4 * fall), pitch=-80 * fall)
    P.spine(bend=20 * knees - 25 * fall)
    P.head(nod=20 * knees - 10 * fall, turn=20 * fall)
    P.leg("Left", 40 * knees - 40 * fall, 90 * knees - 70 * fall, 0, 0.14)
    P.leg("Right", 30 * knees - 30 * fall, 80 * knees - 60 * fall, 0, 0.14)
    P.arm_swing("Left", 20 * knees + 70 * fall, 30, 0.5 + 0.5 * fall)
    P.arm_swing("Right", 10 * knees + 90 * fall, 40, 0.5 + 0.6 * fall)


def p_tread(P, t):
    """Treading water (in place): upright, chin up, slow bicycle legs, sculling arms."""
    P.hips((0, 0, 0.02 * s(t, 0.25)), pitch=8)
    P.spine(bend=6)
    P.head(nod=-12)
    for side, ph in (("Left", 0.0), ("Right", 0.5)):
        sx = 1 if side == "Left" else -1
        c = math.cos(math.tau * (t + ph))
        P.leg(side, 35 + 20 * c, 70 + 25 * max(0.0, c), 25, 0.1)
        sc = s(t * 2, 0.1 if side == "Left" else 0.0)
        P.arm_dirs(side, d(0.85 * sx, -0.2 - 0.25 * sc, -0.45), d(0.55 * sx, -0.65 - 0.45 * sc, -0.45),
                   d(0.4 * sx, -0.75 - 0.4 * sc, -0.5))
        P.fist(side, 0.15, 0.2)


def p_paddle(P, t):
    """Doggy paddle (moving): leaning forward, hands circle in front of the chest, small flutter kicks."""
    P.hips((0, 0, 0.015 * s(t * 2)), pitch=15)
    P.spine(bend=18, twist=3 * s(t))
    P.head(nod=-30)
    for side, ph in (("Left", 0.0), ("Right", 0.5)):
        sx = 1 if side == "Left" else -1
        a = math.tau * (t + ph)
        P.arm_dirs(side, d(0.3 * sx, -0.8 - 0.12 * math.cos(a), -0.5 + 0.22 * math.sin(a)),
                   d(0.12 * sx, -0.85, -0.45 + 0.45 * math.sin(a + 0.4)),
                   d(0.05 * sx, -0.7, -0.7 + 0.3 * math.sin(a + 0.6)))
        P.fist(side, 0.2, 0.2)
        k = math.cos(math.tau * 2 * (t + ph))
        P.leg(side, -4 + 12 * k, 25 + 15 * max(0.0, k), -25, 0.07)


ACTIONS = [
    ("Rex_Idle", 3.0, p_idle_belly, True),
    ("Rex_IdleHero", 3.0, p_idle_hero, True),
    ("Rex_Walk", 1.1, p_walk, True),
    ("Rex_Run", 0.7, p_run, True),
    ("Rex_Jump", 1.1, p_jump, False),
    ("Rex_PunchJab", 0.55, p_punch_jab, False),
    ("Rex_PunchHeavy", 1.0, p_punch_heavy, False),
    ("Rex_Swing", 0.8, p_swing, False),
    ("Rex_Whip", 0.8, p_whip, False),
    ("Rex_Taunt", 1.8, p_taunt, False),
    ("Rex_Photo", 2.0, p_photo, False),
    ("Rex_Hit", 0.5, p_hit, False),
    ("Rex_Slip", 1.6, p_slip, False),
    ("Rex_Death", 1.6, p_death, False),
    ("Rex_Tread", 2.0, p_tread, True),
    ("Rex_Paddle", 1.2, p_paddle, True),
]


def make_all(arm):
    bpy.context.scene.render.fps = FPS
    P = Poser(arm)
    acts = []
    for name, sec, fn, loop in ACTIONS:
        acts.append(make_action(arm, P, name, sec, fn, loop))
    arm.animation_data.action = bpy.data.actions["Rex_Idle"]
    return acts
