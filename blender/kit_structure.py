"""
Kit structure du bateau (E11-06) : coque intérieure modulaire, cloisons, sas de cloison, caillebotis, fond de cale, tuyauterie.
« L'intérieur se construit en kit pour itérer sur le layout sans remodéliser » (GDD, annexe d'assets).

Lancer :  blender --background --factory-startup --python blender/kit_structure.py -- <dossier_sortie_absolu> [--preview <dossier_png>]
Conventions : 1 unité = 1 m ; Z = haut ; l'AXE LONGITUDINAL du bateau est Y (avant = +Y) ; X = tribord.
Profil intérieur du bateau : 6,0 m de large (x de -3 à +3), pont à z = 0, plafond à z = 2,5, pans coupés de 0,4 m en haut.
Les modules de coque font 2 m le long de Y et s'emboîtent bout à bout (origine au centre du module, au niveau du pont).
"""
import sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

HALF_W, HEIGHT, CH = 3.0, 2.5, 0.4        # demi-largeur, hauteur, pan coupé
LENGTH = 2.0


def profile_points(inset=0.0):
    """Contour du profil intérieur (x, z) du bateau, sens trigonométrique, éventuellement réduit de `inset`."""
    w, h, c = HALF_W - inset, HEIGHT - inset, CH
    return [(-w, 0.0), (w, 0.0), (w, h - c), (w - c, h), (-(w - c), h), (-w, h - c)]


def rivets(part, positions, size, color):
    for (x, y, z) in positions:
        part.box((x, y, z), (size, size, size), color)


def hull_module(name="HullModule_2m"):
    """Module de coque droit de 2 m : parois émaillées, pans coupés, membrures (3), rivets, chemin de câbles au plafond."""
    a = Asset(name)
    s = a.part("Shell")
    t = 0.10
    # parois latérales et plafond
    for side in (-1, 1):
        s.box((side * (HALF_W + t / 2), 0, (HEIGHT - CH) / 2), (t, LENGTH, HEIGHT - CH), C["bottle"], bevel=0.015)
        # pan coupé : dalle inclinée à 45 degrés
        s.box((side * (HALF_W - CH / 2), 0, HEIGHT - CH / 2), (CH * math.sqrt(2), LENGTH, t), C["bottle"], rot=(0, side * math.pi / 4, 0), bevel=0.015)
    s.box((0, 0, HEIGHT + t / 2), (2 * (HALF_W - CH), LENGTH, t), C["bottle"], bevel=0.015)
    # membrures : 3 cadres (à -0.9, 0, +0.9) qui font saillie de 0,10 m vers l\x27intérieur
    for y in (-0.9, 0.0, 0.9):
        for side in (-1, 1):
            s.box((side * (HALF_W - 0.05), y, (HEIGHT - CH) / 2), (0.10, 0.14, HEIGHT - CH), C["steel_d"], bevel=0.015)
            s.box((side * (HALF_W - CH / 2 - 0.04), y, HEIGHT - CH / 2 - 0.04), (CH * math.sqrt(2), 0.14, 0.10), C["steel_d"], rot=(0, side * math.pi / 4, 0), bevel=0.015)
        s.box((0, y, HEIGHT - 0.05), (2 * (HALF_W - CH), 0.14, 0.10), C["steel_d"], bevel=0.015)
        # rivets sur chaque montant et sur la traverse haute
        rv = []
        for side in (-1, 1):
            for z in [0.2 + 0.28 * i for i in range(7)]:
                rv.append((side * (HALF_W - 0.105), y - 0.045, z))
                rv.append((side * (HALF_W - 0.105), y + 0.045, z))
        for x in [-2.2 + 0.4 * i for i in range(12)]:
            rv.append((x, y - 0.045, HEIGHT - 0.105))
            rv.append((x, y + 0.045, HEIGHT - 0.105))
        rivets(s, rv, 0.028, C["steel_l"])
    # tôles : joints horizontaux en saillie sur les parois (bandes)
    for side in (-1, 1):
        for z in (0.9, 1.7):
            s.box((side * (HALF_W - 0.015), 0, z), (0.03, LENGTH, 0.05), C["bottle_d"], bevel=0.008, seg=1)
    # chemin de câbles au plafond + 3 câbles
    tray = a.part("CableTray", location=(-1.7, 0, HEIGHT - 0.14))
    tray.box((0, 0, 0), (0.34, LENGTH, 0.05), C["steel"], bevel=0.008)
    for sx in (-1, 1):
        tray.box((sx * 0.165, 0, 0.04), (0.02, LENGTH, 0.07), C["steel"], bevel=0.005)
    for i, col in enumerate((C["black"], C["rust_d"], C["bakelite"])):
        tray.cyl((-0.09 + 0.09 * i, 0, 0.05), 0.026, LENGTH * 0.98, col, axis="Y", segs=10)
    a.mount("Mount_Light", (0, 0, HEIGHT - 0.02))
    a.mount("Mount_PipeLeft", (-HALF_W + 0.35, 0, HEIGHT - 0.45))
    a.mount("Mount_PipeRight", (HALF_W - 0.35, 0, HEIGHT - 0.45))
    return a


