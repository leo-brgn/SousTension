# Couverture des assets MVP / P1

Audit du dépôt au 4 octobre 2026, pendant la production parallèle. Sources : annexe d'assets du `GDD_Sous_Pression.md`, Epic 11 et jalon PROTO de `BACKLOG.md`, FBX présents et scripts/manifests locaux. Les chemins ci-dessous partent de la racine du dépôt.

Le GDD définit P1 comme indispensable au prototype S+8 ; P2/P3 correspondent à des étapes ultérieures. Le jalon PROTO accepte explicitement greybox/placeholder pour E11-06..09 et un Manuel v0 papier. **La géométrie P1 dispose de supports exportés ; cela ne constitue pas un MVP fonctionnel ni une validation de production de tous les assets.** Cet audit n'attribue aucun taux de complétion au gameplay.

## Inventaire P1

Les noms dans les tableaux correspondent à `Assets/_Project/Art/Models/<Kit>/<Nom>.fbx`. Les objets réutilisables couvrent les quantités d'instances demandées ; exporter quatre vannes identiques n'ajouterait pas de géométrie.

| Demande GDD | Kit / exports présents | Source | Maturité et reste à faire |
|---|---|---|---|
| Coque intérieure droite 2 m, membrures/rivets/câbles | Structure / HullModule_2m | blender/kit_structure.py | Géométrie modulaire ; vérifier raccords et circulation dans la scène finale |
| Cloison pleine et sas manuel | Structure / Bulkhead_Solid, Bulkhead_Hatch, HatchDoor | blender/kit_structure.py | Porte/volant/6 loquets séparés ; états et commande réseau à intégrer |
| Caillebotis ajouré, fond de cale | Structure / FloorGrating_1m, BilgeFloor_2m | blender/kit_structure.py | Géométrie ; montée d'eau et collision de marche distinctes |
| Tuyaux droite/coude/T/vanne inline | Structure / Pipe_Straight_1m, Pipe_Elbow, Pipe_Tee, Pipe_ValveInline | blender/kit_structure.py | Kit disponible ; raccords d'instances et état de vanne à intégrer |
| Barre de direction | Central / CentralSteeringWheel | blender/kit_central.py | Volant à pivot ; entrée joueur/animation non prouvées par le FBX |
| Pupitre profondeur/assiette, 2 volants/indicateurs | Central / CentralDepthTrimConsole | blender/kit_central.py | Pièces séparées et aiguilles ; branchement aux données bateau requis |
| Télégraphe machine 5 positions | Central / EngineTelegraphFivePosition | blender/kit_central.py | Poignée et repères de position ; crantage/ordre machine requis |
| Table à cartes, carte papier, crayon gras | Central / CentralChartTable, ChartPaperBlank, GreasePencil | blender/kit_central.py | Carte vierge et surface UV ; dessin persistant et contenu carte requis |
| Manuel OK-114, 8 kg, pages feuilletables/arrachables | Central / ManualOK114OpenBinder, ManualOK114LoosePage | blender/kit_central.py | 30 feuilles à pivots et 30 PNG de mise en page vierge ; procédures/illustrations/localisation/shader de pages/lecture à deux mains requis |
| Pupitre multi-instruments | Central / CentralInstrumentConsole | blender/kit_central.py | Support et montages ; assembler instruments et lier mesures |
| Tube pneumatique et capsule ouvrable | Central / PneumaticArrivalStation, PneumaticCapsule, RolledOrderPaper | blender/kit_central.py | Trappe/capot et papier ; livraison/sifflement/contenu d'ordre requis |
| Tableau RK-1, sélecteur 3 régimes/disjoncteurs/cadrans | Reactor / RK1_Console, Breaker + Instruments | blender/kit_reactor.py | Sous-pièces/pivots disponibles ; montage et états issus simulation requis |
| Levier SCRAM rouge sous capot | Reactor / ScramLever | blender/kit_reactor.py | Capot/levier séparés ; action autoritative, plombage et feedback requis |
| Pompes primaire ×2, marche/arrêt/cassé | Primary / PrimaryPump, PrimaryPump_Broken | blender/kit_primary.py | Variantes exportées ; 2 instances et transitions d'état/effets requis |
| Vannes principales ×4 | Primary / PrimaryValve | blender/kit_primary.py | Géométrie réutilisable, pivot de volant ; 4 instances et animation/état requis |
| Tableau électrique ~20 disjoncteurs individuels | Machines / ElectricalPanel, MachineBreaker | blender/kit_machines.py | Support et module réutilisable ; poser modules à leurs montages et commandes individuelles |
| Pompes de cale ×2 | Machines / BilgePump | blender/kit_machines.py | Géométrie réutilisable ; 2 instances et état débit requis |
| Interphone par compartiment | Machines / Interphone | blender/kit_machines.py | Combiné et montages ; prise/voix/occlusion/câble requis |
| Caisses réglementaires S/M/L | Environment / CargoCrate_S, CargoCrate_M, CargoCrate_L | blender/kit_environment.py | Trois tailles et masses de prototype ; prise, arrimage/réseau à intégrer |
| Matelot et tenue de bord, rig complet/mains FP | Crew / SailorBase, FirstPersonArms | blender/kit_crew.py | 27/17 os, un mesh skinné chacun, poids rigides par composant ; six clips et controllers prototypes ; IK/insertion joueur requis |

