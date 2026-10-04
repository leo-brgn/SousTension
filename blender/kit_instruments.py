"""
Kit instruments (E11-07) : bases communes déclinées par cadran/texture dans Unity (~15 meshes -> des centaines d'instances).
Chaque instrument est un ASSET : racine + parties animables (pivot = axe de rotation) + points de montage « Mount_* ».

Lancer :  blender --background --factory-startup --python blender/kit_instruments.py -- <dossier_sortie_absolu> [--preview <png_absolu>]
Conventions : 1 unité = 1 m ; l'instrument regarde vers -Y (sa face visible) ; +Z = haut ; il se fixe au mur par son dos (+Y).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

TAU = math.tau


def gauge_round(name, radius):
    """Manomètre rond : lunette en laiton, cadran crème avec graduations, aiguille séparée (pivot au centre)."""
    a = Asset(name)
    r = radius
    body = a.part("Body")
    depth = r * 0.55
    # boîtier (cuvette) et fond
    body.cyl((0, depth * 0.25, 0), r * 0.98, depth * 0.9, C["steel_d"], axis="Y", segs=28, bevel=r * 0.05)     # face avant a y = -0.2 d : le cadran (y = -0.22 d) est devant
    # lunette laiton (anneau épais) sur la face
    body.disc_ring((0, -depth * 0.30, 0), r * 0.80, r * 1.05, r * 0.22, C["brass"], axis="Y", segs=32, bevel=r * 0.03)
    # cadran crème (disque légèrement en retrait)
    body.cyl((0, -depth * 0.22, 0), r * 0.80, r * 0.05, C["cream"], axis="Y", segs=28)
    # graduations : 11 traits sur 240 degrés ; les 3 grandes en rouille, la zone rouge en « rouge unique » en bout d'échelle
    ticks = 11
    for i in range(ticks):
        ang = math.radians(-120 + 240 * i / (ticks - 1))
        major = i % 5 == 0
        L = r * (0.20 if major else 0.12)
        W = r * (0.06 if major else 0.04)
        rr = r * 0.66
        cx, cz = math.sin(ang) * rr, math.cos(ang) * rr
        col = C["red"] if i >= ticks - 2 else C["black"]
        body.box((cx, -depth * 0.345, cz), (W, r * 0.02, L), col, rot=(0, ang, 0))
    # moyeu central
    body.cyl((0, -depth * 0.36, 0), r * 0.10, r * 0.12, C["black"], axis="Y", segs=12)
    # aiguille : pivot au centre, repos pointant à -120 degrés (minimum d'échelle)
    needle = a.part("Needle", location=(0, -depth * 0.40, 0), rotation=(0, math.radians(-120), 0))
    needle.box((0, 0, r * 0.32), (r * 0.05, r * 0.02, r * 0.66), C["red_d"], bevel=r * 0.005)
    needle.box((0, 0, -r * 0.10), (r * 0.07, r * 0.02, r * 0.20), C["red_d"])
    needle.cyl((0, 0, 0), r * 0.07, r * 0.04, C["black"], axis="Y", segs=12)
    a.mount("Mount_Face", (0, -depth * 0.42, 0))
    return a


def gauge_vertical(name="GaugeVertical"):
    """Indicateur à aiguille vertical (niveau, tirant d'eau) : boîtier rectangulaire, échelle verticale, curseur coulissant."""
    a = Asset(name)
    W, H, D = 0.12, 0.34, 0.07
    body = a.part("Body")
    body.box((0, 0, 0), (W, D, H), C["steel_d"], bevel=0.012)
    body.box((0, -D * 0.46, 0), (W * 0.74, D * 0.10, H * 0.88), C["cream"], bevel=0.004)
    for i in range(9):
        z = -H * 0.38 + H * 0.76 * i / 8
        major = i % 4 == 0
        body.box((-W * 0.18 + (0 if major else W * 0.04), -D * 0.54, z), (W * (0.34 if major else 0.22), D * 0.06, 0.006),
                 C["red"] if i == 8 else C["black"])
    body.box((0, -D * 0.60, 0), (0.012, D * 0.04, H * 0.86), C["steel_l"])          # rainure
    slider = a.part("Slider", location=(0, -D * 0.64, -H * 0.38))
    slider.box((0, 0, 0), (W * 0.62, D * 0.14, 0.018), C["red_d"], bevel=0.003)
    slider.box((W * 0.34, 0, 0), (W * 0.10, D * 0.14, 0.03), C["red_d"])
    a.mount("Mount_Face", (0, -D * 0.6, 0))
    return a


def counter_rollers(name="CounterRollers"):
    """Compteur à rouleaux : fenêtre avec 4 tambours (parties distinctes, rotation autour de X)."""
    a = Asset(name)
    W, H, D = 0.20, 0.09, 0.07
    body = a.part("Body")
    body.box((0, 0, 0), (W, D, H), C["steel_d"], bevel=0.01)
    body.box((0, -D * 0.50, 0), (W * 0.86, D * 0.08, H * 0.58), C["black"], bevel=0.004)       # fenêtre noire
    for i in range(5):                                                                           # séparateurs
        body.box((-W * 0.36 + W * 0.18 * i, -D * 0.56, 0), (0.004, D * 0.04, H * 0.58), C["steel"])
    for i in range(4):
        x = -W * 0.27 + W * 0.18 * i
        drum = a.part("Roller%d" % i, location=(x, -D * 0.28, 0))
        drum.cyl((0, 0, 0), H * 0.34, W * 0.16, C["cream"], axis="X", segs=16)
        for k in range(10):                                                                      # chiffres : bandes sombres
            ang = TAU * k / 10
            drum.box((0, math.sin(ang) * H * 0.33, math.cos(ang) * H * 0.33), (W * 0.10, 0.004, H * 0.10),
                     C["black"], rot=(ang, 0, 0))
    a.mount("Mount_Face", (0, -D * 0.6, 0))
    return a


def lamp_dome(name="LampDome"):
    """Voyant sous dôme : socle laiton + dôme séparé (Unity le teinte : vert / ambre / rouge réservé aux urgences)."""
    a = Asset(name)
    body = a.part("Body")
    body.cyl((0, 0.012, 0), 0.040, 0.025, C["steel_d"], axis="Y", segs=20, bevel=0.004)
    body.disc_ring((0, -0.004, 0), 0.026, 0.042, 0.016, C["brass"], axis="Y", segs=24, bevel=0.003)
    dome = a.part("Dome", location=(0, -0.012, 0))
    dome.sphere((0, 0, 0), (0.030, 0.026, 0.030), C["white"], u=16, v=10)      # blanc : la couleur vient du matériau Unity
    a.mount("Mount_Face", (0, -0.04, 0))
    return a


def vu_meter(name="VUMeter"):
    """VU-mètre (volume de voix) : boîtier, cadran à arc, aiguille."""
    a = Asset(name)
    W, H, D = 0.16, 0.12, 0.06
    body = a.part("Body")
    body.box((0, 0, 0), (W, D, H), C["bakelite"], bevel=0.012)
    body.box((0, -D * 0.46, 0.002), (W * 0.84, D * 0.10, H * 0.78), C["cream"], bevel=0.006)
    for i in range(9):                                                      # arc de graduations
        ang = math.radians(-50 + 100 * i / 8)
        cx, cz = math.sin(ang) * H * 0.40, -H * 0.18 + math.cos(ang) * H * 0.40
        body.box((cx, -D * 0.54, cz), (0.006, D * 0.04, H * (0.10 if i % 4 == 0 else 0.06)), C["red"] if i >= 7 else C["black"], rot=(0, ang, 0))
    needle = a.part("Needle", location=(0, -D * 0.60, -H * 0.30), rotation=(0, math.radians(-50), 0))
    needle.box((0, 0, H * 0.36), (0.005, 0.004, H * 0.74), C["black"])
    needle.cyl((0, 0, 0), 0.010, 0.012, C["black"], axis="Y", segs=10)
    a.mount("Mount_Face", (0, -D * 0.6, 0))
    return a


def lever_switch(name="LeverSwitch"):
    """Interrupteur à levier : socle à encoche et manette (pivot à la base, ±30 degrés)."""
    a = Asset(name)
    body = a.part("Body")
    body.box((0, 0, 0), (0.07, 0.03, 0.10), C["steel_d"], bevel=0.008)
    body.box((0, -0.017, 0), (0.05, 0.012, 0.02), C["black"])                 # encoche
    body.disc_ring((0, -0.014, 0), 0.012, 0.022, 0.010, C["brass"], axis="Y", segs=16)
    lever = a.part("Lever", location=(0, -0.022, 0))
    lever.cyl((0, 0, 0.03), 0.007, 0.06, C["steel_l"], axis="Z", segs=10)
    lever.sphere((0, 0, 0.065), 0.014, C["bakelite"], u=12, v=8)
    a.mount("Mount_Face", (0, -0.02, 0))
    return a


def button_guarded(name="ButtonGuarded"):
    """Bouton-poussoir sous garde : embase, bouton qui s'enfonce, volet de garde à charnière (pivot à la charnière)."""
    a = Asset(name)
    body = a.part("Body")
    body.box((0, 0, 0), (0.09, 0.03, 0.12), C["steel_d"], bevel=0.008)
    body.cyl((0, -0.020, 0), 0.026, 0.012, C["steel"], axis="Y", segs=18, bevel=0.003)
    body.box((0, 0.005, 0.058), (0.06, 0.02, 0.008), C["brass_d"])            # charnière
    button = a.part("Button", location=(0, -0.026, 0))
    button.cyl((0, 0, 0), 0.020, 0.016, C["red"], axis="Y", segs=18, bevel=0.003)
    guard = a.part("Guard", location=(0, -0.040, 0.058), rotation=(math.radians(0), 0, 0))
    guard.box((0, -0.004, -0.058), (0.078, 0.008, 0.108), C["glass"], bevel=0.004)          # volet transparent (teinté dans Unity)
    guard.box((0, -0.004, -0.108), (0.05, 0.016, 0.014), C["steel_l"], bevel=0.003)          # poignée
    a.mount("Mount_Face", (0, -0.045, 0))
    return a


def selector_rotary(name="SelectorRotary"):
    """Sélecteur rotatif à 3 positions (régimes du réacteur) : embase à repères, bouton avec pointeur (pivot au centre)."""
    a = Asset(name)
    body = a.part("Body")
    body.cyl((0, 0, 0), 0.075, 0.03, C["steel_d"], axis="Y", segs=28, bevel=0.006)
    body.cyl((0, -0.018, 0), 0.062, 0.010, C["cream"], axis="Y", segs=28)
    for i in range(3):                                                         # repères : Veille, Croisière, Pleine
        ang = math.radians(-50 + 50 * i)
        rr = 0.052
        body.box((math.sin(ang) * rr, -0.026, math.cos(ang) * rr), (0.010, 0.006, 0.020), C["red"] if i == 2 else C["black"], rot=(0, ang, 0))
    knob = a.part("Knob", location=(0, -0.024, 0))
    knob.cyl((0, -0.010, 0), 0.036, 0.030, C["bakelite"], axis="Y", segs=22, bevel=0.005)
    knob.box((0, -0.026, 0.020), (0.010, 0.008, 0.034), C["white"], bevel=0.002)     # pointeur vers le haut au repos
    knob.box((0, -0.010, -0.0), (0.060, 0.030, 0.012), C["bakelite"], bevel=0.004)  # ailes de prise
    a.mount("Mount_Face", (0, -0.05, 0))
    return a


def valve_wheel(name, radius):
    """Volant de vanne : jante, 4 rayons, moyeu (pivot au centre, axe Y) + corps de vanne fixe."""
    a = Asset(name)
    r = radius
    body = a.part("Body")
    body.cyl((0, 0.020 * r / 0.15, 0), r * 0.16, r * 0.30, C["steel_d"], axis="Y", segs=14, bevel=r * 0.02)
    body.cyl((0, 0.055 * r / 0.15, 0), r * 0.26, r * 0.14, C["steel"], axis="Y", segs=16, bevel=r * 0.02)
    wheel = a.part("Wheel", location=(0, -r * 0.18, 0))
    wheel.torus((0, 0, 0), r * 0.92, r * 0.085, C["rust"], axis="Y", major_segs=28, minor_segs=8)
    for i in range(4):
        ang = TAU * i / 4 + math.pi / 4
        wheel.box((math.sin(ang) * r * 0.46, 0, math.cos(ang) * r * 0.46), (r * 0.10, r * 0.10, r * 0.92), C["rust_d"], rot=(0, ang, 0), bevel=r * 0.015)
    wheel.cyl((0, 0, 0), r * 0.18, r * 0.20, C["brass"], axis="Y", segs=14, bevel=r * 0.02)
    a.mount("Mount_Axis", (0, -r * 0.25, 0))
    return a


def crank(name="Crank"):
    """Manivelle (périscope, hydrophone, antenne) : support fixe, bras et poignée mobiles (pivot = axe)."""
    a = Asset(name)
    body = a.part("Body")
    body.box((0, 0.02, 0), (0.12, 0.04, 0.12), C["steel_d"], bevel=0.01)
    body.cyl((0, -0.020, 0), 0.020, 0.04, C["steel"], axis="Y", segs=14)
    arm = a.part("Arm", location=(0, -0.040, 0))
    arm.box((0, 0, 0.09), (0.030, 0.020, 0.20), C["steel_l"], bevel=0.006)
    arm.cyl((0, 0, 0), 0.026, 0.024, C["steel"], axis="Y", segs=14)
    arm.cyl((0, -0.040, 0.19), 0.017, 0.10, C["bakelite"], axis="Y", segs=14, bevel=0.004)   # poignée tournante
    a.mount("Mount_Axis", (0, -0.04, 0))
    return a


def ratchet_wheel(name="RatchetWheel"):
    """Roue crantée (cliquet, treuil) : disque, 18 dents, moyeu."""
    a = Asset(name)
    wheel = a.part("Wheel")
    wheel.cyl((0, 0, 0), 0.07, 0.025, C["steel"], axis="Y", segs=24, bevel=0.003)
    n = 18
    for i in range(n):
        ang = TAU * i / n
        wheel.box((math.sin(ang) * 0.078, 0, math.cos(ang) * 0.078), (0.016, 0.025, 0.026), C["steel_l"], rot=(0, ang, 0), bevel=0.002)
    wheel.cyl((0, 0, 0), 0.02, 0.040, C["brass_d"], axis="Y", segs=12, bevel=0.003)
    for i in range(3):                                                           # allègements : trous figurés en sombre
        ang = TAU * i / 3
        wheel.cyl((math.sin(ang) * 0.042, -0.0128, math.cos(ang) * 0.042), 0.016, 0.004, C["steel_d"], axis="Y", segs=12)
    a.mount("Mount_Axis", (0, 0, 0))
    return a


def build_all():
    out = []
    out.append(gauge_round("GaugeRound_S", 0.06))
    out.append(gauge_round("GaugeRound_M", 0.10))
    out.append(gauge_round("GaugeRound_L", 0.15))
    out.append(gauge_vertical())
    out.append(counter_rollers())
    out.append(lamp_dome())
    out.append(vu_meter())
    out.append(lever_switch())
    out.append(button_guarded())
    out.append(selector_rotary())
    out.append(valve_wheel("ValveWheel_S", 0.08))
    out.append(valve_wheel("ValveWheel_M", 0.14))
    out.append(valve_wheel("ValveWheel_L", 0.22))
    out.append(crank())
    out.append(ratchet_wheel())
    return out


if __name__ == "__main__":
    out_dir, preview = cli_args()
    reset_scene()
    assets = build_all()
    for a in assets:
        a.build()
    for a in assets:
        path = a.export(out_dir)
        print("EXPORT %-18s %5d triangles  %s" % (a.name, a.stats(), os.path.basename(path)))
    if preview:
        render_sheet(assets, preview, cols=5, cell=0.5, resolution=(1800, 1000), cam_dir=(0.0, -1.0, 0.0), layout="XZ")
        print("PREVIEW", preview)
