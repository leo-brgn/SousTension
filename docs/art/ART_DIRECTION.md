# Direction artistique — prototype

Guide dérivé de `GDD_Sous_Pression.md` §2, §6 et annexe d'assets, de `CLAUDE.md` et du moodboard courant `moodboard/v2_references/`. Il documente la direction existante sans modifier le design du jeu.

## Intention

**Réalisme pataud** : sous-marin robuste, ancien, réparé, bureaucratique ; proportions légèrement cartoon, matériaux crédibles. Le bateau est vieux, fidèle et dangereux, sans devenir menaçant. Les gestes sont sincères, laborieux : tourner une vanne implique tout le corps. L'humour vient des systèmes et de la panique.

Personnages courtauds : volume en œuf, épaules larges, bras épais, jambes courtes, grosses mains, moustache lisible et casquette verte. Préserver la lisibilité de la silhouette à distance et des mains au premier plan. Référence principale : `11_equipage_personnages.png` ; les cosmétiques multiples dépassent P1.

## Palette de référence

Couleurs sRGB de `blender/lib.py`. Les couleurs de sommets exportées doivent être interprétées par le matériau Unity prévu.

| Usage | Hex |
|---|---|
| Vert bouteille / ombre | #2F5A42 / #1F3D2D |
| Crème administratif / ombre | #E6DCBC / #C9BE9A |
| Rouille / ombre | #8A4A2B / #5E301B |
| Laiton / ombre | #C9A24A / #8F7230 |
| Bakélite | #2A1A12 |
| Acier / sombre / clair | #7C8180 / #4E5352 / #A9AEAD |
| Plancher | #585E5B |
| Rouge d'urgence unique | #D9261C |
| Ambre de signal | #E8A317 |

Le rouge est réservé aux urgences réelles. La référence de casquette présente une étoile rouge ; le personnage de prototype emploie un insigne laiton pour respecter la règle globale. Cette convention de prototype ne tranche pas une évolution du GDD. Aucun objet décoratif ne doit multiplier les rouges.

## Formes et matériaux

Gros boutons/leviers/volants, prises et capots épais, biseaux lisibles. Laiton, bakélite, émail écaillé, acier et rouille restent identifiables ; l'usure raconte entretien et réparations plutôt qu'horreur. Maintenir suffisamment d'espace entre commandes pour les lire dans une vue première personne. La géométrie détaillée ne doit pas obscurcir l'état utile d'un objet.

Construire en modules : structure et bases d'instruments d'abord. Un objet interactif comprend en général 2 à 4 pièces distinctes ; chaque pièce mobile possède son vrai pivot. Les assemblages complexes peuvent dépasser ce nombre lorsque plusieurs commandes sont indépendantes. Les montages `Mount_*` sont des points d'intégration explicites. Échelle : 1 unité Blender = 1 m ; Blender Z haut, Unity Y haut après export.

## Lumière, information et ton

La progression visuelle de tension est blanc → orange → rouge → noir. Le plafonnier appartient au compartiment ; le secours rouge et les lampes torches conservent la lecture locale après perte d'électricité. Les transitions doivent répondre à l'état des systèmes.

Zéro HUD : valeurs sur cadrans/aiguilles/papier/dosimètre. Les textes finaux doivent être localisés ; plaques, documents et tampons sont leurs supports diégétiques. Aucune blague écrite, aucune horreur, aucun cynisme. Aucun monstre ni mort violente : l'évanouissement est théâtral. Éviter gore, silhouettes menaçantes, éclairages d'horreur et gestes héroïques « cool ».

## Références et contrôle visuel

Utiliser `01_poste_central.png`, `02_reacteur_scram.png`, `03_poste_reacteur_panne.png`, `07_manuel_ok114.png`, `11_equipage_personnages.png`, `12_instruments_kit.png`, `13_commandant_capsule.png` pour les lots P1. Les autres images guident P2/P3. Ne pas réinterpréter une image comme une autorisation de changer les procédures ou les systèmes.

Avant livraison : inspecter face et trois-quarts, états ouverts/cassés lorsque pertinents, proportions en situation, pivots, raccords, couleurs, échelle et lecture sous l'éclairage réel. Un rendu Blender prouve la forme ; l'import Unity puis une scène jouable prouvent des aspects différents. Consigner les limites de prototype au lieu de présenter un asset exporté comme entièrement fonctionnel.
