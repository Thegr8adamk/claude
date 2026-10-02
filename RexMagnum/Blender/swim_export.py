"""Rex Magnum - Rex's swim clips (Rex_Tread, Rex_Paddle) as a small extra FBX, without re-baking Rex.

Opens Blender/Rex_v01.blend (Rex's rig), makes the two swim actions with rex_anim, and exports only the
armature with them to Unity/Assets/RexMagnum/Characters/Rex/Rex_Swim.fbx (same FBX settings as rex_export.py).
Unity: RexBuilder imports it as Humanoid with Rex's avatar and uses the clips in a "Swim" blend tree.
Optional: --render <png> renders Rex in the two poses (check picture).

Run:  python swim_export.py [--render out.png]
"""
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
EXTRA = ("Rex_Tread", "Rex_Paddle", "Rex_Comb", "Rex_Throw", "Rex_Stab", "Rex_GolfSwing", "Rex_Drive")   # clips in this FBX
OUT = os.path.join(HERE, "..", "Unity", "Assets", "RexMagnum", "Characters", "Rex", "Rex_Swim.fbx")


def main():
    import addon_utils
    addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Rex_v01.blend"))
    import rex_anim
    arm = bpy.data.objects["Rex_Rig"]
    bpy.context.scene.render.fps = rex_anim.FPS
    P = rex_anim.Poser(arm)
    acts = [rex_anim.make_action(arm, P, n, sec, fn, loop)
            for n, sec, fn, loop in rex_anim.ACTIONS if n in EXTRA]
    if "--render" in sys.argv:
        render(arm, acts, os.path.abspath(sys.argv[sys.argv.index("--render") + 1]))
    # only the swim actions go into this FBX
    keep = {a.name for a in acts}
    for a in list(bpy.data.actions):
        if a.name not in keep:
            a.use_fake_user = False
            bpy.data.actions.remove(a)
    arm.animation_data.action = acts[0]
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=os.path.abspath(OUT), use_selection=True, object_types={"ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL", bake_anim=True,
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.5, path_mode="STRIP")
    print("[Rex] exported", OUT, [a.name for a in acts], flush=True)


def render(arm, acts, path):
    import rex_render
    rex_render.setup(samples=24, res=(1200, 700))
    sc = bpy.context.scene
    cam = bpy.data.objects.new("SwimCam", bpy.data.cameras.new("SwimCam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = (3.2, -3.2, 1.2)
    cam.rotation_euler = (math.radians(85), 0, math.radians(45))
    cam.data.lens = 35
    base, _ = os.path.splitext(path)
    for i, a in enumerate(acts):
        arm.animation_data.action = a
        sc.frame_set(int(a.frame_range[0] + (a.frame_range[1] - a.frame_range[0]) * float(os.environ.get("SWIM_T", "0.3"))))
        sc.render.filepath = f"{base}_{a.name}.png"
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
