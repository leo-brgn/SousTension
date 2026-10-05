# QA visuelle des captures Unity

Relecture des PNG natifs Unity après ajout d'une lumière frontale baked et correction de la classification des objets Underwater pour recevoir les lightmaps. Aucun lancement Unity ni modification de scène pendant cette relecture.

## Échantillon inspecté

Première passe : Cargo_01, Cargo_02, Fixtures_01, Fixtures_02, Tools, Living, Machines, Signage. Nouvelle passe : Machines, Living, Fixtures_02, Detail_PeriscopeInterior, Detail_MarineToiletSevenValve, Detail_NuclearFuelTransportCask, Detail_UnderwaterRock_04 et Detail_UnderwaterSandTile_8m. Les images sont dans `scratch_out/preview/AllAssets`; la liste complète et les paramètres de capture sont dans `screenshots.json`.

| Point | Constat sur les images relues |
|---|---|
| Matériaux manquants | Aucun rose Unity observé. |
| Scènes entièrement noires | Aucune dans l'échantillon inspecté. |
| Cadres et bounds | Aucun objet de l'échantillon coupé par le bord. Le sol des galeries dépasse parfois le cadre, sans couper les modèles. |
| Éclairage de face | Amélioration visible : trous du panneau Machines, rainures du casier Living, charnières/poignée de la porte et détails de la douche sont plus distinguables qu'à la première passe. |
| Gros plans | Périscope, WC à sept vannes et château de transport sont cadrés intégralement ; volants, poignées, reliefs et boulons sont lisibles. |
| Terrain Underwater | Sable relu avec couleur et relief visibles ; le rocher reste très sombre, avec des facettes faiblement distinguables. |
| Émission | Pas de halo envahissant ni de modèle entier blanchi dans les vues relues. Ces vues ne constituent pas une vérification de chaque variante emissive. |

## Points corrigés et limites de lecture

La lumière frontale baked réduit la perte de détails sur les faces tournées vers la caméra. La palette bouteille et bakélite reste sombre : le grand cylindre noir de Fixtures_02, certaines faces du panneau Machines et le rocher Underwater gardent un contraste faible. Cette observation n'est pas un écran noir ou un shader rose ; un réglage artistique de palette/exposition peut encore améliorer la lecture selon les conditions du jeu.

Les vues de galerie montrent les tailles relatives et la couverture, mais ne permettent pas de lire chaque petite pièce à côté d'une grande structure. Les captures individuelles World ajoutées au rapport, ainsi que les détails des pièces importantes, permettent de vérifier ces silhouettes à une échelle adaptée. Leur présence dans le rapport n'implique pas que chacune ait été inspectée visuellement par cette passe : l'échantillon précis est listé ci-dessus. Pour les galeries de petits outils et les variantes de texte, un gros plan supplémentaire reste utile si le détail est nécessaire à une interaction.

Les parties cachées, le dessous des coques, les objets vus depuis une seule orientation et les surfaces transparentes peuvent nécessiter une vue complémentaire. Cette revue n'a pas testé les animations, les collisions, la physique, les interactions, le gameplay ni la luminosité dans toutes les conditions de caméra.

## Sources des compteurs et portée performance

Utiliser les rapports actuels, qui peuvent être régénérés après une nouvelle cuisson :

- `scratch_out/all-asset-scenes.json` : couverture des scènes et des prefabs.
- `scratch_out/preview/AllAssets/screenshots.json` : captures, vues, dimensions et lightmaps observées à la capture.
- `docs/art/ALL_ASSET_LIGHTING_BAKE.json` : lightmaps, texels d'atlas, probes, backend et lumières runtime par scène.

Les totaux d'atlas des scènes distinctes ne décrivent pas une charge simultanée en jeu. Le bake, les réglages des ombres et le batching ne prouvent pas un objectif FPS. Aucun benchmark GPU/CPU, mémoire de build sur matériel cible ou mesure FPS n'est déduit des images ou de ces rapports.