## Kits transversaux P1

| Famille | Exports présents | Source / limite |
|---|---|---|
| Instruments : 15 bases, dont 3 tailles de cadran/volant | Instruments / GaugeRound_S/M/L, GaugeVertical, CounterRollers, LampDome, VUMeter, LeverSwitch, ButtonGuarded, SelectorRotary, ValveWheel_S/M/L, Crank, RatchetWheel | blender/kit_instruments.py ; lecture des cadrans, libellés/unités et interactions restent à relier |
| Outils explicitement P1 | Tools / Hammer, AdjustableWrench, HullPatch, WoodWedge, Flashlight | blender/kit_tools.py ; masses de prototype indiquées au manifeste, prise/réparation à intégrer ; faisceau présent dans le prefab Flashlight |
| Papier P1–P2 | Tools / MissionOrder, PatrolNote, ManualLoosePage, Form_K90B, Form_Maintenance, Form_Incident, Form_Requisition, Form_Radiation, RolledOrder | blender/kit_tools.py ; supports géométriques vierges, pas des documents finalisés ; affiches ×8/photos/étiquettes non couvertes comme illustrations |
| Fuite 3 tailles, rivet, tôle 3 variantes | Damage / LeakAnchor_Small/Medium/Large, PoppedRivet, BentHullPlate_1/2/3 | blender/kit_damage.py ; ancrages/géométrie, émissions de fuite et réparation gameplay requis |
| Eau par compartiment et tuiles | Damage / BilgeWaterTile_2m, BilgeWaterCompartment_4x6, BilgeWaterCompartment_6x8 | blender/kit_damage.py ; supports déformables, simulation/masque/collision/buoyancy requis |
| Vapeur, givre, condensation, étincelles, contamination | Damage / SteamCard, FrostDecal, CondensationDecal, SparkAnchor, ContaminationPuddle | blender/kit_damage.py ; cartes/ancrages ; effets animés/atténuation/données simulation requis |
| Plafonnier/éclairage de secours | Lighting / CompartmentCeilingLight, EmergencyLamp ; Environment / CeilingLight | blender/kit_lighting.py, blender/kit_environment.py ; support physique disponible ; états blanc/orange/rouge/noir et extinction sous SCRAM à vérifier en jeu |

## Fondations Epic 11 et environnement global

| Epic / livrable | Preuve locale | Limite |
|---|---|---|
| E11-01 bible artistique | docs/art/ART_DIRECTION.md, moodboard/v2_references, palette blender/lib.py | Guide de production dérivé du GDD ; approbation visuelle finale non déduite de l'existence du document |
| E11-03 lumière/tension | Lighting FBX et builder d'environnement | Géométrie/assemblage ; test des quatre états et lecture des instruments en noir requis |
| E11-04 matelot | Crew FBX, Crew.blend, crew_manifest.json, crew_validation.json | Roundtrip Blender poids/rig/clips validé ; avatars, meshes skinnés et clips validés dans Unity par le build commun |
| E11-06 kit structure | Structure FBX, Structure.blend | Géométrie/pivots ; collision de passage et placement final requis |
| E11-07 kit instruments | Instruments FBX, kit_instruments.py | Géométrie/pivots ; lecture diégétique des valeurs requise |
| E11-08 réacteur | Reactor/Primary FBX et sources | Art de base disponible ; gameplay réacteur distinct |
| E11-09 poste central | Central FBX, Central.blend, central_manifest.json | Placeholder Manuel accepté pour PROTO ; contenu fini et interactions absents de cette preuve art |
| E10-01 blockout bateau | Models/Environment/boat_layout.json, blender/sources/BoatEnvironment.blend, Editor/BoatEnvironmentBuilder.cs | Layout global 6 compartiments/28 m ; traversabilité des six compartiments et 15 lignes de vue masquées validées par le builder ; test joueur requis. Le proto n'exige que 3 compartiments |