def bulkhead_plate(a, opening=None):
    """Cloison (plaque pleine ou trouée) : contour du profil, raidisseurs et rivets. `opening` = (largeur, hauteur) du sas, centré."""
    p = a.part("Plate")
    T = 0.14
    if opening is None:
        p.prism_xz(profile_points(), T, C["bottle"], y=0.0)
    else:
        ow, oh = opening
        z0 = 0.06                                              # seuil
        # pièces autour du trou : bas (seuil), gauche, droite, haut
        p.box((0, 0, z0 / 2), (2 * HALF_W, T, z0), C["steel_d"], bevel=0.012)
        left = [(-HALF_W, z0), (-ow / 2, z0), (-ow / 2, HEIGHT), (-(HALF_W - CH), HEIGHT), (-HALF_W, HEIGHT - CH)]
        p.prism_xz(left, T, C["bottle"], y=0.0)
        right = [(ow / 2, z0), (HALF_W, z0), (HALF_W, HEIGHT - CH), (HALF_W - CH, HEIGHT), (ow / 2, HEIGHT)]
        p.prism_xz(right, T, C["bottle"], y=0.0)
        p.box((0, 0, (z0 + oh + HEIGHT) / 2 + 0.0), (ow, T, HEIGHT - z0 - oh), C["bottle"], bevel=0.01)
        # collier du sas : cadre en saillie des deux côtés
        for yy in (-T / 2 - 0.03, T / 2 + 0.03):
            p.box((-ow / 2 - 0.04, yy, z0 + oh / 2), (0.08, 0.06, oh + 0.08), C["steel"], bevel=0.012)
            p.box((ow / 2 + 0.04, yy, z0 + oh / 2), (0.08, 0.06, oh + 0.08), C["steel"], bevel=0.012)
            p.box((0, yy, z0 + oh + 0.04), (ow + 0.16, 0.06, 0.08), C["steel"], bevel=0.012)
            p.box((0, yy, z0 + 0.0), (ow + 0.16, 0.06, 0.06), C["steel"], bevel=0.012)
    # raidisseurs en saillie sur les deux faces (poutres verticales à -2,2 et +2,2 ; horizontale à mi-hauteur de chaque côté du sas)
    for yy in (-T / 2 - 0.035, T / 2 + 0.035):
        for x in (-2.2, 2.2):
            p.box((x, yy, HEIGHT / 2 - 0.05), (0.14, 0.07, HEIGHT - 0.5), C["steel_d"], bevel=0.015)
        for x in ((-1.7, 1.7) if opening else (0.0,)):
            p.box((x, yy, 1.25), (1.2 if opening else 5.2, 0.07, 0.12), C["steel_d"], bevel=0.015)
        rv = []
        for x in [-2.8 + 0.4 * i for i in range(15)]:
            rv.append((x, yy + (0.035 if yy > 0 else -0.035), 0.12))
        rivets(p, rv, 0.03, C["steel_l"])
    # joint de pied
    return p


