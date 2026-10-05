# Bibliothèque 3D V1 — livraison du 5 octobre 2026

Le périmètre est l'annexe Asset List v1.0 du GDD : toutes les familles P1/P2/P3 du bateau, des postes, du cargo, des zones, du trafic, des personnages et des cosmétiques. L'Arctique et le second bateau jouable post-lancement sont exclus. Le sous-marin jumeau est livré comme épave.

## Résultat et preuves

387 FBX répartis en 19 kits. Le pointage automatique relie 109 lignes de besoins du GDD et kits transversaux à des fichiers réellement présents : `V1_ASSET_COVERAGE.md` et son JSON, reconstruits par `blender/validate_v1_coverage.py`. Ce pourcentage concerne la couverture géométrique V1. Il ne mesure pas le gameplay ou la finition graphique.

Chaque FBX dispose d'un prefab natif Unity. Les prefabs de présentation, les variantes d'éclairage, le bateau et les trois assemblages de coque à LOD portent le total à 412 prefabs. `Assets/_Project/Art/Catalog/v1_asset_catalog.json` indexe chemins, triangles, pièces, montages, os et clips des 387 modèles. Le build commun vérifie chaque import, ses meshes, les couleurs et les matériaux. Les sept variantes de personnage utilisent des avatars Generic valides, 27 os et quatre clips, avec controllers de présentation.

La coque du Molosse a trois vrais LODs et trois usures : trois prefabs `MolossExterior_Wear0..2`. Les plans de plongée, le safran, l'hélice, le kiosque, l'antenne et la tête de périscope sont montés séparément. La frégate conserve deux FBX à géométries simplifiées. Les voiles et kelp ont un matériau double-face dans leurs prefabs.

Sources éditables dans `blender/sources/`. Galeries Unity : `V1_CargoGallery.unity`, `V1_FixturesGallery.unity`. Scène de bateau : `BoatEnvironment.unity`, six compartiments, quatre tubes reconvertis, sas avec véritable ouverture de cloison et porte intérieure ouverte. La capsule de test doit atteindre les six compartiments depuis les spawns de l'équipage et l'intérieur du sas ; la porte extérieure reste fermée. Les lignes de vue des quinze paires de postes restent bloquées portes de cloison ouvertes.

## Utilisation

Depuis PowerShell à la racine :

```powershell
./blender/build_v1.ps1 -Capture
# Refaire également les exports Blender et les rendus de chaque kit :
./blender/build_v1.ps1 -Rebuild -Preview -Capture
```

Le workflow est séquentiel aux étapes dépendantes : exports → pointage GDD → imports/validation/prefabs/catalogue/scènes → captures natives. Une étape en échec arrête le workflow et garde son log dans `scratch_out`. Il n'achète pas de crédits, ne crée pas de boucle infinie et ne marque pas une issue comme terminée automatiquement. Menu Unity équivalent : `Sous Tension/Art/Build and validate all V1 assets`.

Attention aux objets à sous-pièces imbriquées : l'export désactive `bake_space_transform` pour ceux-ci, car sa version activée déplaçait les meshes enfants dans le FBX. Les variantes riggées utilisent aussi l'export sans ce bake. Conserver la hiérarchie et les rotations de repos importées lors des animations.

## Niveau V1 et reste de production

- Géométrie stylisée, couleurs de sommets et UV simples ; les illustrations du Manuel, les huit affiches, le portrait/photo et les textes localisés restent vierges. Les meshes de support sont livrés.
- Les rigs/poses sont prototypes Generic. L'IK, le contrôle de ragdoll, la locomotion finale, la simulation des câbles/filets/lamelles, les fluides et les interactions ne sont pas implémentés par ces assets.
- Le scaphandre porté, la contamination et Varga existent comme personnages distincts. La contamination actuelle est une variante de géométrie/couleur, pas un système de shader piloté par dose.
- Bâtiments du hub : enveloppes extérieures ; épaves : intérieurs partiellement ouverts. Il reste le travail de niveaux, les parcours/navmeshes et les playtests. Le spectateur de régate est instanciable ; les dix voiliers ont des variantes de voiles.
- La finition exige une passe artistique sur les silhouettes, les matériaux, l'usure, l'éclairage intérieur sombre, les budgets de rendu et la lisibilité à hauteur joueur. Les masses et comportements physiques ne sont pas validés comme équilibrage final.
- Les douze améliorations annoncées par le GDD ne sont pas définies comme douze objets 3D nommés dans l'annexe ; leur design n'est pas inventé ici.

Guides spécifiques : `blender/FIXTURES_INTEGRATION.md`, `WORLD_INTEGRATION.md`, `RADIO_INTEGRATION.md`, `TORPEDOES_INTEGRATION.md`, `docs/art/CREW_VARIANTS.md` et manifests de chaque kit.

## Validation exécutée

`scratch_out/v1-workflow.log` contient `V1_WORKFLOW_PASS` : pointage GDD, build Unity, dix captures du bateau et onze captures d'assets. Les vues natives de la coque assemblée, de Varga et de l'Émetteur ont été inspectées. Scène actuelle : 364 instances modulaires et 219 colliders solides, quatre spawns connectés, six compartiments et sas atteignables, quinze lignes de vue masquées. `git diff --check` est passé.
