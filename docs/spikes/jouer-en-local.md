# Jouer en local : tester le prototype dans Unity (E1-11 / E1-12)

Objectif : appuyer sur Play et jouer, seul (avec un partenaire fantôme) ou à deux, sur ta machine.

## Prérequis
- **Docker** démarré (le serveur Nakama tourne dans un conteneur : `server/docker-compose.yml`, déjà en **mode debug**).
- Unity 6000.3.25f1 avec ce projet ouvert.

## Une partie (le serveur démarre en salle d'attente)
1. **Écran titre** : attends « Connecté », puis **Jouer**.
2. **Salle d'attente** : il faut **au moins 2 équipiers**, tous « prêts » (pas de partie en solo). Le monde est figé, on peut marcher.
3. **Ordre de mission** (10 s) : transit de **1500 m**.
4. **La patrouille** : aucun HUD, tout est sur les instruments du bateau. Les pannes montent toutes seules (fuites, disjoncteurs, vannes) ; le panneau d'alarmes est au poste central (mur de droite), le Manuel s'ouvre à la page de l'alarme. **Échap** = pause (le jeu continue pour les autres).
5. **Bilan** : réussite (1500 m atteints) ou échec (un compartiment noyé 60 s, ou 40 m de profondeur), étoiles, statistiques ; **Nouvelle patrouille** quand tout le monde est prêt.

## Jouer seul (le plus simple)
1. Menu **SousTension > Spike > 3. Play locally (server + scene)** : il démarre le serveur (premier lancement : une minute ou deux), ouvre la scène `MovingFrame` et lance Play.
2. Ouvre la console de debug avec la touche à gauche du `1` (`²` sur un clavier AZERTY) et tape `partner on` : un **joueur fantôme**, **toujours prêt**, rejoint la partie. Clique **Jouer**, puis **Je suis prêt** : la partie démarre. Il arme la commande complémentaire une seconde après que tu as armé la première, ce qui permet de tester toute action à deux **tout seul**.
3. Raccourcis de test : `mission start` (tout le monde prêt), `mission win` (arriver), `mission fail`, `mission goal 300` (objectif court), `faults now` (une panne tout de suite), `regime croisiere` puis `ff 120` (le réseau électrique monte et les lampes s'allument), `tp 4` (aller au compartiment 4).

## Jouer à deux (deux fenêtres)
1. Une seule fois : menu **SousTension > Spike > 4. Build the Windows development client (player 2)** (le build arrive dans `Builds/Dev/`, ignoré par git).
2. Lance l'éditeur en Play (étape ci-dessus) puis, dans un terminal à la racine du dépôt :
   ```
   powershell -ExecutionPolicy Bypass -File tools/play-local.ps1 -Clients 1
   ```
   Le script démarre le serveur s'il ne tourne pas, puis ouvre un client du build. `-Clients 2` en ouvre deux (jeu à quatre avec l'éditeur et le fantôme). `-ServerOnly` ne lance que le serveur.
3. Options d'un client pour simuler un mauvais réseau : `--latency=MS --jitter=MS --loss=PERCENT` (voir `MovingFrameBootstrap`).

## Commandes du jeu
| Action | Commande |
|---|---|
| Marcher / regarder | ZQSD (ou WASD) / souris |
| Utiliser ce qu'on vise (tenir pour un volant) | clic gauche |
| Lancer l'objet en main | clic droit |
| Prendre / poser (le dernier pris) / ranger la torche en poche | F / G / H |
| Feuilleter le Manuel (quand on le tient) | flèches gauche / droite |
| Console de debug | touche à gauche du `1` |
| Overlay réseau (développeur) | F2 |
| Pause | Échap |

