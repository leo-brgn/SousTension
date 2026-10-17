# Outil d'équilibrage headless (E3-10)

`node tools/balance.js` fait tourner la **vraie simulation** du serveur (`simStep`) sans Nakama, sans réseau, sans joueurs, avec des scénarios scriptés et plusieurs graines du réacteur. Un résultat ici est un résultat en jeu.

```
node tools/balance.js                    tous les scénarios, graines 1..5
node tools/balance.js --seeds 20 --minutes 20
node tools/balance.js --scenario cruise --json
```

| Scénario | Ce qu'il mesure |
|---|---|
| `idle-veille`, `cruise`, `full-power` | le réacteur seul : température max, tension min, premier avertissement, premier SCRAM automatique |
| `unattended-leak` | une fuite moyenne ignorée : eau, assiette, temps avant noyade d'un compartiment |
| `scram-restart` | SCRAM puis redémarrage : tension, profondeur atteinte pendant le noir |
| `scheduled-leaks` | le calendrier de fuites du jeu sans personne pour réparer |
| `fault-generator` | le générateur de pannes avec un équipage passif |
| `transit-lent`, `transit-demi`, `transit-toute` | la mission (1500 m) : à quel moment le but est atteint selon le télégraphe |

**Garde-fous** (code de sortie 1 s'ils sont violés) : aucun NaN / infini dans l'état, jamais plus d'eau que la capacité d'un compartiment, aucun joueur hors du bateau, et **déterminisme** : chaque couple (scénario, graine) est joué deux fois, l'empreinte finale doit être identique (règle d'architecture n°1).

Constats du premier passage (10 min) : en *veille* le bateau reste dans le noir (tension ≈ 0,1) ; en *croisière* sans intervention le réacteur tient ; en *pleine puissance* sans intervention l'avertissement tombe vers 1 min 45 et le SCRAM automatique entre 3 min 40 et 4 min 10, bateau à 8–14 m plus bas à 5 min ; une fuite moyenne ignorée met 19 m³ d'eau dans le bateau en 4 min (assiette ~1°).

**Transit (1500 m, croisière, personne ne fait autre chose)** : *lent* (1,8 m/s) en 14 min 10 s ; *demi* (3,6 m/s) en 7 min 55 s ; *toute* (6 m/s) en 5 min 50 s. En pleine puissance sans surveillance le réacteur part en SCRAM avant d arriver. La limite des 3 étoiles (10 min) est donc atteignable avec *demi* ou mieux, sans panne ; les pannes et les alarmes font le reste de la difficulté. Les joueurs doivent penser au télégraphe : l ordre de mission devrait le dire.

Test : `server/test/balance.test.js` (l'outil tourne, rapport lisible par machine, identique d'une exécution à l'autre). À brancher plus tard : des joueurs scriptés (`server/test/route.js`) pour mesurer les temps de procédure (E3-11).

## Équipage scripté (tools/playtest-bot.js)
`node tools/playtest-bot.js [--seeds N] [--reaction S] [--idle a|b|both] [--minutes M]` fait **jouer la mission par deux marins scriptés** à travers les vraies entrées (marche par les écoutilles, pressions visées, volants tenus, patchs) : salle d'attente, briefing, pannes du générateur, but, bilan. Les marins lisent l'état pour savoir ce qui est en panne ; tout ce qu'ils FONT passe par les entrées d'un client. `--reaction` = secondes avant qu'un marin se mette sur un problème apparu (un humain doit remarquer, comprendre, se déplacer).

Réglage du générateur de pannes (10 graines) : budget = joueurs + 3 (+1 par minute), pause de 10 à 30 s entre deux pannes, fuites de taille 3 possibles.
| Réaction | Résultat | Médiane | Étoiles |
|---|---|---|---|
| 20 s | 10/10 réussies | 8 min 26 s | 9 × 3, 1 × 2 |
| 45 s | 10/10 | 9 min 16 s | 9 × 3, 1 × 2 |
| 90 s | 10/10 | 10 min 14 s | 3 × 3, 5 × 2, 2 × 1 |
Avant ce réglage (budget joueurs + 1, pause 20 à 60 s) un équipage à 90 s de réaction faisait 2 étoiles à chaque fois : aucune pression. La mission n'est donc **jamais perdue** par un équipage qui travaille ; l'échec ne vient que de l'abandon (pas de télégraphe, pas de régime) ou d'une noyade non traitée. À rejouer avec de vrais joueurs avant de durcir encore. Test : `server/test/crewbot.test.js`.
