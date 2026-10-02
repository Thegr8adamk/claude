"""Rex Magnum - blob rig, animations and game export.

Rig (brief section 4): Root, 3 body bones (squash/stretch), one 5-bone chain per tentacle,
eye bones (and 2-bone eye stalks for the Lifeguard), a brain bone.
Weights are computed from bone distance (smooth, no seams). The puddle stays on the ground.
Animations (Unity Generic rig): Idle, Slide, SlideFast, Whip, Spit, Throw, Hit, Stun, Angry,
Rude (answer to Rex's taunt), Photo (confused), Pinned, Pop.
Leak (knife), freeze+break (swordfish) and split (bouncer) are done in Unity (shader + VFX).
"""
import json
import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

FPS = 30


def ctx(ob):
    return bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob],
                                     selected_editable_objects=[ob])


def resample(pts, n):
    """n+1 points spaced evenly along a polyline."""
    d = [0.0]
    for a, b in zip(pts, pts[1:]):
        d.append(d[-1] + (b - a).length)
    out = []
    for k in range(n + 1):
        t = d[-1] * k / n
        i = max(0, min(len(d) - 2, next((j for j in range(len(d) - 1) if d[j + 1] >= t), len(d) - 2)))
        f = (t - d[i]) / max(1e-9, d[i + 1] - d[i])
        out.append(pts[i].lerp(pts[i + 1], f))
    return out


def build_rig(kind, data, blob, coll):
    h, r = data["h"], data["r"]
    arm_data = bpy.data.armatures.new(f"Blob_{kind}_Rig")
    arm = bpy.data.objects.new(f"Blob_{kind}_Rig", arm_data)
    coll.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    with ctx(arm):
        bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def bone(name, head, tail, parent=None, deform=True):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        if parent:
            b.parent = eb[parent]
        b.use_deform = deform
        return b

    bone("Root", (0, 0, 0), (0, 0.25 * r, 0))
    bone("Body1", (0, 0, 0.06 * h), (0, 0, 0.38 * h), "Root").use_connect = False
    bone("Body2", (0, 0, 0.38 * h), (0, 0, 0.7 * h), "Body1").use_connect = True
    bone("Body3", (0, 0, 0.7 * h), (0, 0, 1.0 * h), "Body2").use_connect = True
    bone("Brain", data["brain"], Vector(data["brain"]) + Vector((0, 0, 0.12 * r)), "Body2", deform=False)
    segs = []   # (bone name, head, tail) for weights
    for name in ("Body1", "Body2", "Body3"):
        segs.append((name, eb[name].head.copy(), eb[name].tail.copy()))
    for i, path in enumerate(data["tentacles"]):
        pts = resample(path, 5)
        parent = "Body1" if pts[0].z < 0.38 * h else "Body2"
        for k in range(5):
            n = f"Tentacle{i}_{k}"
            bone(n, pts[k], pts[k + 1], parent if k == 0 else f"Tentacle{i}_{k - 1}").use_connect = k > 0
            segs.append((n, pts[k].copy(), pts[k + 1].copy()))
    for i, (c, er) in enumerate(data["eyes"]):
        c = Vector(c)
        parent = "Body3"
        if data["stalks"]:
            base = Vector((c.x * 0.8, c.y + 0.08, h - 0.08))
            mid = base.lerp(c, 0.5)
            bone(f"Stalk{i}_0", base, mid, "Body3")
            bone(f"Stalk{i}_1", mid, c, f"Stalk{i}_0").use_connect = True
            segs.append((f"Stalk{i}_0", base, mid))
            segs.append((f"Stalk{i}_1", mid, c))
            parent = f"Stalk{i}_1"
        bone(f"Eye{i}", c, c + Vector((0, -er, 0)), parent, deform=False)
    with ctx(arm):
        bpy.ops.object.mode_set(mode="OBJECT")

    # ---- weights: inverse distance to bone segments; puddle -> Root
    me = blob.data
    co = np.array([v.co[:] for v in me.vertices])
    names = [s[0] for s in segs]
    W = np.zeros((len(co), len(segs)))
    for j, (_, a, b) in enumerate(segs):
        a, b = np.array(a[:]), np.array(b[:])
        ab = b - a
        t = np.clip(((co - a) @ ab) / max(1e-9, ab @ ab), 0, 1)
        d = np.linalg.norm(co - (a + t[:, None] * ab), axis=1)
        if names[j].startswith("Body"):
            d = d * 0.55          # body bones are on the axis: let them reach the skin
        W[:, j] = 1.0 / (d ** 6 + 1e-9)
    # keep the 3 strongest, normalize
    top = np.argsort(-W, axis=1)[:, :3]
    groups = {n: blob.vertex_groups.new(name=n) for n in names + ["Root"]}
    for vi in range(len(co)):
        if co[vi, 2] < 0.07 * h:                  # puddle: does not move with the body
            groups["Root"].add([vi], 1.0, "REPLACE")
            continue
        ws = W[vi, top[vi]]
        ws = ws / ws.sum()
        for j, w in zip(top[vi], ws):
            if w > 0.02:
                groups[names[j]].add([vi], float(w), "REPLACE")
    md = blob.modifiers.new("Armature", "ARMATURE")
    md.object = arm
    blob.parent = arm

    # eyes + brain follow their bones
    for ob in list(coll.objects):
        if ob == blob or ob == arm:
            continue
        bname = None
        if "_Eye" in ob.name:
            bname = "Eye" + ob.name.split("_Eye")[-1]
        elif ob.name.endswith("_Brain"):
            bname = "Brain"
        if bname and bname in arm.data.bones:
            mw = ob.matrix_world.copy()
            ob.parent = arm
            ob.parent_type = "BONE"
            ob.parent_bone = bname
            bpy.context.view_layer.update()
            ob.matrix_world = mw
    print(f"[Blob] {kind} rig: {len(arm.data.bones)} bones")
    return arm


