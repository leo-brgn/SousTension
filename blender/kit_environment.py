"""First whole-boat environment, GDD 4.1 / E10-01. Blender coordinates in metres."""
import os
import sys
import math
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *
from kit_structure import build_all as structure_assets
from kit_instruments import build_all as instrument_assets
from kit_reactor import build_all as reactor_assets
from kit_primary import build_all as primary_assets
from kit_machines import build_all as machine_assets
from kit_lighting import build_all as lighting_assets
import kit_central as central
from kit_signage import build_all as signage_assets, LABELS
from kit_radio import build_all as radio_assets
from kit_living import build_all as living_assets
from kit_torpedoes import build_all as torpedo_assets


def hull():
    a = Asset("EnvironmentHull_2m")
    for side, name in ((-1, "PortWall"), (1, "StarboardWall")):
        p = a.part(name)
        p.box((side*3.05, 0, 1.05), (0.10, 2, 2.10), C["bottle"], bevel=0.015)
        p.box((side*2.80, 0, 2.30), (0.5657, 2, 0.10), C["bottle"], rot=(0, side*math.pi/4, 0), bevel=0.012)
        for y in (-0.9, 0, 0.9):
            p.box((side*2.95, y, 1.05), (0.10, 0.13, 2.10), C["steel_d"], bevel=0.009)
            for z in (0.3, 0.7, 1.1, 1.5, 1.9):
                p.box((side*2.89, y, z), (0.025, 0.035, 0.035), C["steel_l"])
        for z in (0.9, 1.7):
            p.box((side*2.99, 0, z), (0.025, 2, 0.045), C["bottle_d"])
    p = a.part("Roof")
    p.box((0, 0, 2.55), (5.2, 2, 0.10), C["bottle"], bevel=0.012)
    for y in (-0.9, 0, 0.9):
        p.box((0, y, 2.45), (5.2, 0.13, 0.10), C["steel_d"], bevel=0.009)
    p.box((-1.8, 0, 2.35), (0.35, 2, 0.045), C["steel"])
    for x in (-1.9, -1.8, -1.7):
        p.cyl((x, 0, 2.39), 0.023, 2, C["black"], axis="Y", segs=8)
    return a


def offset_bulkhead(side):
    a = Asset("Bulkhead_" + ("Left" if side < 0 else "Right"))
    p = a.part("Plate")
    x, half, top = side*1.45, 0.45, 1.76
    for left, right in ((-3, x-half), (x+half, 3)):
        p.box(((left+right)/2, 0, 1.25), (right-left, 0.14, 2.5), C["bottle"], bevel=0.012)
    p.box((x, 0, (2.5+top)/2), (0.9, 0.14, 2.5-top), C["bottle"], bevel=0.012)
    p.box((x, 0, 0.03), (0.9, 0.14, 0.06), C["steel_d"], bevel=0.009)
    for y in (-0.10, 0.10):
        for sx in (-1, 1):
            p.box((x+sx*0.49, y, 0.91), (0.08, 0.06, 1.78), C["steel"], bevel=0.009)
        p.box((x, y, 1.80), (1.06, 0.06, 0.08), C["steel"], bevel=0.009)
        for px in (-2.5, 0, 2.5):
            if abs(px-x) > 0.6:
                p.box((px, y, 1.25), (0.09, 0.07, 2.35), C["steel_d"], bevel=0.01)
    a.mount("Mount_Hinge", (x-0.45, -0.10, 0.06))
    return a


def ceiling_light():
    a = Asset("CeilingLight")
    p = a.part("Body")
    p.cyl((0, 0, -0.025), 0.22, 0.05, C["brass_d"], segs=24, bevel=0.006)
    p.cyl((0, 0, -0.075), 0.18, 0.065, C["cream"], segs=24, bevel=0.008)
    for x in (-0.14, 0, 0.14):
        p.box((x, 0, -0.115), (0.012, 0.31 if x == 0 else 0.21, 0.015), C["steel_d"])
    a.mount("Mount_Light", (0, 0, -0.15))
    return a


