"""E11-10 machine-room props. Metres, Blender Z up, operator faces -Y."""
import os
import sys
import math
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *
from kit_primary import flange


def electrical_panel():
    a = Asset("ElectricalPanel")
    p = a.part("Body")
    p.box((0, 0, 0.88), (1.25, 0.38, 1.76), C["bottle"], bevel=0.035, seg=3)
    p.box((0, -0.20, 0.95), (1.12, 0.035, 1.42), C["cream_d"], bevel=0.018)
    p.box((0, 0, 0.06), (1.32, 0.43, 0.12), C["steel_d"], bevel=0.02)
    for x in (-0.58, 0.58):
        for z in (0.3, 0.8, 1.3, 1.6):
            p.cyl((x, -0.224, z), 0.012, 0.016, C["brass_d"], axis="Y", segs=6)
    for row in range(4):
        for col in range(5):
            x, z = -0.40 + col*0.20, 0.54 + row*0.23
            p.box((x, -0.223, z), (0.14, 0.012, 0.155), C["bottle_d"], bevel=0.007)
            a.mount("Mount_Breaker%02d" % (row*5+col), (x, -0.25, z))
    for x in (-0.32, 0.32):
        a.mount("Mount_Gauge" + ("Left" if x < 0 else "Right"), (x, -0.24, 1.52))
    door = a.part("AccessDoor", location=(-0.51, -0.227, 0.26))
    door.box((0.51, 0, 0), (1.02, 0.025, 0.20), C["bottle_d"], bevel=0.012)
    door.box((0.92, -0.026, 0), (0.055, 0.04, 0.10), C["brass"], bevel=0.008)
    a.mount("Mount_Wall", (0, 0.19, 0.88))
    return a


def machine_breaker():
    a = Asset("MachineBreaker")
    p = a.part("Body")
    p.box((0, 0, 0), (0.09, 0.055, 0.13), C["bakelite"], bevel=0.009)
    p.box((0, -0.031, 0.042), (0.06, 0.008, 0.024), C["cream"], bevel=0.003)
    for z in (-0.052, 0.052):
        p.cyl((0.034, -0.032, z), 0.005, 0.012, C["brass_d"], axis="Y", segs=6)
    lever = a.part("Lever", location=(0, -0.037, -0.009))
    lever.box((0, -0.013, 0.017), (0.045, 0.031, 0.055), C["steel_l"], bevel=0.005)
    lever.box((0, -0.032, 0.03), (0.051, 0.017, 0.022), C["bakelite"], bevel=0.006)
    a.mount("Mount_Interaction", (0, -0.08, 0))
    return a


def bilge_pump():
    a = Asset("BilgePump")
    p = a.part("Body")
    p.box((0, 0, 0.05), (0.62, 0.48, 0.10), C["steel_d"], bevel=0.02)
    p.cyl((0, 0, 0.24), 0.205, 0.28, C["rust"], segs=24, bevel=0.016)
    p.cyl((0, 0, 0.52), 0.15, 0.32, C["bottle"], segs=20, bevel=0.015)
    for i in range(10):
        angle = math.tau*i/10
        p.box((math.sin(angle)*0.15, math.cos(angle)*0.15, 0.52), (0.018, 0.026, 0.23), C["bottle_d"], rot=(0, 0, -angle), bevel=0.003)
    for x in (-0.23, 0.23):
        for y in (-0.16, 0.16):
            p.cyl((x, y, 0.11), 0.018, 0.023, C["brass_d"], segs=6)
    p.cyl((0.25, 0, 0.25), 0.08, 0.28, C["rust_d"], axis="X", segs=16)
    flange(p, (0.39, 0, 0.25))
    # Bottom suction basket, slotted rather than a solid plate.
    for i in range(8):
        angle = math.tau*i/8
        p.box((math.sin(angle)*0.13, math.cos(angle)*0.13, 0.08), (0.018, 0.025, 0.12), C["steel"], rot=(0, 0, -angle))
    cap = a.part("Cap", location=(0, 0, 0.68))
    cap.cyl((0, 0, 0), 0.17, 0.08, C["bottle_d"], segs=20, bevel=0.012)
    rotor = a.part("Rotor", location=(0, 0, 0.735))
    rotor.cyl((0, 0, 0), 0.035, 0.022, C["brass"], segs=12)
    for i in range(4):
        angle = math.tau*i/4
        rotor.box((math.sin(angle)*0.065, math.cos(angle)*0.065, 0), (0.029, 0.12, 0.025), C["steel_l"], rot=(0, 0, -angle), bevel=0.005)
    a.mount("Mount_Inlet", (0, 0, 0.015), (math.pi, 0, 0))
    a.mount("Mount_Outlet", (0.418, 0, 0.25), (0, math.pi/2, 0))
    a.mount("Mount_Floor", (0, 0, 0))
    a.mount("Mount_Leak", (0, -0.21, 0.25))
    return a


