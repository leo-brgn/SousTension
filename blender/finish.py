"""
Finition PBR des modèles : matériaux adaptés + dépliage UV + cuisson de textures + export FBX + rendu 3D.

Lit des FBX « couleurs de sommets » (produits par blender/kit_*.py, jamais modifiés ici) et écrit, pour chaque modèle :
    <out>/Models/<Kit>/<Nom>.fbx          modèle avec UV (atlas unique), mêmes noms de parties, pivots et Mount_* que l'original
    <out>/Textures/<Kit>/<Nom>_BaseColor.png   couleur (sRGB)
    <out>/Textures/<Kit>/<Nom>_Mask.png        R = métallique, G = occlusion ambiante, B = 0, A = lissé (1 - rugosité)
    <out>/Textures/<Kit>/<Nom>_Normal.png      normales (espace tangent, convention OpenGL = Unity)
    <out>/Renders/<Kit>/<Nom>.png              rendu « beauté » (Cycles, GPU, débruité)
    <out>/Reports/<Kit>/<Nom>.json             mesures : résolution, utilisation de l'atlas UV, familles de matériaux, durée

Lancer :
    blender --background --factory-startup --python blender/finish.py -- --kit Instruments --in <dossier_fbx_absolu> --out <dossier_sortie_absolu>
            [--only Nom1,Nom2] [--skip-existing] [--max-res N] [--color-mode file-linear|file-srgb] [--res N] [--render-res 960x720] [--samples 32] [--no-render] [--no-bake]

Principe des matériaux : la couleur de chaque face vient de la couleur de sommet d'origine (la conception de l'artiste est conservée) ;
la famille de matériau (peinture, laiton, acier, rouille, bakélite, caoutchouc, verre) est déduite de cette couleur et fixe métallique /
rugosité / usure. Les détails (marbrures de saleté, éclats de peinture sur les arêtes, taches de rouille, rayures de métal, grain) sont
procéduraux, puis cuits dans les textures : rien de procédural ne part dans le jeu.
"""
import colorsys
import json
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector

import re

# Cartes / décalques / surfaces d'eau : supports d'EFFETS (transparence, lueur, vagues) qui demandent un shader d'effet, pas un matériau PBR opaque.
VFX_ONLY = re.compile(r"(Decal|Card|Puddle|WaterTile|WaterCompartment)(_w+)?$")


# ------------------------------------------------------------------ arguments
def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"skip": False, "max_res": 2048, "color_mode": "file-linear", "kit": "Misc", "in": None, "out": None, "only": None, "res": None, "render_res": (960, 720), "samples": 32, "render": True, "bake": True, "emit_only": False}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--kit": opts["kit"] = argv[i + 1]; i += 2
        elif a == "--in": opts["in"] = argv[i + 1]; i += 2
        elif a == "--out": opts["out"] = argv[i + 1]; i += 2
        elif a == "--only": opts["only"] = set(argv[i + 1].split(",")); i += 2
        elif a == "--res": opts["res"] = int(argv[i + 1]); i += 2
        elif a == "--render-res": w, h = argv[i + 1].lower().split("x"); opts["render_res"] = (int(w), int(h)); i += 2
        elif a == "--samples": opts["samples"] = int(argv[i + 1]); i += 2
        elif a == "--skip-existing": opts["skip"] = True; i += 1
        elif a == "--max-res": opts["max_res"] = int(argv[i + 1]); i += 2
        elif a == "--color-mode": opts["color_mode"] = argv[i + 1]; i += 2
        elif a == "--no-render": opts["render"] = False; i += 1
        elif a == "--no-bake": opts["bake"] = False; i += 1
        elif a == "--emit-only": opts["emit_only"] = True; i += 1          # ne retraite que les modèles qui ont des faces émissives
        else: i += 1
    if not opts["in"] or not opts["out"]:
        raise SystemExit("usage: --in <dossier fbx> --out <dossier sortie> [--kit Nom]")
    return opts


# ------------------------------------------------------------------ couleurs des FBX
# Le FBX stocke la couche « Col ». Blender l'importe en la traitant comme des CODES sRGB et la convertit en linéaire.
#   file-linear (actuel : blender/lib.py exporte avec colors_type=LINEAR) : le fichier contient DÉJÀ des valeurs linéaires lin(c) ; l'import les
#       convertit une seconde fois -> valeur lue = lin(lin(c)). L'albédo linéaire correct est donc le fichier = encode(valeur lue).
#   file-srgb (si l'export est corrigé en colors_type=SRGB) : le fichier contient les codes c ; valeur lue = lin(c) = albédo linéaire correct.
COLOR_MODE = "file-linear"