def airlock_exit_bulkhead():
    """Aft boundary with a real opening aligned to the chamber's outer hatch."""
    a = Asset("Bulkhead_AirlockExit"); p = a.part("Plate")
    x, half, top = 1.95, .47, 1.97
    for left, right in ((-3, x-half), (x+half, 3)):
        p.box(((left+right)/2, 0, 1.25), (right-left, .14, 2.5), C['bottle'], bevel=.012)
    p.box((x, 0, (top+2.5)/2), (.94, .14, 2.5-top), C['bottle'], bevel=.012)
    for sx in (-1, 1):
        p.box((x+sx*.49, .10, .98), (.06, .06, 1.96), C['steel'], bevel=.008)
    a.mount('Mount_OuterHatch', (x, .19, .12))
    a.mount('Mount_ExteriorExit', (x, -.25, .12))
    return a


def privacy_baffle():
    a = Asset("PrivacyBaffle")
    p = a.part("Body")
    p.box((0, 0, 1.10), (1.60, 0.12, 2.20), C["bottle"], bevel=0.014)
    for x in (-0.76, 0.76):
        p.box((x, 0, 1.10), (0.08, 0.18, 2.20), C["steel_d"], bevel=0.01)
    p.box((0, 0, 2.22), (1.70, 0.18, 0.08), C["cream_d"], bevel=0.01)
    return a


def helm():
    a = Asset("HelmConsole")
    p = a.part("Body")
    p.box((0, 0, 0.06), (1.65, 0.64, 0.12), C["steel_d"], bevel=0.02)
    p.box((0, 0, 0.59), (1.55, 0.58, 1.08), C["bottle"], bevel=0.035)
    p.box((0, -0.06, 1.37), (1.6, 0.14, 0.63), C["cream_d"], rot=(-0.2, 0, 0), bevel=0.025)
    p.box((0, -0.30, 0.90), (1.32, 0.022, 0.12), C["brass_d"], bevel=0.006)
    a.mount("Mount_Wheel", (0, -0.39, 0.84))
    for i in range(3):
        a.mount("Mount_Gauge%d" % i, (-0.47+i*0.47, -0.16, 1.44), (-0.2, 0, 0))
    return a


def chart_table():
    a = Asset("ChartTable")
    p = a.part("Body")
    p.box((0, 0, 0.92), (1.60, 0.96, 0.10), C["bottle"], bevel=0.025)
    for x in (-0.64, 0.64):
        for y in (-0.34, 0.34):
            p.box((x, y, 0.44), (0.10, 0.10, 0.88), C["steel_d"], bevel=0.012)
    paper = a.part("Paper", location=(0, 0, 0.982))
    paper.box((0, 0, 0), (1.34, 0.73, 0.012), C["cream"], bevel=0.002)
    for i in range(7):
        paper.box((-0.54+i*0.18, 0, 0.008), (0.005, 0.64, 0.003), C["cream_d"])
    for i in range(4):
        paper.box((0, -0.27+i*0.18, 0.008), (1.18, 0.005, 0.003), C["cream_d"])
    a.mount("Mount_Manual", (0.40, -0.1, 1.0))
    return a


def cargo_rack():
    a = Asset("CargoRack")
    p = a.part("Body")
    for x in (-0.80, 0.80):
        for y in (-0.32, 0.32):
            p.box((x, y, 0.85), (0.07, 0.07, 1.7), C["steel_d"], bevel=0.008)
    for z in (0.15, 0.82, 1.49):
        p.box((0, 0, z), (1.7, 0.72, 0.06), C["steel"], bevel=0.008)
    return a


