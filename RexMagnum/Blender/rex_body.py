"""Rex Magnum - base body from MPFB (MakeHuman). Blender 5.x, MPFB 2 installed.

Rex: 58, medium height, thin arms, narrow shoulders, soft belly, strong tan.
"""
import os
import bpy

from bl_ext.user_default.mpfb.services.humanservice import HumanService
from bl_ext.user_default.mpfb.services.targetservice import TargetService
from bl_ext.user_default.mpfb.services.materialservice import MaterialService

TARGETS = os.path.join(os.path.dirname(TargetService.__module__ and __import__(
    "bl_ext.user_default.mpfb", fromlist=["x"]).__file__), "data", "targets")

# Age: 0.5 = 25 years, 1.0 = 90 years  ->  58 years = 0.5 + 33/65 * 0.5
MACRO = {"gender": 1.0, "age": 0.754, "muscle": 0.32, "weight": 0.58, "proportions": 0.45,
         "height": 0.5, "cupsize": 0.5, "firmness": 0.35,
         "race": {"asian": 0.0, "caucasian": 1.0, "african": 0.0}}

# (target file, weight): the "normal man who thinks he is a hero" shape
DETAILS = [
    ("stomach/stomach-pregnant-incr", 0.38),   # soft belly
    ("stomach/stomach-tone-decr", 0.5),
    ("torso/measure-shoulder-dist-decr", 0.45),  # narrow shoulders
    ("torso/torso-vshape-decr", 0.35),
    ("torso/torso-muscle-pectoral-decr", 0.4),
    ("torso/measure-waist-circ-incr", 0.3),
    ("arms/l-upperarm-muscle-decr", 0.6), ("arms/r-upperarm-muscle-decr", 0.6),   # thin arms
    ("arms/l-upperarm-scale-horiz-decr", 0.25), ("arms/r-upperarm-scale-horiz-decr", 0.25),
    ("arms/l-lowerarm-muscle-decr", 0.4), ("arms/r-lowerarm-muscle-decr", 0.4),
    ("head/head-age-incr", 0.3),
]


def build_body(name="Rex"):
    bm = HumanService.create_human(macro_detail_dict=MACRO)
    bm.name = name
    for rel, w in DETAILS:
        path = os.path.join(TARGETS, rel + ".target.gz")
        if os.path.exists(path):
            TargetService.load_target(bm, path, weight=w)
        else:
            print("[Rex] missing target", rel)
    mat = MaterialService.create_v2_skin_material("Rex_Skin", bm)
    return bm, mat