def srgb_encode(x):
    """Linéaire -> code sRGB, formule exacte (la puissance 2,2 est fausse dans les tons sombres : tronçon linéaire sous 0,0031)."""
    x = np.clip(x, 0.0, 1.0)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def decode_colors(read):
    """`read` = valeurs (N,3) lues dans la couche « Col » après import. Renvoie (albédo linéaire, code sRGB d'origine)."""
    albedo = srgb_encode(read) if COLOR_MODE == "file-linear" else read
    return albedo, srgb_encode(albedo)


# ------------------------------------------------------------------ familles de matériaux
def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# Couleurs de la palette du jeu (blender/lib.py) -> famille. Les couleurs inconnues passent par une heuristique teinte/valeur.
PALETTE = {
    "bottle": ("paint", "#2F5A42"), "bottle_d": ("paint", "#1F3D2D"), "cream": ("paint", "#E6DCBC"), "cream_d": ("paint", "#C9BE9A"),
    "white": ("paint", "#F2EEE0"), "red": ("paint", "#D9261C"), "red_d": ("paint", "#9E1A13"), "grey_floor": ("paint", "#585E5B"),
    "amber": ("paint", "#E8A317"), "green_lamp": ("paint", "#4CAF50"),
    "rust": ("rust", "#8A4A2B"), "rust_d": ("rust", "#5E301B"),
    "brass": ("brass", "#C9A24A"), "brass_d": ("brass", "#8F7230"),
    "steel": ("steel", "#7C8180"), "steel_d": ("steel", "#4E5352"), "steel_l": ("steel", "#A9AEAD"), "lead": ("steel", "#8E959A"),
    "bakelite": ("bakelite", "#2A1A12"), "black": ("rubber", "#15130F"), "glass": ("glass", "#9FB8B5"),
}
# Émission : faces des couleurs de voyant (ambre, vert lampe) et toutes les faces des pièces nommées Lens/Bulb/Lamp (lentilles de lampes, quelle que soit leur couleur).
EMISSIVE_COLORS = [hexrgb(PALETTE["amber"][1]), hexrgb(PALETTE["green_lamp"][1])]
EMISSIVE_OBJ = re.compile(r"(Lens|Bulb|Lamp)", re.I)
EMIT_STATS = {"faces": 0, "colors": True}
# L'ambre y est une couleur de matière (canard, bande de la combinaison), pas un voyant : ces modèles n'émettent pas.
EMISSIVE_COLORS_SKIP = re.compile(r"(Duck|Sailor|Commander|Cook|Diving|Uniform|Pyjamas|Vareuse|Arms)", re.I)
FAMILIES = ["paint", "rust", "brass", "steel", "bakelite", "rubber", "glass"]
PALETTE_RGB = {k: (fam, hexrgb(h)) for k, (fam, h) in PALETTE.items()}


def classify(rgb):
    """Couleur sRGB brute d'une face -> famille de matériau."""
    best, bd = None, 1e9
    for fam, c in PALETTE_RGB.values():
        d = sum((rgb[i] - c[i]) ** 2 for i in range(3))
        if d < bd:
            best, bd = fam, d
    if bd < 0.012:                       # assez proche d'une couleur connue
        return best
    h, s, v = colorsys.rgb_to_hsv(*rgb)
    if v < 0.16: return "rubber"
    if s < 0.14 and 0.25 < v < 0.75: return "steel"
    if s < 0.20 and v >= 0.75: return "paint"
    if 0.08 < h < 0.16 and s > 0.45 and v > 0.55: return "brass"
    if h < 0.10 and v < 0.62 and s > 0.35: return "rust"
    if 0.45 < h < 0.6 and s < 0.35: return "glass"
    return "paint"


# ------------------------------------------------------------------ nœuds
def new(nt, t, **kw):
    n = nt.nodes.new(t)
    for k, v in kw.items():
        setattr(n, k, v)
    return n


def mix_rgb(nt, fac, a, b, blend="MIX"):
    n = new(nt, "ShaderNodeMix", data_type="RGBA", blend_type=blend)
    if isinstance(fac, (int, float)): n.inputs[0].default_value = fac
    else: nt.links.new(fac, n.inputs[0])
    for idx, v in ((6, a), (7, b)):
        if isinstance(v, (tuple, list)): n.inputs[idx].default_value = (*v[:3], 1.0)
        else: nt.links.new(v, n.inputs[idx])
    return n.outputs[2]


