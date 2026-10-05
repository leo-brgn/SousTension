"""E11-08 primary circuit: reusable pump and valve, metres, Z-up, front -Y."""
import os
import sys
import math
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *


def flange(p, center, axis="X"):
    p.cyl(center, 0.145, 0.055, C["steel"], axis=axis, segs=24, bevel=0.005)
    for i in range(8):
        angle = math.tau * i / 8
        offset = (0, math.sin(angle)*0.112, math.cos(angle)*0.112) if axis == "X" else (math.sin(angle)*0.112, math.cos(angle)*0.112, 0)
        pos = tuple(center[j] + offset[j] for j in range(3))
        p.cyl(pos, 0.013, 0.067, C["brass_d"], axis=axis, segs=6)


def primary_pump(name="PrimaryPump", damaged=False):
    a = Asset(name)
    body = a.part("Body")
    body.box((0, 0, 0.055), (0.92, 0.58, 0.11), C["steel_d"], bevel=0.025)
    for x in (-0.36, 0.36):
        for y in (-0.21, 0.21):
            body.cyl((x, y, 0.12), 0.022, 0.025, C["brass_d"], segs=6)
    body.cyl((0.18, 0, 0.33), 0.23, 0.48, C["bottle"], axis="X", segs=24, bevel=0.018)
    for i in range(12):
        ang = math.tau*i/12
        body.box((0.18, math.sin(ang)*0.224, 0.33+math.cos(ang)*0.224), (0.36, 0.026, 0.028), C["bottle_d"], rot=(-ang, 0, 0), bevel=0.004)
    body.cyl((-0.24, 0, 0.33), 0.24, 0.22, C["rust"], axis="X", segs=24, bevel=0.018)
    body.cyl((-0.43, 0, 0.33), 0.095, 0.18, C["steel_d"], axis="X", segs=16)
    flange(body, (-0.51, 0, 0.33))
    body.cyl((-0.24, 0, 0.60), 0.095, 0.22, C["rust_d"], segs=16)
    flange(body, (-0.24, 0, 0.71), "Z")
    body.box((0.18, -0.237, 0.33), (0.22, 0.015, 0.09), C["cream"], bevel=0.008)
    body.box((0.44, -0.04, 0.60), (0.16, 0.18, 0.18), C["bakelite"], bevel=0.018)
    rotor = a.part("Rotor", location=(0.43, 0, 0.33))
    rotor.cyl((0, 0, 0), 0.15, 0.022, C["steel_d"], axis="X", segs=20)
    for i in range(6):
        ang = math.tau*i/6
        rotor.box((0.014, math.sin(ang)*0.08, math.cos(ang)*0.08), (0.025, 0.033, 0.13), C["steel_l"], rot=(-ang, 0, 0), bevel=0.004)
    lever = a.part("Switch", location=(0.44, -0.14, 0.61), rotation=(math.radians(25 if damaged else -25), 0, 0))
    lever.cyl((0, 0, 0.025), 0.009, 0.05, C["brass"], segs=10)
    lever.sphere((0, 0, 0.06), 0.022, C["bakelite"])
    cover = a.part("Cover", location=(0.48, 0, 0.33), rotation=(0, 0.28 if damaged else 0, 0))
    # Open cage keeps the rotor visible so rotation can convey the running state.
    cover.disc_ring((0, 0, 0), 0.17, 0.205, 0.035, C["bottle"], axis="X", segs=24)
    for i in range(5):
        offset = (i-2)*0.064
        half = math.sqrt(0.17**2-offset**2)
        cover.box((0.024, offset, 0), (0.018, 0.016, half*2), C["steel"], bevel=0.003)
    if damaged:
        for i in range(3):
            body.box((-0.26+i*0.055, -0.23, 0.38-i*0.037), (0.068, 0.014, 0.023), C["black"], rot=(0, 0.55, 0))
    a.mount("Mount_Inlet", (-0.54, 0, 0.33), (0, -math.pi/2, 0))
    a.mount("Mount_Outlet", (-0.24, 0, 0.74))
    a.mount("Mount_Control", (0.44, -0.18, 0.61))
    a.mount("Mount_Floor", (0, 0, 0))
    a.mount("Mount_Leak", (-0.24, -0.23, 0.33))
    return a


def primary_valve(name="PrimaryValve"):
    a = Asset(name)
    body = a.part("Body")
    body.cyl((0, 0, 0), 0.095, 0.48, C["rust"], axis="X", segs=20, bevel=0.01)
    flange(body, (-0.24, 0, 0))
    flange(body, (0.24, 0, 0))
    body.sphere((0, 0, 0), (0.155, 0.15, 0.17), C["rust_d"])
    body.cyl((0, -0.16, 0), 0.082, 0.20, C["brass_d"], axis="Y", segs=16, bevel=0.008)
    body.box((0, -0.25, 0.19), (0.20, 0.025, 0.055), C["cream"], bevel=0.006)
    wheel = a.part("Wheel", location=(0, -0.30, 0))
    wheel.torus((0, 0, 0), 0.235, 0.024, C["brass_d"], axis="Y", major_segs=32)
    for i in range(4):
        ang = math.tau*i/4 + math.pi/4
        wheel.box((math.sin(ang)*0.11, 0, math.cos(ang)*0.11), (0.029, 0.029, 0.22), C["brass"], rot=(0, ang, 0), bevel=0.006)
    wheel.cyl((0, 0, 0), 0.046, 0.07, C["bakelite"], axis="Y", segs=16, bevel=0.005)
    indicator = a.part("Indicator", location=(0, -0.235, 0.19))
    indicator.box((0, -0.025, 0), (0.018, 0.015, 0.048), C["black"], bevel=0.002)
    a.mount("Mount_A", (-0.268, 0, 0), (0, -math.pi/2, 0))
    a.mount("Mount_B", (0.268, 0, 0), (0, math.pi/2, 0))
    a.mount("Mount_Interaction", (0, -0.34, 0))
    a.mount("Mount_Leak", (0, -0.16, -0.08))
    return a


def build_all():
    return [primary_pump(), primary_pump("PrimaryPump_Broken", True), primary_valve()]


if __name__ == "__main__":
    out_dir, preview = cli_args()
    reset_scene()
    assets = build_all()
    report = {"blender": bpy.app.version_string, "issue": 116, "assets": []}
    for a in assets:
        a.build()
    bpy.context.view_layer.update()
    for a in assets:
        for p in a.parts:
            assert p.ob.data.vertices and "Col" in p.ob.data.color_attributes
            assert all(math.isfinite(c) for v in p.ob.data.vertices for c in v.co)
        a.export(out_dir)
        report["assets"].append({"name": a.name, "triangles": a.stats(), "parts": [p.name for p in a.parts], "mounts": [m[0] for m in a.mounts]})
        print("EXPORT %-22s %5d triangles" % (a.name, a.stats()))
    with open(os.path.join(out_dir, "primary_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2)
    source_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources")
    os.makedirs(source_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, "PrimaryCircuit.blend"))
    if preview:
        os.makedirs(preview, exist_ok=True)
        render_each(assets, preview)
        print("PREVIEW", preview)