def interphone():
    a = Asset("Interphone")
    p = a.part("Body")
    p.box((0, 0, 0), (0.28, 0.10, 0.44), C["bottle"], bevel=0.025, seg=3)
    p.box((0, -0.057, 0.13), (0.21, 0.016, 0.05), C["cream"], bevel=0.006)
    for x in (-0.092, 0.092):
        p.box((x, -0.09, 0.025), (0.029, 0.07, 0.08), C["brass_d"], bevel=0.007)
    p.cyl((0, -0.065, -0.14), 0.047, 0.024, C["bakelite"], axis="Y", segs=18, bevel=0.005)
    handset = a.part("Handset", location=(0, -0.123, 0.01))
    handset.box((0, 0, 0), (0.25, 0.045, 0.055), C["bakelite"], bevel=0.02, seg=3)
    for x in (-0.135, 0.135):
        handset.sphere((x, -0.008, 0), (0.057, 0.057, 0.065), C["bakelite"], u=16, v=10)
        handset.cyl((x, -0.055, 0), 0.036, 0.012, C["steel_d"], axis="Y", segs=16)
        for z in (-0.014, 0, 0.014):
            handset.box((x, -0.063, z), (0.04, 0.006, 0.005), C["black"])
    cable = a.part("Cord", location=(0.135, -0.123, -0.055))
    # Continuous helical tube; separate rings would leave gaps in the telephone cord.
    before = cable._snapshot()
    rings = []
    for i in range(169):
        angle = math.tau*i/12
        center = Vector((0.016*math.cos(angle), 0.016*math.sin(angle), -i*0.00135))
        tangent = Vector((-0.016*math.sin(angle), 0.016*math.cos(angle), -0.00135*12/math.tau)).normalized()
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        normal = tangent.cross(radial).normalized()
        ring = []
        for j in range(6):
            phase = math.tau*j/6
            ring.append(cable.bm.verts.new(center + 0.005*(radial*math.cos(phase)+normal*math.sin(phase))))
        rings.append(ring)
    for i in range(len(rings)-1):
        for j in range(6):
            k = (j+1)%6
            cable.bm.faces.new((rings[i][j], rings[i][k], rings[i+1][k], rings[i+1][j]))
    cable.bm.faces.new(rings[0][::-1])
    cable.bm.faces.new(rings[-1])
    cable._finish_prim(before, Matrix.Identity(4), C["black"], 0)
    a.mount("Mount_Wall", (0, 0.05, 0))
    a.mount("Mount_HandsetGrip", (0, -0.123, 0.01))
    return a


def turbine():
    a = Asset("TurbineReducer")
    p = a.part("Body")
    p.box((0, 0, 0.08), (2.25, 1.03, 0.16), C["steel_d"], bevel=0.035)
    for x in (-0.68, 0.22):
        p.box((x, 0, 0.28), (0.19, 0.74, 0.40), C["bottle_d"], bevel=0.025)
        p.disc_ring((x, 0, 0.65), 0.29, 0.39, 0.12, C["steel"], axis="X", segs=28, bevel=0.006)
    p.box((0.75, 0, 0.49), (0.65, 0.65, 0.62), C["bottle"], bevel=0.055, seg=3)
    p.box((0.75, -0.33, 0.52), (0.35, 0.018, 0.12), C["brass_d"], bevel=0.006)
    for z in (0.29, 0.36, 0.43):
        p.box((0.75, -0.336, z), (0.45, 0.016, 0.025), C["bottle_d"], bevel=0.004)
    rotor = a.part("Rotor", location=(-0.25, 0, 0.65))
    rotor.cyl((0.10, 0, 0), 0.067, 1.78, C["steel_l"], axis="X", segs=18, bevel=0.006)
    for x in (-0.28, -0.08, 0.12, 0.32):
        rotor.cyl((x, 0, 0), 0.16, 0.055, C["brass_d"], axis="X", segs=24)
        for i in range(12):
            angle = math.tau*i/12
            rotor.box((x, math.sin(angle)*0.23, math.cos(angle)*0.23), (0.10, 0.04, 0.15), C["steel"], rot=(-angle, 0, 0), bevel=0.004)
    cover = a.part("Cover", location=(-0.25, 0.35, 0.65))
    # Upper semi-cylinder shell, hinged along the rear edge.
    for i in range(13):
        angle = math.pi*i/12
        cover.box((0, -0.35+math.cos(angle)*0.35, math.sin(angle)*0.35), (0.87, 0.105, 0.055), C["bottle"], rot=(angle+math.pi/2, 0, 0), bevel=0.006)
    cover.box((0, -0.70, 0.04), (0.23, 0.04, 0.04), C["brass"], bevel=0.01)
    a.mount("Mount_Shaft", (1.105, 0, 0.65), (0, math.pi/2, 0))
    a.mount("Mount_SteamInlet", (-0.65, 0.25, 0.90))
    a.mount("Mount_Gauge", (0.74, -0.34, 0.71))
    a.mount("Mount_Floor", (0, 0, 0))
    return a


