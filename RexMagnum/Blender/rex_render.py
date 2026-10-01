"""Approval renders (Cycles): front, side, 3/4 and face. Lights per the character rules."""
import math
import os

import bpy
from mathutils import Vector


def _look(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def _area(name, loc, energy, color, size, target):
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
        bpy.context.scene.collection.objects.link(ob)
    ob.data.energy, ob.data.color, ob.data.size = energy, color, size
    ob.location = loc
    _look(ob, target)
    return ob


def setup(samples=96, res=(900, 1200)):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
            try:
                prefs.compute_device_type = kind
            except TypeError:
                continue
            prefs.get_devices()
            if any(d.type == kind for d in prefs.devices):
                for d in prefs.devices:
                    d.use = d.type == kind
                sc.cycles.device = "GPU"
                break
    except KeyError:
        pass
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "AgX"
    # World: the camera sees dark grey (strength 0.2). Reflections see a soft sky,
    # so mirror sunglasses and gold show their real look.
    w = bpy.data.worlds.get("Rex_World") or bpy.data.worlds.new("Rex_World")
    w.use_nodes = True
    nt = w.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    dark = nt.nodes.new("ShaderNodeBackground")
    dark.inputs[0].default_value = (0.05, 0.05, 0.055, 1)
    dark.inputs[1].default_value = 0.2
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0.45, (0.08, 0.06, 0.05, 1)
    e[1].position, e[1].color = 0.75, (0.55, 0.7, 0.95, 1)
    nt.links.new(sep.outputs[2], ramp.inputs[0])
    sky = nt.nodes.new("ShaderNodeBackground")
    sky.inputs[1].default_value = 0.6
    nt.links.new(ramp.outputs[0], sky.inputs[0])
    lp = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(sky.outputs[0], mix.inputs[1])
    nt.links.new(dark.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    sc.world = w
    # Lights (character rules): key warm 220 W, fill cool 45 W, rim 140 W
    tgt = (0, 0, 1.1)
    _area("R_Key", (-1.8, -2.4, 2.4), 220, (1.0, 0.9, 0.8), 1.2, tgt)
    _area("R_Fill", (2.4, -1.8, 1.4), 45, (0.75, 0.85, 1.0), 1.5, tgt)
    _area("R_Rim", (1.0, 2.6, 2.4), 140, (1.0, 0.95, 0.9), 1.0, tgt)
    if "R_Floor" not in bpy.data.objects:
        me = bpy.data.meshes.new("R_Floor")
        s = 8
        me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
        fl = bpy.data.objects.new("R_Floor", me)
        sc.collection.objects.link(fl)
        m = bpy.data.materials.new("R_FloorMat")
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (0.03, 0.03, 0.032, 1)
        b.inputs["Roughness"].default_value = 0.6
        me.materials.append(m)


def shot(out, name, loc, target, lens):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("R_Cam")
    if cam is None:
        cam = bpy.data.objects.new("R_Cam", bpy.data.cameras.new("R_Cam"))
        sc.collection.objects.link(cam)
    cam.location = loc
    cam.data.lens = lens
    _look(cam, target)
    sc.camera = cam
    sc.render.filepath = os.path.join(out, name)
    bpy.ops.render.render(write_still=True)
    print("[Rex] rendered", name)


def approval_set(out, height=1.73, prefix="rex"):
    os.makedirs(out, exist_ok=True)
    mid = (0, 0, height * 0.52)
    d = 5.2
    shot(out, f"{prefix}_front.png", (0, -d, height * 0.55), mid, 70)
    shot(out, f"{prefix}_side.png", (d, 0, height * 0.55), mid, 70)
    shot(out, f"{prefix}_three_quarter.png", (-d * 0.7, -d * 0.7, height * 0.62), mid, 70)
    face = (0, 0, height * 0.93)
    shot(out, f"{prefix}_face.png", (0.25, -1.15, height * 0.95), face, 85)
