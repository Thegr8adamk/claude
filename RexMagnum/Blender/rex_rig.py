"""Rex Magnum - rig (Mixamo + Unity Humanoid bone names) and fit all parts to it.

Body: MPFB 'mixamo_unity' rig with MPFB weights.
Clothes (shirt, shorts, socks): copy skin weights from the body (Data Transfer), Armature modifier.
Hard parts (eyes, hair, stubble, brows, sunglasses, chain, ring, watch, flip-flops): child of one bone.
"""
import bpy

from bl_ext.user_default.mpfb.services.humanservice import HumanService

B = "mixamorig:"

# part name prefix -> bone it follows
BONE_PARENTS = [
    ("Rex_Eye_l", "LeftEye"), ("Rex_Eye_r", "RightEye"),
    ("Rex_Hair", "Head"), ("Rex_Stubble", "Head"), ("Rex_Brows", "Head"), ("Rex_Sunglasses", "Head"),
    ("Rex_GoldChain", "Spine2"),
    ("Rex_PinkyRing", "RightHandPinky1"),
    ("Rex_WatchBand", "LeftForeArm"), ("Rex_WatchFace", "LeftForeArm"),
    ("Rex_FlipFlop_l", "LeftFoot"), ("Rex_FlipFlop_r", "RightFoot"),
    ("Rex_Strap_l", "LeftFoot"), ("Rex_Strap_r", "RightFoot"),
    ("Rex_Button", "Spine"),
    ("Rex_TeethUpper", "Head"), ("Rex_TeethLower", "Jaw"),
]

SKINNED = ("Rex_Shirt", "Rex_Shorts", "Rex_Socks")


def parent_to_bone(ob, arm, bone):
    mw = ob.matrix_world.copy()
    ob.parent = arm
    ob.parent_type = "BONE"
    ob.parent_bone = B + bone
    bpy.context.view_layer.update()
    ob.matrix_world = mw


def skin_from_body(ob, body, arm):
    """Copy body bone weights to a clothing mesh, then add an Armature modifier."""
    for g in body.vertex_groups:
        if g.name.startswith(B) and g.name not in ob.vertex_groups:
            ob.vertex_groups.new(name=g.name)
    dt = ob.modifiers.new("WeightsFromBody", "DATA_TRANSFER")
    dt.object = body
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.layers_vgroup_select_src = "ALL"
    dt.layers_vgroup_select_dst = "NAME"
    # Move the data transfer to the top of the stack and apply it
    ob.modifiers.move(ob.modifiers.find(dt.name), 0)
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=dt.name)
    am = ob.modifiers.new("Armature", "ARMATURE")
    am.object = arm
    ob.modifiers.move(ob.modifiers.find(am.name), 0)
    mw = ob.matrix_world.copy()
    ob.parent = arm
    ob.matrix_world = mw


def rig(body):
    arm = HumanService.add_builtin_rig(body, "mixamo_unity")
    if arm is None:
        arm = body.parent
    arm.name = "Rex_Rig"
    bpy.context.view_layer.update()
    for ob in list(bpy.data.objects):
        if any(ob.name.startswith(s) for s in SKINNED):
            skin_from_body(ob, body, arm)
            continue
        for prefix, bone in BONE_PARENTS:
            if ob.name.startswith(prefix) and ob.parent is None:
                parent_to_bone(ob, arm, bone)
                break
    print("[Rex] rigged:", arm.name, len(arm.data.bones), "bones")
    return arm


def aim_bone(arm, bone, direction):
    """Turn a pose bone so it points along a world direction (keeps the twist as small as possible)."""
    from mathutils import Matrix, Vector
    pb = arm.pose.bones[B + bone]
    bpy.context.view_layer.update()
    mw3 = arm.matrix_world.to_3x3().normalized()
    cur = (mw3 @ (pb.tail - pb.head)).normalized()
    q = cur.rotation_difference(Vector(direction).normalized())
    # rotation in armature space
    q_arm = (mw3.inverted() @ q.to_matrix() @ mw3)
    m = pb.matrix.copy()
    t = m.translation.copy()
    pb.matrix = Matrix.Translation(t) @ q_arm.to_4x4() @ Matrix.Translation(-t) @ m
    pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def rot_local(arm, bone, x=0.0, y=0.0, z=0.0):
    import math
    pb = arm.pose.bones.get(B + bone)
    if pb:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (math.radians(x), math.radians(y), math.radians(z))


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def hero_pose(arm):
    """Chest out, belly in, arms a little away from the body, fists loose."""
    reset_pose(arm)
    rot_local(arm, "Spine", x=4)
    rot_local(arm, "Spine2", x=-9)
    rot_local(arm, "Neck", x=5)
    for side, sx in (("Left", 1), ("Right", -1)):
        aim_bone(arm, side + "Arm", (0.28 * sx, 0.05, -1.0))
        aim_bone(arm, side + "ForeArm", (0.18 * sx, -0.25, -1.0))
        aim_bone(arm, side + "Hand", (0.1 * sx, -0.15, -1.0))


def rotate_world(arm, bone, axis, degrees):
    """Rotate a pose bone around a world-space axis through its head."""
    import math
    from mathutils import Matrix, Quaternion, Vector
    pb = arm.pose.bones[B + bone]
    bpy.context.view_layer.update()
    mw3 = arm.matrix_world.to_3x3().normalized()
    ax = (mw3.inverted() @ Vector(axis)).normalized()
    q = Quaternion(ax, math.radians(degrees)).to_matrix().to_4x4()
    m = pb.matrix.copy()
    t = m.translation.copy()
    pb.matrix = Matrix.Translation(t) @ q @ Matrix.Translation(-t) @ m
    pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def bone_world(arm, bone, tail=False):
    pb = arm.pose.bones[B + bone]
    return arm.matrix_world @ (pb.tail if tail else pb.head)


def hand_frame(arm, side):
    """(hand direction, side axis index->pinky, palm normal) in world space, from the current pose."""
    h0 = bone_world(arm, side + "Hand")
    i1 = bone_world(arm, side + "HandIndex1")
    p1 = bone_world(arm, side + "HandPinky1")
    m1 = bone_world(arm, side + "HandMiddle1")
    d = (m1 - h0).normalized()
    s = (p1 - i1).normalized()
    s = (s - d * s.dot(d)).normalized()
    n = d.cross(s) if side == "Left" else s.cross(d)
    return d, s, n


def fist(arm, side, amount=1.0, thumb=1.0):
    """Close the hand. amount 0 = open, 1 = tight fist."""
    for f in ("Index", "Middle", "Ring", "Pinky"):
        for seg, ang in ((1, 75), (2, 95), (3, 60)):
            _, s, _ = hand_frame(arm, side)
            rotate_world(arm, f"{side}Hand{f}{seg}", s, ang * amount * (1 if side == "Left" else -1) * FIST_SIGN)
    if thumb:
        _, s, n = hand_frame(arm, side)
        for seg, ang in ((1, 25), (2, 30), (3, 30)):
            rotate_world(arm, f"{side}HandThumb{seg}", n, ang * amount * thumb * (1 if side == "Left" else -1) * FIST_SIGN)


FIST_SIGN = 1
