# Assets procéduraux — Blender 5.2

Depuis la racine : `./blender/build.ps1 -Kit Primary -Preview`.
Sans `-Kit`, tous les kits sont générés. `-Blender` permet de fournir un autre exécutable.

## MVP et environnement global

Générer tous les modèles : `./blender/build.ps1 -Preview`.
Kits : Instruments, Reactor, Structure, Primary, Machines, Central, Tools, Damage, Crew, Lighting, Environment.
Les sorties FBX restent dans `Assets/_Project/Art/Models/<kit>/` ; sources éditables dans `blender/sources/`.

Dans Unity : menu **Sous Tension → Art → Build and validate MVP assets**
ou batch `-executeMethod SousTension.EditorTools.MvpAssetBuild.Run`.
Cette commande crée les prefabs, matériaux, contrôleurs d'animation et la scène
`Assets/_Project/Art/Scenes/BoatEnvironment.unity`, sans modifier la scène de gameplay existante.

L'environnement reprend les six compartiments en enfilade du GDD sur 28 m : torpilles, central, radio, réacteur, machines, vie.
La répartition provisoire est 4/6/4/4/4/6 m. Sas ouverts en quinconce et écrans de séparation gardent les postes isolés.
Le réacteur, les machines et le central utilisent les modèles détaillés ; radio et cambuse restent des blockouts P2.
Le builder vérifie une route pour une capsule de 46 cm sur une grille de 20 cm et les 15 paires de points de poste,
portes ouvertes. Cela ne remplace pas les tests multijoueurs dans le référentiel mobile.

Les exports actuels convertissent les points Blender `(x,y,z)` en Unity `(-x,z,-y)`.
Les rotations Blender X/Y/Z se convertissent vers Unity X/Z/Y avec les signes +/+/−.
Les scripts d'assemblage appliquent cette conversion vérifiée sur les points de montage importés.

Prefabs : outils avec masses prototype et colliders, Manuel 8 kg, caisses S/M/L, matelot/bras avec contrôleurs Generic,
éclairage Normal/LowVoltage/Emergency/Blackout, eau/cartes VFX, fuites et étincelles.
Les animations du matelot et des mains sont des prototypes ; les transitions, IK et commandes de gameplay restent à brancher.
Les 30 matériaux de feuilles utilisent des gabarits d'art **vierges**, sans prétendre fournir les procédures finalisées.
La lampe torche porte une lumière spot. Les effets sont des présentations autonomes, sans simulation d'eau ni alimentation électrique.

Sources globales : `blender/sources/BoatEnvironment.blend` ; rendus Blender : `scratch_out/preview/Environment/`.
Rendus natifs Unity : batch avec GPU `-executeMethod SousTension.EditorTools.EnvironmentCapture.Run`
→ `scratch_out/preview/UnityEnvironment/` (coupe, central, réacteur, machines et trois états de tension).
Les sources restent dans leur état normal, les variantes de capture ne sont pas sauvegardées dans la scène.

Voir `docs/art/MVP_ASSET_COVERAGE.md` pour la couverture P1 et les limites de contenu/interaction,
et `docs/art/ART_DIRECTION.md` pour les conventions visuelles.

## Circuit primaire — E11-08 / #116

Complément du tableau RK-1 et du SCRAM existants, suivant le GDD §3.3 et l'annexe Réacteur.

| Modèle | Triangles | Utilisation |
| --- | ---: | --- |
| PrimaryPump | 5 020 | Deux instances, rotor visible, commande et capot séparés |
| PrimaryPump_Broken | 5 056 | Variante endommagée, fissures et capot désaxé |
| PrimaryValve | 2 912 | Quatre instances, volant et indicateur séparés |

Exports : `Assets/_Project/Art/Models/Primary/`. Source editable : `blender/sources/PrimaryCircuit.blend`.
Les modèles sont à l'origine dans le fichier source ; sélectionner une racine pour isoler un modèle.
Aperçus face et trois quarts : `scratch_out/preview/Primary/`.

Conventions : mètres, Z haut dans Blender, Y haut dans Unity. `Col` contient les couleurs de sommets.
Palette laiton, bakélite, vert bouteille et rouille ; aucun rouge sur les commandes courantes.
Rotor : rotation autour de X Blender. Switch : bascule autour de X. Wheel : rotation autour de Y Blender.
Indicator : translation locale X entre -0,075 et +0,075 m. Les états marche/arrêt restent à câbler côté View.
Les positions Mount_Inlet/Outlet et Mount_A/B marquent les faces de raccordement, normales sur Z local.
Mount_Leak est un point d'ancrage VFX ; aucun effet ni gameplay n'est inclus.

