# BACKLOG — SOUS PRESSION / DERNIÈRE PATROUILLE

Source : `GDD_Sous_Pression.md` (v0.3) + `moodboard/`. Dernière mise à jour : 2026-10-05 (cases cochées = issues fermées sur GitHub).

## Légende

- **Jalon** : `PROTO` (S+8) · `VS` vertical slice (M+5) · `ALPHA` (M+9) · `BETA` (M+12) · `GOLD` (M+14) · `POST` (post-lancement)
- **Taille** : `S` ≤ 2 j · `M` ≤ 1 sem · `L` ≤ 3 sem · `XL` > 3 sem (à découper)
- **Types** : 🧪 spike/prototype · ⚙️ code · 🎨 art · 🔊 audio · ✍️ design/écriture · 🧰 outillage/prod
- **Dép.** : dépendance bloquante
- 🔧 interactif · 📦 transportable (convention de l'annexe d'assets du GDD)
- Cases : `[ ]` à faire · `[~]` en cours · `[x]` fait

## Critères de passage des jalons (rappel GDD §10.1)

| Jalon | Critère |
|---|---|
| PROTO | 20 min drôles sans monstre ni objectif, sinon pivot |
| VS | Testable par des inconnus sans explication orale |
| ALPHA | Boucle complète rejouable 10 h |
| BETA | Zéro bug bloquant, onboarding validé |

---

# 0. DÉCISIONS ET QUESTIONS OUVERTES (à trancher en priorité)

Ces points bloquent ou déforment le backlog. Chaque case = une décision à prendre et à reporter dans le GDD.

