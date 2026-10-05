"""P1 compartment ceiling and battery emergency lighting models."""
import os
import sys
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *


def ceiling():
    a = Asset("CompartmentCeilingLight")
    p = a.part("Body")
    p.cyl((0, 0, -0.03), 0.25, 0.06, C["brass_d"], segs=28, bevel=0.009)
    p.disc_ring((0, 0, -0.092), 0.19, 0.235, 0.05, C["steel_d"], segs=28)
    for i in range(6):
        ang = math.tau*i/6
        p.cyl((0.216*math.cos(ang), 0.216*math.sin(ang), -0.123), 0.011, 0.017, C["steel_l"], segs=6)
    lens = a.part("Lens", location=(0, 0, -0.093))
    lens.sphere((0, 0, 0), (0.19, 0.19, 0.053), C["white"], u=24, v=12)
    a.mount("Mount_Light", (0, 0, -0.17))
    return a


def emergency():
    a = Asset("EmergencyLamp")
    p = a.part("Body")
    p.box((0, 0, 0), (0.20, 0.11, 0.29), C["bottle_d"], bevel=0.018)
    p.disc_ring((0, -0.075, 0.03), 0.058, 0.083, 0.035, C["brass_d"], axis="Y", segs=24)
    for x in (-0.07, 0, 0.07):
        p.box((x, -0.145, 0.03), (0.01, 0.01, 0.17), C["steel_d"], bevel=0.002)
    p.box((0, -0.139, 0.03), (0.17, 0.01, 0.01), C["steel_d"], bevel=0.002)
    p.box((0, -0.058, -0.105), (0.14, 0.012, 0.03), C["cream"], bevel=0.003)
    lens = a.part("Lens", location=(0, -0.087, 0.03))
    lens.sphere((0, 0, 0), (0.059, 0.053, 0.059), C["red"], u=20, v=12)
    a.mount("Mount_Light", (0, -0.17, 0.03))
    a.mount("Mount_Wall", (0, 0.055, 0))
    return a


def build_all():
    return [ceiling(), emergency()]


if __name__ == "__main__":
    out, preview = cli_args()
    reset_scene()
    assets = build_all()
    for a in assets:
        a.build()
    bpy.context.view_layer.update()
    entries = []
    for a in assets:
        a.export(out)
        entries.append({"name": a.name, "triangles": a.stats(), "parts": [p.name for p in a.parts], "mounts": [m[0] for m in a.mounts]})
        print("EXPORT", a.name, a.stats())
    with open(os.path.join(out, "lighting_manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"blender": bpy.app.version_string, "issue": 111, "assets": entries}, f, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__), "sources", "Lighting.blend"))
    if preview:
        os.makedirs(preview, exist_ok=True)
        render_each(assets, preview)
