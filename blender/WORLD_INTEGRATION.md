# World / extérieur — bibliothèque GDD V1

`blender/kit_world.py` expose `build_all()` : 76 modèles. Source `blender/sources/World.blend`, exports `Assets/_Project/Art/Models/World`, manifeste `world_manifest.json`, 76 rendus dans `scratch_out/preview/World`. Blender 5.2.2 : triangles non dégénérés, sommets finis, couleurs `Col`, UVMap. Les UV sont des projections simples pour matériaux et supports visuels ; aucun atlas peint n'est livré.

| Ligne du GDD | Modèles |
|---|---|
| Coque Molosse : 3 LOD × 3 usures | `MolossHull_LOD0..2_Wear0..2` (9 exports) |
| Extérieurs mobiles | `MolossPropeller`, `MolossRudder`, `MolossDivingPlane`, `MolossConningTower`, `MolossAntenna`, `MolossPeriscopeHead` |
| Base du fjord, environ 15 majeurs | 16 modèles `Fjord*` : workshop, stamp office, barracks, shed, pontoon, crane, notice board, lighthouse, bollard, gangway, ladder, fuel tank, pallet, fence, workbench, power pedestal |
| Terrain sous-marin | 8 `UnderwaterRock`, cliff, sand tile, kelp cluster, hydrothermal vent |
| Épaves explorables | `WreckCargo`, `WreckTrawler`, `WreckTwinSub` : ouvertures du pont/coque et plateformes intérieures |
| Dépôt militaire | `UnderwaterMilitaryDepot`, `DepotAirlock` avec deux portes indépendantes |
| Filets | `IndustrialFishingNet` : grille en cordages et quatre mounts d'angles |
| Trafic civil | `CivilFerry`, `CivilContainerShip`, `CivilSailboat`, `CivilJetSki`, `CivilPedalBoat` |
| Entente | `EntenteFrigate_LOD0/1`, `EntentePatrolPlane`, `SonarBuoy`, `ExerciseGrenade` |
| Offshore et régate | `OffshoreWindTurbine`, `OffshorePlatform`, `ChannelBuoy`, `Iceberg_0/1/2`, 10 `RaceSailboat_01..10`, `RaceSpectatorLowPoly` |

La régate contient dix exports avec dimensions de grand-voile, orientation et palettes différentes ; la coque et le gréement partagent la même construction. Instancier le spectateur plusieurs fois, varier rotation/échelle raisonnable et palette pour la foule. Ce sont des assets 1.0 de base, à mettre en scène ; pas de monde jouable assemblé dans cette source. Le sous-marin jumeau est uniquement une épave de la liste 1.0, pas un second bateau jouable. Les icebergs couvrent la liste 1.0, pas une zone Arctique post-lancement.

## Assemblage du Molosse

Coordonnées source : X largeur, Y longitudinal, Z vertical. Coque 36 × 6,8 × 6 m, centre au milieu du volume. L'intérieur existant de 28 m tient dans le corps ; l'alignement précis du pont intérieur doit être vérifié dans la scène. `Mount_Interior` = (0,0,-1.5). `Mount_Tower` = (0,1,2.85), hélice à (0,-18.3,0), safran à (0,-16,0), plans latéraux à X ±3.3 / Y -12. La pointe avant est +Y. Tourner le plan bâbord de 180° autour de Z pour qu'il s'étende à gauche ; son pivot est à la racine. Ne pas assigner les trois variantes d'usure dans un même LODGroup : choisir l'usure, puis les trois LODs correspondant à cette usure.

Trois résolutions de coque réduisent les anneaux longitudinaux de 48 à 24 puis 12 sommets par section. LOD2 omet les détails du pont et simplifie les plaques d'usure ; les frégates passent de 24 à 12 sommets par section et perdent les fenêtres. Utiliser le manifeste pour les triangles exacts. Ce sont des géométries réellement différentes, pas des doublons renommés.

## Pivots et matériaux

Hélice : rotation autour de Y source / Z Unity à la racine, les cinq pales restent fixes dans l'ensemble. Safran : racine, axe Z source. Plans de plongée : racine, axe X source. Antenne et périscope : translation Z source. Éolienne : `Rotor` autour de Y source ; ses trois pales sont enfants. Grue : `Trolley` translation X et `Hook` translation Z. `DepotAirlock/Door0..1` : rotation Z source. Pédalo : `PaddleWheel` autour de X. Avion : deux propellers autour de Y. Voiles : `MainSail` rotation Z source. Bouées : mounts de lampe, ancrage et sonar selon l'asset. Le manifeste décrit parties et positions des mounts.

Voiles et kelp utilisent des faces simples avec UV ; fournir un matériau double-face ou cartes à transparence si nécessaire. Les autres meshes utilisent les couleurs de sommets. Les peintures administratives du tableau d'affichage, le beacon du phare, les lumières et les bulles/fumées des cheminées nécessitent leurs matériaux/contenus/VFX. Le filet est un visuel en cordage : le solver câble/tissu doit utiliser une grille de contrôle séparée ou remplacer le support selon sa technologie.

## Limites d'intégration

Pas de moteurs, animations enregistrées, physique du vent/eau/cargo, navigation des navires, IA Entente, interactions, terrain de fjord assemblé, collisions ou navmesh. Les épaves contiennent des zones ouvertes et des sols larges pour exploration partielle ; les parcours, rebords, échelles et collisions restent à mettre en scène. Les tailles sont choisies pour silhouettes de gameplay et ne prétendent pas reproduire des bâtiments/navires historiques. Vérification Unity, LODGroup, colliders concaves des zones explorables et essais joueurs nécessaires avant de déclarer les modèles intégrés à la release.
