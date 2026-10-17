# Architecture réseau (E1-04, #19) — état au MVP

> Le backlog décrivait « host-client » avec découpage E1-04a/b/c. La décision « Nakama pour tout » a remplacé le host-client par un **match autoritatif Nakama** ; ce document décrit ce qui est livré, comment c'est fait et ce qui reste (E1-07 reconnexion, E1-05 Steam).

## 1. Principes (règles d'architecture du projet)
1. **Serveur autoritatif** sur le bateau, l'eau, le réacteur, l'électricité, les alarmes, la mission (`server/src/*.js`, une seule fonction `simStep`, 10 Hz, déterministe : graine dans l'état, ni `Math.random` ni horloge).
2. Le client **envoie des entrées**, jamais un état ; il **prédit seulement son propre déplacement** et se réconcilie.
3. Toute règle de simulation côté client existe en double (JS serveur, C# client) : gardée identique par des **tests de parité** (marche : 9000 pas ; géométrie ; positions des commandes ; catalogue d'alarmes).
4. **Resynchronisation complète à chaque tick** : l'état diffusé contient tout (joueurs, cargo, réacteur, électricité, eau, fuites, pompes, propulsion, Manuel, alarmes, mission) ; un client qui arrive ou revient n'a rien d'autre à demander.

## 2. Découpage livré
| Ancien lot | Livré | Où |
|---|---|---|
| **E1-04a lobby / session** | Un match unique créé au démarrage du serveur (`rpc get_moving_frame_match`), 2 à 4 joueurs ; salle d'attente, « prêt », briefing, bilan, nouvelle partie (`mission.js`) ; pas de partie en solo | `server/src/match.js`, `mission.js` ; écrans `ScreensView` |
| **E1-04b réplication d'état** | `OP_STATE` à 10 Hz, JSON, ~20 kB/s en descente à 4 joueurs ; horloge serveur suivie (offset lissé, pas copié) | `matchLoop`, `NakamaNetworkService`, `MovingFrameModel` |
| **E1-04c prédiction** | Tampon de prédiction + réconciliation ; mouvement extrapolé à la cadence de l'écran ; correction lissée (< 1,5 m) ou immédiate (téléportation) ; collisions identiques client / serveur | `PredictionBuffer`, `CharacterMotion`, `MovingFrameController` |

## 3. Flux d'un joueur
```
client (frame) --> input à 10 Hz {seq, mx, mz, ry, act, hold, use, hand, rdy, dbg}
                          | (WebSocket Nakama, opcode 1)
serveur  queueInput -> simStep(tick) -> broadcast OP_STATE (opcode 2) {tick, t, players[seq acquitté], rx, pw, bw, al, ms, ...}
client   Reconcile(x, z, seq) : on rejoue les entrées non acquittées depuis la position serveur
```
- **Budget d'entrées** : le serveur applique au plus une entrée par tick et par joueur (rattrapage plafonné à 4 après une pause) : inonder le serveur n'accélère pas un joueur.
- **Interpolation** des autres joueurs et du cargo : 100 ms de retard sur l'horloge serveur lissée.
- **Le bateau** n'est pas répliqué : son mouvement est une fonction déterministe du temps serveur (`BoatMotion`), identique des deux côtés ; seul le poids de l'eau (assiette / gîte) vient de l'état.

## 4. Mesures et tests
- Spike E1-01 (`docs/spikes/E1-01-referentiel-mobile.md`) : 4 clients, RTT 200 ms avec 2 % de perte TCP simulée → aucune correction, ack p95 333 ms, < 4 kB/s montant.
- Serveur : tests sans Nakama (`node --test server/test/*.test.js`, dont `latency.test.js`, `determinism.test.js` avec empreinte dorée), `node server/test/e2e.js` avec Nakama.
- Client : EditMode (prédiction, parité, horloge) ; PlayMode de mesure avec 4 clients.
- Outils : latence / gigue / perte simulées par client (`--latency --jitter --loss`), canal de debug serveur (`dbg`, mode debug uniquement), outil d'équilibrage headless, tour de captures.

## 5. Ce qui reste (hors MVP ou autres issues)
- **Reconnexion en cours de partie (E1-07, #22)** : l'état est complet à chaque tick, donc le serveur n'a rien à ajouter ; il manque côté client la **reprise de session** (nouveau socket, nouveau `matchJoin` avec le même utilisateur, reprise de la place) et côté serveur de garder la place quelques secondes à la déconnexion (aujourd'hui, partir = être retiré du match et de la liste des joueurs).
- **Steam / Dissonance (E1-05, E8-07)** : à brancher sur l'identité Nakama (le lien Steam passe par le compte), voix à relayer par le serveur (voir `docs/design/audio-direction.md`).
- **Taille de l'état** : le JSON complet à 10 Hz suffit à 4 joueurs ; au besoin, passer en différences ou en binaire (mesure d'abord).
- **Plusieurs parties / matchmaking** : un seul match par serveur aujourd'hui.