- [ ] **D-01 Titre du jeu** : « Sous Pression », « Dernière Patrouille » ou « Sous Tension » (nom du repo) ? Bloque page Steam, logo, typo.
- [x] **D-02 Moteur Unity vs Unreal** — conditionné au spike E1-01 (référentiel mobile + netcode). Bloque presque tout le code.
- [ ] **D-03 Équipe** (GDD 1.1 « TODO ») : combien de devs / artistes 3D / sound designers ? Les estimations (~405 j-artiste) en dépendent.
- [ ] **D-04 Acte IV « Le Jumeau »** (proposition, §2.6) : retenu ou non ? Impacte le périmètre 1.0 (« actes I-III » en §10.2), les assets (sous-marin jumeau, torpille) et la fin.
- [ ] **D-05 Graffitis multilingues** (§2.2) : oui/non selon budget de localisation.
- [ ] **D-06 Dose de radiation** : §3.6 dit « réinitialisée entre les runs » mais l'ancienne version la soignait à la base. Aligner, et préciser ce qui persiste vs. ce qui est reset (le bateau, lui, est persistant §3.9).
- [ ] **D-07 « ~12 types de missions »** (§10.2) vs 4 familles d'objectifs (§2.5, dont Écoute fusionnée dans Relevé) : lister les ~12 variantes réelles.
- [ ] **D-08 Liste exhaustive des mécaniques « Deux Joueurs »** (TODO §3.4) → livrable E4-01.
- [ ] **D-09 Arbre de décision des dépendances réacteur** (TODO §1.4) → livrable E3-01.
- [ ] **D-10 Duo (2 joueurs)** : définir précisément « minuteries élargies » + « télécommande bricolée » (§3.4/§11) vs. suppression du mode Effectifs Réduits. Que fait le jeu à 3 joueurs si un se déconnecte ?
- [ ] **D-11 Règle de patrouille** (§2.5) « soit agir avec les commandes, soit sortir du sous-marin » : formaliser en structure de mission (qui a besoin de quoi, par type d'objectif).
- [ ] **D-12 Système de succès** : liste des succès qui débloquent moustaches/casquettes (§2.3) ; cohérence avec « cosmétiques gagnés en jeu uniquement » (§9).
- [ ] **D-13 Barre de vie / PV** (§3.6) : la contamination s'y ajoute et se lit au dosimètre — définir PV, seuils, lien avec évanouissement (§3.9).
- [ ] **D-14 Plateformes** : manette « supportée » alors que Steam first — Steam Deck vérifié ? Crossplay en POST.
- [ ] **D-15 Incohérences à nettoyer dans le GDD** : le tableau de jalons parle encore de « 4 joueurs en ligne » (ok) mais l'annexe est titrée « GDD v0.2 » ; le pitch Steam dit « Coop 2-4 » (ok) ; §1.1 « 900 pages » vs. procédures ≤ 5 étapes.

---

# EPIC 1 — Fondations techniques & spikes (PROTO)

Objectif : lever le risque n°1 (référentiel mobile + réseau) avant d'investir.

- [ ] **E1-01 🧪 Spike « joueurs dans un référentiel mobile »** — 4 joueurs marchent dans un volume en mouvement/inclinaison, en ligne, sur Unity ET Unreal (ou comparatif ciblé). Sortie : recommandation moteur (D-02). `L` — semaine 1.
- [x] **E1-02 🧰 Setup projet** : moteur choisi, structure du repo, `.gitignore`, LFS, conventions de nommage/dossiers, CI de build Windows. `M` · dép. E1-01
- [ ] **E1-03 🧰 Pipeline d'assets** : import, naming, LODs, matériaux de base, prefab/blueprint types « interactif 🔧 » et « transportable 📦 ». `M`
- [ ] **E1-04 ⚙️ Architecture réseau** : host-client, autorité serveur sur bateau/eau/réacteur, prédiction locale des joueurs, tick fixe. `XL` → découper en E1-04a lobby/session, E1-04b réplication d'état, E1-04c prédiction. · dép. E1-01
- [ ] **E1-05 ⚙️ Intégration Steam** : Steamworks, invitations par code 6 caractères (registre de bord), lobby, succès, cloud saves. `L` · dép. E1-04
- [x] **E1-06 ⚙️ Boucle de simulation à pas fixe 10 Hz, déterministe** (squelette commun réacteur/eau). `M`
- [ ] **E1-07 ⚙️ Reconnexion en cours de partie** (obligatoire 1.0) : resynchronisation complète de l'état. `L` · dép. E1-04 · jalon `BETA` (prototyper tôt)
- [ ] **E1-08 ⚙️ Sauvegarde / persistance** : état du bateau (dégâts persistants), progression matérielle, Notes de Patrouille, succès. `L` · jalon `ALPHA`
- [x] **E1-09 🧰 Outils de debug** : console, tweak des paramètres de simulation, téléport, forcer pannes, overlay réseau. `M`
- [ ] **E1-10 🧰 Télémétrie de playtest** (causes d'échec, temps par procédure, usage du Manuel). `M` · `VS`

---

# EPIC 2 — Joueur, interaction & contrôles (PROTO → VS)

- [ ] **E2-01 ⚙️ Contrôleur première personne** marchant sur plateformes mobiles/inclinées (dép. E1-01). `L`
- [x] **E2-02 ⚙️ Système d'interaction** : clic gauche (tenir pour manivelle ; geste circulaire pour vannes), clic droit examiner/lire. `L`
- [x] **E2-03 ⚙️ « Une main = une chose »** : deux mains, poche de poitrine (1 petit objet), objets à deux mains qui verrouillent les autres actions. `L`
- [x] **E2-04 ⚙️ Prendre/poser objets physiques 📦** (masse, lancers, collisions réseau). `L`
- [ ] **E2-05 ⚙️ Course (bruit ●●●) et chuchotement (V maintenu)** liés au système de bruit (E8-01). `S`
- [ ] **E2-06 ⚙️ Support manette** + remap clavier complet. `M` · `ALPHA`
- [ ] **E2-07 ⚙️ Roue d'émotes sonores et gestuelles** (100 % jouable sans micro). `M` · `VS`
- [ ] **E2-08 ⚙️ Mains/bras en première personne** : animations de tourner une vanne « avec tout le corps », laborieuses, jamais « cool ». `L` · dép. E11-01
- [ ] **E2-09 ⚙️ Évanouissement théâtral** (« mort »), traînage vers l'infirmerie, ranimation aux sels, joueur au sol qui peut parler (§3.9). `L` · `VS`
- [ ] **E2-10 ⚙️ Barre de vie/PV cachée (lue au dosimètre)** — voir D-13. `M` · `ALPHA`

---

# EPIC 3 — Réacteur RK-1 « Petit Soleil » (cœur, PROTO)

- [x] **E3-01 ✍️ Arbre de décision des dépendances** réacteur → vapeur → électricité → systèmes (livrable demandé §1.4). Chaque lien doit être lisible, jamais arbitraire ; délais (vapeur ~90 s, électricité ~3 min). `M` · PROTO (D-09)
- [x] **E3-02 ⚙️ Modèle de simulation** : barres de contrôle → chaleur → vapeur → turbine → électricité + propulsion ; circuit de refroidissement alimenté par l'électricité produite (« boucle diabolique »). Équations simples à tick 10 Hz. `L` · dép. E1-06, E3-01
- [x] **E3-03 ⚙️ Trois régimes** Veille / Croisière / Pleine puissance (sélecteur) : production, bruit ○/●●/●●●●, risque (surchauffe ≈ 4 min en pleine puissance sans surveillance). `M`
- [x] **E3-04 ⚙️ SCRAM** : levier sous capot plombé, accessible seul, sans vote ; coupe toute l'électricité (noir, silence, pompes arrêtées, bateau qui coule doucement). `M` · PROTO
- [x] **E3-05 ⚙️ Redémarrage du réacteur** : procédure à deux joueurs, 90 s, à la lampe torche (utilise E4). `M` · dép. E4-02
- [x] **E3-06 ⚙️ Pompes primaire ×2, vannes principales ×4** : états marche/arrêt/cassé. `M` · PROTO
- [x] **E3-07 ⚙️ Réseau électrique** : tableau principal (~20 disjoncteurs à réarmer), batteries de secours, consommateurs (pompes, air, lumières, sonar, cafetière, samovar). `L`
- [x] **E3-08 ⚙️ Propulsion** liée à la turbine/régime, télégraphe machine (5 positions). `M`
- [ ] **E3-09 ⚙️ Dérives lentes + système d'alarmes** : ~40 alarmes distinctes, dérives en chaîne, « correction excessive ». Générateur de pannes avec budget d'attention (plus de systèmes que de joueurs). `XL` → découper par système · `VS`
- [ ] **E3-10 ⚙️ Cohérence d'équilibrage** : outil de simulation headless pour tester sans joueurs (déterminisme). `M`
- [ ] **E3-11 ✍️ Test « utile en 5 min »** : un nouveau joueur doit pouvoir contribuer sans comprendre la chaîne (risque 🔴 §11). `M` · `VS`

---

# EPIC 4 — Règle des Deux Joueurs (signature, PROTO)

- [ ] **E4-01 ✍️ Liste exhaustive des actions « deux joueurs »** (TODO §3.4) : démarrage/arrêt réacteur, ballast, leurres, sas extérieur, purge primaire, envoi du Signal, + autres (combustible 📦 à 2, casque de scaphandre, pose sur le fond…). Annexe au GDD. `S` (D-08)
- [x] **E4-02 ⚙️ Framework d'actions couplées** : deux commandes physiques éloignées (clés/manivelles/bouton+pédale), fenêtre de 3 s, sans se voir, validation serveur tolérante à la latence. `L` · PROTO
- [ ] **E4-03 ⚙️ Feedback diégétique** de l'attente/échec/succès (voyants, sons), jamais de HUD. `M`
- [ ] **E4-04 ⚙️ Verrou « pas de partie en solo »** : le jeu ne se lance pas seul ; comportement si un joueur quitte (duo minimum). `S` · D-10
- [ ] **E4-05 ⚙️ Mode duo** : minuteries élargies, « télécommande bricolée » déblocable (diégétique, « strictement interdite par le règlement »). `M` · `ALPHA` · D-10
- [x] **E4-06 🧪 Test de latence** : fenêtre de 3 s à 150–250 ms de ping, interphone grésillant inclus. `M`

---

# EPIC 5 — La Procédure : Manuel OK-114 (PROTO v0 → VS v1)

- [ ] **E5-01 ✍️ Format des procédures** : ≤ 5 étapes, schémas gros, mots-clés en gras, illustrées, numérotées. Gabarit unique. `M`
- [x] **E5-02 ⚙️ Objet Manuel 📦🔧** : classeur 8 kg tenu à deux mains (celui qui lit ne peut pas agir), exemplaire unique, pages feuilletables. `L` · PROTO (v0 papier/placeholder)
- [ ] **E5-03 ⚙️ Pages arrachables** : se perdent, brûlent, se mouillent (page volante). `M` · `VS`
- [ ] **E5-04 ⚙️ Aides de navigation** : ouverture automatique à la bonne page quand une alarme sonne, bouton « GOTO » sur l'alarme (§3.2/3.5 — remplace le mini-jeu d'index). `M` · dép. E3-09
- [ ] **E5-05 ✍️ Rédaction des procédures** : une procédure par panne/manœuvre (réacteur, ballast, fuite, décon, redémarrage…), ~30 doubles-pages, correctes. `L`, évolutif
- [ ] **E5-06 ✍️ Annotations manuscrites** des anciens équipages qui contredisent le manuel — et ont raison (boutons officiels disparus/remplacés ; seule l'annotation indique le bon). Doit coller aux commandes réelles en jeu. `M` · `VS`
- [ ] **E5-07 ⚙️ Shader de pages + texte localisable** (taille de texte réglable). `M`
- [ ] **E5-08 🎨 Textures des doubles-pages** (~30), errata collés, graffitis. `L` · dép. D-05
- [ ] **E5-09 🧪 Test de lisibilité** : le Manuel ne devient pas une corvée (risque 🟠). `S` · `VS`

---

# EPIC 6 — Eau, dégâts, fuites & flottabilité (PROTO)

- [x] **E6-01 ⚙️ Simulation d'eau par volumes-par-compartiment** + centre de masse → assiette/gîte du bateau. `L` · PROTO
- [x] **E6-02 ⚙️ Fuites** (3 tailles), rivet sauté, tôle déformée ; réparations (patch de coque + cale, marteau, clé). `L` · PROTO (une fuite)
- [x] **E6-03 ⚙️ Pompes de cale ×2**, seau, serpillière. `M`
- [ ] **E6-04 ⚙️ Cargo & assiette** : le cargo mal arrimé glisse et modifie l'assiette (sangles, râteliers). `M` · `VS`
- [ ] **E6-05 ⚙️ Soupe/fluides simples** qui se renversent à l'inclinaison (marmite de la cambuse). `M` · `VS`
- [ ] **E6-06 ⚙️ Air / CO₂** : cartouches remplaçables, ventilation, déclin lent en Veille. `M` · `VS`
- [ ] **E6-07 ⚙️ Vapeur, électricité (étincelles), incendie léger**, extincteur. `M`
- [ ] **E6-08 ⚙️ Immersion/ballast** : vannes principales (deux joueurs), profondeur, poser le bateau sur le fond. `L` · `VS`
- [ ] **E6-09 ⚙️ Persistance des dégâts** d'une run à l'autre. `M` · `ALPHA` · dép. E1-08

---

# EPIC 7 — Radiation & contamination (VS → ALPHA)

- [ ] **E7-01 ⚙️ Zones chaudes** invisibles (fuite primaire, combustible endommagé). `M`
- [ ] **E7-02 ⚙️ Dosimètre-bracelet** à aiguille au poignet (crépitement, pas de HUD) ; panneau dosimétrie mural + alarme. `M` · `VS`
- [ ] **E7-03 ⚙️ Contamination du joueur** (s'ajoute aux PV, crépite en permanence, contamine poignées/leviers/le Manuel, mains qui tremblent → mini-jeux plus durs). `L` · `ALPHA`
- [ ] **E7-04 ⚙️ Douche de décontamination** : un joueur asperge l'autre ~20 s ; mécanique de nettoyage « PowerWash Simulator » (frotter la contamination visible jusqu'à disparition). `L` · `ALPHA` · réf. `moodboard/v2_references/15_decon_powerwash.png`
- [ ] **E7-05 ⚙️ Reset de la dose entre les runs** (D-06). `S`
- [ ] **E7-06 ⚙️ Shader de contamination** (décals animés, flaques, version « contaminé » du personnage). `M`

---

# EPIC 8 — Discrétion acoustique & Entente (ALPHA)

- [ ] **E8-01 ⚙️ Bruit total** : régime, pompes, chocs, voix dans les compartiments proches de la coque, courir. Jauge diégétique. `L` · dép. E3-03 (régimes) ; version simple dès `VS`
- [ ] **E8-02 ⚙️ IA des patrouilles de l'Entente** (frégates, avions, bouées sonar) : balayage de zones, détection par seuil de bruit. `L`
- [ ] **E8-03 ⚙️ Poursuite non létale** : grenades d'exercice assourdissantes (secousses, pannes, chute de Note « DISCRÉTION : INSATISFAISANT »). `L`
- [ ] **E8-04 ⚙️ Silence radio** (Veille + interdiction de courir + chuchotement) ; inspection surprise « contrôle dans 4 minutes ». `M`
- [ ] **E8-05 ⚙️ Contre-mesures** : poser le bateau sur le fond (2 joueurs), leurres à remonter à la manivelle (bruyant), thermocline (couche visible sur carte, déforme le sonar). `L`
- [ ] **E8-06 ⚙️ Sonar** : écran circulaire (render target), hydrophone à manivelle, casque. `L`
- [ ] **E8-07 ⚙️ Voix proximité + occlusion + captation du volume réel**, VU-mètre diégétique, chat de proximité vs interphones (un par compartiment ; combiné, touche T). `XL` → E8-07a voix proximité, E8-07b interphones grésillants, E8-07c VU-mètre · dép. E1-04 · prototype dès `PROTO` (le canal de coop)

---

# EPIC 9 — Missions, notation & progression (VS → ALPHA)

- [ ] **E9-01 ✍️ Règle de patrouille** « soit agir avec les commandes, soit sortir du sous-marin » → structure de chaque type de mission (D-11). `M`
- [ ] **E9-02 ⚙️ Boucle macro** : base → capsule d'ordres → appareillage → transit → objectif → retour → débrief. Machine à états réseau. `L` · `VS`
- [ ] **E9-03 ⚙️ Mission Ravitaillement** : le sous-marin arrive seul au dépôt, équipage déjà en scaphandre dans le sas, collecte vivres/pièces/combustible nucléaire. `L` · `VS` (« une mission »)
- [ ] **E9-04 ⚙️ Mission Relevé (+ Écoute fusionnée)** : un joueur au périscope, un autre à l'écran qui prend les « screens », deux « zouaves » qui font du bruit ; variante « relevé sonore » avec antenne à déployer (manivelle bruyante). `L` · `ALPHA`
- [ ] **E9-05 ⚙️ Mission Entretien du Signal** : réparation en scaphandre + pose de cages anti-filets sur la coque. `L` · `ALPHA`
- [ ] **E9-06 ✍️ Catalogue des ~12 types de missions** (variantes des 3 familles + missions narratives) (D-07). `M`
- [ ] **E9-07 ⚙️ Générateur d'objectifs** par zone (dépôts, épaves, points d'écoute), météo et trafic variables. `L` · `ALPHA`
- [ ] **E9-08 ⚙️ Note de Patrouille** : critères affichés à l'avance (objectif, discrétion, état du bateau, « tenue réglementaire »), partiellement absurdes ; 3 mauvaises notes = cour martiale (reset de la progression matérielle). `L` · `ALPHA`
- [ ] **E9-09 ⚙️ Écran de constat d'échec** façon rapport dactylographié : cause, chronologie des 60 dernières secondes, responsable probable (aléatoire pondéré par les actions). `M` · `VS`
- [ ] **E9-10 ⚙️ Échec de run** : bateau qui coule ou s'échoue ; retour à la base. `M`
- [ ] **E9-11 ⚙️ Cargo & inventaire du bateau** (~30 objets en 1.0), valeur, poids, bruit. `L` · `ALPHA`
- [ ] **E9-12 ⚙️ Améliorations (~12)** achetées à la base, validées au tampon. `L` · `ALPHA`
- [ ] **E9-13 ⚙️ Filets de pêche industriels** (simulation câble/tissu, « ennemi n°1 ») : détection, emmêlement, dégagement en scaphandre. `L` · `ALPHA`
- [ ] **E9-14 ⚙️ Inspections / formulaires à tamponner** pour débloquer le matériel. `M`
- [ ] **E9-15 ⚙️ Scaphandre** : sortie par le sas de plongée (cycle inondation/vidange), ombilical + touret, casque à visser à 2 joueurs, déplacements sous l'eau. `XL` → découper · `VS`
- [ ] **E9-16 ⚙️ Navigation** : barre, profondeur/assiette, carte papier dessinable au crayon gras, compas/règle/chronomètre, périscope à manivelle (anim « gifle »). `L` · `VS`

---

# EPIC 10 — Monde, niveaux & hub (VS → ALPHA)

## Bateau (blockout dès PROTO)
- [ ] **E10-01 🧪 Blockout du bateau** : 3 compartiments (PROTO) → 6 compartiments en enfilade, 28 m, sas manuels, aucun poste visible depuis un autre. `L` → `M`
- [ ] **E10-02 🎨 Compartiments détaillés** dans l'ordre d'attaque : 4 Réacteur → 2 Poste central → 5 Machines → 6 Sas & vie → 3 Radio/sonar → 1 Torpilles. `XL` (voir Epic 12)

## Zones
- [ ] **E10-03 ⚙️ Le Fjord** : tutoriel implicite, épaves kraviques, aucun ennemi. `L` · `VS`
- [ ] **E10-04 ⚙️ La Mer Grise** : hauts-fonds, filets, trafic civil, patrouilles légères. `XL` · `ALPHA`
- [ ] **E10-05 ⚙️ La Fosse de Varn** : profonde, sombre, dépôts riches, thermoclines, patrouilles lourdes. `XL` · `ALPHA`
- [ ] **E10-06 ⚙️ Cartes apprises par cœur** (non procédurales) + points de repère nommables (« le chenal de la grue »). Carte de navigation en jeu. `M`

## Base (hub à pied)
- [ ] **E10-07 ⚙️ Hub du fjord** : ponton, grue, atelier, bureau des tampons, tableau d'affichage (missions, notes, classement entre équipages amis), baraquements, phare. `L` · `VS` (version minimale)
- [ ] **E10-08 ⚙️ Tampon encreur** : tout achat/validation = tampon posé soi-même. `S`
- [ ] **E10-09 ⚙️ Menus diégétiques** : options = poste radio, cosmétiques = armoire de l'équipage, invitations = registre de bord (code 6 caractères). `L` · dép. E1-05

---

# EPIC 11 — Direction artistique & assets (continu)

Références visuelles : `moodboard/01..05`, `moodboard/v2_references/01..16`, et `moodboard/references/` (Lethal Company, Content Warning, PEAK, RV There Yet?, PowerWash Simulator). Palette : vert bouteille, crème administratif, rouille, **un seul rouge** pour l'urgence ; réalisme pataud (gros boutons/leviers, matériaux crédibles : laiton, bakélite, émail écaillé).

## Fondations
- [ ] **E11-01 🎨 Bible artistique** (à partir du moodboard v2) : palette, proportions, langage de formes, règle du rouge unique, références hors-limites. `M` · PROTO
- [ ] **E11-02 🎨 Typographie « administration kravique »** + tampons + gabarit **K-90/B** (formulaire de fin de mission, 3 exemplaires — TODO §2.1). `M` · `VS`
- [ ] **E11-03 🎨 Système de lumière/tension** : blanc → orange → rouge → noir ; plafonnier par compartiment, éclairage de secours rouge, lampes torches. `L` · PROTO
- [ ] **E11-04 🎨 Matelot de base** (corps pataud, rig complet + mains 1re personne) — réf. `v2_references/11_equipage_personnages.png` : silhouettes en œuf, moustaches, casquettes vertes à étoile rouge. `L` · PROTO
- [ ] **E11-05 🎨 Concepts manquants** : plans d'ensemble du bateau (coupe 6 compartiments), carte des zones, Entente (frégate/avion), sous-marin jumeau. `L`

## Production d'assets (suivre l'ordre d'attaque du GDD)
- [ ] **E11-06 🎨 Kit structure** : coque droite 2 m, cloison, sas de cloison 🔧, caillebotis, fond de cale, tuyauterie modulaire. `L` · PROTO
- [ ] **E11-07 🎨 Kit instruments (le plus rentable)** : ~15 meshes de base (manomètres S/M/L, indicateur vertical, compteur à rouleaux, voyant, VU-mètre, levier, bouton sous garde, sélecteur, volants S/M/L, manivelle, roue crantée) — réf. `v2_references/12_instruments_kit.png`. `L` · PROTO
- [ ] **E11-08 🎨 Réacteur** : tableau RK-1 🔧, levier SCRAM 🔧 (« asset le plus filmé », soigner à l'extrême), pompes ×2, vannes ×4 — réf. `02_reacteur_scram.png`, `03_poste_reacteur_panne.png`. `L` · PROTO
- [ ] **E11-09 🎨 Poste central** : barre, pupitre, télégraphe, table à cartes, **Manuel OK-114**, tube pneumatique + capsule — réf. `01_poste_central.png`, `07_manuel_ok114.png`, `13_commandant_capsule.png`. `L` · PROTO/VS
- [ ] **E11-10 🎨 Machines** : tableau électrique (~20 disjoncteurs à états), pompes de cale, interphone (même asset partout), turbine, batteries. `L`
- [ ] **E11-11 🎨 Sas & vie** : sas de plongée, scaphandre, douche de décon 🔧 (réf. `04_douche_decon.png`, `10_scaphandre_depot.png`), cambuse + marmite (réf. `05_cambuse_soupe.png`), couchettes, **porte du carré** (réf. `06_porte_carre.png`). `XL`
- [ ] **E11-12 🎨 Radio & sonar** : console sonar, enregistreur à bandes, poste VLF + antenne, machine à chiffrer. `L` · `VS/ALPHA`
- [ ] **E11-13 🎨 Torpilles / cargo** : tubes, râteliers, sangles. `M`
- [ ] **E11-14 🎨 Kit outils** (marteau, clé, patch, lampe… dosimètre-bracelet) · **Kit papier** (5 formulaires, Note de Patrouille, affiches ×8) · **Kit dégâts & fluides** (VFX d'ancrage). `L`
- [ ] **E11-15 🎨 Coque extérieure Molosse** (3 LODs, hélice animée, usure ×3) — réf. `08_sous_marin_exterieur.png`. `L` · `VS`
- [ ] **E11-16 🎨 Fjord & base** (~15 bâtiments/props) — réf. `09_fjord_base.png` · terrain sous-marin (rochers ×8, tombants, kelp, cheminée). `XL`
- [ ] **E11-17 🎨 Dépôt militaire + filets de pêche** — réf. `14_filets_peche.png`. `L`
- [ ] **E11-18 🎨 Épaves kraviques**, sous-marin jumeau, trafic civil (ferry, porte-conteneurs, voilier, jet-ski, pédalo), Entente (frégate à 2 niveaux de détail, avion, bouée sonar), parc éolien, icebergs ×3, régate de fin (10 voiliers + foule low-poly). `XL` · P3
- [ ] **E11-19 🎨 Objets de cargo & mission** (~30) : caisses S/M/L, fûts, château de combustible (très lourd, crépite, à 2 joueurs), pièces détachées, objets anachroniques, reliques kraviques, **5 pièces de l'Émetteur**. `XL`
- [ ] **E11-20 🎨 Personnages modulaires** : têtes ×6, moustaches ×12, coiffures ×8, uniformes, casquettes ×10, ~50 cosmétiques, scaphandre porté, version contaminé, état évanoui. `XL` · P2–P3
- [ ] **E11-21 🎨 Commandant Varga** (un seul modèle, silhouette/ombre sous la porte d'abord ; sa cabine n'est **pas** modélisée). `M` · `BETA`
- [ ] **E11-22 🎨 Animation** : gestes laborieux (vannes, manivelles), évanouissement, traînage, douche de décon. `L`
- [ ] **E11-23 🎨 VFX** : vapeur, fuites, étincelles, givre, eau montante sous le caillebotis, lueur Cherenkov (hublot du cœur), contamination. `L`
- [ ] **E11-24 🎨 Plaques gravées / panneaux (~15 formes)** + graffitis au crayon gras (multilingues ? D-05). `M`

---

# EPIC 12 — Audio (priorité budgétaire n°1, PROTO → BETA)

- [ ] **E12-01 🔊 Direction audio** : « compréhensible les yeux fermés » ; chaque système a sa voix (réacteur ronronne, turbine siffle, coque parle selon la profondeur). `M` · PROTO
- [ ] **E12-02 🔊 Moteur audio spatial** : occlusion quasi totale entre compartiments, réverb par compartiment, propagation par coque. `L` · `VS`
- [ ] **E12-03 🔊 Sons des systèmes** : réacteur, turbine, pompes, alarmes (~40 distinctes), SCRAM, vannes, manivelles, sas, eau, vapeur, étincelles. `XL`
- [ ] **E12-04 🔊 Interphone** : grésillement « par design », volume trop fort en Machines. `M`
- [ ] **E12-05 🔊 Dosimètre** : crépitement mixé pour être désagréable à fort niveau. `S`
- [ ] **E12-06 🔊 Grenades d'exercice** + option de réduction des basses fréquences. `M`
- [ ] **E12-07 🔊 Musique** : aucune en patrouille. Hymne kravique (gramophone, cassable) + un unique thème orchestral de retour au fjord. `M` · `ALPHA`
- [ ] **E12-08 🔊 Tube pneumatique** (sifflement), machine à écrire, tampon, pas sur caillebotis. `M`
- [ ] **E12-09 🔊 Mix & mastering**, tests sur casque et haut-parleurs. `M` · `BETA`

---

# EPIC 13 — Narration, Commandant & contenu écrit (VS → BETA)

- [ ] **E13-01 ✍️ Capsules du Commandant** : briefings, réprimandes, quotas, souvenirs inutiles, gâteau — écrits « sans blague », solennité décalée (règle des trois interdits). Banque de messages par acte. `L`
- [ ] **E13-02 ⚙️ Système de capsules** : arrivée par tube pneumatique à intervalles, ouverture, papier roulé ; capsules d'ordres de mission. `M` · `VS`
- [ ] **E13-03 ✍️ Acte I — La Routine** : patrouilles ordinaires, indices que la guerre est finie. `L`
- [ ] **E13-04 ✍️ Acte II — Le Doute** : poursuites, inspections, silence radio forcé, missions d'histoire (radeau de 1983, dépôt jamais atteint), capsules étranges. `L`
- [ ] **E13-05 ✍️ Acte III — Le Signal** : Émetteur en 5 pièces trouvé dans les sites de ravitaillement ; **routine vs curiosité** (la routine est le piège, fouiller est la récompense) ; dernière patrouille en surface au milieu d'une régate ; ouverture de la porte du carré (contenu écrit à la production, hors GDD). `XL`
- [ ] **E13-06 ✍️ Acte IV — Le Jumeau** *(si D-04 validé)* : le jumeau ne reconnaît pas le code et torpille l'*Irrévocable* ; évasion. `L`
- [ ] **E13-07 ⚙️ Assemblage de l'Émetteur** (5 pièces) + envoi du Signal par action à deux joueurs. `L`
- [ ] **E13-08 ✍️ Objets anachroniques & documents** trouvés (jet-ski, conteneurs, canard gonflable, emballages plastiques) — narration environnementale. `M`
- [ ] **E13-09 ⚙️ Rejouabilité post-fin** (« la fin est un choix, pas un mur »). `M` · `BETA`
- [ ] **E13-10 ✍️ Relecture « trois interdits »** de tout le contenu : pas de blague écrite, pas d'horreur, pas de cynisme. `S` récurrent

---

# EPIC 14 — Succès & personnalisation (ALPHA)

- [ ] **E14-01 ✍️ Liste des succès** (dont « s'asseoir dans le fauteuil du chef de quart ») qui débloquent moustaches/casquettes (D-12). `M`
- [ ] **E14-02 ⚙️ Armoire de l'équipage** (diégétique) : choix visage/moustache/casquette ; options simples de départ. `M`
- [ ] **E14-03 ⚙️ Casiers personnels ×4** (rangement cosmétiques). `S`
- [ ] **E14-04 ⚙️ Succès Steam** + progression. `M` · dép. E1-05

---

# EPIC 15 — UI/UX, accessibilité & onboarding (VS → BETA)

- [ ] **E15-01 ⚙️ Zéro HUD** : sous-titres + nom du joueur qui parle uniquement. `M`
- [ ] **E15-02 🎨 Lisibilité** : gros cadrans, aiguilles franches, rouge réservé aux urgences réelles. `M`
- [ ] **E15-03 ⚙️ Accessibilité** : jouable sans micro (roue d'émotes), sous-titres directionnels, mode daltonien, réduction des basses fréquences, taille de texte du Manuel. `L` · `BETA`
- [ ] **E15-04 ⚙️ Onboarding implicite** par le Fjord (zéro écran de tuto), validation sur inconnus. `L` · `VS/BETA`
- [ ] **E15-05 ⚙️ Menu principal / options** diégétiques (poste radio), paramètres vidéo/audio/contrôles. `M`
- [ ] **E15-06 ⚙️ Localisation** : pipeline de textes (Manuel inclus), 9 langues — FR, EN, DE, ES, PT-BR, PL, RU, JA, ZH. Budget et planification précoces (coût réel). `XL` · pipeline dès `VS`, traduction `BETA`

---

# EPIC 16 — QA, playtests & performance (continu)

- [ ] **E16-01 🧪 Playtest PROTO** : 20 min drôles sans objectif (critère pivot). `S`
- [ ] **E16-02 🧪 Playtests réguliers entre amis** (groupes de 2, 3, 4) ; grille d'observation : moments clip, frustration Deux Joueurs, usage du Manuel. `récurrent`
- [ ] **E16-03 🧪 Playtest « inconnus sans explication »** (critère VS). `M`
- [ ] **E16-04 🧰 Plan de test réseau** : pertes de paquets, déconnexion/reconnexion, host migration (si retenue), duo. `L`
- [ ] **E16-05 🧰 Budgets de performance** (Steam Deck / config modeste), LODs, occlusion. `L` · `ALPHA`
- [ ] **E16-06 🧰 Passes de bugs** ; critère BETA : zéro bug bloquant. `récurrent`
- [ ] **E16-07 🧪 Test « boucle rejouable 10 h »** (critère ALPHA). `M`

---

# EPIC 17 — Business, marketing & sortie (parallèle → GOLD)

- [ ] **E17-01 ✍️ Page Steam** (pitch du GDD §12), capsule art, trailer centré clips de 40 s. `L` · avant démo
- [ ] **E17-02 🧰 Démo Steam Next Fest** : le Fjord uniquement. `L` · `ALPHA/BETA`
- [ ] **E17-03 🧰 Programme créateurs** avant sortie (clés, kit presse, scénarios clippables : SCRAM dans le noir, douche de décon, périscope-gifle, compte à rebours à deux clés raté). `M`
- [ ] **E17-04 🧰 Classification PEGI 12** : dossier. `S`
- [ ] **E17-05 🧰 Budget & financement**, prix 7–9 €, statut juridique. `M`
- [ ] **E17-06 🧰 Pré-sortie** : wishlists, Discord, pipeline de retours. `M`
- [ ] **E17-07 🧰 Gold** : build candidate, checklists Steamworks, patch day-one. `M` · `GOLD`

---

# EPIC 18 — Post-lancement (POST)

- [ ] **E18-01** 4ᵉ zone : l'Arctique.
- [ ] **E18-02** Second bateau (diesel, 2 joueurs, « encore pire »).
- [ ] **E18-03** Inspections coopératives entre deux équipages (8 joueurs).
- [ ] **E18-04** Crossplay.
- **Jamais** : PvP, imposteur, battle pass, horreur.

---

# ROADMAP PAR JALON (vue condensée)

## PROTO — S+8 : « 20 minutes drôles »
E1-01 → E1-02/03/04a/06 · E2-01..05 · E3-01..04, E3-06 · E4-02, E4-06 · E5-02 (v0) · E6-01, E6-02 · E8-07 (voix de proximité + interphone, version basique) · E10-01 (blockout 3 compartiments) · E11-01, E11-03, E11-04, E11-06..09 (greybox/placeholder acceptable) · E12-01 · E16-01.
Décision : **D-02 moteur**, **D-03 équipe**.

## VS — M+5 : bateau complet + Fjord + une mission
Bateau 6 compartiments (E10-01/02 partiel) · E3-05, E3-09 (partiel) · E4-01/03 · E5-01/04/05/06 · E6-04..08 · E7-02 · E8-01 (simple) · E9-02, E9-03, E9-09, E9-15, E9-16 · E10-03, E10-07 (min.) · E12-02..05 · E13-02 · E15-04 · E16-03 · E11-02, E11-15.

## ALPHA — M+9 : 3 zones, notation, base, progression, décontamination
E7-03/04/05 · E8-02..06 · E9-04..14 · E10-04..06, E10-09 · E1-08, E1-05 · E4-05 · E14-* · E11 P2/P3 majorité · E12-07.

## BETA — M+12 : contenu 1.0 verrouillé, localisation, accessibilité
E13-03..09 · E15-03, E15-06 · E1-07 · E16-04..06 · E17-02.

## GOLD — M+14
E17-07, patchs, build finale.

---

# RISQUES SUIVIS (lien avec GDD §11)

| Risque | Gravité | Tâches associées |
|---|---|---|
| Réacteur trop complexe pour un party game | 🔴 | E3-01, E3-11 |
| Silence = ambiance vocale morte | 🔴 | E8-01, E8-03, E8-07 |
| Deux Joueurs frustrant en duo | 🟠 | E4-05, E4-06, D-10 |
| Manuel = corvée | 🟠 | E5-01, E5-04, E5-09 |
| Référentiel mobile | 🟠 | **E1-01** (priorité absolue) |
| Réception du ton « guerre froide » | 🟡 | E13-10 |
| Budget localisation sous-estimé | 🟠 | E15-06 (cadrer tôt) |
| Volume d'assets (~405 j-artiste) | 🟠 | E11-06/07 (kits d'abord), lisser P3 sur l'alpha, D-03 |