def battery_bank():
    a = Asset("BatteryBank")
    p = a.part("Body")
    p.box((0, 0, 0.055), (1.15, 0.53, 0.11), C["steel_d"], bevel=0.025)
    for i in range(4):
        x = -0.405+i*0.27
        p.box((x, 0, 0.27), (0.235, 0.42, 0.34), C["bakelite"], bevel=0.02)
        p.box((x, 0, 0.455), (0.25, 0.44, 0.065), C["cream_d"], bevel=0.015)
        p.box((x, -0.217, 0.28), (0.16, 0.014, 0.085), C["cream"], bevel=0.004)
        for y in (-0.13, 0.13):
            p.cyl((x, y, 0.51), 0.023, 0.065, C["lead"], segs=12, bevel=0.004)
        if i < 3:
            p.box((x+0.135, 0.13 if i%2 == 0 else -0.13, 0.535), (0.28, 0.025, 0.015), C["brass"], bevel=0.004)
    for y in (-0.23, 0.23):
        p.box((0, y, 0.37), (1.14, 0.026, 0.045), C["steel"], bevel=0.006)
    p.box((0.49, -0.05, 0.70), (0.13, 0.18, 0.28), C["bottle"], bevel=0.012)
    cover = a.part("TerminalCover", location=(-0.55, 0, 0.61))
    # Cage protects terminals while preserving visible connections.
    for y in (-0.23, 0.23):
        cover.box((0.55, y, 0), (1.12, 0.026, 0.025), C["steel_d"], bevel=0.005)
    for i in range(6):
        cover.box((i*0.22, 0, 0), (0.022, 0.48, 0.025), C["steel_d"], bevel=0.005)
    a.mount("Mount_Voltmeter", (0.49, -0.15, 0.73))
    a.mount("Mount_Sparks", (-0.405, -0.13, 0.545))
    a.mount("Mount_Floor", (0, 0, 0))
    return a


def build_all():
    return [electrical_panel(), machine_breaker(), bilge_pump(), interphone(), turbine(), battery_bank()]


def assemble_panel(panel):
    # Preview/source assembly only. FBX panel contains sockets; breaker is reusable.
    from kit_instruments import gauge_round
    for name, loc, rot in panel.mounts:
        if name.startswith("Mount_Breaker"):
            index = int(name[-2:])
            module = machine_breaker()
            module.name = "PanelBreaker%02d" % index
            module.build()
            module.root.parent = panel.root
            module.root.location = loc
            module.parts[1].ob.rotation_euler.x = math.radians(28 if index in (3, 12) else -28)
        elif name.startswith("Mount_Gauge"):
            module = gauge_round("PanelGauge" + name[-4:], 0.10)
            module.build()
            module.root.parent = panel.root
            module.root.location = loc
    bpy.context.view_layer.update()


if __name__ == "__main__":
    out_dir, preview = cli_args()
    reset_scene()
    assets = build_all()
    report = {"blender": bpy.app.version_string, "issue": 118, "assets": []}
    for a in assets:
        a.build()
    bpy.context.view_layer.update()
    for a in assets:
        for p in a.parts:
            assert p.ob.data.vertices and "Col" in p.ob.data.color_attributes
            assert all(math.isfinite(c) for v in p.ob.data.vertices for c in v.co)
            p.ob.data.calc_loop_triangles()
            assert all(t.area > 1e-12 for t in p.ob.data.loop_triangles), (a.name, p.name, "zero-area triangle")
        a.export(out_dir)
        report["assets"].append({"name": a.name, "triangles": a.stats(), "parts": [p.name for p in a.parts], "mounts": [m[0] for m in a.mounts]})
        print("EXPORT %-22s %5d triangles" % (a.name, a.stats()))
    with open(os.path.join(out_dir, "machines_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2)
    assemble_panel(assets[0])
    from kit_instruments import gauge_round
    for a, mount_name, radius in ((assets[4], "Mount_Gauge", 0.065), (assets[5], "Mount_Voltmeter", 0.045)):
        loc = next(m[1] for m in a.mounts if m[0] == mount_name)
        module = gauge_round(a.name + "Gauge", radius)
        module.build()
        module.root.parent = a.root
        module.root.location = loc
    bpy.context.view_layer.update()
    source_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources")
    os.makedirs(source_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, "Machines.blend"))
    if preview:
        os.makedirs(preview, exist_ok=True)
        render_each(assets, preview)
        # Show the maintenance state separately; rest transforms stay in the source.
        cover = assets[4].parts[2].ob
        cover.rotation_euler.x = math.radians(-105)
        render_each(assets, preview,
                    views=(("open", (0.8, -1, 0.65)),))
        print("PREVIEW", preview)