Vérification Unity en batch : `-executeMethod SousTension.EditorTools.PrimaryAssetValidation.Run`.
Le contrôle vérifie l'import des trois FBX, les parties séparées, le matériau, l'échelle et les raccords.
La disposition en scène, les colliders et la liaison des états à la simulation restent à intégrer.

Le script de build utilise `--python-exit-code 1` pour signaler les erreurs Python.
Les variations de couleur sont reproductibles entre exécutions. Le cadrage des aperçus s'adapte aux vues obliques.

## Machines — E11-10 / #118

Génération : `./blender/build.ps1 -Kit Machines -Preview`.
Exports : `Assets/_Project/Art/Models/Machines/`. Source : `blender/sources/Machines.blend`.
Aperçus : `scratch_out/preview/Machines/`, y compris la turbine capot ouvert (`TurbineReducer_open.png`).

| Modèle | Triangles | Parties mobiles / montage |
| --- | ---: | --- |
| ElectricalPanel | 2 940 | Porte basse sur charnière, 20 emplacements de disjoncteurs + 2 cadrans |
| MachineBreaker | 472 | Levier à bascule indépendant, poignée bakélite sans rouge d'urgence |
| BilgePump | 3 100 | Rotor visible en rotation, capot démontable ; deux instances prévues |
| Interphone | 3 704 | Combiné séparé et cordon spiralé continu, réutilisable par compartiment |
| TurbineReducer | 10 012 | Rotor et capot articulé, réducteur, sortie d'arbre |
| BatteryBank | 4 036 | Quatre batteries, cosses, liaisons, protection articulée et emplacement voltmètre |

Le fichier Blender et les aperçus montrent le tableau assemblé : 20 instances du disjoncteur et 2 cadrans du kit instruments.
Les FBX restent modulaires : le tableau exporté est une coque avec emplacements, le disjoncteur est un FBX distinct.
Les cadrans de turbine et de batterie sont aussi assemblés uniquement dans la source/les aperçus.
Dans Unity, placer ces modèles enfants sur les `Mount_*` correspondants.
Les plaques crème sont vierges pour recevoir des textes localisés ultérieurement.

Axes Blender : levier de disjoncteur autour de X (±28°), porte basse autour de Z,
rotor de pompe autour de Z, rotor de turbine autour de X, capot de turbine autour de X (0 à -105°),
protection des batteries autour de Y. Ces axes sont convertis par le FBX vers Unity Y haut.
Le combiné peut être détaché ; le cordon reste un mesh statique à remplacer ou déformer lors de l'intégration.
Pas de gameplay, colliders, VFX ni animation enregistrée dans ce lot.

Contrôle Unity : `-executeMethod SousTension.EditorTools.MachinesAssetValidation.Run`.
Il vérifie les six FBX, les triangles, les couleurs de sommets, les matériaux, les noms des parties/raccords,
les pivots décentrés, l'échelle métrique et les 20 emplacements distincts de disjoncteurs.

## Compartiments détaillés du 5 octobre

Nouveaux kits : Radio (11 modèles), Living (19), Signage (15). Ils font partie du build par défaut et précèdent Environment. `./blender/build.ps1 -Kit Radio,Living,Signage,Crew,Environment -Preview` les régénère. Sources : `RadioSonar.blend`, `Living.blend`, `Signage.blend` dans `blender/sources/`. Le layout remplace les anciens blockouts radio/cambuse/couchettes et instancie les six plaques de compartiment. Les appareils radio, les portes et les accessoires gardent leurs pièces mobiles et mounts ; consulter `RADIO_INTEGRATION.md` et les manifests.

Le matelot conserve ses clips Generic avec des transitions de coudes à deux os et des prises corrigées. Rapport et limites : `docs/art/DETAIL_MODELS_2026-10-05.md`. Total importé et validé dans Unity : 152 FBX et 98 prefabs. Les modèles de sas/touret sont autonomes et ne sont pas installés dans le layout actuel.

## Bibliothèque complète V1

Le workflow courant est `./blender/build_v1.ps1 -Capture` ; ajouter `-Rebuild -Preview` pour refaire les 19 kits. Il valide les 109 besoins géométriques de l'annexe GDD 1.0, puis les 387 FBX dans Unity et leurs 412 prefabs. Rapport `docs/art/V1_DELIVERY.md` et pointage `docs/art/V1_ASSET_COVERAGE.md`. Catalogue natif `Assets/_Project/Art/Catalog/v1_asset_catalog.json`. Sources et guides séparés pour Fixtures, World, Cargo et CrewVariants. Les anciennes sections de ce README décrivent les lots successifs ; le rapport V1 est l'état courant.