def bulkhead_solid(name="Bulkhead_Solid"):
    a = Asset(name)
    bulkhead_plate(a)
    return a


def bulkhead_hatch(name="Bulkhead_Hatch"):
    """Cloison à sas : ouverture 0,9 x 1,7 m ; la porte est un asset séparé (HatchDoor), montée sur Mount_Hinge."""
    a = Asset(name)
    ow, oh = 0.90, 1.70
    bulkhead_plate(a, opening=(ow, oh))
    a.mount("Mount_Hinge", (-ow / 2, -0.10, 0.06))
    a.mount("Mount_HatchCentre", (0, -0.10, 0.06 + oh / 2))
    return a


def hatch_door(name="HatchDoor"):
    """Porte de sas (🔧 P1) : battant à charnière, volant central, 6 loquets (états ouvert / fermé / entrebâillé)."""
    a = Asset(name)
    ow, oh = 0.90, 1.70
    door = a.part("Door", location=(0, 0, 0))              # pivot = charnière (côté -x), le battant s\x27étend vers +x
    door.box((ow / 2, -0.04, oh / 2), (ow - 0.04, 0.09, oh - 0.04), C["bottle_d"], bevel=0.025, seg=3)          # battant (coins arrondis)
    door.box((ow / 2, -0.095, oh / 2), (ow - 0.20, 0.03, oh - 0.20), C["bottle"], bevel=0.009, seg=3)           # panneau en relief
    door.torus((ow / 2, -0.09, oh / 2), 0.24, 0.025, C["steel"], axis="Y", major_segs=24, minor_segs=6)         # couronne autour du volant
    for i in range(6):                                                                                          # rivets de la couronne
        ang = math.tau * i / 6
        door.box((ow / 2 + math.sin(ang) * 0.30, -0.10, oh / 2 + math.cos(ang) * 0.30), (0.03, 0.03, 0.03), C["steel_l"])
    # charnières côté -x
    for z in (0.25, oh / 2, oh - 0.25):
        door.cyl((0.0, -0.05, z), 0.035, 0.18, C["steel"], axis="Z", segs=12, bevel=0.004)
    # volant (pivot au centre du battant)
    wheel = a.part("Wheel", location=(ow / 2, -0.12, oh / 2), parent=door)
    r = 0.20
    wheel.torus((0, 0, 0), r, 0.022, C["rust"], axis="Y", major_segs=24, minor_segs=6)
    for i in range(4):
        ang = math.tau * i / 4 + math.pi / 4
        wheel.box((math.sin(ang) * r * 0.5, 0, math.cos(ang) * r * 0.5), (0.03, 0.03, r), C["rust_d"], rot=(0, ang, 0), bevel=0.006)
    wheel.cyl((0, 0, 0), 0.05, 0.06, C["brass"], axis="Y", segs=14, bevel=0.006)
    # 6 loquets autour du battant : manettes pivotantes (pivot sur le battant)
    pts = [(ow - 0.11, 0.30), (ow - 0.11, oh / 2), (ow - 0.11, oh - 0.30), (0.11 + 0.10, 0.30), (0.11 + 0.10, oh - 0.30), (ow / 2, oh - 0.12)]
    for i, (x, z) in enumerate(pts):
        latch = a.part("Latch%d" % i, location=(x, -0.10, z), parent=door)
        latch.box((0.07, -0.01, 0), (0.16, 0.03, 0.04), C["steel_l"], bevel=0.008)
        latch.cyl((0, 0, 0), 0.03, 0.05, C["brass_d"], axis="Y", segs=10, bevel=0.004)
    a.mount("Mount_Hinge", (0, 0, 0))
    return a


