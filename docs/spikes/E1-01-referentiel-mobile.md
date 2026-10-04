# E1-01 — Spike « joueurs dans un référentiel mobile » (Nakama + Unity)

Issue : [#16](https://github.com/leo-brgn/SousTension/issues/16) · Date des mesures : 2026-10-04 · Unity 6000.3.25f1 · Nakama 3.37.0 · `nakama-unity` v3.10.1

> Décision utilisateur : **Nakama pour tout** (backend et gameplay temps réel). NGO et Fish-Net ne sont donc **pas** comparés ; ce rapport évalue la faisabilité avec Nakama seul.

## Ce qui a été construit
- **Serveur** (`server/modules/index.js`, JavaScript) : match autoritatif Nakama à **10 Hz**. Reçoit une entrée par tick client (`seq`, `mx`, `mz`), simule la position des joueurs en **repère local du bateau**, diffuse l'état (`tick`, `t`, joueurs) avec la dernière séquence traitée. L'identifiant du match est publié en stockage au démarrage.
- **Mouvement du bateau** (`BoatMotion`, C# pur) : fonction **déterministe du temps** (tangage ±15°, roulis ±20°, pilonnement, avance). Chaque client le calcule à partir de l'horloge serveur estimée : seule la position locale des joueurs est répliquée.
- **Client Unity en MVCS** (`Assets/_Spikes/MovingFrame/`) : `Core` (Models, Controller, interfaces de Services, décorateur de latence simulée), `Nakama` (implémentation du Service réseau), `Views` (vues passives + composition root). Prédiction locale + réconciliation (`PredictionBuffer`, dans `Sim`), interpolation des joueurs distants à −100 ms.
- **Tests** : 7 tests Node (serveur), 14 tests EditMode (Unity), 1 test PlayMode de mesure (4 clients Nakama complets), 1 test de bout en bout Node (`server/test/e2e.js`).

## Mesures (4 clients, 10 s par scénario, Nakama local)
Latence simulée : demi-RTT ajouté dans chaque sens ; « perte » simulée en **sémantique TCP** (le message touché est retardé de 200 ms et bloque ceux qui le suivent), car le socket de Nakama est un WebSocket.

| Scénario | Ack p50 | Ack p95 | Ack p99 | Correction max | Écart max entre états | Débit montant | Débit descendant |
|---|---|---|---|---|---|---|---|
| localhost | 66 ms | 83 ms | 99 ms | 0,00 cm | 115 ms | 0,28 kB/s | 3,76 kB/s |
| RTT 100 ms, 2 % | 183 ms | 216 ms | 217 ms | 0,00 cm | 300 ms | 0,27 kB/s | 3,78 kB/s |
| RTT 200 ms, 2 % | 283 ms | 333 ms | 350 ms | 0,00 cm | 333 ms | 0,27 kB/s | 3,80 kB/s |

Mesures **après le rattrapage de file** (budget d'entrées côté serveur, voir ci-dessous). Avant le correctif : RTT 100 ms p50 183 / p95 400 / p99 416 ms ; RTT 200 ms p50 467 / p95 533 / p99 550 ms ; localhost p50 83 / p95 99 / p99 100 ms.

« Ack » = délai entre l'envoi d'une entrée et le retour d'un état qui la contient. Débits par client.

## Lecture des résultats
1. **Aucune correction de prédiction** (0,00 cm) dans les trois scénarios : la simulation de déplacement est identique côté client et serveur, et le TCP garde l'ordre, donc la prédiction n'est jamais contredite. Le critère « écart < 5 cm » est satisfait **pour le joueur local**.
2. **Le déplacement local est réactif par construction** (prédiction immédiate) ; le délai d'ack n'affecte pas la sensation de contrôle.
3. **Bande passante négligeable** (< 4 kB/s par client à 4 joueurs).
4. **Les 4 clients se voient** et le bateau reste cohérent entre eux (même fonction du temps serveur).
5. **Retard après un blocage réseau : corrigé.** Constat initial : après un message retardé (perte TCP), les entrées arrivaient en rafale mais le serveur n'en consommait qu'une par tick, donc la file ne se vidait jamais (ack p50 de 467 ms à RTT 200 ms au lieu des ~283 ms attendus). Correctif : chaque joueur dispose d'un **budget d'entrées** (+1 par tick, plafonné à 4) ; en régime normal c'est une entrée par tick, après un blocage la file se vide en quelques ticks. Le débit moyen ne peut pas dépasser 10 entrées/s, donc inonder le serveur ne rend pas un joueur plus rapide (test `flooding the server...`). Résultat : p50 à RTT 200 ms = 283 ms, soit exactement latence + un tick.

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
1. ~~Rattrapage de file côté serveur~~ : fait (point 5).
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
