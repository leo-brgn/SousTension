"""Vérifications automatiques d'images générées (Pillow + numpy) : une image est ACCEPTÉE ou REJETÉE avec des raisons.

Ce que ces contrôles attrapent : image vide/uniforme, trop sombre ou cramée, floue ou « soupe » sans détail, doublon d'une image
déjà acceptée. Ce qu'ils n'attrapent PAS (revue visuelle sur planche de contact) : anatomie, texte déformé, fidélité au sujet.
Le score de palette (couleurs du jeu : vert bouteille, crème, rouille, laiton, acier, rouge unique) est indicatif, jamais bloquant.
"""
import io

import numpy as np
from PIL import Image

PALETTE = {                       # sRGB, voir blender/lib.py et le GDD (§6 Direction artistique)
    "bottle": (47, 90, 66), "cream": (230, 220, 188), "rust": (138, 74, 43), "brass": (201, 162, 74),
    "steel": (124, 129, 128), "red": (217, 38, 28), "black": (21, 19, 15), "white": (242, 238, 224),
}

DEFAULT_RULES = {
    "min_std": 20.0,              # écart-type de la luminance : en dessous = image presque uniforme
    "mean_range": (25.0, 232.0),  # luminance moyenne acceptable
    "max_black_frac": 0.55,       # part de pixels quasi noirs
    "max_white_frac": 0.55,       # part de pixels quasi blancs
    "min_sharpness": 6.0,         # variance du laplacien (image à 512 px) : en dessous = flou/bouillie
    "dup_hamming": 18,            # distance de Hamming (dHash 16x16, 256 bits) en dessous de laquelle c'est un doublon
}


def analyze(data):
    """`data` = bytes d'image. Renvoie un dict de mesures (et le hash perceptuel)."""
    img = Image.open(io.BytesIO(data)).convert("RGB")
    w, h = img.size
    small = img.copy()
    small.thumbnail((512, 512))
    arr = np.asarray(small, dtype=np.float32)
    lum = 0.299 * arr[..., 0] + 0.587 * arr[..., 1] + 0.114 * arr[..., 2]
    lap = -4 * lum[1:-1, 1:-1] + lum[:-2, 1:-1] + lum[2:, 1:-1] + lum[1:-1, :-2] + lum[1:-1, 2:]
    tiny = np.asarray(small.resize((128, 128)), dtype=np.float32).reshape(-1, 3)
    dists = np.stack([np.linalg.norm(tiny - np.array(c, dtype=np.float32), axis=1) for c in PALETTE.values()], axis=1)
    pal = float((dists.min(axis=1) < 55).mean())
    # empreinte perceptuelle « dHash » 16x16 (256 bits) : bien plus fine que l'8x8, ne confond plus deux images différentes sur fond uni
    g = np.asarray(img.convert("L").resize((17, 16)), dtype=np.float32)
    ahash = int("".join("1" if v else "0" for v in (g[:, 1:] > g[:, :-1]).flatten()), 2)
    return {
        "width": w, "height": h,
        "mean": round(float(lum.mean()), 1), "std": round(float(lum.std()), 1),
        "black_frac": round(float((lum < 12).mean()), 3), "white_frac": round(float((lum > 243).mean()), 3),
        "sharpness": round(float(lap.var()), 1), "palette": round(pal, 3), "ahash": ahash,
    }


def hamming(a, b):
    return bin(a ^ b).count("1")


def verdict(m, accepted_hashes, expected_size=None, rules=None):
    """Renvoie (ok, [raisons de rejet])."""
    r = dict(DEFAULT_RULES)
    r.update(rules or {})
    why = []
    if expected_size and (m["width"], m["height"]) != tuple(expected_size):
        why.append("taille %dx%d != %s" % (m["width"], m["height"], tuple(expected_size)))
    if m["std"] < r["min_std"]:
        why.append("quasi uniforme (écart-type %.1f)" % m["std"])
    if not (r["mean_range"][0] <= m["mean"] <= r["mean_range"][1]):
        why.append("luminance %.0f hors plage" % m["mean"])
    if m["black_frac"] > r["max_black_frac"]:
        why.append("%.0f %% de noir" % (m["black_frac"] * 100))
    if m["white_frac"] > r["max_white_frac"]:
        why.append("%.0f %% de blanc" % (m["white_frac"] * 100))
    if m["sharpness"] < r["min_sharpness"]:
        why.append("flou/sans détail (netteté %.1f)" % m["sharpness"])
    for h in accepted_hashes:
        if hamming(m["ahash"], h) <= r["dup_hamming"]:
            why.append("doublon d'une image déjà acceptée")
            break
    return (not why), why