def floor_grating(name="FloorGrating_1m"):
    """Caillebotis 1 x 1 m : cadre et barres espacées (on voit l\x27eau monter dessous)."""
    a = Asset(name)
    g = a.part("Grating")
    H = 0.05
    for sx in (-1, 1):
        g.box((sx * 0.485, 0, -H / 2), (0.03, 1.0, H), C["steel_d"], bevel=0.004, seg=1)
        g.box((0, sx * 0.485, -H / 2), (1.0, 0.03, H), C["steel_d"], bevel=0.004, seg=1)
    for i in range(11):                                   # barres porteuses (le long de Y)
        g.box((-0.40 + 0.08 * i, 0, -H / 2), (0.012, 0.94, H), C["steel"])
    for j in range(5):                                    # entretoises (le long de X), plus basses
        g.box((0, -0.40 + 0.2 * j, -H * 0.75), (0.94, 0.012, H * 0.5), C["steel_d"])
    return a


def bilge_floor(name="BilgeFloor_2m"):
    """Fond de cale sous le caillebotis : bac de 2 m en tôle rouillée, pente vers un caniveau central (reçoit l\x27eau, les débris)."""
    a = Asset(name)
    b = a.part("Bilge")
    w = HALF_W - 0.1
    b.box((0, 0, -0.42), (2 * w - 1.0, LENGTH, 0.04), C["rust_d"], bevel=0.008)                        # fond plat
    for side in (-1, 1):
        b.box((side * (w - 0.30), 0, -0.22), (0.55, LENGTH, 0.04), C["rust"], rot=(0, side * math.radians(-35), 0), bevel=0.008)   # pans inclinés
        b.box((side * (w + 0.02), 0, -0.10), (0.04, LENGTH, 0.40), C["rust_d"], bevel=0.008)             # bord
    b.box((0, 0, -0.45), (0.5, LENGTH, 0.03), C["black"], bevel=0.005)                                   # caniveau central
    for y in (-0.9, 0.0, 0.9):                                                                           # varangues
        b.box((0, y, -0.38), (2 * w - 0.8, 0.10, 0.08), C["steel_d"], bevel=0.01)
    a.mount("Mount_PumpSump", (0, 0.0, -0.45))
    return a


def pipe_flange(part, centre, axis, r, color):
    part.disc_ring(centre, r * 0.78, r * 1.7, 0.03, color, axis=axis, segs=18)
    for i in range(6):
        ang = math.tau * i / 6
        o = (math.cos(ang) * r * 1.38, math.sin(ang) * r * 1.38)
        if axis == "Y":
            p = (centre[0] + o[0], centre[1], centre[2] + o[1])
        elif axis == "X":
            p = (centre[0], centre[1] + o[0], centre[2] + o[1])
        else:
            p = (centre[0] + o[0], centre[1] + o[1], centre[2])
        part.cyl(p, r * 0.22, 0.045, C["steel_l"], axis=axis, segs=6)


def pipe_straight(name="Pipe_Straight_1m", length=1.0):
    a = Asset(name)
    p = a.part("Pipe")
    r = 0.06
    p.cyl((0, 0, 0), r, length, C["steel"], axis="Y", segs=16)
    p.cyl((0, -length * 0.25, 0), r * 1.06, 0.07, C["cream_d"], axis="Y", segs=16)       # bande de repérage
    for y in (-length / 2 + 0.015, length / 2 - 0.015):
        pipe_flange(p, (0, y, 0), "Y", r, C["steel_d"])
    a.mount("Mount_A", (0, -length / 2, 0))
    a.mount("Mount_B", (0, length / 2, 0))
    return a


def pipe_elbow(name="Pipe_Elbow"):
    """Coude à 90 degrés (rayon 0,25 m) : va de l\x27origine (entrée selon -Y) vers +X."""
    a = Asset(name)
    p = a.part("Pipe")
    r, R = 0.06, 0.25
    # tore d\x27axe Z, centre en (R, 0, 0) : le quart de tour part de (0,0,0) (tangent à Y) vers (R, R, 0)
    p.torus((R, 0, 0), R, r, C["steel"], axis="Z", major_segs=10, minor_segs=14, arc=math.pi / 2)
    for v in p.bm.verts: v.co.x = 2 * R - v.co.x
    pipe_flange(p, (0, -0.005, 0), "Y", r, C["steel_d"])
    pipe_flange(p, (R + 0.005 - 0.0, R, 0), "X", r, C["steel_d"])
    a.mount("Mount_A", (0, 0, 0))
    a.mount("Mount_B", (R, R, 0))
    return a


