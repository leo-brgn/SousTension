"""
Poste réacteur (E11-08) : tableau de commande RK-1 « Petit Soleil », levier SCRAM (« l'asset le plus filmé du jeu ») et disjoncteur.

Lancer :  blender --background --factory-startup --python blender/kit_reactor.py -- <dossier_sortie_absolu> [--preview <png_absolu>]
Conventions : 1 unité = 1 m ; la face visible regarde vers -Y, le dos (au mur) vers +Y ; +Z = haut.
Les points de montage « Mount_* » indiquent où Unity place les instruments du kit (cadrans, sélecteur, disjoncteurs, voyants, levier SCRAM).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import *

TAU = math.tau


def rivet_row(part, start, end, n, size, color, y):
    """Rangée de rivets (petits cubes) alignés entre deux points (x, z), face visible à `y`."""
    for i in range(n):
        t = i / max(1, n - 1)
        part.box((start[0] + (end[0] - start[0]) * t, y, start[1] + (end[1] - start[1]) * t), (size, size * 0.7, size), color, bevel=size * 0.25, seg=1)


def rk1_console(name="RK1_Console"):
    """Tableau de commande du réacteur : armoire émaillée vert bouteille, pupitre incliné, rangées de rivets, plaque en laiton."""
    a = Asset(name)
    W, D = 1.60, 0.62                       # largeur, profondeur
    body = a.part("Body")
    # socle + armoire basse
    body.box((0, 0, 0.05), (W + 0.04, D + 0.04, 0.10), C["steel_d"], bevel=0.015)
    body.box((0, 0, 0.55), (W, D, 0.90), C["bottle"], bevel=0.03, seg=3)
    # pupitre incliné (haut) : 22 degrés vers l'opérateur (-Y)
    tilt = math.radians(22)
    body.box((0, -0.06, 1.28), (W, 0.12, 0.84), C["bottle"], rot=(-tilt, 0, 0), bevel=0.03, seg=3)
    # dos du pupitre (volume sous le plan incliné)
    body.box((0, 0.18, 1.12), (W, 0.26, 0.55), C["bottle_d"], bevel=0.02)
    # capot haut + visière
    body.box((0, 0.10, 1.72), (W, 0.30, 0.10), C["bottle_d"], bevel=0.03, seg=3)
    # bandeau crème sur l'armoire (marquage administratif)
    body.box((0, -D / 2 - 0.004, 0.62), (W * 0.94, 0.012, 0.10), C["cream"], bevel=0.004, seg=1)
    # plaque en laiton « RK-1 » (blocs figurant la gravure)
    body.box((0, -D / 2 - 0.008, 0.32), (0.46, 0.012, 0.11), C["brass"], bevel=0.006, seg=1)
    for i in range(4):
        body.box((-0.15 + 0.10 * i, -D / 2 - 0.016, 0.32), (0.05, 0.006, 0.05), C["bakelite"])
    # rivets le long des arêtes de l'armoire
    rivet_row(body, (-W / 2 + 0.05, 0.14), (W / 2 - 0.05, 0.14), 14, 0.024, C["steel_l"], -D / 2 - 0.005)
    rivet_row(body, (-W / 2 + 0.05, 1.00), (W / 2 - 0.05, 1.00), 14, 0.024, C["steel_l"], -D / 2 - 0.005)
    for xs in (-W / 2 + 0.05, W / 2 - 0.05):
        rivet_row(body, (xs, 0.18), (xs, 0.96), 6, 0.024, C["steel_l"], -D / 2 - 0.005)
    # grilles d'aération latérales : lamelles
    for side in (-1, 1):
        for i in range(7):
            body.box((side * (W / 2 + 0.002), 0.02, 0.28 + 0.07 * i), (0.012, 0.28, 0.025), C["bottle_d"], bevel=0.004, seg=1)
    # poignées de manutention en laiton
    for side in (-1, 1):
        body.box((side * (W / 2 + 0.03), 0.0, 0.70), (0.03, 0.03, 0.22), C["brass_d"], bevel=0.008)

    # --- points de montage : sur le plan incliné du pupitre (normale n vers l'opérateur, u = direction « vers le haut du pupitre »)
    rot_panel = (-tilt, 0, 0)
    n = (0.0, -math.cos(tilt), math.sin(tilt))
    u = (0.0, math.sin(tilt), math.cos(tilt))
    centre = (0.0, -0.06, 1.28)
    front = (centre[0], centre[1] + n[1] * 0.06, centre[2] + n[2] * 0.06)       # face avant du pupitre (épaisseur 0,12)

    def on_panel(x, dz):                                                         # dz : distance le long du pupitre
        return (x, front[1] + u[1] * dz + n[1] * 0.004, front[2] + u[2] * dz + n[2] * 0.004)
    a.mount("Mount_Selector", on_panel(0.0, 0.20), rot_panel)
    for i in range(4):                                  # 4 cadrans : température, vapeur, électricité, bruit
        a.mount("Mount_Gauge%d" % i, on_panel(-0.54 + 0.36 * i, -0.15), rot_panel)
    for i in range(3):                                  # voyants : alerte, critique, SCRAM
        a.mount("Mount_Lamp%d" % i, on_panel(0.56, 0.28 - 0.12 * i), rot_panel)
    for i in range(8):                                  # rangée de disjoncteurs (armoire basse)
        a.mount("Mount_Breaker%d" % i, (-0.49 + 0.14 * i, -D / 2 - 0.004, 0.86), (0, 0, 0))
    a.mount("Mount_Scram", (W / 2 + 0.09, -0.02, 0.95), (0, 0, 0))     # le levier SCRAM est à côté, bien en vue, sur le flanc droit
    return a


def breaker(name="Breaker"):
    """Disjoncteur de la rangée (réarmement individuel, ~20 au tableau électrique) : embase, manette à bascule, repère d'état."""
    a = Asset(name)
    body = a.part("Body")
    body.box((0, 0, 0), (0.055, 0.035, 0.12), C["bakelite"], bevel=0.007)
    body.box((0, -0.019, 0.040), (0.036, 0.006, 0.018), C["cream"], bevel=0.002, seg=1)      # étiquette
    body.box((0, -0.019, -0.048), (0.036, 0.006, 0.010), C["steel_d"])
    lever = a.part("Lever", location=(0, -0.020, -0.01), rotation=(0, 0, 0))
    lever.box((0, -0.010, 0.020), (0.026, 0.020, 0.052), C["steel_l"], bevel=0.005)
    lever.box((0, -0.020, 0.040), (0.030, 0.010, 0.014), C["red_d"], bevel=0.003, seg=1)
    a.mount("Mount_Face", (0, -0.025, 0))
    return a


