# E1-01 — Spike « joueurs dans un référentiel mobile » (Nakama + Unity)

Issue : [#16](https://github.com/leo-brgn/SousTension/issues/16) · Date des mesures : 2026-10-04 · Unity 6000.3.25f1 · Nakama 3.37.0 · `nakama-unity` v3.10.1

> Décision utilisateur : **Nakama pour tout** (backend et gameplay temps réel). NGO et Fish-Net ne sont donc **pas** comparés ; ce rapport évalue la faisabilité avec Nakama seul.

## Ce qui a été construit
- **Serveur** (`server/modules/index.js`, JavaScript) : match autoritatif Nakama à **10 Hz**. Reçoit une entrée par tick client (`seq`, `mx`, `mz`), simule la position des joueurs en **repère local du bateau**, diffuse l'état (`tick`, `t`, joueurs) avec la dernière séquence traitée. L'identifiant du match est publié en stockage au démarrage.
- **Mouvement du bateau** (`BoatMotion`, C# pur) : fonction **déterministe du temps** (tangage ±15°, roulis ±20°, pilonnement, avance). Chaque client le calcule à partir de l'horloge serveur estimée : seule la position locale des joueurs est répliquée.
- **Client Unity en MVCS** (`Assets/_Spikes/MovingFrame/`) : `Core` (Models, Controller, interfaces de Services, décorateur de latence simulée), `Nakama` (implémentation du Service réseau), `Views` (vues passives + composition root). Prédiction locale + réconciliation (`PredictionBuffer`, dans `Sim`), interpolation des joueurs distants à −100 ms.
- **Tests** : 5 tests Node (serveur), 14 tests EditMode (Unity), 1 test PlayMode de mesure (4 clients Nakama complets), 1 test de bout en bout Node (`server/test/e2e.js`).

## Mesures (4 clients, 10 s par scénario, Nakama local)
Latence simulée : demi-RTT ajouté dans chaque sens ; « perte » simulée en **sémantique TCP** (le message touché est retardé de 200 ms et bloque ceux qui le suivent), car le socket de Nakama est un WebSocket.

| Scénario | Ack p50 | Ack p95 | Ack p99 | Correction max | Écart max entre états | Débit montant | Débit descendant |
|---|---|---|---|---|---|---|---|
| localhost | 83 ms | 99 ms | 100 ms | 0,00 cm | 112 ms | 0,28 kB/s | 3,74 kB/s |
| RTT 100 ms, 2 % | 183 ms | 400 ms | 416 ms | 0,00 cm | 316 ms | 0,27 kB/s | 3,77 kB/s |
| RTT 200 ms, 2 % | 467 ms | 533 ms | 550 ms | 0,00 cm | 350 ms | 0,27 kB/s | 3,88 kB/s |

« Ack » = délai entre l'envoi d'une entrée et le retour d'un état qui la contient. Débits par client.

## Lecture des résultats
1. **Aucune correction de prédiction** (0,00 cm) dans les trois scénarios : la simulation de déplacement est identique côté client et serveur, et le TCP garde l'ordre, donc la prédiction n'est jamais contredite. Le critère « écart < 5 cm » est satisfait **pour le joueur local**.
2. **Le déplacement local est réactif par construction** (prédiction immédiate) ; le délai d'ack n'affecte pas la sensation de contrôle.
3. **Bande passante négligeable** (< 4 kB/s par client à 4 joueurs).
4. **Les 4 clients se voient** et le bateau reste cohérent entre eux (même fonction du temps serveur).
5. **Problème constaté : retard qui s'installe après un blocage réseau.** Après un message retardé (perte TCP), les entrées arrivent en rafale, mais le serveur n'en consomme qu'**une par tick** ; la file ne se vide jamais. À RTT 200 ms le p50 d'ack passe à 467 ms au lieu d'environ 283 ms attendus. La conséquence est un décalage durable des actions validées par le serveur (important pour la fenêtre de 3 s de la Règle des Deux Joueurs, qui reste très au-dessus de ces valeurs).

## Limites de ce spike (à lire avant de conclure)
- **Serveur sur la même machine** : les latences sont simulées, le vrai réseau (jitter, pertes réelles, NAT) n'est pas testé.
- **Clients scriptés (bots)** dans un seul processus : pas de test visuel avec 4 humains dans une scène (glissement, motion sickness), et pas de build joueur.
- **Pas de physique** : pas d'objets 📦 portés, pas de collisions, pas de double verrou « deux joueurs » ; ces points de la spécification d'E1-01 **ne sont pas couverts**.
- **Pas de comparaison** avec NGO/Fish-Net (décision utilisateur).
- **Pas de mesure de l'erreur de position vue par un autre joueur** (seul le joueur local est mesuré en erreur de prédiction) ; l'interpolation distante est implémentée mais non quantifiée.
- Le critère « < 5 cm à 100 ms » est vérifié sur la **réconciliation locale**, pas sur l'écart entre un joueur et sa vue par un autre.

## Recommandation
**Poursuivre avec Nakama** pour le gameplay du bateau et des joueurs : le modèle « bateau déterministe en fonction du temps serveur + joueurs en repère local + prédiction/réconciliation » fonctionne, ne coûte presque rien en bande passante et reste simple. Les points durs à traiter ensuite (non couverts ici) sont les **objets physiques portés** et la **latence du TCP en cas de perte** : à décider au vu de tests avec de vrais joueurs sur un vrai réseau.

## Suites proposées
1. **Rattrapage de file côté serveur** : consommer 2 entrées par tick quand la file dépasse 2 (corrige le retard installé, point 5), avec tests.
2. **Test avec 4 humains** sur le réseau local, puis à distance : mesurer l'écart de position distant et le confort.
3. **Objets portés et double verrou** : prototyper la synchronisation d'objets en match autoritatif (pas de moteur physique serveur dans Nakama : probablement une physique simplifiée côté serveur ou une autorité déléguée).
4. Relire la décision « Nakama pour le gameplay temps réel » à la lumière du point 3 ; un netcode Unity (NGO/Fish-Net) reste l'alternative si les objets physiques s'avèrent trop coûteux à répliquer à la main.

## Reproduire
```bash
docker compose -f server/docker-compose.yml up -d
node --test server/test/match.test.js        # logique du match
node server/test/e2e.js                      # 4 clients WebSocket Node
# Unity (EditMode puis PlayMode, mesures dans Logs/e1-01-metrics.json) :
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```
