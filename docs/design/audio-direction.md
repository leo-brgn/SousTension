# Direction audio (E12-01, #133) — proposition à valider

> **Statut : décisions prises (17 oct. 2026).** Middleware : **Wwise**. Sources des sons : un mélange de bibliothèque libre et de synthèse. Réseau noir : le silence **et** un son d'alarme dédié. Voix : **Dissonance** (paquet payant, à acquérir par l'utilisateur ; transport des paquets audio par Nakama via un comms network personnalisé). Wwise n'est pas installé : le Wwise Launcher demande un compte Audiokinetic (action de l'utilisateur). Ce document fixe **ce que chaque système doit dire**, indépendamment de l'outil. Principe du GDD : *« compréhensible les yeux fermés »*, sons **diégétiques** ; le Commandant seul est sous-titré.

## 1. Règles
1. **Une voix par système**, reconnaissable seule : le joueur doit pouvoir dire quel système a un problème sans regarder.
2. **Ce qui va bien se fait entendre, ce qui va mal change** : un ronronnement qui s'altère informe plus qu'un bip.
3. **L'espace est une information** : le son vient du compartiment concerné ; une écoutille fermée atténue fortement (et on l'entend se fermer) ; l'eau étouffe les aigus.
4. **Priorité** : au plus 3 alarmes audibles à la fois ; une alarme rouge masque (duck) les autres ; le silence relatif est un état du jeu (réseau noir).
5. **Rouge = urgence réelle** : seules les alarmes `crit` ont un timbre à deux tons.

## 2. Les voix des systèmes (signaux déjà disponibles dans l'état du serveur)
| Système | Son de fond (boucle) | Ce qui varie | Signal |
|---|---|---|---|
| Réacteur | ronronnement grave | hauteur et grain avec la température ; claquements métalliques au-delà du seuil d'alerte | `rx.T`, `rx.reg` |
| Turbine | sifflement | monte avec la pression de vapeur et l'électricité produite | `rx.S`, `rx.E` |
| Circuit primaire | glouglou des pompes ; claquements des vannes | une pompe arrêtée = silence ; cassée = grincement ; volant de vanne qui tourne = cliquetis | `rx.pu`, `rx.v` |
| Électricité | ronflement 50 Hz des machines | s'affaisse avec la tension ; relais qui claque à chaque disjoncteur | `pw.V`, `pw.br` |
| Réseau noir | le fond disparaît d'un coup (« tue instantanément toute l'électricité »), reste le grincement de la coque et un tic des lampes de secours | batterie faible = lampes de secours qui faiblissent | `pw.em`, `pw.B` |
| Coque | craquements ; plus graves et plus fréquents avec la profondeur | descente après SCRAM | `boat.d` |
| Eau | filet / jet selon la taille de la fuite, clapotis qui monte avec le niveau, gargouillis dans les cales | taille et position de la fuite | `lk`, `bw.l` |
| Pompes de cale | moteur de pompe ; râle quand elles aspirent à sec | en marche / à sec / cassée | `bp` |
| Écoutilles | volant + verrous à la fermeture ; claque à l'ouverture | | `bw.dr` |
| Propulsion | hélice dont la hauteur suit la vitesse ; cloche du télégraphe à chaque ordre | | `pr` |
| Pas | grille métallique ; change sur le caillebotis, la tôle, dans l'eau | la course est plus forte (bruit, E2-05 / E8-01) | position |
| Manuel | page qui tourne ; la reliure qui craque | | `mn` |
| Interphone | grésillement + voix du compartiment | (E12-04) | |

## 3. Les alarmes : 38, mais 6 familles à reconnaître
Les 38 alarmes (`server/src/alarms.js`) ne peuvent pas avoir 38 sons. Elles sonnent par **famille × sévérité** ; la lampe du panneau et le Manuel disent laquelle :
| Famille | Timbre | Rythme | Alarmes |
|---|---|---|---|
| Réacteur | gong grave | un coup, pause, un coup | `rx_*` |
| Électricité | bourdonnement sec | trois brefs | `pw_*` |
| Eau | cloche étouffée | roulement montant | `wt_leak`, `wt_level` |
| Noyade (rouge) | sirène à deux tons | continue | `wt_flood_*` |
| Critique réacteur (rouge) | klaxon à deux tons | continu | `rx_temp_crit`, `rx_scram_auto` |
| Réseau noir | le fond disparaît d'un coup, puis **un son d'alarme dédié** (décision utilisateur) et les lampes de secours | un coup grave, long, répété | `pw_v_dark` |

Une alarme acquittée (clic sur la lampe) se tait ; la lampe reste fixe tant que la cause dure.

## 4. Mix et technique
- 3 bus : **machines** (fond), **alarmes** (priorité), **voix** (équipage + interphone + Commandant). Les alarmes baissent les machines de 6 dB, la voix baisse tout de 4 dB.
- **Spatialisation par compartiment** : chaque source appartient à un compartiment ; filtre passe-bas et gain selon les écoutilles entre la source et l'auditeur (même géométrie que la collision et l'eau : `BoatLayout`).
- Mixage au casque et sur haut-parleurs ; pas de HRTF obligatoire au MVP.
- Budget : ≤ 32 voix simultanées, sons courts en mémoire, fonds en streaming.

## 5. Ordre de réalisation proposé
1. Choisir l'outil (FMOD / Wwise) — **décision à prendre**.
2. Fonds : réacteur, électricité, eau, coque (4 boucles pilotées par l'état) : le jeu devient lisible aux oreilles.
3. Alarmes par famille + acquittement.
4. Événements : vannes, écoutilles, pas, télégraphe, disjoncteurs.
5. Voix : interphone, proximité (E8-07), Commandant (E13).

## 6. Décisions et reste à faire
1. Wwise retenu. Installation : Wwise Launcher + intégration Unity, avec un compte Audiokinetic (à faire par l'utilisateur ; non automatisable).
2. Sources : bibliothèque libre + synthèse, selon le son.
3. Réseau noir : silence du fond suivi d'un son d'alarme dédié.
4. Prochaine étape de code, une fois l'intégration installée : `IAudioService` (interface dans `Gameplay`), implémentation Wwise isolée dans son assembly, pilotée par l'état du serveur (fonds, alarmes par famille).
