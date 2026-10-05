"""Boucle de génération avec vérification : pour chaque sujet d'un fichier de lot, génère des images jusqu'à en avoir N ACCEPTÉES.

    D:\\AI\\ComfyUI_windows_portable\\python_embeded\\python.exe tools/ai/run_batch.py tools/ai/prompts/comms_r1.json [--only id1,id2] [--n 4] [--redo] [--dry]

Pour chaque tentative : génération (ComfyUI) -> mesures (tools/ai/checks.py) -> acceptée ou rejetée avec la raison -> relance avec une autre graine.
Sorties (dossier IGNORÉ par Git) : ai_gen/<kind>/<nom du lot>/
    <id>/<id>_sNNNN.png            images acceptées          <id>/_rejected/…   images rejetées (nom = raison)
    <id>_sheet.png                  planche de contact de l'entrée (à relire à l'œil : anatomie, texte, fidélité au sujet)
    overview.png                    une vignette par entrée      manifest.jsonl   prompt, graine, mesures, verdict de chaque tentative
    report.md                       tableau récapitulatif
"""
import argparse
import json
import os
import sys
import time

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import checks
import comfy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_ROOT = os.path.join(ROOT, "ai_gen")


def load_styles():
    with open(os.path.join(HERE, "styles.json"), encoding="utf-8") as f:
        return json.load(f)


