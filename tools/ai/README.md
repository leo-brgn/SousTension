# Génération d'images locale (ComfyUI) — communication et brainstorming

Boucle de génération **avec vérifications** pour produire des visuels de communication (key art, scènes « clip de 40 secondes », en-têtes Steam)
et d'idées (personnages, affiches, emblèmes, décors) avec les modèles installés sur `D:\AI\ComfyUI_windows_portable`.
**Les images vont dans `ai_gen/` (ignoré par Git).** Seuls ces outils et les fichiers de sujets sont versionnables.

## Modèles utilisés (déjà installés, rien à télécharger)
| Usage | Modèle | Vitesse mesurée (RTX 4070 SUPER, 12 Go) |
|---|---|---|
| Texte → image | Z-Image Turbo (int8), 8 étapes | ≈ 5 s pour 1344×768 |
| Retouche / restyle d'une image | Qwen-Image-Edit 2511 (int8) + LoRA Lightning 4 étapes | voir le journal du lot |
| (non utilisés ici) | Wan 2.2 (vidéo), ACE-Step (musique) | — |

## Utilisation
1. Démarrer ComfyUI (une fois) :
   ```powershell
   $out = "D:\Game\Sous Tension\ai_gen"
   Start-Process "D:\AI\ComfyUI_windows_portable\python_embeded\python.exe" -WindowStyle Hidden -WorkingDirectory "D:\AI\ComfyUI_windows_portable" `
     -ArgumentList "-s D:\AI\ComfyUI_windows_portable\ComfyUI\main.py --windows-standalone-build --listen 127.0.0.1 --port 8188 --disable-auto-launch --output-directory `"$out\_comfy_out`" --temp-directory `"$out\_comfy_tmp`""
   ```
   (les chemins avec une espace doivent être entre guillemets, comme ci-dessus)
2. Lancer un lot :
   ```powershell
   D:\AI\ComfyUI_windows_portable\python_embeded\python.exe tools/ai/run_batch.py tools/ai/prompts/comms_r1.json [--only id1,id2] [--n 4] [--redo] [--dry]
   ```

## La boucle (ce que fait `run_batch.py` pour chaque sujet)
génération → **mesures automatiques** (`checks.py`) → acceptée / rejetée avec la raison → relance avec une autre graine, jusqu'à N images acceptées
→ **planche de contact** à relire à l'œil → verdict (`review.json`) → prompts corrigés dans le lot suivant (`r2`, `r3`…).

Contrôles automatiques : image quasi uniforme, trop sombre / cramée, trop de noir ou de blanc, floue ou sans détail, taille inattendue, **doublon**
(empreinte dHash 16×16). Seuils réglables par lot ou par sujet (`"rules": {...}`) — indispensable pour une scène volontairement sombre.
Ils n'attrapent **pas** l'anatomie, le texte déformé ni la fidélité au sujet : c'est le rôle de la revue visuelle des planches.
Le score de palette (`pal`) mesure la part de pixels proches des couleurs du jeu ; il est indicatif.

## Fichiers
- `styles.json` : préfixes/suffixes de style partagés (`key_art`, `key_art_night`, `concept_paint`, `poster`, `character_sheet`, `photo`) ; `edit_suffix` pour les retouches.
- `prompts/*.json` : un lot = un nom, un type de dossier, une taille, un style par défaut et une liste de sujets (`id`, `prompt` ou `edit_from` + `instruction`).
- Sorties : `ai_gen/<kind>/<lot>/<id>/` (acceptées), `_rejected/` (rejets, raison dans `manifest.jsonl`), `<id>_sheet.png`, `overview.png`, `report.md`.

## Pièges rencontrés
- Le mot « submarine » dans une scène d'intérieur fait apparaître un sous-marin jouet : décrire la scène sans lui (« ship's galley »).
- Le style par défaut impose une lumière chaude : utiliser `key_art_night` pour les scènes sombres.
- Une grille d'objets variés (« douze moustaches ») donne douze fois le même : générer **une pièce par sujet**.
- Les modèles déforment le texte : tous les styles demandent « no text » ; le texte se pose ensuite dans un logiciel de montage.
