"""Build a local HTML review index after Unity bake and screenshot reports exist.
Usage: python tools/build_all_asset_index.py [--root REPO] [--output FILE]
No Unity calls, no screenshot generation, no source-report mutation.
"""
import argparse
import html
import json
import os
from pathlib import Path
from urllib.parse import quote


def read_json(path):
    if not path.is_file():
        raise FileNotFoundError(f'Required completed report missing: {path}')
    return json.loads(path.read_text(encoding='utf-8-sig'))


def esc(value):
    return html.escape(str(value), quote=True)


def relative_link(path, root, destination):
    resolved = Path(path)
    if not resolved.is_absolute():
        resolved = root / resolved
    return quote(os.path.relpath(resolved, destination.parent).replace('\\', '/'), safe='/.:')


def build(root, destination):
    manifest_path = root / 'scratch_out/all-asset-scenes.json'
    shots_path = root / 'scratch_out/preview/AllAssets/screenshots.json'
    bake_path = root / 'docs/art/ALL_ASSET_LIGHTING_BAKE.json'
    manifest, capture, bake = map(read_json, (manifest_path, shots_path, bake_path))
    scenes = manifest.get('scenes', [])
    shots = capture.get('images', [])
    baked = {s['path']: s for s in bake.get('scenes', [])}
    if not scenes or not shots:
        raise ValueError('Completed scene and screenshot inventories must be nonempty.')
    for shot in shots:
        image = Path(shot['imagePath'])
        if not image.is_absolute():
            image = root / image
        if not image.is_file():
            raise FileNotFoundError(f'Screenshot report references a missing file: {image}')
    expected_paths = {s['path'] for s in scenes}
    captured_paths = {s['scenePath'] for s in shots}
    missing_capture = expected_paths - captured_paths
    missing_bake = expected_paths - set(baked)
    if missing_capture or missing_bake:
        raise ValueError(f'Incomplete reports. Missing captures: {sorted(missing_capture)}; missing bakes: {sorted(missing_bake)}')
    discovered = int(manifest.get('discoveredPrefabCount', 0))
    presented = int(manifest.get('showcasedPrefabCount', sum(s.get('prefabCount', 0) for s in scenes)))
    prefab_paths = [p for s in scenes if s.get('kit') != 'BoatEnvironment' for p in s.get('prefabPaths', [])]
    unique_prefabs = len(set(prefab_paths))
    if prefab_paths and unique_prefabs != presented:
        raise ValueError(f'Prefab coverage mismatch: {unique_prefabs} unique paths, declared {presented}.')
    if discovered != presented:
        raise ValueError(f'Discovered prefab coverage incomplete: {presented}/{discovered}.')
    galleries = [s for s in shots if s.get('kit') != 'BoatEnvironment' and s.get('view') == 'gallery']
    details = [s for s in shots if s.get('kit') != 'BoatEnvironment' and s.get('view') != 'gallery']
    boat = [s for s in shots if s.get('kit') == 'BoatEnvironment']
    seconds = sum(float(s.get('seconds', 0)) for s in baked.values())
    texels = sum(int(s.get('atlasTexels', 0)) for s in baked.values())
    runtime_lights = sum(int(s.get('runtimeLights', 0)) for s in baked.values())
    links = ' · '.join(f'<a href="{relative_link(p, root, destination)}">{esc(label)}</a>' for label, p in [('Scènes', manifest_path), ('Captures', shots_path), ('Bakes', bake_path)])
    cards = []
    for section, images in [('Galeries des assets', galleries), ('Bateau et compartiments', boat), ('Assets en détail', details)]:
        cards.append(f'<section><h2>{section}</h2><div class="grid">')
        for shot in images:
            scene = shot['scenePath']
            result = baked[scene]
            image_url = relative_link(shot['imagePath'], root, destination)
            scene_url = relative_link(scene, root, destination)
            title = Path(scene).stem
            view = shot.get('view', 'gallery')
            if view.startswith('detail '):
                title = view.split(';')[0][7:]
            cards.append(f'''<article><a href="{image_url}"><img src="{image_url}" alt="{esc(title)} — {esc(view)}" loading="lazy" width="1900" height="1100"></a>
<div class="caption"><h3>{esc(title)}</h3><p>{esc(shot.get('kit',''))} · {esc(view)} · {int(shot.get('prefabCount',0))} prefabs dans la scène</p>
<p>{int(shot.get('lightmapCount',0))} lightmaps · {int(shot.get('lightmappedRendererCount',0))} renderers lightmappés · {float(result.get('seconds',0)):.1f} s de bake</p>
<a href="{scene_url}">Fichier de scène Unity</a> · <a href="{image_url}">Image complète</a></div></article>''')
        cards.append('</div></section>')
    document = f'''<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Sous Tension — tous les assets Unity</title>
<style>body{{margin:0;background:#111c19;color:#e6dcbc;font:15px/1.55 system-ui,sans-serif}}main{{max-width:1500px;margin:auto;padding:28px}}h1{{font-size:30px;margin:0 0 12px}}h2{{margin-top:38px}}a{{color:#e8bd65}}.stats{{display:flex;flex-wrap:wrap;gap:12px;margin:22px 0}}.stat{{padding:14px 20px;background:#22362d;border:1px solid #3d5648;border-radius:8px}}.stat strong{{display:block;font-size:23px}}.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:20px}}article{{background:#1d2b25;border:1px solid #405347;border-radius:8px;overflow:hidden}}img{{display:block;width:100%;height:auto;aspect-ratio:1900/1100;object-fit:contain;background:#182322}}.caption{{padding:14px}}h3{{font-size:16px;margin:0}}p{{margin:8px 0}}.limits{{max-width:1000px;color:#c4c1aa}}footer{{margin:38px 0}}</style></head><body><main>
<h1>Sous Tension — bibliothèque Unity éclairée</h1><p>Captures des scènes enregistrées avec leurs lightmaps. Cliquer une image pour examiner sa résolution complète.</p>
<div class="stats"><div class="stat"><strong>{presented}/{discovered}</strong>prefabs représentés exactement une fois</div><div class="stat"><strong>{len(scenes)}</strong>scènes couvertes</div><div class="stat"><strong>{len(shots)}</strong>captures disponibles</div><div class="stat"><strong>{seconds/60:.1f} min</strong>bake cumulé</div></div>
<p>{links}</p><p class="limits">Les totaux proviennent des rapports locaux, pas d'une estimation. Atlas cumulés : {texels:,} texels ; lumières runtime rapportées : {runtime_lights}. Ces nombres décrivent des scènes distinctes et ne constituent pas une charge simultanée en jeu.</p>
{''.join(cards)}
<footer class="limits"><h2>Portée des vérifications</h2><p>La couverture, les captures et les données de bake sont vérifiées par les rapports. Examiner visuellement l'éclairage, les matériaux, les personnages et les effets reste nécessaire. Les réglages de lightmaps, les lumières baked, le SRP Batcher et la réduction des ombres supplémentaires limitent certains coûts ; aucun benchmark FPS, profil GPU/CPU, budget mémoire sur matériel cible ou mesure de performance en gameplay n'est fourni ici.</p><p>Cette page ne modifie ni scène Unity, ni lightmap, ni prefab. Les fichiers et images sont liés relativement pour une consultation locale.</p></footer></main></body></html>'''
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(document, encoding='utf-8')
    print(f'INDEX {destination} — {presented}/{discovered} prefabs, {len(scenes)} scenes, {len(shots)} screenshots')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    destination = args.output.resolve() if args.output else root / 'scratch_out/preview/AllAssets/index.html'
    build(root, destination)


if __name__ == '__main__':
    main()