# ------------------------------------------------------------------ animation

def _pb(arm, n):
    return arm.pose.bones.get(n)


def pose_reset(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


def body(arm, squash=0.0, lean=0.0, side=0.0, twist=0.0, puff=0.0):
    """squash > 0: shorter and wider. lean > 0: forward (-Y)."""
    for k, n in enumerate(("Body1", "Body2", "Body3")):
        pb = _pb(arm, n)
        sy = 1 - squash * (0.6 + 0.2 * k)
        sx = (1 + squash * 0.5) * (1 + puff)
        pb.scale = (sx, sy * (1 + puff * 0.5), sx)
        # bone Y is up; local X bends front/back, local Z bends sideways
        pb.rotation_euler = (math.radians(-lean * (0.3 + 0.2 * k)), math.radians(twist * 0.33),
                             math.radians(side * (0.3 + 0.2 * k)))


def tentacle(arm, i, bend, wave=0.0, phase=0.0, lift=0.0, side=0.0, amp=1.0):
    """bend/wave in degrees per bone. wave travels from root to tip."""
    for k in range(5):
        pb = _pb(arm, f"Tentacle{i}_{k}")
        if pb is None:
            return
        w = math.sin(phase - k * 0.9) * wave * amp
        pb.rotation_euler = (math.radians(bend * amp + w + (lift if k == 0 else 0)), 0.0,
                             math.radians(side * amp + w * 0.5))


def eyes(arm, roll=0.0, look=(0.0, 0.0), t=0.0):
    for pb in arm.pose.bones:
        if pb.name.startswith("Eye"):
            if roll:
                pb.rotation_euler = (math.radians(math.sin(t) * roll), math.radians(math.cos(t) * roll), 0)
            else:
                pb.rotation_euler = (math.radians(look[1]), 0, math.radians(look[0]))


def key_all(arm, frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_euler", frame=frame, group=pb.name)
        pb.keyframe_insert("scale", frame=frame, group=pb.name)
        pb.keyframe_insert("location", frame=frame, group=pb.name)


def ntent(arm):
    return len({pb.name.split("_")[0] for pb in arm.pose.bones if pb.name.startswith("Tentacle")})


def A_idle(arm, t):
    s = math.sin(math.tau * t)
    body(arm, squash=0.03 * s, side=2 * math.sin(math.tau * t + 1))
    for i in range(ntent(arm)):
        tentacle(arm, i, 4 * math.sin(math.tau * t + i), wave=10, phase=math.tau * t + i * 1.7)


def A_slide(arm, t, fast=False):
    s = math.sin(math.tau * t * 2)
    body(arm, squash=0.05 * s, lean=8 if not fast else 16, side=4 * math.sin(math.tau * t))
    for i in range(ntent(arm)):
        tentacle(arm, i, -10 if not fast else -20, wave=18 if not fast else 25, phase=math.tau * t * 2 + i)


def A_whip(arm, t):
    wind = math.sin(min(1, t / 0.4) * math.pi / 2) * (1 - _smooth(t, 0.4, 0.55))
    lash = _smooth(t, 0.4, 0.55) * (1 - _smooth(t, 0.7, 1.0))
    body(arm, lean=-6 * wind + 10 * lash, twist=20 * wind - 25 * lash)
    for i in range(ntent(arm)):
        if i == 0:
            tentacle(arm, i, -35 * wind + 30 * lash, wave=5, phase=t * 10, lift=-30 * wind + 50 * lash)
        else:
            tentacle(arm, i, 3, wave=8, phase=math.tau * t + i)


def A_spit(arm, t):
    load = _smooth(t, 0.0, 0.45) * (1 - _smooth(t, 0.45, 0.55))
    shot = _smooth(t, 0.45, 0.55) * (1 - _smooth(t, 0.6, 1.0))
    body(arm, squash=0.15 * load - 0.15 * shot, lean=-8 * load + 18 * shot, puff=0.08 * load)
    for i in range(ntent(arm)):
        tentacle(arm, i, 10 * load - 15 * shot, wave=6, phase=math.tau * t + i)


def A_throw(arm, t):
    up = _smooth(t, 0.0, 0.4) * (1 - _smooth(t, 0.45, 0.6))
    fwd = _smooth(t, 0.45, 0.6) * (1 - _smooth(t, 0.7, 1.0))
    body(arm, lean=-10 * up + 15 * fwd, squash=-0.06 * up)
    for i in range(ntent(arm)):
        if i < 2:
            tentacle(arm, i, -40 * up + 35 * fwd, lift=-60 * up + 40 * fwd)
        else:
            tentacle(arm, i, 5, wave=8, phase=math.tau * t + i)


def A_hit(arm, t):
    k = math.exp(-5 * t) * math.sin(t * 30)
    body(arm, squash=0.18 * k, lean=-10 * k, side=6 * k)
    for i in range(ntent(arm)):
        tentacle(arm, i, 15 * k, wave=12 * abs(k), phase=t * 20 + i)


def A_stun(arm, t):
    body(arm, side=8 * math.sin(math.tau * t), squash=0.08 + 0.03 * math.sin(math.tau * 2 * t))
    eyes(arm, roll=25, t=math.tau * 2 * t)
    for i in range(ntent(arm)):
        tentacle(arm, i, 25, wave=6, phase=math.tau * t + i)      # limp


def A_angry(arm, t):
    sh = math.sin(math.tau * 8 * t)
    body(arm, side=3 * sh, puff=0.08, squash=-0.04)
    for i in range(ntent(arm)):
        tentacle(arm, i, -15, wave=20, phase=math.tau * 4 * t + i)


def A_rude(arm, t):
    """Answer to Rex's taunt: one tentacle goes straight up, curled tip, and pumps."""
    up = _smooth(t, 0.0, 0.25) * (1 - _smooth(t, 0.85, 1.0))
    pump = math.sin(math.tau * 3 * t) * 6 * up
    body(arm, lean=-6 * up, side=-5 * up)
    for i in range(ntent(arm)):
        if i == 0:
            for k in range(5):
                pb = _pb(arm, f"Tentacle0_{k}")
                straight = -55 if k == 0 else (-20 if k < 4 else 40)
                pb.rotation_euler = (math.radians(straight * up + pump), 0, 0)
        else:
            tentacle(arm, i, 5, wave=6, phase=math.tau * t + i)


def A_photo(arm, t):
    body(arm, side=10 * math.sin(math.tau * 0.5 * t), lean=-4)
    eyes(arm, look=(20 * math.sin(math.tau * t), 8))
    for i in range(ntent(arm)):
        if i == 1:
            tentacle(arm, i, -30, lift=-45, wave=15, phase=math.tau * 3 * t)   # scratches its head
        else:
            tentacle(arm, i, 5, wave=6, phase=math.tau * t + i)


def A_pinned(arm, t):
    body(arm, lean=-14, squash=-0.08, side=4 * math.sin(math.tau * 3 * t))
    for i in range(ntent(arm)):
        tentacle(arm, i, -20, wave=30, phase=math.tau * 3 * t + i * 2)


def A_pop(arm, t):
    grow = _smooth(t, 0.0, 0.7)
    gone = _smooth(t, 0.7, 1.0)
    body(arm, puff=0.4 * grow, squash=-0.1 * grow)
    if gone > 0:
        # shrink at the Root only (every bone inherits it): scaling each bone to 0.001 multiplied down the chain
        # (~1e-15 at the eye bones), and the FBX exporter wrote NaN keys for the bone-parented eye meshes
        pb = _pb(arm, "Root")
        pb.scale = tuple(s * (1 - gone) + 0.01 for s in pb.scale)
    for i in range(ntent(arm)):
        tentacle(arm, i, -25 * grow, wave=10, phase=t * 30 + i)


def _smooth(t, a, b):
    x = max(0.0, min(1.0, (t - a) / (b - a)))
    return x * x * (3 - 2 * x)


ACTIONS = [
    ("Idle", 2.0, A_idle, True), ("Slide", 1.0, A_slide, True),
    ("SlideFast", 0.6, lambda a, t: A_slide(a, t, True), True), ("Whip", 0.8, A_whip, False),
    ("Spit", 0.9, A_spit, False), ("Throw", 1.0, A_throw, False), ("Hit", 0.45, A_hit, False),
    ("Stun", 2.0, A_stun, True), ("Angry", 1.0, A_angry, True), ("Rude", 1.6, A_rude, False),
    ("Photo", 2.0, A_photo, False), ("Pinned", 1.5, A_pinned, True), ("Pop", 0.5, A_pop, False),
]


def make_actions(kind, arm):
    arm.animation_data_create()
    names = []
    for name, sec, fn, loop in ACTIONS:
        full = f"Blob{kind}_{name}"
        old = bpy.data.actions.get(full)
        if old:
            bpy.data.actions.remove(old)
        act = bpy.data.actions.new(full)
        act.use_fake_user = True
        arm.animation_data.action = act
        frames = max(2, round(sec * FPS))
        for f in list(range(0, frames, 2)) + [frames]:
            pose_reset(arm)
            fn(arm, f / frames)
            key_all(arm, f + 1)
        act.frame_range = (1, frames + 1)
        act.use_cyclic = loop
        names.append(full)
    arm.animation_data.action = bpy.data.actions[f"Blob{kind}_Idle"]
    pose_reset(arm)
    print(f"[Blob] {kind}: {len(names)} actions")
    return names


# ------------------------------------------------------------------ game export

def export_game(kind, arm, blob, out_dir, tri_budget):
    os.makedirs(out_dir, exist_ok=True)
    # game mesh: decimate the jelly body to the budget (weights are kept)
    tris = sum(len(p.vertices) - 2 for p in blob.data.polygons)
    if tris > tri_budget:
        md = blob.modifiers.new("Decimate", "DECIMATE")
        md.ratio = tri_budget / tris
        blob.modifiers.move(blob.modifiers.find("Decimate"), 0)
        arm_md = blob.modifiers.get("Armature")
        arm_md.show_viewport = False
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(blob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        blob.modifiers.remove(blob.modifiers["Decimate"])
        arm_md.show_viewport = True
        old = blob.data
        blob.data = me
        bpy.data.meshes.remove(old)
    objs = [arm] + [o for o in arm.children_recursive if o.type == "MESH"]
    for x in bpy.context.view_layer.objects:
        x.select_set(x in objs)
    bpy.context.view_layer.objects.active = arm
    path = os.path.join(out_dir, f"Blob_{kind}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True, bake_anim_force_startend_keying=True, mesh_smooth_type="FACE",
        path_mode="STRIP")
    bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, f"Blob_{kind}.glb"), use_selection=True,
                              export_animations=True, export_animation_mode="NLA_TRACKS")
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")
    print(f"[Blob] exported {kind}: {tris} triangles")
    return tris


def actions_to_nla(arm, names):
    """One NLA track per action, so the FBX exporter writes one clip per action for this rig only."""
    arm.animation_data.action = None
    for n in names:
        tr = arm.animation_data.nla_tracks.new()
        tr.name = n
        st = tr.strips.new(n, int(bpy.data.actions[n].frame_range[0]), bpy.data.actions[n])
        st.name = n
        tr.mute = False
