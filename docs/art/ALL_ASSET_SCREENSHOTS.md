# Index des captures Unity — assets et lumière cuite

API : `SousTension.EditorTools.AllAssetSceneCapture.Run()` dans `Assets/_Project/Art/Editor/AllAssetSceneCapture.cs`.

Exécuter **après** génération et cuisson de toutes les scènes listées dans `scratch_out/all-asset-scenes.json`. Cette entrée contient `scenes:[{path,kit,prefabCount}]`. Chaque scène doit être sauvegardée avec ses lightmaps et posséder une caméra `MainCamera`. Le script refuse une scène sans lightmap sauvegardée. Le contrôleur racine est seul responsable des exécutions Unity pour éviter deux éditeurs concurrents.

Chaque image est un rendu natif URP de **1900 × 1100**, via `RenderPipeline.SubmitRenderRequest`/`UniversalRenderPipeline.SingleCameraRequest`. Deux requêtes initialisent le rendu de la scène chargée. Les réglages de volume/post-traitement du modèle sont conservés, y compris le bloom s'il est configuré. Les lightmaps et GI de la scène ne sont ni retirées ni désactivées.

## Sorties

- `scratch_out/preview/AllAssets/<sceneName>.png` : vue MainCamera de chaque scène de l'entrée.
- `scratch_out/preview/AllAssets/screenshots.json` : index exact des fichiers réellement générés, chemins absolus, scène source, kit, nombre de prefabs attendu, nombre de lightmaps et de renderers lightmappés, état du post-traitement, dimensions et vue.
- Pour la scène du kit `BoatEnvironment` : six captures supplémentaires `BoatEnvironment_Lit_01_Torpedoes.png`, `...02_Central.png`, `...03_Radio.png`, `...04_Reactor.png`, `...05_Machines.png`, `...06_Living.png`.
- `BoatEnvironment_Lit_cutaway.png` : vue OverviewCamera, PortWall/Roof masqués temporairement et lumière directionnelle PreviewSun ajoutée. Les lightmaps restent chargées ; la lumière supplémentaire est notée dans les métadonnées.
- `Detail_<asset>.png` : vues rapprochées de tous les prefabs World et de neuf assets ciblés des autres kits. Les modèles voisins sont masqués temporairement pour éviter les occultations ; leurs contributions déjà précalculées restent dans les lightmaps. Les renderers et la caméra sont restaurés. Le plafonnier est photographié par dessous, avec le plateau masqué, pour montrer sa lentille émissive.
- `index.html` : index généré après captures, avec sections galeries, bateau et assets en détail. Le champ prefabCount des captures décrit la scène source, même quand une vue rapprochée n'en présente qu'un.

Les positions intérieures proviennent du layout `Models/Environment/boat_layout.json` et des postes disponibles. La caméra et les renderers sont restaurés après chaque série bateau ; PreviewSun est détruit. Aucune scène n'est sauvegardée par le script de capture. La découpe constitue une vue de présentation, pas une nouvelle cuisson ni la preuve d'une visibilité correcte dans le jeu.

L'index JSON est la preuve des captures effectivement exécutées. L'existence de ce guide ou du script ne prouve pas que la cuisson/capture ait réussi. Après exécution, inspecter les PNG pour vérifier cadrage, lecture de tous les assets, matériaux, exposition et absence d'erreurs de bake ; une galerie dense peut demander des captures rapprochées supplémentaires.
