# Voix de proximité (E8-07, #83) — affinage et découpage

> **Statut : validé (17 oct. 2026), sous-issues #253 à #259 créées.** Décision déjà prise : **UniVoice** (open source, MIT) à la place de Dissonance. Rien n'est installé. Taille XL : découpage ci-dessous.

## 1. Ce que fournit UniVoice (lu dans son README)
- Agnostique du réseau : on implémente `IAudioClient` et `IAudioServer` ; des intégrations existent pour Mirror, FishNet et NGO, **pas pour Nakama** : il faut écrire la nôtre.
- Entrée micro (UniMic), encodage / décodage Opus (Concentus, C# pur), lecture dans un `AudioSource` (donc spatialisable par Unity), filtres (détection de voix, réduction de bruit RNNoise en option, flou gaussien).
- Contrôle du flux : mute / deafen par pair et **étiquettes** (tags) pour décider qui entend qui.
- Installation : registre npm à portée dans `Packages/manifest.json` (scopes `com.adrenak.*`), donc pas de dépendance git ; le test CI « deps-lock-check » l'accepte (il ne refuse que les URL git / github).

## 2. Contraintes du projet
- **Autorité serveur et reconnexion** : la voix n'est pas de la simulation. Elle n'entre pas dans `simStep` ni dans la graine, ne casse pas le déterminisme, et un pair qui revient reprend simplement l'écoute.
- **Occlusion** : le son d'un joueur est atténué par les cloisons entre lui et l'auditeur, avec la même géométrie que l'audio du bateau (`AudioMix.Reach`, `BoatLayout`).
- **Chat de proximité + interphones uniquement** (règle de design) ; pas de salon vocal global.
- **Jouable sans micro** : roue d'émotes (sous-issue dédiée).
- **Bruit (E8-01)** : le volume réel du micro doit alimenter le bruit du bateau (GDD) ; dépend du système de bruit, pas encore fait.

## 3. Risque principal : le transport par Nakama
Le match autoritatif traite les messages **une fois par tick (10 Hz)**. Relayer l'Opus (paquets de 20 ms) par `matchLoop` ajoute jusqu'à 100 ms de latence et de la gigue, à absorber par un tampon de gigue côté client. Il faut un **spike** (mesure avec 4 clients) avant tout le reste. Si c'est insuffisant, solution de repli : un canal temps réel Nakama séparé du match (chat / stream), ou un relais UDP dédié (nouvelle décision à prendre).

## 3 bis. Résultat du spike A (#253, `server/test/voice-spike.js`)
4 clients, paquets de 60 octets toutes les 20 ms (taille d'une trame Opus), relayés par le match (opcode `OP_VOICE = 4`, non fiable, plafonné à 10 paquets par tick et par émetteur), serveur local :

| Cas | Perte | Retard p50 | p95 | max | Descente par client |
|---|---|---|---|---|---|
| 1 émetteur | 0 % | 56 ms | 100 ms | 104 ms | 46 ko/s |
| 4 émetteurs | 0 % | 51 ms | 100 ms | 105 ms | 97 ko/s |

**Lecture** : le retard ajouté par le relais est celui du tick (au plus 100 ms, 50 ms en médiane), sans perte. Sur Internet, il faut y ajouter la moitié du ping : ~150 à 200 ms de bouche à oreille dans le pire cas, sous le budget de 250 ms. **Le relais par le match est retenu** (pas besoin de canal séparé) avec un tampon de gigue d'environ 60 ms côté client. **Point d'attention** : le JSON de Nakama gonfle chaque paquet (base64 + enveloppe) : 4 émetteurs coûtent ~50 ko/s de descente en plus de l'état (~45 ko/s) ; à surveiller, le protocole binaire (protobuf) du SDK réduirait cela.

## 4. Découpage proposé
| # | Sous-issue | Taille | Dépend de |
|---|---|---|---|
| A | **Spike transport** : paquets Opus factices relayés par le match Nakama, mesure latence / gigue / bande passante à 4 clients | M | — |
| B | **Installer UniVoice** (registre npm à portée, version figée, lock) et capter / lire une voix en local (boucle micro → haut-parleur) | S | accord d'installation |
| C | **`IAudioClient` / `IAudioServer` Nakama** : relais des paquets par le match (opcode dédié, serveur sans état de jeu) | M | A, B |
| D | **Proximité et occlusion** : gain selon la distance et `Reach` entre compartiments ; tags / mute | M | C |
| E | **Roue d'émotes** (équivalent sans micro) | S | — |
| F | **Volume du micro → bruit du bateau** | S | E8-01 (bruit) |
| G | **Interphones** (E12-04 : qui parle à qui entre postes) | M | C |

Tests : le calcul de gain (distance + cloisons) est pur et testé en EditMode comme `AudioMix` ; le relais serveur a un test Node (paquets reçus, relayés, jamais dans l'état de simulation).

## 5. Questions
1. Valides-tu ce découpage et l'ordre (A en premier) ?
2. J'installe UniVoice (registre npm à portée dans `manifest.json`) au démarrage de B ?
3. Si le spike A montre trop de latence : on tente d'abord un canal Nakama hors match, avant tout autre relais ?
