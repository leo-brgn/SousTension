# Compartiments détaillés — 5 octobre 2026

Suite de la production Blender 5.2, à partir des issues #119 (sas et vie), #120 (radio/sonar), #132 (plaques) et #112/#130 (matelot/animation). Les lots radio et vie étendent les supports P1 vers les équipements P2 ; leur géométrie ne prouve pas la réalisation des fonctionnalités des issues.

## Nouveaux modèles

- Radio : 11 modèles, dont console sonar, casque/câble, hydrophone à manivelle, enregistreur et deux bobines, radio VLF et antenne télescopique, chiffreur mécanique à 30 touches et papier de message. Source `blender/sources/RadioSonar.blend`, kit `blender/kit_radio.py`, contrat `blender/RADIO_INTEGRATION.md`.
- Sas et vie : 19 modèles, dont couchettes, casier articulé, cambuse/marmite, table/banquette, douche/lance, scaphandre/casque, touret, sas, porte du carré et six pièces de vaisselle. Source `blender/sources/Living.blend`, kit `blender/kit_living.py`.
- Signalétique : 15 plaques à quatre fixations avec lettres en géométrie et couleurs de sommets. Les six noms de compartiment sont placés dans le bateau. Source `blender/sources/Signage.blend`, kit `blender/kit_signage.py`. Textes ASCII français de prototype ; pas encore une typographie kravique finalisée ni des variantes localisées. Seule la plaque SCRAM emploie le rouge d'urgence.

Les blockouts radio et cambuse et les anciennes couchettes ne sont plus instanciés dans le layout. Ils sont conservés comme modèles de référence. Le sas de plongée et le touret sont disponibles comme assets autonomes ; leur installation dans une zone réellement accessible et les ouvertures de coque restent à concevoir.

## Personnage

Les transitions des manches aux coudes utilisent deux os et des poids normalisés (284 sommets mixtes sur chacun des deux modèles), au lieu de la pondération entièrement rigide. Les poses de prise referment les pouces et tournent les poignets ; la pose vanne réduit l'oscillation du coude. Les noms, les 27/17 os, les meshes uniques et les 4/2 clips sont préservés. Le roundtrip FBX vérifie aussi les déformations évaluées à cinq instants par clip.

Limites : silhouette encore stylisée et segmentée, IK sur les équipements absente, clips de locomotion prototypes, ragdoll/traînage non réalisés. Écran sonar, contenus des messages, simulations radio/décontamination, portes et commandes nécessitent leur intégration de jeu.

## Reproduction

Depuis PowerShell, `./blender/build.ps1 -Kit Radio,Living,Signage,Crew,Environment -Preview` génère les exports et aperçus. Dans Unity, exécuter le menu `Sous Tension/Art/Build and validate MVP assets`, puis `SousTension.EditorTools.EnvironmentCapture.Run` en batch avec GPU pour les captures. La scène d'art reste `Assets/_Project/Art/Scenes/BoatEnvironment.unity`.

Validation Unity réussie (`scratch_out/detail-assets-unity.log`, `MVP_ASSET_BUILD_PASS`) : 152 FBX et 98 prefabs au total. Le bateau contient maintenant 365 instances et 179 colliders solides. Les six compartiments restent atteignables avec une capsule de 46 cm ; les 15 lignes de vue entre postes restent bloquées avec les portes ouvertes. Ce contrôle de géométrie ne remplace pas un playtest.

Neuf captures natives URP réussies (`scratch_out/detail-environment-capture.log`, `ENVIRONMENT_CAPTURE_PASS`). Les vues radio et vie ont été inspectées : équipements orientés vers l'intérieur et matériaux sans erreur rose. L'éclairage intérieur reste sombre et nécessite une passe de lisibilité avant finition.