def crate(name="CargoCrate_M", scale=1.0):
    a = Asset(name)
    p = a.part("Body")
    p.box((0, 0, 0.25), (0.62, 0.48, 0.50), C["cream_d"], bevel=0.02)
    for x in (-0.21, 0.21):
        p.box((x, 0, 0.25), (0.045, 0.49, 0.51), C["bottle_d"], bevel=0.006)
    p.box((0, -0.248, 0.25), (0.28, 0.015, 0.12), C["cream"], bevel=0.004)
    a.mount("Mount_Grip", (0, 0, 0.25))
    if scale != 1:
        for v in p.bm.verts:
            v.co *= scale
        a.mounts = [(n, tuple(c*scale for c in loc), rot) for n, loc, rot in a.mounts]
    return a


def bunks():
    a = Asset("BunkPair")
    p = a.part("Body")
    for x in (-0.90, 0.90):
        for y in (-0.35, 0.35):
            p.box((x, y, 0.93), (0.06, 0.06, 1.86), C["steel_d"], bevel=0.006)
    for z in (0.38, 1.22):
        p.box((0, 0, z), (1.9, 0.82, 0.08), C["steel"], bevel=0.015)
        p.box((0, 0, z+0.095), (1.78, 0.72, 0.13), C["cream_d"], bevel=0.035)
        p.box((0.68, 0, z+0.20), (0.35, 0.57, 0.09), C["cream"], bevel=0.03)
        p.box((-0.22, 0, z+0.17), (1.20, 0.74, 0.045), C["bottle"], bevel=0.008)
    return a


def radio_console():
    a = Asset("RadioConsole_Blockout")
    p = a.part("Body")
    p.box((0, 0, 0.55), (1.3, 0.65, 1.1), C["bottle"], bevel=0.025)
    p.box((0, -0.1, 1.23), (1.26, 0.2, 0.40), C["bakelite"], bevel=0.025)
    p.disc_ring((-0.28, -0.22, 1.27), 0.105, 0.14, 0.025, C["brass"], axis="Y", segs=24)
    p.cyl((-0.28, -0.212, 1.27), 0.105, 0.012, C["bottle_d"], axis="Y", segs=24)
    for x in (0.16, 0.40):
        p.cyl((x, -0.22, 1.27), 0.074, 0.018, C["steel"], axis="Y", segs=18)
        p.cyl((x, -0.235, 1.27), 0.022, 0.025, C["bakelite"], axis="Y", segs=12)
    p.box((0, -0.35, 1.03), (1.30, 0.34, 0.06), C["cream_d"], bevel=0.008)
    a.mount("Mount_VUMeter", (0, -0.22, 1.27))
    return a


def galley():
    a = Asset("Galley_Blockout")
    p = a.part("Body")
    p.box((0, 0, 0.45), (1.30, 0.65, 0.90), C["cream_d"], bevel=0.025)
    p.box((0, 0, 0.94), (1.36, 0.72, 0.08), C["steel"], bevel=0.02)
    for x in (-0.4, 0, 0.4):
        p.box((x, -0.335, 0.58), (0.30, 0.025, 0.55), C["bottle"], bevel=0.01)
        p.box((x, -0.36, 0.73), (0.14, 0.025, 0.025), C["brass_d"], bevel=0.004)
    p.cyl((0.25, 0, 1.11), 0.18, 0.25, C["steel_d"], segs=20, bevel=0.008)
    p.cyl((0.25, 0, 1.25), 0.195, 0.025, C["steel"], segs=20, bevel=0.004)
    return a


def build_all():
    return [hull(), offset_bulkhead(-1), offset_bulkhead(1), ceiling_light(), privacy_baffle(), helm(), chart_table(),
            cargo_rack(), crate(), crate("CargoCrate_S", 0.65), crate("CargoCrate_L", 1.35), bunks(), radio_console(), galley(), airlock_exit_bulkhead()]