def mix_f(nt, fac, a, b):
    n = new(nt, "ShaderNodeMix", data_type="FLOAT")
    if isinstance(fac, (int, float)): n.inputs[0].default_value = fac
    else: nt.links.new(fac, n.inputs[0])
    for idx, v in ((2, a), (3, b)):
        if isinstance(v, (int, float)): n.inputs[idx].default_value = v
        else: nt.links.new(v, n.inputs[idx])
    return n.outputs[0]


def map_range(nt, src, lo, hi, out_lo=0.0, out_hi=1.0, clamp=True):
    n = new(nt, "ShaderNodeMapRange", clamp=clamp)
    nt.links.new(src, n.inputs[0])
    n.inputs[1].default_value, n.inputs[2].default_value = lo, hi
    n.inputs[3].default_value, n.inputs[4].default_value = out_lo, out_hi
    return n.outputs[0]


def math_n(nt, op, a, b=None):
    n = new(nt, "ShaderNodeMath", operation=op)
    for idx, v in ((0, a), (1, b)):
        if v is None: continue
        if isinstance(v, (int, float)): n.inputs[idx].default_value = v
        else: nt.links.new(v, n.inputs[idx])
    return n.outputs[0]


def build_material(family, size, res=512):
    """Matériau procédural d'une famille. `size` = plus grande dimension du modèle (m), pour régler l'échelle des détails.
    Renvoie (matériau, {'color','rough','metal'} = sockets sources pour la cuisson)."""
    mat = bpy.data.materials.new("PBR_" + family)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = new(nt, "ShaderNodeOutputMaterial")
    bsdf = new(nt, "ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    sc = max(1.0, 4.0 / max(size, 0.05))                       # échelle de bruit : ~4 marbrures sur la taille de l'objet
    max_freq = (res / max(size, 0.05)) / 7.0                  # fréquence max que la texture peut porter (évite le repliement en blocs)
    texco = new(nt, "ShaderNodeTexCoord")
    vcol = new(nt, "ShaderNodeVertexColor", layer_name="Albedo")
    # couche « Albedo » : albédo linéaire exact, calculé en Python par decode_colors() (voir « couleurs des FBX »)
    base = vcol.outputs["Color"]
    emit_src = new(nt, "ShaderNodeVertexColor", layer_name="Emit").outputs["Color"]

    def noise(scale, detail=5.0, rough=0.55):
        n = new(nt, "ShaderNodeTexNoise", noise_dimensions="3D")
        n.inputs["Scale"].default_value, n.inputs["Detail"].default_value, n.inputs["Roughness"].default_value = scale, detail, rough
        nt.links.new(texco.outputs["Object"], n.inputs["Vector"])
        return n.outputs["Fac"]

    big, mid, fine = noise(min(sc * 1.5, max_freq * 0.2), 4), noise(min(sc * 7, max_freq * 0.5), 5), noise(min(sc * 60, max_freq), 6, 0.6)

    # masque d'arête : Bevel (normale lissée sur les arêtes) comparée à la normale géométrique
    bevel = new(nt, "ShaderNodeBevel")
    bevel.inputs["Radius"].default_value = max(0.002, size * 0.006)
    bevel.samples = 8
    geo = new(nt, "ShaderNodeNewGeometry")
    dot = new(nt, "ShaderNodeVectorMath", operation="DOT_PRODUCT")
    nt.links.new(bevel.outputs["Normal"], dot.inputs[0])
    nt.links.new(geo.outputs["Normal"], dot.inputs[1])
    edge = map_range(nt, math_n(nt, "SUBTRACT", 1.0, dot.outputs["Value"]), 0.03, 0.22)                 # arêtes franches seulement
    edge_noisy = math_n(nt, "MULTIPLY", edge, map_range(nt, mid, 0.56, 0.72))                           # éclats rares et irréguliers

    mott = map_range(nt, big, 0.30, 0.70, 0.80, 1.00)                                  # marbrures de saleté
    color = mix_rgb(nt, 1.0, base, mott, "MULTIPLY")       # un flottant relié à une entrée couleur = gris
    metal = 0.0
    rough = 0.5
    height = fine

    if family == "paint":
        steel_c = tuple(c ** 2.2 for c in hexrgb("#4E5352"))
        rust_c = tuple(c ** 2.2 for c in hexrgb("#6A3A1F"))
        rust_mask = math_n(nt, "MULTIPLY", map_range(nt, big, 0.68, 0.82), map_range(nt, mid, 0.55, 0.72))
        color = mix_rgb(nt, rust_mask, color, rust_c)
        color = mix_rgb(nt, edge_noisy, color, steel_c)                                # peinture écaillée : acier à nu aux arêtes
        metal = 0.0 if os.environ.get("FINISH_NOCHIP") else mix_f(nt, edge_noisy, 0.0, 1.0)
        rough = mix_f(nt, edge_noisy, mix_f(nt, rust_mask, math_n(nt, "ADD", 0.38, math_n(nt, "MULTIPLY", fine, 0.14)), 0.88), 0.50)
        height = mix_f(nt, edge_noisy, math_n(nt, "MULTIPLY", fine, 0.35), 0.0)
    elif family == "rust":
        dark = mix_rgb(nt, map_range(nt, mid, 0.35, 0.7), color, (0.05, 0.02, 0.01))
        color = dark
        metal = 0.25
        rough = math_n(nt, "ADD", 0.80, math_n(nt, "MULTIPLY", mid, 0.18))
        height = math_n(nt, "ADD", mid, fine)
    elif family == "brass":
        patina = math_n(nt, "MULTIPLY", map_range(nt, mid, 0.55, 0.80), 0.55)                              # patine discrète
        color = mix_rgb(nt, patina, color, tuple(c ** 2.2 for c in hexrgb("#5B4A22")))
        color = mix_rgb(nt, edge, color, tuple(c ** 2.2 for c in hexrgb("#E3C46E")))   # arêtes polies
        metal = 1.0
        rough = math_n(nt, "ADD", 0.28, math_n(nt, "MULTIPLY", mix_f(nt, patina, fine, 1.0), 0.22))
        height = math_n(nt, "MULTIPLY", fine, 0.3)
    elif family == "steel":
        scratch = map_range(nt, fine, 0.58, 0.72)
        color = mix_rgb(nt, scratch, color, tuple(c ** 2.2 for c in hexrgb("#B9BDBC")))
        rust_spot = math_n(nt, "MULTIPLY", map_range(nt, big, 0.76, 0.88), map_range(nt, mid, 0.62, 0.80))      # rares taches de rouille
        color = mix_rgb(nt, rust_spot, color, tuple(c ** 2.2 for c in hexrgb("#6A3A1F")))
        metal = mix_f(nt, rust_spot, 1.0, 0.3)
        rough = mix_f(nt, rust_spot, math_n(nt, "ADD", 0.40, math_n(nt, "MULTIPLY", fine, 0.25)), 0.85)
        height = math_n(nt, "MULTIPLY", fine, 0.25)
    elif family == "bakelite":
        rough = math_n(nt, "ADD", 0.22, math_n(nt, "MULTIPLY", fine, 0.12))
        color = mix_rgb(nt, edge, color, (0.06, 0.04, 0.03))
        height = math_n(nt, "MULTIPLY", fine, 0.15)
    elif family == "rubber":
        rough = math_n(nt, "ADD", 0.72, math_n(nt, "MULTIPLY", fine, 0.15))
        height = math_n(nt, "MULTIPLY", fine, 0.3)
    elif family == "glass":
        rough = 0.07
        height = 0.0
        color = mix_rgb(nt, map_range(nt, big, 0.4, 0.7), color, (0.55, 0.65, 0.63))   # voile de salissure

    # câblage Principled
    nt.links.new(color, bsdf.inputs["Base Color"])
    for key, v in (("Metallic", metal), ("Roughness", rough)):
        if isinstance(v, (int, float)): bsdf.inputs[key].default_value = v
        else: nt.links.new(v, bsdf.inputs[key])
    bump = new(nt, "ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.55
    bump.inputs["Distance"].default_value = max(0.002, size * 0.01)
    if isinstance(height, (int, float)): bump.inputs["Height"].default_value = height
    else: nt.links.new(height, bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    # sources pour la cuisson (via émission)
    def as_socket(v, kind):
        if isinstance(v, (int, float)):
            n = new(nt, "ShaderNodeValue")
            n.outputs[0].default_value = v
            return n.outputs[0]
        return v
    return mat, {"emit": emit_src, "color": color, "rough": as_socket(rough, "f"), "metal": as_socket(metal, "f"), "bsdf": bsdf, "out": out}


# ------------------------------------------------------------------ scène
def meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def bbox_size():
    pts = [o.matrix_world @ Vector(c) for o in meshes() for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def pick_res(size):
    return 256 if size < 0.10 else 512 if size < 1.5 else 1024 if size < 6.0 else 2048


def assign_families(ob):
    """Répartit les faces d'un objet en emplacements de matériau selon la famille déduite de leur couleur de sommet."""
    me = ob.data
    attr = me.color_attributes.get("Col") or (me.color_attributes[0] if me.color_attributes else None)
    me.materials.clear()
    for f in FAMILIES:
        me.materials.append(None)
    counts = {f: 0 for f in FAMILIES}
    if attr is None:
        for p in me.polygons: p.material_index = FAMILIES.index("paint")
        me.color_attributes.new("Emit", "FLOAT_COLOR", "CORNER")
        return counts
    cols = np.zeros(len(me.loops) * 4, dtype=np.float32)
    attr.data.foreach_get("color", cols)
    cols = cols.reshape(-1, 4)
    albedo, code = decode_colors(cols[:, :3])
    alb_attr = me.color_attributes.get("Albedo") or me.color_attributes.new("Albedo", "FLOAT_COLOR", "CORNER")
    out_a = np.concatenate([albedo, np.ones((len(albedo), 1), dtype=np.float32)], axis=1).astype(np.float32)
    alb_attr.data.foreach_set("color", out_a.reshape(-1))
    emit = np.zeros((len(me.loops), 4), dtype=np.float32)
    emit[:, 3] = 1.0
    whole = bool(EMISSIVE_OBJ.search(ob.name))
    for p in me.polygons:
        c = code[list(p.loop_indices)].mean(axis=0)
        fam = classify((float(c[0]), float(c[1]), float(c[2])))
        p.material_index = FAMILIES.index(fam)
        counts[fam] += 1
        if whole or (EMIT_STATS["colors"] and any(sum((float(c[i]) - e[i]) ** 2 for i in range(3)) < 0.012 for e in EMISSIVE_COLORS)):
            idx = list(p.loop_indices)
            emit[idx, :3] = albedo[idx]
            EMIT_STATS["faces"] += 1
    if os.environ.get("FINISH_DEBUG"): print("EMITDBG", ob.name, "whole" if whole else "colors", int((emit[:, :3].sum(axis=1) > 0).sum()), "loops", flush=True)
    em_attr = me.color_attributes.get("Emit") or me.color_attributes.new("Emit", "FLOAT_COLOR", "CORNER")
    em_attr.data.foreach_set("color", emit.reshape(-1))
    return counts


def unwrap_and_pack(objs):
    """Dépliage « Smart UV Project » de toutes les parties puis atlas unique, échelle de texel uniforme."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.012, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=0.006)
    bpy.ops.object.mode_set(mode="OBJECT")


def uv_stats(objs):
    """Utilisation de l'atlas (somme des aires d'îlots / 1) et débordement hors [0,1]."""
    area, oob = 0.0, 0
    for o in objs:
        me = o.data
        uv = me.uv_layers.active
        if uv is None:
            continue
        arr = np.zeros(len(me.loops) * 2, dtype=np.float32)
        uv.data.foreach_get("uv", arr)
        arr = arr.reshape(-1, 2)
        oob += int(((arr < -0.001) | (arr > 1.001)).any(axis=1).sum())
        for p in me.polygons:
            pts = arr[list(p.loop_indices)]
            a = 0.0
            for i in range(len(pts)):
                x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % len(pts)]
                a += x1 * y2 - x2 * y1
            area += abs(a) / 2
    return round(float(area), 3), int(oob)


# ------------------------------------------------------------------ cuisson
def setup_cycles(samples):
    scn = bpy.context.scene
    scn.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    gpu = False
    for t in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = t
            prefs.get_devices()
            devs = [d for d in prefs.devices if d.type == t]
            if devs:
                for d in prefs.devices:
                    d.use = (d.type == t)
                gpu = True
                break
        except Exception:
            continue
    scn.cycles.device = "GPU" if gpu else "CPU"
    scn.cycles.samples = samples
    scn.cycles.use_denoising = True
    return gpu


def bake_pass(objs, mats_info, image, source_key, bake_type, margin=None, **kw):
    """Cuit une passe : relie temporairement la source choisie à une émission (ou utilise un type natif), cuit sur `image`."""
    scn = bpy.context.scene
    for o in objs:
        for slot in o.material_slots:
            m = slot.material
            if m is None: continue
            nt = m.node_tree
            tex = nt.nodes.get("BAKE_TEX") or new(nt, "ShaderNodeTexImage", name="BAKE_TEX")
            tex.image = image
            nt.nodes.active = tex
            info = mats_info[m.name]
            if source_key:
                em = nt.nodes.get("BAKE_EM") or new(nt, "ShaderNodeEmission", name="BAKE_EM")
                nt.links.new(info[source_key], em.inputs["Color"])
                nt.links.new(em.outputs[0], info["out"].inputs["Surface"])
            else:
                nt.links.new(info["bsdf"].outputs["BSDF"], info["out"].inputs["Surface"])
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if margin is None:
        margin = max(1, int(image.size[0] * 0.004))              # marge < gouttière de l'atlas (0,6 %) : une couleur ne déborde pas sur un îlot voisin
    bpy.ops.object.bake(type=bake_type, margin=margin, margin_type="EXTEND", use_clear=False, **kw)


def new_image(name, res, color_space, fill):
    img = bpy.data.images.new(name, res, res, alpha=False)
    img.colorspace_settings.name = color_space
    img.generated_color = (*fill, 1.0)
    return img


def px(img):
    a = np.zeros(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(-1, 4)


def save_png(arr, res, path, name, color_space):
    img = bpy.data.images.new(name, res, res, alpha=True)
    img.colorspace_settings.name = color_space
    img.alpha_mode = "CHANNEL_PACKED"                        # RGBA = 4 canaux indépendants (le masque met le lissé dans l'alpha)
    img.pixels.foreach_set(arr.astype(np.float32).reshape(-1))
    img.file_format = "PNG"
    img.filepath_raw = path
    img.save()
    bpy.data.images.remove(img)


# ------------------------------------------------------------------ rendu
def build_final_material(name, base_img, mask_img, normal_img, emit_img=None):
    """Matériau final : trois textures cuites (c'est ce que verra Unity ; sert aussi au rendu de contrôle)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    out = new(nt, "ShaderNodeOutputMaterial")
    bsdf = new(nt, "ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    b = new(nt, "ShaderNodeTexImage", image=base_img)
    nt.links.new(b.outputs["Color"], bsdf.inputs["Base Color"])
    m = new(nt, "ShaderNodeTexImage", image=mask_img)
    sep = new(nt, "ShaderNodeSeparateColor")
    nt.links.new(m.outputs["Color"], sep.inputs[0])
    nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
    inv = math_n(nt, "SUBTRACT", 1.0, m.outputs["Alpha"])
    nt.links.new(inv, bsdf.inputs["Roughness"])
    n = new(nt, "ShaderNodeTexImage", image=normal_img)
    nm = new(nt, "ShaderNodeNormalMap")
    nt.links.new(n.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if emit_img is not None:
        e = new(nt, "ShaderNodeTexImage", image=emit_img)
        nt.links.new(e.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 3.0
    return mat


def render_beauty(path, res, samples, lo, hi):
    scn = bpy.context.scene
    size = (hi - lo).length
    centre = (lo + hi) / 2
    # fond : sol clair qui reçoit l'ombre + dégradé de ciel
    bpy.ops.mesh.primitive_plane_add(size=size * 14, location=(centre.x, centre.y, lo.z - 0.002))
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("Ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.18, 0.17, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
    ground.data.materials.append(gm)
    world = bpy.data.worlds.new("W")
    scn.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.68, 1)
    bg.inputs["Strength"].default_value = 0.18
    def area(loc, energy, sz, color=(1, 1, 1)):
        bpy.ops.object.light_add(type="AREA", location=loc)
        l = bpy.context.active_object
        l.data.energy, l.data.size, l.data.color = energy, sz, color
        l.rotation_euler = (centre - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    d = size * 2.2
    k = d * d                                     # l'énergie suit le carré de la distance : même exposition quelle que soit la taille de l'objet
    area(centre + Vector((0.9, -1.2, 1.1)).normalized() * d, 52 * k, size * 1.6, (1.0, 0.96, 0.9))   # clé chaude
    area(centre + Vector((-1.2, -0.6, 0.5)).normalized() * d, 14 * k, size * 1.8, (0.85, 0.92, 1.0))  # remplissage froid
    area(centre + Vector((-0.2, 1.2, 0.9)).normalized() * d, 28 * k, size * 1.2)                      # contre-jour
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 70
    cam = bpy.data.objects.new("Cam", cam_data)
    scn.collection.objects.link(cam)
    direction = Vector((0.65, -1.0, 0.55)).normalized()
    cam.location = centre + direction * (size * 2.6)
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scn.camera = cam
    scn.render.resolution_x, scn.render.resolution_y = res
    scn.render.filepath = path
    scn.render.image_settings.file_format = "PNG"
    available = [v.identifier for v in scn.view_settings.bl_rna.properties["view_transform"].enum_items]
    scn.view_settings.view_transform = next((t for t in ("Khronos PBR Neutral", "AgX", "Standard") if t in available), "Standard")   # couleurs de produit fidèles
    scn.cycles.samples = samples
    bpy.ops.render.render(write_still=True)
    for ob in (ground, cam):
        bpy.data.objects.remove(ob, do_unlink=True)


# ------------------------------------------------------------------ un modèle
def finish_one(fbx, name, opts):
    t0 = time.time()
    kit, out = opts["kit"], opts["out"]
    for sub in ("Models", "Textures", "Renders", "Reports"):
        os.makedirs(os.path.join(out, sub, kit), exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx, axis_forward="-Z", axis_up="Y", use_custom_normals=True)
    objs = meshes()
    if not objs:
        raise RuntimeError("aucun mesh")
    EMIT_STATS["faces"] = 0
    EMIT_STATS["colors"] = not EMISSIVE_COLORS_SKIP.search(name)
    lo, hi = bbox_size()
    size = max((hi - lo).x, (hi - lo).y, (hi - lo).z)
    res = min(opts["max_res"], opts["res"] or pick_res(size))
    gpu = setup_cycles(max(8, opts["samples"] // 2))

    # 1. familles + matériaux procéduraux
    counts = {f: 0 for f in FAMILIES}
    for o in objs:
        for f, n in assign_families(o).items():
            counts[f] += n
    if opts["emit_only"] and EMIT_STATS["faces"] == 0:
        return None
    used = [f for f in FAMILIES if counts[f] > 0]
    mats, info = {}, {}
    for f in used:
        m, i = build_material(f, size, res)
        mats[f], info[m.name] = m, i
    for o in objs:
        for idx, f in enumerate(FAMILIES):
            o.data.materials[idx] = mats.get(f, mats[used[0]])
    # 2. UV
    unwrap_and_pack(objs)
    uv_area, oob = uv_stats(objs)
    report = {"name": name, "kit": kit, "resolution": res, "size_m": round(size, 3), "families": {f: counts[f] for f in used},
              "uv_utilisation": uv_area, "uv_out_of_bounds_loops": oob, "gpu": gpu, "meshes": len(objs), "emissive_faces": EMIT_STATS["faces"]}

    base_p = os.path.join(out, "Textures", kit, name + "_BaseColor.png")
    mask_p = os.path.join(out, "Textures", kit, name + "_Mask.png")
    norm_p = os.path.join(out, "Textures", kit, name + "_Normal.png")
    emit_p = os.path.join(out, "Textures", kit, name + "_Emission.png")
    has_emit = EMIT_STATS["faces"] > 0
    if os.path.exists(emit_p) and not has_emit:
        os.remove(emit_p)
    if opts["bake"]:
        # 3. cuisson
        scn = bpy.context.scene
        scn.cycles.samples = 12                                 # le noeud Bevel (masque d'arête) est échantillonné : 1 échantillon = mouchetures
        im_base = new_image(name + "_b", res, "sRGB", (0.5, 0.5, 0.5))
        bake_pass(objs, info, im_base, "color", "EMIT")
        im_r = new_image(name + "_r", res, "Non-Color", (0.5, 0.5, 0.5))
        bake_pass(objs, info, im_r, "rough", "EMIT")
        im_m = new_image(name + "_m", res, "Non-Color", (0.0, 0.0, 0.0))
        bake_pass(objs, info, im_m, "metal", "EMIT")
        im_e = None
        if has_emit:
            im_e = new_image(name + "_e", res, "sRGB", (0.0, 0.0, 0.0))
            bake_pass(objs, info, im_e, "emit", "EMIT")
        scn.cycles.samples = max(16, opts["samples"])
        im_n = new_image(name + "_n", res, "Non-Color", (0.5, 0.5, 1.0))
        bake_pass(objs, info, im_n, None, "NORMAL")
        im_ao = new_image(name + "_ao", res, "Non-Color", (1.0, 1.0, 1.0))
        scn.world = scn.world or bpy.data.worlds.new("W")
        scn.world.light_settings.distance = max(0.05, size * 0.25)
        bake_pass(objs, info, im_ao, None, "AO")
        if os.environ.get("FINISH_DEBUG"):                       # sauvegarde chaque passe cuite pour diagnostic
            dbg = os.path.join(out, "Debug", kit)
            os.makedirs(dbg, exist_ok=True)
            for im, tag in ((im_base, "base"), (im_r, "rough"), (im_m, "metal"), (im_n, "normal"), (im_ao, "ao")):
                im.filepath_raw = os.path.join(dbg, name + "_" + tag + ".png"); im.file_format = "PNG"; im.save()
        # 4. assemblage des textures
        base, rough, metal, ao, nrm = px(im_base), px(im_r), px(im_m), px(im_ao), px(im_n)
        mask = np.zeros_like(base)
        mask[:, 0] = metal[:, 0]
        mask[:, 1] = ao[:, 0]
        mask[:, 2] = 0.0
        mask[:, 3] = 1.0 - rough[:, 0]
        base[:, 3] = 1.0
        nrm[:, 3] = 1.0
        save_png(base, res, base_p, name + "_BaseColor", "sRGB")
        save_png(mask, res, mask_p, name + "_Mask", "Non-Color")
        save_png(nrm, res, norm_p, name + "_Normal", "Non-Color")
        if im_e is not None:
            em = px(im_e); em[:, 3] = 1.0
            save_png(em, res, emit_p, name + "_Emission", "sRGB")
        # matériau final à base de textures cuites
        ib = bpy.data.images.load(base_p); ib.colorspace_settings.name = "sRGB"
        imk = bpy.data.images.load(mask_p); imk.colorspace_settings.name = "Non-Color"; imk.alpha_mode = "CHANNEL_PACKED"
        inr = bpy.data.images.load(norm_p); inr.colorspace_settings.name = "Non-Color"
        ie = None
        if has_emit:
            ie = bpy.data.images.load(emit_p); ie.colorspace_settings.name = "sRGB"
        final = build_final_material(name, ib, imk, inr, ie)
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(final)
    # 5. export FBX (mêmes réglages que blender/lib.py) ; les couleurs de sommets ne servent plus : tout est dans les textures
    for o in objs:
        for ca in list(o.data.color_attributes):
            o.data.color_attributes.remove(ca)
    # modèles à squelette : on garde l'armature (sinon le skinning est perdu) et on n'applique pas ses modificateurs (pose de repos)
    rigged = any(o.type == "ARMATURE" for o in bpy.context.scene.objects)
    types = {"MESH", "EMPTY", "ARMATURE"} if rigged else {"MESH", "EMPTY"}
    # comme blender/lib.py : pas de bake_space_transform dès qu'une pièce est enfant d'une autre (sinon l'empty racine garde une rotation de 90° et les pivots dérivent)
    bake = not rigged and not any(o.parent is not None and o.parent.type == "MESH" for o in objs)
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.context.scene.objects:
        if o.type in types:
            o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out, "Models", kit, name + ".fbx"), use_selection=True, object_types=types,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=bake,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, use_mesh_modifiers=not rigged,
        use_armature_deform_only=False, use_custom_props=False)
    # 6. rendu de contrôle
    if opts["render"]:
        render_beauty(os.path.join(out, "Renders", kit, name + ".png"), opts["render_res"], opts["samples"], lo, hi)
    report["seconds"] = round(time.time() - t0, 1)
    with open(os.path.join(out, "Reports", kit, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    return report


def main():
    global COLOR_MODE
    opts = parse_args()
    COLOR_MODE = opts["color_mode"]
    files = sorted(f for f in os.listdir(opts["in"]) if f.lower().endswith(".fbx"))
    ok, failed = 0, []
    for f in files:
        name = os.path.splitext(f)[0]
        if opts["only"] and name not in opts["only"]:
            continue
        if VFX_ONLY.search(name):
            print("SKIPVFX %s (support d'effet : pas de matériau PBR)" % name, flush=True)
            continue
        if opts["skip"] and os.path.exists(os.path.join(opts["out"], "Reports", opts["kit"], name + ".json")):
            print("SKIP   %s (déjà fait)" % name, flush=True)
            continue
        try:
            r = finish_one(os.path.join(opts["in"], f), name, opts)
            if r is None:
                print("NOEMIT %s (aucune face émissive)" % name, flush=True)
                continue
            ok += 1
            print("FINISH %-26s res=%4d uv=%.2f oob=%d %s %.1fs" % (name, r["resolution"], r["uv_utilisation"], r["uv_out_of_bounds_loops"], r["families"], r["seconds"]), flush=True)
        except Exception as e:                                   # un modèle en échec ne bloque pas le lot
            failed.append((name, str(e)[:200]))
            print("FAILED %s : %s" % (name, str(e)[:200]), flush=True)
    print("KIT %s : %d terminés, %d en échec" % (opts["kit"], ok, len(failed)), flush=True)
    for n, e in failed:
        print("  -", n, e, flush=True)


main()
