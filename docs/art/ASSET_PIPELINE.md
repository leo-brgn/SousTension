# Pipeline d'assets 3D (E1-03, #18)

Comment un objet passe de Blender au jeu, et comment en ajouter un. Les détails par kit sont dans `blender/README.md` ; les conventions visuelles dans `docs/art/ART_DIRECTION.md`.

## 1. De la source au prefab
```
blender/kit_<kit>.py  --(blender/build.ps1)-->  Assets/_Project/Art/Models/<Kit>/<Asset>.fbx   (+ blender/sources/<Kit>.blend)
        |                                                   |
   lib.py (couleurs de sommets, primitives)       ArtModelPostprocessor (import : échelle, hiérarchie, pivots, Mount_*)
                                                            |
                          Editor MvpAssetBuild / V1AssetBuild  -->  Assets/_Project/Art/Prefabs/<Kit>/<Asset>.prefab
                                                            |
                          Editor BoatEnvironmentBuilder  -->  Prefabs/BoatEnvironment.prefab  (le bateau assemblé, d'après boat_layout.json)
```
- **Matériau** : tous les modèles utilisent `SousTension/VertexColorLit` (couleur dans les **couleurs de sommets**, PBR URP) ; émission HDR et teinte par `_EmissionColor` / `_Tint`. Le matelot et les bras ont leur variante `CrewVertexLit`.
- **Unités** : mètres, Y vers le haut dans Unity (Z haut dans Blender). Les exports convertissent les points Blender `(x, y, z)` en Unity `(-x, z, -y)` : le bateau préfabriqué est donc **tourné de 180°** pour tomber dans le repère de la simulation (x droite, z avant, proue = +z) ; voir `BoatEnvironmentView`.

## 2. Conventions de nommage et de hiérarchie
| Élément | Règle |
|---|---|
| Fichier | `PascalCase` sans espace, un modèle = un FBX ; kit = dossier (`Structure`, `Instruments`, `Reactor`, `Primary`, `Machines`, `Central`, `Tools`, `Damage`, `Lighting`, `Environment`, `Crew`…) |
| Racine | un objet racine du nom du modèle ; **parties animables** = enfants nommés par leur rôle (`Wheel`, `Lever`, `Cover`, `Rotor`, `Switch`, `Needle`, `Door`, `Indicator`…) avec **leur pivot à l'axe de rotation** |
| Ancres | enfants vides `Mount_<Rôle>` : où poser un autre modèle (`Mount_Gauge0`, `Mount_Breaker12`, `Mount_Grip`, `Mount_Leak`, `Mount_Wall`…). Les instruments sont **placés sur les ancres** des supports, jamais à la main |
| Objet posé dans le bateau | nom d'instance `<NN>_<Salle>_<Asset>_<n>` (ex. `04_Reactor_ScramLever_302`) : le jeu retrouve les équipements par ce nom (`BoatEnvironmentView.All / Nearest`) |
| Lumières | `RoomLight` (une par compartiment), `EmergencyLight` (rouge, secours) |

## 3. Les deux types d'objets de jeu
- **Interactif 🔧** : un support + des **parties mobiles** (2 à 4 sous-meshes : corps, levier, aiguille, capot) avec pivots animables. Dans le jeu les vues animent ces parties à partir de l'état du serveur (`PartRig` : rotation autour d'un pivot) et les rendent **visables** (`SpikeArt.MakeAimable` : un collider boîte ajusté + l'identifiant que le serveur comprend, ex. `valve2`, `breaker_light1`, `hatch3`). Mise en lumière à la visée par l'émission du matériau.
- **Transportable 📦** : un modèle avec `Mount_Grip` et un **collider de prise**, une **masse** définie côté données (`ITEM_KINDS` du serveur / `CarryLoad` du client : 1,2 % de vitesse par kg) ; deux mains ou une main selon la taille.

## 4. Ajouter un objet (liste de contrôle)
1. Modèle Blender dans `blender/kit_<kit>.py` (parties nommées, pivots, ancres `Mount_*`), `./blender/build.ps1 -Kit <Kit> -Preview`.
2. Unity : menu **Sous Tension → Art → Build and validate MVP assets** (prefabs, matériaux, validations du kit).
3. Si le jeu doit le faire apparaître à l'exécution : l'ajouter à `PlayLocal.ArtEntries` (clé → prefab) puis **SousTension → Spike → Refresh the art catalog** ; l'appeler par `SpikeArt.Spawn("<Clé>", parent)`.
4. Si c'est un équipement du bateau : l'ajouter à `kit_environment.py` (position d'après le plan `boat_layout.json`) ; les tests `BoatLayoutTests` vérifient que l'art place les écoutilles et les commandes là où le serveur les attend.
5. Interactif : brancher la vue (`Views/*View.cs`) sur ses parties ; l'identifiant visable doit exister côté serveur (`InteractableIds` est comparé au serveur par un test).
6. Textes sur un objet : **jamais écrits dans le modèle** (gabarits vierges) ; la localisation les fournit.

## 5. Limites actuelles
- Pas de **LOD** : le bateau (≈ 660 rendus) suffit au MVP, à mesurer avant d'en ajouter (voir les relevés `[Perf]` du journal).
- Les prefabs du matelot / des bras sont encore des prototypes d'animation (une autre session les retravaille).
- Le catalogue d'art du spike est dans `Resources` pour être embarqué dans les builds ; la version finale passera par des **Addressables** ou des références de scène.
