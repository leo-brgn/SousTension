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
