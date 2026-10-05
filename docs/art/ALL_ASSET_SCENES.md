# Toutes les scènes d'assets Unity

Générateur : `SousTension.EditorTools.AllAssetSceneBuilder.Build()` (menu Sous Tension / Art / Build all asset showcase scenes). Exécuter dans Unity ; le générateur ne lance ni bake ni capture. Le contrôleur de production doit ensuite ouvrir chaque scène, appliquer ses LightingSettings partagés, lancer le bake, attendre son résultat et produire la capture 1900 × 1100.

Le builder découvre récursivement **tous** les fichiers `.prefab` de `Assets/_Project/Art/Prefabs`, y compris Effects et les assemblages. La couverture est calculée à l'exécution, sans constante dépendant du nombre actuel. Chaque prefab apparaît exactement une fois dans les galeries. Le manifeste `scratch_out/all-asset-scenes.json` contient `scenes:[{path,kit,prefabCount,prefabPaths}]`, ainsi que les totaux découvert et présenté. La copie du bateau est une scène supplémentaire avec prefabCount=0 ; elle ne gonfle pas la couverture.

Les scènes sont enregistrées sous `Assets/_Project/Art/Scenes/Showcase`. Chaque kit est réparti en groupes de 24 prefabs maximum, World en groupes de 12. La disposition respecte les mètres et utilise les bounds des renderers : largeur et profondeur variables, rangées espacées, origine verticale calée sur le sol. Les noms des racines identifient les prefabs. Les objets ne sont pas réduits pour tenir dans une grille fixe. Les Rigidbody sont rendus kinematic dans les scènes de présentation pour conserver la disposition.

Chaque galerie contient un sol blanc mat URP Lit, une caméra orthographique ajustée à tous les bounds pour un ratio 1900/1100, deux lumières directionnelles **Baked** et un ambiant modeste. Le remplissage frontal a été ajouté après contrôle visuel des matériaux sombres. Les lumières internes des prefabs sont désactivées dans les galeries. Les MeshRenderer ordinaires sont ContributeGI et BatchingStatic ; les skinned meshes, particules et surfaces d'effets/eau restent dynamiques. Des probes entourent les personnages skinnés. World utilise un scaleInLightmap réduit de 0.03 à 0.15 ; les petits objets utilisent une densité augmentée. Le sol utilise 0.04 pour limiter son atlas.

Le générateur demande generateSecondaryUV pour les modèles statiques. **Le postprocesseur commun doit respecter cette demande** : une règle qui force generateSecondaryUV=false au reimport empêche la génération effective. Le builder signale le conflit, mais la préparation UV2 et la vérification du bake incombent au pipeline commun. Il ne modifie pas les LightingSettings globaux, le profil URP ni le contrôleur de capture.

Les overrides d'émission se limitent aux parties Lens, LightLens, LampGlass, Display, IndicatorLens et CoilGlow. Matériaux URP Lit persistants dans `Materials/Showcase` : crème chaude, ambre, rouge pour l'urgence. Les matériaux emissifs sont BakedEmissive. Les corps complets ne deviennent pas emissifs. Les autres matériaux d'origine, notamment les couleurs de sommets et matériaux VFX, sont conservés.

`BoatEnvironment_Lit.unity` est une copie enregistrée depuis le bateau existant. L'original n'est pas enregistré après mutation. Les lampes normales deviennent Baked, chaudes, portée 6.5 m, intensité 18 ; les urgences sont désactivées et leurs lentilles assombries. Une grille de probes couvre les six compartiments. Le builder ne change pas les placements, les colliders ni les interactions du bateau.

## Validation attendue

Le manifeste de couverture prouve la présence de chaque prefab ; il ne prouve pas un bake réussi. Vérifier les erreurs Unity, les UV2, les lightmaps, la luminosité des visages et des pièces intérieures, les surfaces transparentes, puis inspecter les screenshots après capture. Une absence de realtime lights dans les galeries suppose que le bake soit terminé avant d'évaluer leur apparence. Les paramètres du contrôleur peuvent être adaptés par scène pour éviter les atlas excessifs des grandes structures World.

## Index HTML après captures

Exécuter `python tools/build_all_asset_index.py` depuis le dépôt après le workflow Unity. Le script exige les trois rapports terminés (`scratch_out/all-asset-scenes.json`, `scratch_out/preview/AllAssets/screenshots.json`, `docs/art/ALL_ASSET_LIGHTING_BAKE.json`) et vérifie l'existence de chaque image, la couverture des scènes dans les captures et les bakes, ainsi que le total unique des prefabs. Il écrit `scratch_out/preview/AllAssets/index.html` avec galeries, vues du bateau, liens relatifs vers images/scènes/rapports et compteurs issus des données. Il ne lance pas Unity et ne modifie aucune scène.

Les bakes, la densité réduite des lightmaps World, les limites d'ombres et le SRP Batcher sont des réglages de coût. Ils ne constituent pas un benchmark FPS. Les performances en gameplay, la mémoire réelle, les coûts GPU/CPU sur matériel cible et la stabilité des effets restent à profiler.
