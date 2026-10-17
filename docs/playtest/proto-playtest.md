# Playtest PROTO (E16-01) — grille d'observation

**Critère pivot** : 20 minutes drôles, **sans objectif donné**. Si les joueurs s'amusent avec le bateau qui tangue, les alarmes, la Règle des Deux Joueurs et l'eau, le prototype tient ; sinon on change de piste avant d'investir.

## Mise en place
- 2 à 4 joueurs (minimum 2), chacun sur son PC : un éditeur Unity (`SousTension > Spike > 3. Play locally`) + clients de dev (`tools/play-local.ps1 -Clients N`). Un seul testeur : `partner on` dans la console.
- Serveur local en mode debug (`docs/spikes/jouer-en-local.md`). Pas de micro requis ; si possible un appel vocal à côté (la voix de proximité n'est pas faite).
- **Consigne unique** : « Vous êtes l'équipage d'un sous-marin. Faites ce que vous voulez. » Ne rien expliquer, ne pas guider. Répondre aux questions par « essayez ».
- Noter la date, la version (`git log -1`), les joueurs (jamais joué / habitué).
- Le maître de jeu peut lancer, sans prévenir, une panne par la console : `leak 3 2 g` vers 5 min, `scram` vers 10 min, `trip galley` vers 15 min.

## Pendant la session (toutes les minutes, une croix par fait)
| Minute | Rires / cris | Joueur perdu ou bloqué | Coopération spontanée | Une alarme / panne gérée | Ennui (silence, errance) |
|---|---|---|---|---|---|
| 0–5 | | | | | |
| 5–10 | | | | | |
| 10–15 | | | | | |
| 15–20 | | | | | |

À consigner en texte, avec l'heure : moments **drôles** (ce qui les a causés : système, panique, malentendu), moments **frustrants** (cause : contrôle, caméra, lisibilité, bug), **bugs** (reproductible ?), premières actions des joueurs, ce qu'ils n'ont jamais trouvé.

## Mesures (de la console `state` et du HUD)
- Corrections de prédiction max (HUD « correction »), latence d'ack, nombre de reconnexions.
- Durée avant la première Règle des Deux Joueurs réussie, nombre d'échecs (« timeout »).
- Eau max atteinte, nombre de fuites réparées, SCRAM / redémarrages réussis.

## Questions après la session (1 à 5)
1. Avez-vous ri ? Quand ? (libre)
2. Vouliez-vous continuer ? (1 = non, 5 = oui, tout de suite)
3. Vous êtes-vous senti perdu ? Où ?
4. Le mouvement dans le bateau était-il confortable (nausée, glissement, caméra) ?
5. Avez-vous compris quand une action demandait l'autre joueur ? Comment ?
6. Qu'avez-vous voulu faire et n'avez pas pu ?

## Décision
- **Pivot réussi** : ≥ 2 rires francs par 5 min en moyenne, question 2 ≥ 4 pour la majorité, aucun blocage > 2 min.
- **À corriger avant de conclure** : confort de mouvement (< 3 sur la question 4), lisibilité des commandes, bugs bloquants.
- **Pivot raté** : ennui dominant malgré les pannes → on revoit la boucle avant d'investir (GDD §1).
Consigner le verdict et les actions dans l'issue E16-01.