Confort : la caméra compense 70 % du roulis / tangage du bateau (option du client `--stab=0..1` : 0 = caméra rigide, 1 = caméra toujours à l'horizontale). Si le mal de mer persiste, essayer `--stab=1`.

La touche **E** garde l'ancien comportement par position (la commande la plus proche), utile pour dépanner.

## Provoquer des pannes (console de debug)
`help` liste tout. Les plus utiles :

| Commande | Effet |
|---|---|
| `leak 3 2 g` | fuite de taille 2 dans le compartiment 3, paroi gauche |
| `water 4 3` | 3 m³ d'eau dans le compartiment 4 |
| `scram` / `restart` | SCRAM / redémarrage forcé (sans procédure) |
| `regime pleine` | passe à la pleine puissance |
| `break pump 1`, `break bilge 2`, `trip galley` | pompe cassée, pompe de cale cassée, disjoncteur qui saute |
| `tp 4` ou `tp reacteur` | se téléporter au milieu d'un compartiment (1 à 6, ou torpilles, central, radio, reacteur, machines, vie) |
| `tp 0 -9` | se téléporter à une position (le bateau fait 28 m : z de -14 à +14) |
| `ff 60` | avance la simulation de 60 s d'un coup |
| `state` | résumé de l'état du jeu |
| `partner on` / `partner off` | joueur fantôme (E1-12) |

## Le bateau (E10-01)
Le bateau est le modèle 3D de `Assets/_Project/Art` : 28 m, six compartiments de l'avant à l'arrière — torpilles, poste central, radio, réacteur, machines, vie. Cinq cloisons, chacune avec **une écoutille** (décalée à gauche / à droite) et un **écran** derrière. Cloisons, écrans et écoutilles fermées **bloquent** les joueurs ; une écoutille fermée bloque aussi l'eau. On apparaît dans la radio.

- Écoutille : viser la porte ou son volant, clic = ouvrir / fermer (refusé si quelqu'un est dans l'ouverture).
- Réacteur (compartiment 4) : console RK-1 et levier SCRAM à gauche, 4 vannes et 2 pompes primaires à droite, pompe de cale au milieu. Machines (5) : tableau de 20 disjoncteurs à gauche, pompe de cale au milieu. Poste central (2) : télégraphe à gauche (deux pédales au sol).
- Au départ (régime *veille*) le réseau est **sombre** : lampes rouges de secours. Passer le RK-1 en *croisière* (`regime croisiere`, puis `ff 120`) fait monter la tension : les lampes de chaque compartiment s'allument.
- Vérifier le bateau sans jouer : `powershell -File tools/shot-tour.ps1` (client de dev + serveur) laisse 13 captures dans `BuildsShots`.
- Déplacement : la simulation reste à 10 Hz mais la caméra bouge à la cadence de l'écran (la partie déjà écoulée du pas est prédite) et une correction du serveur s'efface en ~0,1 s au lieu de téléporter la vue.

## Ce qu'il faut tester d'abord (5 minutes)
1. Marcher dans le bateau qui tangue : pas de glissement bizarre, la caméra suit.
2. Viser le **sélecteur de régime** (console RK-1, mur gauche du compartiment 4 réacteur) : il s'éclaire, un clic tourne le sélecteur.
3. `partner on`, puis armer la clé `demo` à un bout du bateau : le fantôme arme l'autre bout, les deux clés passent au vert.
4. `leak 3 2 d`, prendre un patch dans la caisse à outils (poste central, mur de gauche, `F`), cliquer sur la fuite : un cran de moins.
5. `scram` : le noir se fait, le bateau coule doucement ; redémarrer par la procédure (levier, vannes, pompes, puis les deux clés).
6. Prendre le **Manuel** sur la table du poste central, tourner les pages avec les flèches : tant qu'on le tient, aucune commande ne répond.

## En cas de problème
- « the server did not answer » : Docker n'est pas démarré ou le port 7350 est pris.
- Écran vide : la scène n'a pas de caméra principale ; relancer **1. Create the MovingFrame scene**.
- Les tests : `node --test server/test/*.test.js` pour le serveur, Test Runner > EditMode pour Unity.
