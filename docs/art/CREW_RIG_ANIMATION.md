# Crew — rig et animations v1

Livraison du 5 octobre 2026 pour le matelot validé du moodboard. La géométrie, les couleurs et les poids du modèle approuvé sont conservés.

## Ouvrir et utiliser

- **Blender** : `blender/sources/Crew.blend`. Sélectionner `SailorBase_Rig`, passer en Pose Mode. Les anneaux bleus sont les contrôles FK ; les cibles vertes contrôlent les mains et les pieds en IK.
- **Unity** : `Assets/_Project/Art/Scenes/CrewAnimationDemo.unity`, puis Play. La séquence parcourt le matelot et les bras avec boutons précédent, suivant et rejouer.
- **Vidéo locale** : `scratch_out/preview/CrewAnimation/Crew_animations_v1.mp4` — 33 secondes, 640 × 640, 24 images/s. `index.html` permet de choisir un passage et de consulter les poses Blender et les captures Unity.

## Rig Blender

Le corps utilise 27 os de déformation et 31 contrôles ; les bras FP utilisent 17 os de déformation et 19 contrôles. Collections distinctes : `Deformation`, `Controls FK`, `Controls IK`. La première est masquée pour faciliter l'édition. Les contrôles ne sont pas exportés dans le FBX.

`CTRL_Root` (ou `CTRL_FP_Root`) déplace l'ensemble. Dans ses propriétés personnalisées, `Arm_IK_L/R` et `Leg_IK_L/R` passent de 0 (FK) à 1 (IK). Les cibles `IK_Hand_L/R` et `IK_Foot_L/R` pilotent une chaîne de deux os sans étirement et l'orientation de son extrémité. Les doigts et le pouce se règlent individuellement en FK. Placer la cible sur la pose courante avant de changer de mode ; le rig ne fournit pas de bouton de correspondance automatique FK/IK ni de contrôles de pôles dédiés.

Les clips sont des Actions éditables, archivées dans des pistes NLA séparées et désactivées à l'ouverture pour présenter la pose de repos. Dans l'Action Editor, choisir l'Action voulue pour l'éditer ; laisser les autres pistes désactivées. La bibliothèque est animée en FK, échantillonnée à 24 images/s. L'export cuit les contraintes sur les seuls os de déformation.

## Bibliothèque

| Corps | Durée | Boucle |
|---|---:|:---:|
| Idle | 4 s | oui |
| Walk | 1 s | oui |
| WalkBack, StrafeLeft, StrafeRight | 1,33 s chacun | oui |
| Run | 0,67 s | oui |
| InteractReach | 1 s | non |
| ValveTurn | 2 s | oui |
| CarryIdle | 2 s | oui |
| CarryWalk | 1,33 s | oui |
| Lift, PutDown | 2 s chacun | non |
| Stagger | 1,33 s | non |
| Faint | 2 s | non |
| GetUp | 2,5 s | non |

| Bras FP | Durée | Boucle |
|---|---:|:---:|
| HandsIdle | 2 s | oui |
| HandsReach, HandsGrip, HandsRelease | 1 s chacun | non |
| HandsValve, HandsCarry | 2 s chacun | oui |

Animations sur place : le jeu déplace l'acteur. Le déplacement vertical de présentation et la chute restent dans le squelette. Aucun root motion Unity. Les poses terminales Faint → GetUp → Idle et Lift → CarryIdle → PutDown correspondent exactement dans la source. Les gestes de vanne et de portage sont génériques : la scène de démonstration présente les mouvements sans accessoire attaché.

## API Unity

Les prefabs `Art/Prefabs/Crew/SailorBase.prefab` et `FirstPersonArms.prefab` contiennent leur Animator et `SousTension.Art.CrewAnimationDriver`.

```csharp
crew.SetLocomotion(new Vector2(strafe, forward), running);
crew.SetCarrying(true);
crew.PlayAction(CrewAnimationDriver.CrewAction.Valve);
crew.StopAction();
crew.PlayAction(CrewAnimationDriver.CrewAction.Faint);
crew.PlayAction(CrewAnimationDriver.CrewAction.GetUp);
hands.PlayHands(CrewAnimationDriver.HandAction.Grip);
```

Actions corps : `Reach`, `Valve`, `Lift`, `PutDown`, `Stagger`, `Faint`, `GetUp`. Actions FP : `Grip`, `Reach`, `Valve`, `Carry`, `Release`. Lift active le portage ; PutDown le désactive. Les actions ponctuelles reviennent à la locomotion ou au portage ; Faint reste au sol jusqu'à GetUp. Valve et les prises FP se terminent par `StopAction()` ou par une autre action.

Le contrôleur utilise MoveX/MoveY pour la locomotion directionnelle, Speed pour le portage et Carry pour les transitions. `MvpPresentationBuilder` réutilise le nouveau générateur pour préserver cette configuration lors des reconstructions. `CrewAnimationDemo` est un composant de présentation séparé.

## Reproduction et validation

Depuis la racine du projet, avec Blender 5.2 :

```powershell
# Refaire les animations sur la géométrie source actuelle
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python blender/animate_crew.py
# Reconstruire également la géométrie : powershell -File blender/build.ps1 -Kit Crew
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python blender/validate_crew_rig.py
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python blender/validate_crew.py -- 'D:\Game\Sous Tension\Assets\_Project\Art\Models\Crew'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\Game\Sous Tension' -executeMethod SousTension.EditorTools.CrewAnimationBuilder.Run -logFile 'D:\Game\Sous Tension\scratch_out\crew-animation-unity.log'
```

Contrôles exécutés :

- Blender : tous les frames générés finis, géométrie à moins de 5 m de l'origine ; minimum du corps à 12 mm du sol ; raccords de boucles identiques ; 6 chaînes IK testées avec erreur inférieure à 0,001 mm ; 4 transitions de poses vérifiées.
- Réimport FBX : 27/17 os, 15/6 clips, aucun contrôle exporté, poids normalisés sur 1 ou 2 os, UV et couleurs préservées ; neuf poses échantillonnées par clip.
- Unity : compilation et batch réussis ; avatars Generic valides, boucles et fréquence, déformation réelle des 21 clips ; entrée dans les 21 états Animator, retour des actions, transition de portage et maintien Faint. Sept captures du mesh évalué relues.

Les journaux locaux sont dans `scratch_out/crew_animation_*.log`, `crew_control_validation.log` et `crew-animation-unity.log`. Les manifestes versionnés sont dans `Art/Models/Crew/crew_animation_manifest.json` et `crew_validation.json`.

## Périmètre

Cette v1 couvre le matelot de base et ses bras FP. L'intégration aux déplacements réseau, l'alignement précis des mains sur les vannes/objets, les contacts sur sols irréguliers, le ragdoll et l'animation faciale ne sont pas implémentés. Les sept anciens costumes CrewVariants conservent leurs prototypes. Ajuster la vitesse de déplacement du jeu aux cycles courts du personnage pour limiter le glissement des pieds.