def layout():
    rooms = [
        {"id": "01_Torpedoes", "label": "Torpilles", "start": 10, "end": 14},
        {"id": "02_Central", "label": "Poste central", "start": 4, "end": 10},
        {"id": "03_Radio", "label": "Radio et sonar", "start": 0, "end": 4},
        {"id": "04_Reactor", "label": "Réacteur", "start": -4, "end": 0},
        {"id": "05_Machines", "label": "Machines", "start": -8, "end": -4},
        {"id": "06_Living", "label": "Sas et vie", "start": -14, "end": -8}]
    data = {"length": 28, "width": 6, "height": 2.5, "rooms": rooms, "instances": [], "stations": []}
    def add(room, kit, asset, position, rotation=(0, 0, 0), parent=None, suffix="", part_states=None):
        ident = "%s_%s_%03d%s" % (room, asset, len(data["instances"]), suffix)
        entry = {"id": ident, "room": room, "kit": kit, "asset": asset, "position": position,
                 "rotation": rotation, "parent": parent or "", "partStates": part_states or []}
        data["instances"].append(entry)
        return ident
    for room in rooms:
        rid, lo, hi = room["id"], room["start"], room["end"]
        mid = (lo+hi)/2
        for y in range(lo+1, hi, 2):
            add(rid, "Environment", "EnvironmentHull_2m", (0, y, 0))
            add(rid, "Structure", "BilgeFloor_2m", (0, y, 0))
        for y in range(lo, hi):
            for x in range(-3, 3):
                add(rid, "Structure", "FloorGrating_1m", (x+0.5, y+0.5, 0))
        add(rid, "Lighting", "CompartmentCeilingLight", (0, mid, 2.44))
        add(rid, "Lighting", "EmergencyLamp", (-2.92, mid+0.6, 1.95), (0, 0, 90))
        add(rid, "Machines", "Interphone", (2.84, mid+0.9, 1.40), (0, 0, -90))
        for y in range(lo, hi):
            add(rid, "Structure", "Pipe_Straight_1m", (-2.70, y+0.5, 2.12))
        add(rid, "Signage", LABELS[rooms.index(room)][0], (-2.885, mid-0.8, 1.78), (0, 0, 90))
    for i, (y, side) in enumerate(((10, -1), (4, 1), (0, -1), (-4, 1), (-8, -1))):
        rid = rooms[i+1]["id"]
        add(rid, "Environment", "Bulkhead_Left" if side < 0 else "Bulkhead_Right", (0, y, 0))
        add(rid, "Structure", "HatchDoor", (side*1.45-0.45, y-0.10, 0.06), (0, 0, 105 if side > 0 else -105))
        add(rid, "Environment", "PrivacyBaffle", (side*1.45, y-0.95, 0))
    add(rooms[0]['id'], "Structure", "Bulkhead_Solid", (0, 14, 0))
    add(rooms[-1]['id'], "Environment", "Bulkhead_AirlockExit", (0, -14, 0))
    # Work stations face into the boat; side-wall placement leaves circulation clear.
    add("02_Central", "Central", "CentralDepthTrimConsole", (-2.18, 7.3, 0), (0, 0, 90))
    add("02_Central", "Central", "CentralSteeringWheel", (-1.9, 8.3, 0), (0, 0, 90))
    add("02_Central", "Central", "EngineTelegraphFivePosition", (-2.3, 5.75, 0), (0, 0, 90))
    table = add("02_Central", "Central", "CentralChartTable", (2.05, 6.1, 0), (0, 0, -90))
    add("02_Central", "Central", "ChartPaperBlank", (0, 0, 0.936), parent=table)
    add("02_Central", "Central", "ManualOK114OpenBinder", (0.20, 0.05, 0.946), parent=table)
    add("02_Central", "Central", "GreasePencil", (-0.48, -0.32, 0.94), (90, 0, 0), table)
    add("02_Central", "Central", "CentralInstrumentConsole", (2.25, 8.05, 0), (0, 0, -90))
    arrival = add("02_Central", "Central", "PneumaticArrivalStation", (2.78, 5.0, 0.85), (0, 0, -90))
    capsule = add("02_Central", "Central", "PneumaticCapsule", (0, -0.30, 0.35), parent=arrival)
    add("02_Central", "Central", "RolledOrderPaper", (0, 0, 0.06), parent=capsule)
    sonar = add("03_Radio", "Radio", "SonarConsole", (-2.23, 2.1, 0), (0, 0, 90))
    add("03_Radio", "Radio", "SonarHeadphones", (.50, -.31, .70), parent=sonar)
    add("03_Radio", "Radio", "HeadphoneCableCoiled", (.50, -.31, .30), parent=sonar)
    add("03_Radio", "Radio", "HydrophoneCrankStation", (-2.86, .75, .85), (0, 0, 90))
    radio_table = add("03_Radio", "Environment", "ChartTable", (2.13, 2.1, 0), (0, 0, -90))
    add("03_Radio", "Radio", "VLFRadioStation", (-.30, .10, .98), parent=radio_table)
    cipher = add("03_Radio", "Radio", "MechanicalCipherMachine", (.48, -.03, .98), parent=radio_table)
    add("03_Radio", "Radio", "CipherMessagePad", (0, .20, .39), parent=cipher)
    recorder = add("03_Radio", "Radio", "MagneticTapeRecorder", (2.75, .72, .83), (0, 0, -90))
    for x, name in ((-.205, "TapeReelBlank"), (.205, "TapeReelRecorded")):
        add("03_Radio", "Radio", name, (x, -.155, .47), parent=recorder)
    add("03_Radio", "Radio", "VLFAntennaDeployable", (2.70, 3.30, .65))
    reactor = add("04_Reactor", "Reactor", "RK1_Console", (-2.24, -2, 0), (0, 0, 90))
    for i in range(4):
        # Exact coordinates from kit_reactor's tilted panel sockets.
        t = math.radians(22)
        dz = -0.15
        loc = (-0.54+0.36*i, -0.06-math.cos(t)*0.064+math.sin(t)*dz,
               1.28+math.sin(t)*0.064+math.cos(t)*dz)
        add("04_Reactor", "Instruments", "GaugeRound_M", loc, (-22, 0, 0), reactor)
    add("04_Reactor", "Instruments", "SelectorRotary", (0, -0.06-math.cos(t)*0.064+math.sin(t)*0.20,
        1.28+math.sin(t)*0.064+math.cos(t)*0.20), (-22, 0, 0), reactor)
    for i in range(8):
        add("04_Reactor", "Machines", "MachineBreaker", (-0.49+0.14*i, -0.314, 0.86), parent=reactor)
    add("04_Reactor", "Reactor", "ScramLever", (-2.75, -0.8, 1.15), (0, 0, 90))
    for y in (-2.9, -1.1):
        add("04_Reactor", "Primary", "PrimaryPump", (2.0, y, 0), (0, 0, 90))
    for y in (-3.3, -2.5, -1.7, -0.9):
        add("04_Reactor", "Primary", "PrimaryValve", (2.60, y, 1.2), (0, 0, -90))
    panel = add("05_Machines", "Machines", "ElectricalPanel", (-2.69, -5.6, 0), (0, 0, 90))
    for i in range(20):
        add("05_Machines", "Machines", "MachineBreaker", (-0.4+(i%5)*0.2, -0.25, 0.54+(i//5)*0.23), parent=panel)
    for x in (-0.32, 0.32):
        add("05_Machines", "Instruments", "GaugeRound_M", (x, -0.24, 1.52), parent=panel)
    turbine_id = add("05_Machines", "Machines", "TurbineReducer", (2.14, -6.2, 0), (0, 0, 90))
    add("05_Machines", "Instruments", "GaugeRound_S", (0.74, -0.34, 0.71), parent=turbine_id)
    battery = add("05_Machines", "Machines", "BatteryBank", (-1.85, -6.8, 0), (0, 0, 90))
    add("05_Machines", "Instruments", "GaugeRound_S", (0.49, -0.15, 0.73), parent=battery)
    for rid, y in (("04_Reactor", -3.45), ("05_Machines", -7.35)):
        add(rid, "Machines", "BilgePump", (0, y, -0.12))
    for x in (-2.1, 2.1):
        for z in (0, 1.02):
            add("01_Torpedoes", "Torpedoes", "CargoTorpedoTube", (x, 12.35, z), (0, 0, 180))
    rack = add("01_Torpedoes", "Torpedoes", "CargoRack", (0, 13.38, 0))
    for z in (.18,.82):
        add("01_Torpedoes", "Environment", "CargoCrate_M", (0, 0, z), parent=rack)
        add("01_Torpedoes", "Torpedoes", "CargoTieDownStrap", (0, 0, z+.52), (0, 0, 90), parent=rack)
    for y in (-10.1,):
        add("06_Living", "Living", "BunkBed_Double", (-2.32, y, 0), (0, 0, 90))
    stove = add("06_Living", "Living", "GalleyStove", (2.38, -10.85, 0), (0, 0, -90))
    add("06_Living", "Living", "SoupPot", (-.22, 0, .99), parent=stove)
    shower = add("06_Living", "Living", "DeconShowerCabin", (2.39, -9.55, 0), (0, 0, -90))
    add("06_Living", "Living", "DeconWaterLance", (-.22, .38, .55), parent=shower)
    suit = add("06_Living", "Living", "DivingSuitOnRack", (2.65, -8.55, 0), (0, 0, -90))
    add("06_Living", "Living", "DivingHelmet", (0, 0, 1.51), parent=suit)
    for x in (-2.15, -1.53, -.91, -.29):
        add("06_Living", "Living", "PersonalLocker", (x, -13.7, 0), (0, 0, 180))
    add("06_Living", "Living", "DivingAirlockChamber", (1.95, -12.95, 0), (0, 0, 180),
        part_states=[{'name':'Hatch0', 'rotation':[0, 0, -105]}])
    add("06_Living", "Living", "UmbilicalReel", (2.65, -11.65, 0), (0, 0, -90))
    mess = add("06_Living", "Living", "MessTable", (-2.20, -12.2, 0), (0, 0, 90))
    add("06_Living", "Living", "MessBench", (-1.45, -12.2, 0), (0, 0, -90))
    add("06_Living", "Living", "EnamelBowl", (-.22, 0, .79), parent=mess)
    add("06_Living", "Living", "EnamelSpoon", (.10, -.15, .79), parent=mess)
    for rid, position in (("01_Torpedoes", (-1.2, 12, 1.6)), ("02_Central", (-1.1, 7.3, 1.6)),
                          ("03_Radio", (-1.1, 2, 1.6)), ("04_Reactor", (-1.1, -2, 1.6)),
                          ("05_Machines", (-1.2, -5.6, 1.6)), ("06_Living", (1.1, -11.3, 1.6))):
        data["stations"].append({"room": rid, "position": position})
    return data


def clone_asset(asset, ident, parent, position, rotation):
    originals = [asset.root] + list(asset.root.children_recursive)
    copies = {}
    for ob in originals:
        cp = ob.copy()
        bpy.context.scene.collection.objects.link(cp)
        copies[ob] = cp
    for ob, cp in copies.items():
        cp.parent = copies.get(ob.parent, parent)
    root = copies[asset.root]
    root.name = ident
    root.location = position
    root.rotation_euler = tuple(math.radians(x) for x in rotation)
    return root


def render_environment(data, instances, preview):
    os.makedirs(preview, exist_ok=True)
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.render.resolution_x, scn.render.resolution_y = 1900, 1100
    scn.render.resolution_percentage = 100
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "VERTEX"
    scn.display.shading.show_shadows = True
    scn.display.shading.show_cavity = True
    scn.display.shading.background_type = "WORLD"
    scn.world = bpy.data.worlds.new("EnvironmentPreviewWorld")
    scn.world.color = (0.18, 0.23, 0.27)
    scn.display.render_aa = "16"
    scn.view_settings.view_transform = "Standard"
    cam_data = bpy.data.cameras.new("EnvironmentCamera")
    cam = bpy.data.objects.new("EnvironmentCamera", cam_data)
    scn.collection.objects.link(cam)
    scn.camera = cam
    cut = []
    for entry in data["instances"]:
        if entry["asset"] == "EnvironmentHull_2m":
            for ob in instances[entry["id"]].children:
                if ob.name.startswith(("PortWall", "Roof")):
                    ob.hide_render = True
                    cut.append(ob)
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 33
    cam.location = (-21, -16, 25)
    cam.rotation_euler = (Vector((0, 0, 0.5))-cam.location).to_track_quat("-Z", "Y").to_euler()
    scn.render.filepath = os.path.join(preview, "Boat_cutaway.png")
    bpy.ops.render.render(write_still=True)
    for ob in cut:
        ob.hide_render = False
    cam_data.type = "PERSP"
    cam_data.lens = 20
    for name, position, target in (("Reactor", (0.2, -3.2, 1.65), (-2.3, -1.8, 1.0)),
                                   ("Machines", (0, -7.3, 1.65), (1.5, -5.8, 0.9)),
                                   ("Central", (0.2, 5.0, 1.65), (-1.8, 7.0, 1.1))):
        cam.location = position
        cam.rotation_euler = (Vector(target)-cam.location).to_track_quat("-Z", "Y").to_euler()
        scn.render.filepath = os.path.join(preview, name + "_interior.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    out_dir, preview = cli_args()
    reset_scene()
    kits = {"Environment": build_all(), "Structure": structure_assets(), "Instruments": instrument_assets(),
            "Reactor": reactor_assets(), "Primary": primary_assets(), "Machines": machine_assets(),
            "Lighting": lighting_assets(), "Signage": signage_assets(), "Radio": radio_assets(), "Living": living_assets(), "Torpedoes": torpedo_assets(), "Central": [central.steering(), central.helm(), central.telegraph(), central.charttable(),
                central.paper(), central.pencil(), central.manual(), central.console(), central.station(), central.capsule(), central.rolledpaper()]}
    prototypes = {}
    library = bpy.data.collections.new("AssetLibrary")
    bpy.context.scene.collection.children.link(library)
    for kit, assets in kits.items():
        for a in assets:
            a.build()
            prototypes[(kit, a.name)] = a
    bpy.context.view_layer.update()
    for a in kits["Environment"]:
        a.export(out_dir)
        print("EXPORT %-26s %6d triangles" % (a.name, a.stats()))
    for a in prototypes.values():
        for ob in [a.root] + list(a.root.children_recursive):
            for collection in list(ob.users_collection):
                collection.objects.unlink(ob)
            library.objects.link(ob)
    library.hide_render = True
    library.hide_viewport = True
    data = layout()
    boat = bpy.data.objects.new("BoatEnvironment", None)
    bpy.context.scene.collection.objects.link(boat)
    rooms = {}
    for room in data["rooms"]:
        root = bpy.data.objects.new(room["id"], None)
        bpy.context.scene.collection.objects.link(root)
        root.parent = boat
        rooms[room["id"]] = root
    instances = {}
    for entry in data["instances"]:
        parent = instances[entry["parent"]] if entry["parent"] else rooms[entry["room"]]
        instances[entry["id"]] = clone_asset(prototypes[(entry["kit"], entry["asset"])], entry["id"],
            parent, entry["position"], entry["rotation"])
        for state in entry['partStates']:
            part = next(ob for ob in instances[entry['id']].children_recursive
                        if ob.name.split('.')[0] == state['name'])
            part.rotation_euler = tuple(math.radians(v) for v in state['rotation'])
    bpy.context.view_layer.update()
    with open(os.path.join(out_dir, "boat_layout.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    source_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources")
    os.makedirs(source_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, "BoatEnvironment.blend"))
    if preview:
        render_environment(data, instances, preview)
        print("PREVIEW", preview)
    print("ENVIRONMENT", len(data["instances"]), "instances, six compartments, 28 metres")