def make_sheet(items, out_path, thumb_w=448, cols=4):
    """items = [(chemin_png, légende)] -> planche de contact."""
    if not items:
        return None
    cols = min(cols, len(items))
    thumbs = []
    for path, cap in items:
        im = Image.open(path).convert("RGB")
        h = int(im.height * thumb_w / im.width)
        thumbs.append((im.resize((thumb_w, h), Image.LANCZOS), cap))
    cell_h = max(t.height for t, _ in thumbs) + 22
    rows = (len(thumbs) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (thumb_w + 8) + 8, rows * (cell_h + 8) + 8), (28, 30, 28))
    d = ImageDraw.Draw(sheet)
    for i, (im, cap) in enumerate(thumbs):
        x, y = 8 + (i % cols) * (thumb_w + 8), 8 + (i // cols) * (cell_h + 8)
        sheet.paste(im, (x, y))
        d.text((x + 2, y + im.height + 4), cap, fill=(220, 220, 200))
    sheet.save(out_path)
    return out_path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("batch")
    ap.add_argument("--only", help="ids séparés par des virgules")
    ap.add_argument("--n", type=int, help="images acceptées voulues par entrée (écrase le fichier de lot)")
    ap.add_argument("--redo", action="store_true", help="refait les entrées déjà complètes")
    ap.add_argument("--dry", action="store_true", help="affiche les prompts sans générer")
    args = ap.parse_args()

    with open(args.batch, encoding="utf-8") as f:
        batch = json.load(f)
    styles = load_styles()
    base_dir = os.path.join(OUT_ROOT, batch.get("kind", "misc"), batch["name"])
    os.makedirs(base_dir, exist_ok=True)
    entries = batch["entries"]
    if args.only:
        wanted = set(args.only.split(","))
        entries = [e for e in entries if e["id"] in wanted]

    client = None
    if not args.dry:
        client = comfy.Comfy()
        client.wait_ready()
    manifest_path = os.path.join(base_dir, "manifest.jsonl")
    summary = []
    t_batch = time.time()

    for e in entries:
        n = args.n or e.get("n") or batch.get("n", 3)
        size = e.get("size") or batch.get("size", [1024, 1024])
        style = styles[e.get("style") or batch.get("style", "key_art")]
        edit_from = e.get("edit_from")
        eid = e["id"]
        edir = os.path.join(base_dir, eid)
        rej_dir = os.path.join(edir, "_rejected")
        existing = sorted(p for p in os.listdir(edir) if p.endswith(".png")) if os.path.isdir(edir) else []
        if existing and len(existing) >= n and not args.redo:
            print("[%s] déjà %d images, ignoré (--redo pour refaire)" % (eid, len(existing)))
            continue
        os.makedirs(rej_dir, exist_ok=True)
        if edit_from:
            prompt = e["instruction"] + style.get("edit_suffix", "")
        else:
            prompt = style.get("prefix", "") + e["prompt"] + style.get("suffix", "")
        print("[%s] %s | %dx%d | style=%s" % (eid, "édition de " + os.path.basename(edit_from) if edit_from else "texte->image", size[0], size[1], e.get("style") or batch.get("style")))
        if args.dry:
            print("   ", prompt)
            continue

        uploaded = client.upload(os.path.join(ROOT, edit_from)) if edit_from else None
        accepted, hashes, attempts, errors = [], [], 0, 0
        max_attempts = n * batch.get("max_attempts_factor", 3)
        seed0 = batch.get("seed", 1000) + (abs(hash(eid)) % 100000) * 10
        while len(accepted) < n and attempts < max_attempts and errors < 3:
            seed = seed0 + attempts
            attempts += 1
            t0 = time.time()
            try:
                if edit_from:
                    graph = comfy.qwen_edit(uploaded, prompt, seed=seed, prefix="qe_" + eid)
                else:
                    graph = comfy.zimage_txt2img(prompt, size[0], size[1], seed=seed, prefix="zi_" + eid)
                images = client.run(graph, timeout=batch.get("timeout", 900))
                data = images[0]
            except Exception as ex:                                  # erreur de génération : on note et on retente
                errors += 1
                print("   tentative %d : ERREUR %s" % (attempts, str(ex)[:160]))
                with open(manifest_path, "a", encoding="utf-8") as mf:
                    mf.write(json.dumps({"id": eid, "seed": seed, "error": str(ex)[:300]}, ensure_ascii=False) + "\n")
                continue
            errors = 0
            m = checks.analyze(data)
            ok, why = checks.verdict(m, hashes, expected_size=None if edit_from else size, rules={**batch.get("rules", {}), **e.get("rules", {})})
            name = "%s_s%d.png" % (eid, seed)
            target = os.path.join(edir if ok else rej_dir, name if ok else name.replace(".png", "__REJET.png"))
            with open(target, "wb") as f:
                f.write(data)
            rec = {"id": eid, "seed": seed, "file": os.path.relpath(target, OUT_ROOT).replace("\\", "/"), "accepted": ok, "why": why,
                   "metrics": {k: v for k, v in m.items() if k != "ahash"}, "seconds": round(time.time() - t0, 1),
                   "prompt": prompt, "edit_from": edit_from}
            with open(manifest_path, "a", encoding="utf-8") as mf:
                mf.write(json.dumps(rec, ensure_ascii=False) + "\n")
            if ok:
                accepted.append((target, "s%d | pal %.2f | net %.0f | %.0fs" % (seed, m["palette"], m["sharpness"], time.time() - t0)))
                hashes.append(m["ahash"])
            print("   tentative %d (graine %d) : %s %s [%.0fs]" % (attempts, seed, "OK" if ok else "REJET", "; ".join(why), time.time() - t0))

        sheet = make_sheet(accepted, os.path.join(base_dir, eid + "_sheet.png"))
        summary.append({"id": eid, "accepted": len(accepted), "wanted": n, "attempts": attempts, "sheet": sheet, "first": accepted[0][0] if accepted else None})

    # vue d'ensemble + rapport
    overview_items = [(s["first"], "%s (%d/%d)" % (s["id"], s["accepted"], s["wanted"])) for s in summary if s["first"]]
    if overview_items:
        make_sheet(overview_items, os.path.join(base_dir, "overview.png"), thumb_w=480, cols=4)
    if summary:
        with open(os.path.join(base_dir, "report.md"), "a", encoding="utf-8") as r:
            r.write("\n## Exécution %s\n\n| entrée | acceptées | voulues | tentatives |\n|---|---|---|---|\n" % time.strftime("%Y-%m-%d %H:%M"))
            for s in summary:
                r.write("| %s | %d | %d | %d |\n" % (s["id"], s["accepted"], s["wanted"], s["attempts"]))
    print("terminé en %.0f s -> %s" % (time.time() - t_batch, base_dir))


if __name__ == "__main__":
    main()