def scram_lever(name="ScramLever"):
    """
    Levier SCRAM : GROS levier rouge sous capot plombé à soulever. Le rouge est réservé aux urgences réelles.
    Parties animables : Cover (charnière en haut), Seal (plomb + fil, à rompre), Lever (pivot en bas, bascule de ~70 degrés).
    """
    a = Asset(name)
    W, H = 0.34, 0.74
    base = a.part("Base")
    base.box((0, 0.03, 0), (W, 0.06, H), C["steel_d"], bevel=0.02, seg=3)                     # platine
    base.box((0, 0.0, 0), (W * 0.84, 0.03, H * 0.90), C["cream_d"], bevel=0.012)               # fond crème
    base.box((0, -0.018, -H * 0.38), (W * 0.74, 0.03, 0.06), C["red_d"], bevel=0.008)          # butée basse
    base.box((0, -0.024, H * 0.42), (W * 0.60, 0.012, 0.05), C["white"], bevel=0.004, seg=1)     # plaque « SCRAM »
    for i in range(5):
        base.box((-0.10 + 0.05 * i, -0.031, H * 0.42), (0.026, 0.006, 0.030), C["black"])
    for sx in (-1, 1):                                                                         # supports de la charnière
        base.box((sx * W * 0.38, -0.03, H * 0.46), (0.035, 0.06, 0.06), C["steel"], bevel=0.008)
    # chape du pivot du levier
    base.box((0, -0.03, -H * 0.30), (W * 0.46, 0.05, 0.09), C["steel"], bevel=0.012)
    # marques de fin de course
    base.box((0.115, -0.022, 0.02), (0.008, 0.008, H * 0.50), C["black"])
    base.box((-0.115, -0.022, 0.02), (0.008, 0.008, H * 0.50), C["black"])

    lever = a.part("Lever", location=(0, -0.045, -H * 0.30))                                   # pivot en bas ; repos = en haut
    lever.cyl((0, 0, 0), 0.034, W * 0.5, C["steel_d"], axis="X", segs=14, bevel=0.004)           # axe
    lever.cyl((0, 0, H * 0.36), 0.020, H * 0.72, C["red"], axis="Z", segs=16, bevel=0.004)       # bras
    lever.sphere((0, 0, H * 0.78), (0.060, 0.060, 0.060), C["red"], u=18, v=12)                  # poignée en boule
    lever.cyl((0, 0, H * 0.70), 0.034, 0.05, C["red_d"], axis="Z", segs=16)                      # collerette
    lever.box((0, -0.001, 0.02), (0.06, 0.05, 0.10), C["red_d"], bevel=0.01)                     # base du bras

    cover = a.part("Cover", location=(0, -0.06, H * 0.46), rotation=(0, 0, 0))                   # charnière en haut ; s'ouvre vers l'avant
    # capot = cadre ouvert à barreaux (cage plombée) : le levier rouge reste visible derrière
    cw, ch = W * 0.82, H * 0.92
    for sx in (-1, 1):
        cover.box((sx * (cw / 2 - 0.018), -0.045, -ch / 2), (0.036, 0.05, ch), C["steel_l"], bevel=0.008)       # montants
    cover.box((0, -0.045, -0.018), (cw, 0.05, 0.036), C["steel_l"], bevel=0.008)                                # traverse haute
    cover.box((0, -0.045, -ch + 0.018), (cw, 0.05, 0.036), C["steel_l"], bevel=0.008)                            # traverse basse
    for i in range(4):                                                                                           # barreaux horizontaux
        cover.box((0, -0.050, -ch * (0.2 + 0.2 * i)), (cw - 0.05, 0.016, 0.016), C["steel"], bevel=0.004, seg=1)
    cover.box((0, -0.075, -ch + 0.03), (W * 0.40, 0.04, 0.05), C["steel_l"], bevel=0.01)                        # poignée du capot
    for sx in (-1, 1):
        cover.cyl((sx * W * 0.32, 0.0, 0.0), 0.014, 0.05, C["steel"], axis="X", segs=10)                        # charnières

    seal = a.part("Seal", location=(0, -0.11, -H * 0.05))                                        # plomb sur fil : à rompre pour ouvrir
    seal.cyl((0, 0, 0), 0.020, 0.018, C["lead"], axis="Y", segs=12, bevel=0.003)
    seal.torus((0, 0.004, 0.04), 0.030, 0.005, C["steel_l"], axis="Y", major_segs=14, minor_segs=5)

    a.mount("Mount_Wall", (0, 0.06, 0))
    return a


def build_all():
    return [rk1_console(), breaker(), scram_lever()]


if __name__ == "__main__":
    out_dir, preview = cli_args()
    reset_scene()
    assets = build_all()
    for a in assets:
        a.build()
    for a in assets:
        path = a.export(out_dir)
        print("EXPORT %-14s %5d triangles  %s" % (a.name, a.stats(), os.path.basename(path)))
    if preview:                                   # --preview <dossier> : un PNG par asset et par vue
        os.makedirs(preview, exist_ok=True)
        render_each(assets, preview)
        print("PREVIEW", preview)