La génération commune a réussi dans Unity 6000.3.25f1 : `scratch_out/mvp-assets-unity.log` contient `MVP_ASSET_BUILD_PASS`. Les 107 modèles et 53 prefabs sont importés. La scène `Assets/_Project/Art/Scenes/BoatEnvironment.unity` et son prefab sont générés : 28 m, six compartiments, 335 instances modulaires et 142 colliders solides. Une capsule de 46 cm atteint les six compartiments ; les 15 lignes de vue entre postes sont bloquées, portes ouvertes (`scratch_out/environment-validation.txt`).

Le lot comprend également les caisses S/M/L avec masses de prototype, deux prefabs de personnage avec controllers et six clips, les matériaux URP des pages vierges et des effets, les particules de trois tailles de fuite et les étincelles, quatre variantes d'éclairage et le faisceau de lampe torche. Ces éléments de présentation restent à raccorder au gameplay.

## Manques concrets avant toute déclaration de finition

1. **Contenu du Manuel** : les 30 templates PNG sont vierges. Récupérer les procédures approuvées, leur mapping alarmes/pages, les illustrations et les textes localisés ; ne pas inventer d'instructions opérationnelles dans l'art. Le shader de feuilletage et la sélection de la bonne page demandent une implémentation.
2. **Carte et documents** : fournir les zones/mission réellement utilisées, ordre du Commandant/Note de Patrouille, gabarits administratifs/tampons. Les noms de formulaires et leurs meshes ne prouvent pas que le K-90/B est finalisé.
3. **Lectures d'instruments** : unités, plages, graduations et états doivent correspondre aux données de simulation approuvées. Les cadrans en géométrie servent de supports.
4. **Effets** : eau/vapeur/fuites/étincelles/givre/contamination sont surtout des cartes/ancrages. Matériaux URP et particules de prototype générés ; raccordement aux événements et vérification visuelle dynamique restent requis.
5. **Interaction et physique** : un pivot ou montage ne remplace pas un collider précis, une commande, une masse équilibrée, le système une-main/une-chose ou la réplication. Tester portes, outils, capots, combiné et pompes dans les prefabs finalisés.
6. **Personnage** : la peau par composants garde la silhouette mais reste un prototype. Tester déformations, grip des objets, collision des doigts, vue FP, IK, locomotion et évanouissement. Le clip Faint ne fournit pas un ragdoll contrôlé ni traînage.
7. **Assemblage global** : la scène d'art doit rester distinguée de la scène jouable autoritative à référentiel mobile. Vérifier stations masquées, raccords, échelle/ergonomie et performance.

Aucun trou de **support géométrique P1 strict** n'a été identifié dans l'inventaire au moment de l'audit. Les caisses S/M/L sont couvertes. La génération et les validations de la scène, des prefabs et des matériaux ont réussi ; le contenu documentaire, les déformations finales et les raccordements aux systèmes de jeu empêchent une affirmation « 100 % production prête ». P2/P3 (coque extérieure/LOD, scaphandre, fjord, radio/sonar finalisés, cosmétiques, Varga…) restent hors de cette couverture MVP.

Rendus natifs URP : sept captures produites avec succès dans `scratch_out/preview/UnityEnvironment` (`ENVIRONMENT_CAPTURE_PASS`). Vue globale et poste réacteur inspectés visuellement. Unity réduit la résolution des ombres ponctuelles pour respecter son atlas ; le budget lumière/ombres reste à optimiser pour la scène jouable.

## Suite du 5 octobre 2026

Voir [le lot de compartiments détaillés](DETAIL_MODELS_2026-10-05.md) : 45 nouveaux modèles Radio/Living/Signage et amélioration des poids des coudes et des prises du matelot. Total courant : 152 modèles et 98 prefabs, 365 instances et 179 colliders dans la scène. Validation commune Unity et neuf captures URP réussies ; les six compartiments restent traversables et les 15 lignes de vue entre postes restent masquées. Le pointage P1 ci-dessus est l'audit initial du 4 octobre, avant cette extension P2.

## État V1 courant

Le périmètre dépasse maintenant cet audit MVP : [livraison V1](V1_DELIVERY.md) et [pointage de l'annexe complète](V1_ASSET_COVERAGE.md), 387 modèles/412 prefabs, 109 besoins géométriques. Les anciennes mentions P2/P3 horspérimètre décrivent le lot initial et sont remplacées par ce pointage pour l'état courant. La finition graphique et les systèmes de jeu restent distincts de la géométrie V1.