def pipe_tee(name="Pipe_Tee"):
    a = Asset(name)
    p = a.part("Pipe")
    r = 0.06
    p.cyl((0, 0, 0), r, 0.60, C["steel"], axis="Y", segs=16)
    p.cyl((0.15, 0, 0), r, 0.30, C["steel"], axis="X", segs=16)
    p.sphere((0, 0, 0), (r * 1.25, r * 1.25, r * 1.25), C["steel"], u=14, v=10)
    for y in (-0.285, 0.285):
        pipe_flange(p, (0, y, 0), "Y", r, C["steel_d"])
    pipe_flange(p, (0.285, 0, 0), "X", r, C["steel_d"])
    a.mount("Mount_A", (0, -0.30, 0))
    a.mount("Mount_B", (0, 0.30, 0))
    a.mount("Mount_C", (0.30, 0, 0))
    return a


def pipe_valve_inline(name="Pipe_ValveInline"):
    """Tuyau avec vanne en ligne : corps de vanne et volant tournant (pivot sur l\x27axe vertical)."""
    a = Asset(name)
    p = a.part("Body")
    r = 0.06
    p.cyl((0, 0, 0), r, 0.60, C["steel"], axis="Y", segs=16)
    p.sphere((0, 0, 0), (r * 1.9, r * 1.9, r * 1.9), C["steel_d"], u=16, v=10)             # corps de vanne
    p.cyl((0, 0, r * 2.0), r * 0.7, r * 2.2, C["steel"], axis="Z", segs=12, bevel=0.004)    # chapeau
    for y in (-0.285, 0.285):
        pipe_flange(p, (0, y, 0), "Y", r, C["steel_d"])
    w = a.part("Wheel", location=(0, 0, r * 3.4))
    w.torus((0, 0, 0), 0.10, 0.012, C["rust"], axis="Z", major_segs=22, minor_segs=6)
    for i in range(4):
        ang = math.tau * i / 4 + math.pi / 4
        w.box((math.sin(ang) * 0.05, math.cos(ang) * 0.05, 0), (0.016, 0.10, 0.016), C["rust_d"], rot=(0, 0, -ang))
    w.cyl((0, 0, 0), 0.022, 0.03, C["brass"], axis="Z", segs=10)
    a.mount("Mount_A", (0, -0.30, 0))
    a.mount("Mount_B", (0, 0.30, 0))
    return a


def build_all():
    return [hull_module(), bulkhead_solid(), bulkhead_hatch(), hatch_door(), floor_grating(), bilge_floor(),
            pipe_straight(), pipe_elbow(), pipe_tee(), pipe_valve_inline()]


if __name__ == "__main__":
    out_dir, preview = cli_args()
    reset_scene()
    assets = build_all()
    for a in assets:
        a.build()
    bpy.context.view_layer.update()
    for a in assets:
        for part in a.parts:
            assert 'Col' in part.ob.data.color_attributes
            assert all(math.isfinite(c) for v in part.ob.data.vertices for c in v.co)
        path = a.export(out_dir)
        print("EXPORT %-18s %5d triangles  %s" % (a.name, a.stats(), os.path.basename(path)))
    with open(os.path.join(out_dir, 'structure_manifest.json'), 'w') as f:
        json.dump({'blender':bpy.app.version_string, 'assets':[{'name':a.name,'triangles':a.stats(),'parts':[p.name for p in a.parts],'mounts':[m[0] for m in a.mounts]} for a in assets]},f,indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__), 'sources', 'Structure.blend'))
    if preview:
        os.makedirs(preview, exist_ok=True)
        render_each(assets, preview, views=(("three_quarter", (0.8, -1.0, 0.6)),))
        print("PREVIEW", preview)
