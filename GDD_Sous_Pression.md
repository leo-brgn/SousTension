# SOUS PRESSION / DERNIÈRE PATROUILLE

### Game Design Document — v0.3 (intègre les commentaires de relecture du 05/08/2026)

---

# 1\. VUE D'ENSEMBLE

## 1.1 Fiche d'identité

| Genre | Co-op chaotique à runs, simulation-parodie de sous-marin nucléaire |
| :---- | :---- |
| **Joueurs** | 2–4 en ligne (solo supprimé : la Règle des Deux Joueurs exige un équipage ; le jeu ne se lance pas seul) |
| **Durée d'une run** | 25–40 min (plusieurs run pour finir) |
| **Vue** | Première personne |
| **Plateformes** | Steam first |
| **Prix cible** | \< 10 € |
| **Classification visée** | PEGI 12 (humour, péril, aucune violence graphique) |
| **Marché de référence** | PEAK, RV There Yet?, Lethal Company, Content Warning |
| **Équipe cible** | À définir — **TODO** |

## 1.2 High concept

> **La guerre est finie depuis 34 ans. Personne n'a prévenu le sous-marin et ses quatre idiots de matelot.** Vous êtes ces 4 idiots matelots du *KMS Irrévocable*, sous-marin nucléaire d'une nation qui n'existe plus. Vos ordres depuis 1971 sont formels : patrouiller jusqu'à réception du signal de fin de mission. Le signal n'est jamais venu. Le réacteur tient avec du scotch, le manuel fait 900 pages (en théorie : en pratique chaque procédure est aidée et tient en 5 étapes, voir 3.5), et chaque manœuvre exige deux personnes synchronisées à deux bouts du sous-marin. Deux moyens de communication seulement : le **chat de proximité** et les **interphones** (vieux comms qui grésillent). Suivez la procédure. Ou survivez. Rarement les deux.

## 1.3 Fantasme du joueur

Être un rouage incompétent mais indispensable d'une machine de guerre nucléaire tenue par la bureaucratie, la rouille et l'amour. Le joueur ne rêve pas d'être un héros : il rêve que, pour une fois, la vidange du circuit primaire se passe bien.

## 1.4 Arguments de vente (USP)

1. **Un réacteur nucléaire qui fait rire.** C'est la machine centrale du bateau : personne ne la comprend vraiment, elle alimente tout, et le moindre réglage déclenche une réaction en chaîne ailleurs. *(À produire : l'arbre de décision des dépendances réacteur → vapeur → électricité → systèmes, pour que la chaîne reste lisible et jamais arbitraire.)*  
2. **La Règle des Deux Joueurs.** Les manœuvres importantes demandent deux joueurs, chacun à un bout du sous-marin, qui doivent agir en même temps. On ne suggère pas de jouer en équipe : on l'impose, littéralement, dans les règles du bateau.  
3. **La Procédure.** Un immense manuel qui fait partie du décor et qu'on doit consulter en jeu. Il a toujours raison — le problème, c'est qu'on n'a jamais le temps de le lire à temps. Il doit rester lisible même pour des joueurs qui pensent que lire est la bonne idée : pages courtes, schémas gros, mots-clés en gras (voir 3.5).  
4. **Un humour qui n'est jamais écrit.** Aucune blague, aucun dialogue drôle : tout le comique vient de la physique, des systèmes qui s'enraillent et de la panique des joueurs face à ça.

---

# 2\. NARRATION ET UNIVERS

## 2.1 Le monde

Uchronie légère, esthétique guerre froide, années 80 alternatives. Deux blocs se sont fait face pendant quarante ans : la **Fédération Kravique** (bloc est fictif, bureaucratie sublime, ingénierie robuste et absurde) et **l'Entente** (bloc ouest fictif, jamais montré). En 1992 (21 ans après la mise en service du sous-marin), la Fédération s'est dissoute paisiblement, en trois semaines, dans une indifférence administrative totale.

Le problème : la doctrine navale kravique reposait sur des sous-marins « à ordres scellés », conçus pour patrouiller coupés du monde, insensibles à toute transmission non authentifiée — précisément pour qu'aucun ennemi ne puisse leur faire croire que la guerre était finie. Le formulaire officiel de fin de mission (le **K-90/B**, en trois exemplaires — **à designer** : gabarit, tampons, typographie « administration kravique », voir kit papier de l'annexe) devait être remis en main propre à chaque commandant.

Un sous-marin a été oublié dans la pile.

## 2.2 Le bateau : KMS *Irrévocable*

Sous-marin nucléaire lance-missiles de classe *Molosse*, fierté de la flotte en 1971 (mise en service : un premier marin de 20 ans à l'époque en a 75 en 2026), ruine flottante aujourd'hui. Son réacteur, le **RK-1 « Petit Soleil »**, était réputé increvable ; il l'est, au sens où il refuse de mourir, pas au sens où il fonctionne bien. Le bateau est couvert de plaques gravées, d'avertissements contradictoires et de réparations artisanales léguées par 34 ans d'équipages successifs — chaque génération a laissé des mots griffonnés au crayon gras à côté des instructions officielles (*« NE PAS TOUCHER »*, *« si ça siffle c'est normal »*, *« Micha est mort ici, pense à lui »*). Ces graffitis sont écrits dans plusieurs langues (les équipages successifs venaient de toute la Fédération) — **à trancher** selon le budget de localisation.

L'*Irrévocable* est le vrai personnage principal. Il n'est jamais menaçant : il est **vieux, fidèle et dangereux**, comme un chien énorme qui ne sait pas sa force.

## 2.3 L'équipage (les joueurs)

Les joueurs incarnent des **matelots de 3e classe** — la conscription kravique était à vie, et l'équipage d'origine (120 personnes) s'est réduit au fil des décennies par départs à la retraite en mer, radeaux de fortune et promotions posthumes. Le plus jeune membre d'équipage avait 18 ans en 1992 : il en a 52 en 2026. Il ne reste que le bas de la hiérarchie : quatre matelots qui n'ont jamais été formés qu'à éplucher des pommes de terre, désormais responsables d'un vecteur de dissuasion nucléaire.

Pas de classes, pas de rôles fixes : tout le monde peut tout faire, et personne ne sait rien faire. Les personnages sont personnalisables : options simples au départ (visages patauds, quelques moustaches, une casquette), le reste (moustaches réglementaires en 12 modèles, casquettes) se débloque grâce à des **succès**.

## 2.4 Le Commandant

Un seul PNJ à bord : le **Commandant Ossip Varga**, 75 ans (20 ans à la mise en service en 1971), qui s'est enfermé **tout seul** — il est un peu fou — dans le carré des officiers depuis 1997, et qui communique uniquement par **tube pneumatique**. Sa cabine n'est pas modélisée : seule la porte existe, et c'est de derrière qu'il envoie ses ordres. Ses capsules arrivent en sifflant à intervalles : ordres de mission, réprimandes, quotas, souvenirs de guerre inutiles, et parfois une part de son gâteau d'anniversaire.

Fonctions de design :

- **Diégétiser le quota et le briefing** (rôle de « la compagnie » dans les jeux du genre) sans écran de menu.  
- **Ton du jeu** : ses messages sont tapés à la machine, solennels et complètement déconnectés de la réalité (*« Matelots. Le pays compte sur vous. »* — le pays n'existe plus).  
- **Mystère filé** : ne l'a-t-on jamais vu parce qu'il est timide… ou pour une autre raison ? La porte du carré est le seul endroit du bateau qui ne s'ouvre pas. Arc narratif de fin de jeu (voir 2.6).

## 2.5 La mission (structure des runs)

Le Commandant ordonne des **patrouilles** : sortir du fjord-base secret, accomplir un objectif, revenir. **Règle de patrouille : à chaque patrouille, l'équipage doit soit agir avec les commandes (activées) du sous-marin, soit sortir du sous-marin.** Les objectifs sont des relectures militaires absurdes des standards du genre :

- **Ravitaillement** : rejoindre un dépôt sous-marin kravique abandonné et récupérer vivres, pièces et combustible nucléaire. Le sous-marin arrive tout seul à l'endroit : l'équipage est déjà en scaphandre dans le sas et sort directement collecter. (= la collecte de butin)  
- **Relevé** : photographier au périscope des installations « ennemies » — en réalité des parcs éoliens, des ferries et des clubs de plongée. (= objectifs de discrétion). Attention au bruit : un joueur manœuvre le périscope, un autre regarde l'écran et prend les « screens », les deux autres font les zouaves (bruits aléatoires, soupe qui déborde…) et mettent la discrétion en péril. **Fusionné avec Écoute** (voir ci-dessous).  
- **Écoute** : déployer une antenne et enregistrer des « transmissions codées de l'Entente » — la radio FM locale. (= missions d'escorte d'un objet fragile et bruyant). *Peu d'intérêt en mission à part : devient une variante « relevé sonore » du Relevé, avec antenne à déployer (à la manivelle, donc bruyante).*  
- **Entretien du Signal** : maintenir la ligne de réception du fameux signal de fin de guerre, que l'équipage attend toujours. (= missions de réparation en scaphandre). Inclut la **protection du bateau contre les filets de pêche** (poser des cages anti-filets sur la coque, en scaphandre).

**L'ironie structurelle** : les joueurs, eux, comprennent vite que la guerre est finie (les indices sont partout : conteneurs modernes, épaves de jet-skis, emballages plastiques). Le comique naît de l'écart entre la solennité des ordres et la banalité du monde extérieur. On ne se moque jamais de l'équipage — on est ému *pour* lui.

## 2.6 Arc narratif long

Trois actes, racontés uniquement par l'environnement, les capsules pneumatiques et les documents trouvés en mission — jamais par des cinématiques.

- **Acte I — La Routine** : patrouilles ordinaires, découverte du bateau, montée du doute (« attendez, c'est un paquebot de croisière, ça »).  
- **Acte II — Le Doute** : la tension monte. De nouveaux types de patrouilles apportent du stress : **poursuites** (voir 3.7), inspections surprises, silence radio forcé. Des capsules du Commandant de plus en plus étranges ; des missions qui remontent la propre histoire du bateau (retrouver un radeau parti en 1983, un dépôt jamais atteint).  
- **Acte III — Le Signal** : l'équipage peut reconstituer, pièce par pièce, l'émetteur capable de générer lui-même le code de fin de mission — et choisir de l'envoyer. Dernière patrouille : remonter en surface, en plein jour, au milieu d'une régate. Fin du jeu : la porte du carré s'ouvre. Ce qu'il y a derrière est écrit à la production, pas dans ce document, et ne fuitera pas.
- **Acte IV — Le Jumeau** *(proposition)* : à peine le Signal envoyé, un **sous-marin jumeau abandonné**, lui aussi oublié dans la pile, ne reconnaît pas le code et torpille l'*Irrévocable* — l'ultime patrouille est une évasion. (L'option « bombe nucléaire sur un pays au hasard » est écartée : elle viole la règle n°3 des trois interdits.)

**Acte III — routine ou compréhension.** Le matériel de l'Émetteur se trouve dans les sites de ravitaillement, à l'intérieur de la routine des patrouilles : soit le joueur exécute la routine et recommence du début, soit il comprend qu'il peut faire autre chose (fouiller, dévier) et découvre une pièce. La routine est le piège, la curiosité la récompense.

Le contenu reste rejouable après la fin (structure « la fin est un choix, pas un mur »).

## 2.7 Ton — la règle des trois interdits

1. **Interdit d'écrire des blagues.** Aucun dialogue comique, aucun PNJ rigolo à part la solennité déplacée du Commandant. L'humour sort des systèmes.  
2. **Interdit de faire peur pour de vrai.** Tension oui, horreur non. La mort est toujours un peu ridicule, jamais cruelle.  
3. **Interdit de cynisme.** Le jeu aime son équipage, son bateau et sa nation disparue. Référence de ton : la tendresse de *Grand Budapest Hotel* appliquée aux mécaniques de *Lethal Company*.

---

# 3\. GAMEPLAY

## 3.1 Boucle macro

BASE (fjord)  →  CAPSULE D'ORDRES  →  APPAREILLAGE  →  TRANSIT  →  OBJECTIF  →  RETOUR  →  DÉBRIEF

     ↑                                                                                        │

     └────────────────  réparations, tampons, améliorations, capsules du Commandant  ─────────┘

Le **quota** est remplacé par la **Note de Patrouille** : le Commandant note chaque sortie (objectif, discrétion, état du bateau, « tenue réglementaire »). Trois mauvaises notes \= passage en cour martiale, c'est-à-dire reset de la progression matérielle (le bateau est « rendu à l'état d'inventaire »). Les critères de notation sont partiellement absurdes et affichés à l'avance : cela crée des dilemmes drôles (risquer la coque pour récupérer la casquette réglementaire tombée à la mer).

## 3.2 Boucle micro

UN SYSTÈME DÉRIVE → UNE ALARME (parmi 40\) SONNE → QUELQU'UN CHERCHE LAQUELLE

        ↑                                                    ↓

   NOUVELLE DÉRIVE  ←  CORRECTION (excessive)  ←  LE MANUEL DIT QUOI FAIRE (page 612\) *(le manuel s'ouvre tout seul à la bonne page, voir 3.5)*

Le jeu est une machine à dette d'attention : plus de systèmes que de joueurs, et chaque correction déséquilibre autre chose. La spécificité « nucléaire » : la chaîne causale est **longue et différée**. Toucher aux barres de contrôle maintenant, c'est un problème de vapeur dans 90 secondes, un problème électrique dans trois minutes. Les joueurs apprennent à lire l'avenir du bateau — et à paniquer en avance, ce qui est objectivement plus drôle que paniquer à l'heure.

## 3.3 Le cœur du jeu : le réacteur RK-1 « Petit Soleil »

Toute la conception gravite autour d'une chaîne physique simplifiée mais **vraie** — le joueur apprend, sans s'en rendre compte, le vrai principe d'un sous-marin nucléaire :

BARRES DE CONTRÔLE → CHALEUR → VAPEUR → TURBINE → ÉLECTRICITÉ \+ PROPULSION

                        ↓                              ↓

                  CIRCUIT DE                     TOUT LE RESTE

                REFROIDISSEMENT                (pompes, air, lumières,

                 (pompes… électriques)          sonar, cafetière)

**La boucle diabolique** : le refroidissement du réacteur consomme l'électricité que produit le réacteur. Monter la puissance chauffe plus, donc exige plus de refroidissement, donc plus d'électricité, donc plus de puissance. Le bateau est un animal qu'on nourrit de lui-même.

Trois régimes, choisis au tableau de commande :

| Régime | Produit | Bruit | Risque |
| :---- | :---- | :---- | :---- |
| **Veille** | Le minimum vital | ○ | L'air et la lumière déclinent lentement |
| **Croisière** | Confortable | ●● | Dérives lentes, gérable |
| **Pleine puissance** | Tout, vite | ●●●● | Surchauffe en \~4 min sans surveillance active |

**Le SCRAM** (arrêt d'urgence) : un gros levier rouge, accessible à n'importe qui, n'importe quand, sans vote. Il sauve le réacteur et tue instantanément toute l'électricité du bord : noir total, silence, pompes arrêtées, sous-marin qui commence à couler doucement. C'est le pendant du « largage d'urgence » du genre : la décision individuelle qui produit les meilleures histoires. Redémarrer le réacteur est une procédure à deux joueurs de 90 secondes, à la lampe torche, en suivant le manuel — procédure elle aussi bien aidée (ruban, page ouverte, étapes ≤ 5).

## 3.4 La Règle des Deux Joueurs — mécanique signature

Doctrine kravique : **aucune action critique ne peut être exécutée par un joueur seul.** Conséquence assumée : il est **impossible de lancer une partie seul**, le solo est interdit. Mécaniquement : deux commandes physiques éloignées (deux clés, deux manivelles, un bouton \+ une pédale) à actionner dans une fenêtre de 3 secondes, sans se voir, dans un bateau où crier a un coût.

*(À faire : la liste exhaustive de toutes les mécaniques passant en mode deux joueurs, à consolider en annexe.)*

S'applique à : démarrage/arrêt réacteur, ouverture des vannes principales de ballast, tir de leurres, ouverture du sas extérieur, purge du circuit primaire, et — sommet du jeu — **l'envoi du Signal** en acte III.

Pourquoi c'est la bonne mécanique pour ce jeu :

- Elle rend la coop **structurellement obligatoire** sans écrire « jouez ensemble » nulle part.  
- Elle transforme la communication en gameplay de précision (« 3, 2, 1, TOURNE » hurlé dans l'interphone qui grésille). Deux canaux : chat de proximité et interphones.  
- Elle est historiquement authentique (contrôle des armes nucléaires), donc thématiquement parfaite.  
- **Pas de solo.** Le mode « Effectifs Réduits » est supprimé ; le minimum est un duo, ce qui coupe court à la frustration du speedrun d'une clé à l'autre.

## 3.5 La Procédure : le manuel comme objet de jeu

Le **Manuel d'Exploitation OK-114** est un objet physique : un classeur de 8 kg posé au poste central. Il contient *réellement* toutes les procédures du jeu, correctes, illustrées, numérotées — c'est le tutoriel, l'anti-sèche et le compagnon du jeu.

Contraintes de design qui le rendent drôle :

- Il faut **le tenir à deux mains** pour le lire. Donc celui qui lit ne peut pas agir : le jeu pousse naturellement vers le duo lecteur/exécutant (« étape 4 : tourner la vanne C-12 d'un quart de tour — UN QUART j'ai dit »).  
- Il n'existe qu'**en un exemplaire**. On peut arracher des pages (elles se perdent, brûlent, se mouillent).  
- L'index renvoie à des pages, qui renvoient à des annexes, qui renvoient à des errata collés par les équipages précédents. Trouver la bonne procédure sous pression devait être un mini-jeu : **non, on simplifie par des mécaniques d'aide** — un bouton « GOTO » sur l'alarme qui sonne, ou une ouverture automatique du manuel à la bonne page.  
- Les annotations manuscrites des anciens équipages contredisent parfois l'officiel — et ce sont *elles* qui ont raison. Exemple : les boutons officiels décrits par le manuel n'existent plus (arrachés et remplacés), seule l'annotation manuscrite indique le bon. Récompense la curiosité et construit la narration environnementale.

## 3.6 Radiation et contamination

La radiation n'est **jamais létale rapidement** (règle de ton n°2) — c'est un système de *contrainte spatiale et sociale* :

- Certaines pannes (fuite primaire, combustible endommagé, récupéré dans la zone de ravitaillement) créent des **zones chaudes** invisibles à l'œil nu.  
- Le seul instrument : le **dosimètre à aiguille**, porté au poignet, qui crépite. Pas de HUD : on tend le bras vers les choses pour les « écouter ».  
- Un joueur trop exposé devient **contaminé** : la contamination s'ajoute à la **barre de vie / PV** (lue sur le dosimètre, pas de HUD) ; il crépite en permanence, contamine ce qu'il touche (poignées, leviers, le manuel \!), et ses mains tremblent (mini-jeux plus durs).  
- Remède : la **douche de décontamination** au sas — un joueur doit asperger l'autre au jet, intégralement, pendant 20 secondes. Le jet de décon fonctionne comme un mode nettoyage à la *PowerWash Simulator* : on frotte la contamination visible jusqu'à ce qu'elle disparaisse. C'est la mécanique la plus filmée du jeu, on le sait, on l'assume.  
- La dose est **réinitialisée entre les runs**, comme tout le reste de l'état du joueur. Aucune mort par radiation : au pire, un évanouissement théâtral (voir 3.9).

## 3.7 Discrétion acoustique et « l'Entente »

Le bateau émet un **bruit total** (régime réacteur \+ pompes \+ chocs \+ voix des joueurs dans les compartiments proches de la coque). En mission, des **patrouilles de l'Entente** (frégates, avions, sonars trempés) balaient les zones : être détecté ne déclenche pas un combat — l'*Irrévocable* n'a plus une seule arme fonctionnelle — mais une **poursuite** : grenades d'exercice assourdissantes qui secouent le bateau, cassent des systèmes et font dégringoler la Note de Patrouille (« DISCRÉTION : INSATISFAISANT »).

Contre-mesures, toutes manuelles :

- **Silence radio** : régime Veille \+ interdiction de courir \+ chuchotement. Le jeu au bord de l'implosion nerveuse.  
- **Poser le bateau sur le fond** : manœuvre à deux joueurs, risquée, magnifique.  
- **Leurres** : un tube lance un émetteur de bruit… qu'il faut d'abord remonter à la manivelle, ce qui fait du bruit.  
- **La thermocline** : une couche d'eau qui déforme le sonar, visible sur la carte, refuge naturel. Enseigne un vrai concept de guerre sous-marine sans un mot de tutoriel.

*(Note : le sonar et la furtivité par signature reprennent l'idée de mimétisme du v0.1, mais recentrée sur la seule discrétion acoustique — plus de créatures imitatrices, plus d'emprunt narratif.)*

## 3.8 Menaces — jamais de monstres

Le jeu n'a **aucune créature fantastique**. Les antagonistes, par ordre d'importance :

1. **Le bateau lui-même** (70 % des morts visées) : pannes en cascade, eau, vapeur, électricité.  
2. **La mer** : courants, hauts-fonds, filets de pêche industriels (le pire ennemi du jeu, sincèrement), icebergs, tempêtes en surface.  
3. **L'Entente** : la poursuite acoustique décrite en 3.7. Toujours non létale, toujours humiliante.  
4. **L'administration** : la Note de Patrouille, les inspections surprises annoncées par capsule (« rangez le bateau, contrôle dans 4 minutes »), les formulaires à tamponner pour débloquer le matériel.

## 3.9 Mort, échec et rejouabilité

- Un joueur « meurt » \= il **s'évanouit théâtralement** (vapeur, choc, asphyxie légère). Un coéquipier peut le traîner à l'infirmerie et le ranimer aux sels. Sans secours, il reste au sol jusqu'à la fin de la patrouille — et peut parler, ce qui est pire.  
- **Échec de la run** \= le bateau coule ou s'échoue. Écran de constat façon rapport dactylographié : cause de l'avarie, chronologie des 60 dernières secondes, responsable probable (le jeu désigne un joueur au hasard pondéré par ses actions — source de mauvaise foi infinie).  
- Le bateau est **persistant** : les dégâts non réparés restent d'une run à l'autre. La flotte kravique n'a plus de budget.

## 3.10 Contrôles (PC, manette supportée)

| Entrée | Action |
| :---- | :---- |
| ZQSD / stick | Déplacement |
| Souris / stick droit | Regard |
| Clic gauche / RT | Interagir (tenir pour les manivelles, vannes : geste circulaire) |
| Clic droit / LT | Examiner / lire |
| E / A | Prendre-poser (objets à deux mains : verrouille les autres actions) |
| Maj / L3 | Courir (bruit ●●●) |
| V (maintenu) | Chuchoter |
| T | Parler dans l'interphone (si on est au combiné) |
| Molette / D-pad | Roue d'émotes sonores et gestuelles (jouable 100 % sans micro) |

Principe : **une main \= une chose**. Pas d'inventaire au-delà des deux mains et d'une poche de poitrine (un petit objet : lampe, tampon, sandwich).

---

# 4\. WORLD & LEVEL DESIGN

## 4.1 Le bateau (le niveau principal)

28 m jouables, six compartiments en enfilade, sas manuels. Aucun poste n'est visible depuis un autre — l'isolement est la matière première du jeu.

| \# | Compartiment | Fonction | Spécificité comique |
| :---- | :---- | :---- | :---- |
| 1 | **Torpilles (proue)** | Stockage cargo, tubes reconvertis en casiers | Le cargo modifie l'assiette ; tubes utilisables pour « expédier » des objets (ou des joueurs, déconseillé) |
| 2 | **Poste central** | Barre, périscope, carte papier, le Manuel, tube pneumatique du Commandant | Le périscope monte/descend à la manivelle et gifle les distraits |
| 3 | **Radio & sonar** | Écoute, enregistreur à bandes, chiffrement | La machine à chiffrer exige de taper les messages *à la machine à écrire* |
| 4 | **Réacteur** | Tableau RK-1, barres, dosimétrie | Zone chaude potentielle ; hublot de visée du cœur (« il est joli quand même ») |
| 5 | **Machines** | Turbine, tableau électrique, pompes | On n'y entend rien ; l'interphone y est réglé trop fort |
| 6 | **Sas & vie (poupe)** | Scaphandre, douche de décon, couchettes, cambuse | La soupe sur le feu se renverse à la première inclinaison |

## 4.2 Les zones de patrouille (1.0 : trois zones)

- **Le Fjord** (tutoriel implicite) : eaux de la base, épaves kraviques, aucun ennemi. Apprendre le bateau.  
- **La Mer Grise** : hauts-fonds, filets, trafic civil dense, patrouilles légères de l'Entente. Zone principale.  
- **La Fosse de Varn** : profonde, sombre, dépôts militaires les plus riches, thermoclines, patrouilles lourdes. Endgame.

Chaque zone est une carte semi-ouverte à objectifs générés (dépôts, épaves, points d'écoute), météo et trafic variables. Pas de génération procédurale du terrain : des cartes **apprises par cœur** créent l'expertise et le vocabulaire commun (« passe par le chenal de la grue »).

## 4.3 Structure hors-bateau : la base

Le fjord-base est un mini-hub à pied : ponton, grue de chargement, atelier, bureau des tampons, tableau d'affichage (missions, notes, classements entre équipages amis). Tout achat se conclut par un tampon encreur à apposer soi-même. C'est idiot et tout le monde le fera avec un plaisir immense.

---

# 5\. INTERFACE & UX

- **Zéro HUD.** Toute information est un instrument : cadrans, aiguilles, bandes de papier, dosimètre au poignet. Un seul élément d'écran : les sous-titres et le nom du joueur qui parle.  
- **Diégétisation des menus** : options \= le poste radio de la base ; cosmétiques \= l'armoire de l'équipage ; invitations \= le registre de bord (code à 6 caractères).  
- **Lisibilité avant réalisme** : gros cadrans, aiguilles franches, code couleur strict (rouge réservé aux urgences réelles).  
- **Accessibilité** : jouable sans micro (roue d'émotes complète), sous-titres directionnels, mode daltonien sur les codes couleur, option réduisant les basses fréquences (grenades), taille de texte du Manuel réglable.

---

# 6\. DIRECTION ARTISTIQUE

- **Visuel** : réalisme pataud — proportions légèrement cartoon (gros boutons, gros leviers, personnages courtauds), matériaux crédibles (laiton, bakélite, émail écaillé). Palette : vert bouteille, crème administratif, rouille, un rouge unique pour l'urgence.  
- **Typographie diégétique** : tout le lore passe par des plaques gravées, tampons, formulaires. La typo « administration kravique » est un personnage.  
- **Lumière** : un plafonnier par compartiment, lampes torches, éclairage de secours rouge sur batterie. La chute de tension (blanc → orange → rouge → noir) est la signature visuelle du jeu.  
- **Animation** : les gestes des personnages sont sincères et laborieux — on tourne les vannes avec tout le corps. Aucune animation « cool ».

# 7\. AUDIO

- **Priorité budgétaire n°1.** Le jeu doit être compréhensible les yeux fermés : chaque système a une voix (le réacteur ronronne, la turbine siffle, la coque parle selon la profondeur).  
- Occlusion quasi totale entre compartiments ; l'interphone grésille *par design*.  
- Le crépitement du dosimètre est mixé pour être physiquement désagréable à fort niveau.  
- Musique : aucune en patrouille. Deux exceptions : l'hymne kravique au gramophone de la cambuse (diégétique, cassable), et un unique thème orchestral réservé au retour au fjord après une patrouille réussie.

---

# 8\. TECHNIQUE

- **Moteur** : Unity ou Unreal ; décision conditionnée au prototype physique (voir Jalons).  
- **Simulation** : eau par volumes-par-compartiment \+ centre de masse (pas de fluide réel) ; chaîne réacteur \= système d'équations simple à pas fixe, tick 10 Hz, déterministe (indispensable pour le netcode et l'équilibrage).  
- **Réseau** : host-client, autorité serveur sur bateau/eau/réacteur, prédiction locale joueurs. Risque n°1 : les joueurs marchant dans un référentiel mobile — à prototyper en premier.  
- **Reconnexion en cours de partie** : obligatoire dès la 1.0.  
- **Voix** : proximité \+ occlusion \+ captation du volume réel (avec VU-mètre diégétique pour que la règle soit lisible).

---

# 9\. MODÈLE ÉCONOMIQUE & MARCHÉ

- **Premium à petit prix (7–9 €)**, achat d'impulsion de groupe, pas de microtransactions, pas de saison, pas de monnaie premium. Cosmétiques gagnés en jeu uniquement.  
- **Cible** : groupes de 3-4 amis, 18-30 ans, consommateurs de PEAK / RV There Yet? / Lethal Company ; forte sensibilité au bouche-à-oreille TikTok/YouTube.  
- **Stratégie de visibilité** : le jeu est conçu pour le clip de 40 secondes (SCRAM dans le noir, douche de décon, périscope-gifle, compte à rebours à deux clés raté). Programme créateurs avant la sortie ; démo Steam Néxt Fest avec le Fjord uniquement.  
- **Localisation 1.0** : FR, EN, DE, ES, PT-BR, PL, RU, JA, ZH — le jeu étant très textuel (le Manuel), la localisation est un poste de coût réel à budgéter tôt.

---

# 10\. PLAN DE PRODUCTION

## 10.1 Jalons

| Jalon | Échéance | Contenu | Critère de passage |
| :---- | :---- | :---- | :---- |
| **Prototype** | S+8 | 3 compartiments, chaîne réacteur, eau, une fuite, 4 joueurs en ligne | 20 min drôles sans monstre ni objectif, sinon pivot |
| **Vertical slice** | M+5 | Bateau complet, le Fjord, une mission, Manuel v1, Deux Joueurs, SCRAM | Testable par des inconnus sans explication orale |
| **Alpha** | M+9 | 3 zones, notation, base, progression, décontamination | Boucle complète rejouable 10 h |
| **Beta** | M+12 | Contenu 1.0 verrouillé, localisation, accessibilité | Zéro bug bloquant, onboarding validé |
| **Gold** | M+14 | — | — |

## 10.2 Périmètre 1.0

1 bateau, 3 zones, \~12 types de missions, \~30 objets de cargo, \~12 améliorations, \~50 cosmétiques, l'arc narratif complet (actes I-III), mode duo (2 joueurs, minuteries élargies).

**Post-lancement** : 4e zone (l'Arctique), second bateau (diesel, 2 joueurs, encore pire), inspections coopératives entre deux équipages (8 joueurs), crossplay.

**Jamais** : PvP, imposteur, battle pass, horreur.

---

# 11\. RISQUES

| Risque | Gravité | Mitigation |
| :---- | :---- | :---- |
| La chaîne réacteur est trop complexe pour un party game | 🔴 | Trois régimes seulement en façade ; la complexité est *découvrable*, jamais requise. Test : un nouveau doit être utile en 5 min, pas compétent |
| La contrainte de silence tue l'ambiance vocale | 🔴 | Le bruit ne tue jamais vite : il déclenche une poursuite lente, audible, non létale — on a le temps de paniquer en riant |
| La Règle des Deux Joueurs frustre en duo | 🟠 | Solo supprimé ; en duo : minuteries élargies, télécommande bricolée déblocable (diégétique : « strictement interdite par le règlement ») |
| Le Manuel devient une corvée de lecture | 🟠 | Procédures ≤ 5 étapes, toutes illustrées ; les habitués finissent par les connaître par cœur — c'est la courbe de maîtrise voulue |
| Référentiel mobile (joueurs sur bateau en mouvement) | 🟠 | Prototype semaine 1 ; le choix du moteur en dépend |
| Le ton militaire froid-guerre mal reçu selon l'actualité | 🟡 | Nation 100 % fictive, aucune arme fonctionnelle à bord, zéro violence ; le jeu parle d'abandon administratif, pas de guerre |

---

# 

# 12\. ANNEXE — LE PITCH STEAM

> **La guerre est finie. Personne n'a prévenu votre sous-marin.** Coop 2-4. Un réacteur nucléaire capricieux, un manuel de 900 pages, et des ordres que plus personne ne peut annuler. Suivez la procédure. Tournez la clé en même temps que votre pote. Et par pitié, arrêtez de courir dans la coursive.

### Annexe production au GDD v0.2 — Asset List v1.0

**Conventions**

- **P1** \= indispensable au prototype (S+8) · **P2** \= vertical slice (M+5) · **P3** \= alpha/1.0  
- 🔧 \= objet interactif (nécessite états/pivots animables, colliders précis, versions main)  
- 📦 \= objet physique transportable (masse, deux mains)  
- Un objet interactif \= souvent 2 à 4 sous-meshes (corps, levier, aiguille, capot)  
- Estimations en fin de document.

---

## 1\. LE BATEAU — STRUCTURE (kit modulaire)

L'intérieur se construit en kit pour itérer sur le layout sans remodéliser.

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Section de coque intérieure droite (module 2 m) | P1 | Membrures, rivets, câblage plafond |
| Section de coque courbe proue | P2 |  |
| Section de coque courbe poupe | P2 |  |
| Cloison pleine inter-compartiments | P1 |  |
| Sas manuel de cloison 🔧 | P1 | Porte \+ volant central \+ 6 loquets, états ouvert/fermé/entrebâillé |
| Plancher caillebotis (module) | P1 | Grille ajourée — on voit l'eau monter dessous |
| Fond de cale sous caillebotis | P1 | Reçoit le volume d'eau, débris, « choses tombées » |
| Échelle verticale \+ trémie | P2 | Accès kiosque |
| Kiosque intérieur (habitacle périscope) | P2 |  |
| Coque extérieure complète classe *Molosse* | P2 | Vue plongeur \+ vue carte ; 3 LODs |
| Barres de plongée, safran, hélice (ext.) 🔧 | P2 | L'hélice tourne selon le régime |
| Kiosque extérieur, antennes, périscope sorti | P2 |  |
| Variantes d'usure de coque ext. (3 niveaux) | P3 | Dégâts persistants entre runs |
| Tuyauterie modulaire (droite, coude, T, vanne inline) | P1 | Kit — sert partout, dizaines d'instances |
| Chemin de câbles \+ boîtiers de jonction (kit) | P2 |  |
| Plaques gravées / panneaux réglementaires (kit, \~15 formes) | P2 | Le texte est texture — 1 mesh, N matériaux |

## 2\. COMPARTIMENT 1 — TORPILLES / CARGO

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Tube lance-torpilles (x4 visibles) 🔧 | P2 | Porte de culasse ouvrable, intérieur vide/casier |
| Râtelier de stockage cargo 🔧 | P2 | Points d'ancrage : le cargo mal arrimé glisse |
| Sangles d'arrimage 🔧 | P2 |  |
| Palan/rail de manutention plafond 🔧 | P3 |  |
| Torpille d'exercice inerte (déco) 📦 | P3 | Souvenir de guerre, très lourde, inutile, transportable quand même |

## 3\. COMPARTIMENT 2 — POSTE CENTRAL

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Barre de direction (volant) 🔧 | P1 |  |
| Pupitre de barre : profondeur/assiette (2 volants \+ indicateurs) 🔧 | P1 |  |
| Télégraphe machine 🔧 | P1 | Cadran laiton à poignée, 5 positions |
| Périscope 🔧 | P2 | Monte/descend à la manivelle, poignées rabattables, tête ext. séparée — inclut l'anim « gifle » |
| Table à cartes \+ carte papier 🔧 | P1 | Carte \= mesh plan \+ décal dessinable (crayon gras) |
| Crayon gras 📦 | P1 |  |
| Compas de relèvement, règle Cras, chronomètre 📦 | P2 | Outils de nav posables sur la carte |
| **Le Manuel OK-114** 📦🔧 | P1 | Asset star : classeur 8 kg, pages feuilletables (shader de pages \+ \~30 doubles-pages texturées), pages arrachables (mesh page volante) |
| Pupitre central multi-instruments | P1 | Support des cadrans (voir kit instruments §9) |
| Tube pneumatique du Commandant 🔧 | P1 | Station d'arrivée \+ trappe \+ sifflement ; capsule séparée |
| Capsule pneumatique 📦 | P1 | S'ouvre, contient un papier roulé |
| Horloge de bord | P2 |  |
| Tableau à craie \+ craie 🔧📦 | P2 | Surface dessinable |
| Portrait officiel du Commandant (cadre) | P2 | Toujours légèrement de travers |
| Fauteuil du chef de quart | P2 | Personne n'a le droit de s'y asseoir (succès Steam si on le fait) |

## 4\. COMPARTIMENT 3 — RADIO & SONAR

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Console sonar à écran circulaire 🔧 | P2 | Écran \= render target ; capot, molettes de gain |
| Casque d'écoute sonar 📦🔧 | P2 | Portable, câble physique |
| Hydrophone à manivelle 🔧 | P2 | Orientation manuelle de l'écoute |
| Enregistreur à bandes magnétiques 🔧 | P2 | Bobines qui tournent, bande chargeable |
| Bobine de bande vierge / enregistrée 📦 | P2 |  |
| Poste radio VLF \+ antenne déployable 🔧 | P2 | Manivelle de déploiement (bruit \!) |
| Machine à chiffrer (à clavier mécanique) 🔧 | P2 | Touches animées individuellement, chariot |
| Feuillets de messages / bloc de chiffrement 📦 | P2 |  |
| Baie électronique à lampes (déco animée) | P3 | Lampes qui chauffent — lisibilité de l'état électrique |
| **L'Émetteur du Signal** (acte III) 🔧 | P3 | Assemblé en 5 pièces trouvées en mission : 5 meshes \+ version assemblée |

## 5\. COMPARTIMENT 4 — RÉACTEUR

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Tableau de commande RK-1 « Petit Soleil » 🔧 | P1 | La pièce maîtresse : sélecteur 3 régimes, rangée de disjoncteurs, cadrans température/pression/flux |
| Levier SCRAM 🔧 | P1 | Gros, rouge, sous capot plombé à soulever — l'asset le plus filmé du jeu, soigner à l'extrême |
| Colonne des barres de contrôle (mécanisme apparent) 🔧 | P2 | Tiges qui montent/descendent visiblement selon le régime |
| Hublot de visée du cœur | P2 | Verre épais, lueur Cherenkov (émissif) |
| Circuit primaire : pompes (x2) 🔧 | P1 | État marche/arrêt/cassé lisible à l'œil |
| Vannes principales primaire (x4, volants) 🔧 | P1 |  |
| Échangeur / générateur de vapeur | P2 | Gros volume déco-fonctionnel, points de fuite vapeur |
| Panneau dosimétrie mural \+ alarme | P2 |  |
| Rideau/portique de zone contrôlée | P3 | Lamelles plastiques, franchissable |
| Bidon de bore d'urgence 📦 | P3 | Procédure annexe du Manuel |

## 6\. COMPARTIMENT 5 — MACHINES

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Turbine \+ réducteur | P2 | Grosse masse animée (vitesse liée au régime), capots ouvrables |
| Tableau électrique principal 🔧 | P1 | \~20 disjoncteurs à réarmer individuellement — chaque disjoncteur \= mesh à état |
| Batteries de secours (banc) 🔧 | P2 | Voltmètre, cosses, étincelles |
| Pompe de cale (x2) 🔧 | P1 |  |
| Ligne d'arbre \+ presse-étoupe 🔧 | P2 | Point de fuite classique, serrage à la clé |
| Compresseur d'air / cartouches CO₂ 🔧📦 | P2 | Cartouches remplaçables (mesh cartouche propre/usée) |
| Établi \+ panneau d'outils | P2 |  |
| Interphone (combiné mural) 🔧 | P1 | Un par compartiment — même asset partout |
| Ventilateur de gaine 🔧 | P3 |  |

## 7\. COMPARTIMENT 6 — SAS & VIE

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Sas de plongée (chambre \+ 2 portes \+ volants) 🔧 | P2 | Cycle inondation/vidange visible |
| Scaphandre sur son support 🔧 | P2 | Voir aussi personnages §11 (version portée) |
| Casque de scaphandre 📦 | P2 | Se visse — animation à 2 joueurs |
| Ombilical \+ touret 🔧 | P2 | Câble physique simulé \+ enrouleur à manivelle |
| Douche de décontamination 🔧 | P2 | Cabine \+ pommeau-jet orientable tenu par un joueur |
| Lance à eau de décon 📦🔧 | P2 |  |
| Couchettes superposées (x2 modules) | P2 | On peut s'y allonger |
| Cambuse : cuisinière \+ marmite de soupe 🔧 | P2 | La soupe \= fluide simple qui se renverse à l'inclinaison — asset comique prioritaire |
| Vaisselle en fer émaillé (kit 6 pièces) 📦 | P2 | Roule au sol, bruit |
| Table \+ banquettes | P2 |  |
| Gramophone \+ disque de l'hymne 🔧📦 | P3 | Cassable |
| Casiers personnels (x4) 🔧 | P3 | Rangement cosmétiques |
| Samovar 🔧 | P3 | Institution nationale, consomme de l'électricité, personne ne l'éteint jamais |
| WC marin \+ sa procédure murale 🔧 | P3 | 7 vannes. Historiquement exact. Comiquement obligatoire |
| **Porte du carré des officiers** (fermée) | P2 | L'asset le plus regardé du jeu : porte condamnée, fentes de tube pneumatique, lumière dessous |

## 8\. OBJETS DE MISSION & CARGO (\~30 en 1.0)

| Famille | Exemples | Prio |
| :---- | :---- | :---- |
| Caisses réglementaires (S/M/L) 📦 | Vivres, pièces, munitions d'exercice | P1 (la M dès le proto) |
| Fûts 📦 | Carburant, saumure, « NE PAS OUVRIR » | P2 |
| Combustible nucléaire (château de transport) 📦 | Très lourd, crépite, à 2 joueurs | P2 |
| Pièces détachées 📦 | Pompe neuve, cartouches, fusibles, joints, roulement | P2 |
| Objets d'épave à valeur 📦 | Coffre, cloche de bateau, hélice de bronze, instruments anciens | P2–P3 |
| Objets anachroniques (indices narratifs) 📦 | Jet-ski cassé, conteneur moderne, canard gonflable, panneau solaire | P3 |
| Objets bruyants à haute valeur 📦 | Balise active, boîte à musique bloquée | P3 |
| Reliques kraviques (acte II) 📦 | Radeau de 1983, journal de bord, médailles | P3 |
| Les 5 pièces de l'Émetteur 📦 | Voir §4 | P3 |

## 9\. KITS TRANSVERSAUX

**Kit instruments (le plus rentable du projet)** — P1 Bases communes déclinées par cadran/texture : manomètre rond S/M/L, indicateur à aiguille vertical, compteur à rouleaux, voyant sous dôme, VU-mètre (pour le volume vocal), interrupteur à levier, bouton-poussoir sous garde, sélecteur rotatif, volant de vanne S/M/L, manivelle, roue crantée. \~15 meshes de base → des centaines d'instances.

**Kit outils** 📦 — P1–P2 Marteau (P1), clé à molette (P1), patch de coque \+ cale en bois (P1), lampe torche (P1), lampe frontale, tournevis, pied-de-biche, chalumeau, seau (écoper \!), serpillière, extincteur 🔧, tampon encreur \+ encrier (P2 — mécanique de base), dosimètre-bracelet (P2, visible au poignet en 1re personne).

**Kit papier** — P1–P2 Formulaires (5 gabarits), ordre de mission, Note de Patrouille, page volante du Manuel, affiche de propagande (x8 visuels), photo d'époque, étiquettes.

**Kit dégâts & fluides (meshes support des VFX)** — P1 Jet de fuite (3 tailles — mesh d'ancrage), rivet sauté, tôle déformée (3 décals-meshes), plan d'eau de cale dynamique (mesh déformable par compartiment), nappe de vapeur (cartes), givre/condensation (décals), étincelles (point d'ancrage), flaque de contamination (décal animé).

## 10\. EXTÉRIEURS & ZONES

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| **La base du fjord** : ponton, grue 🔧, atelier, bureau des tampons, tableau d'affichage 🔧, baraquements, phare | P2 | Hub à pied — \~15 bâtiments/props majeurs |
| Terrain sous-marin : kit rochers (8), tombants, sable, forêt de kelp (cartes), cheminée hydrothermale | P2 |  |
| Épaves kraviques : cargo, chalutier, sous-marin jumeau (grand frisson garanti) | P2–P3 | Intérieurs partiellement explorables à pied |
| Dépôt militaire sous-marin (structure \+ sas) | P2 | Lieu de collecte principal |
| Filets de pêche industriels 🔧 | P2 | L'ennemi n°1 — simulation câble/tissu |
| Trafic civil de surface (coques vues du dessous) : ferry, porte-conteneurs, voilier, jet-ski, pédalo | P3 | Silhouettes \+ hélices — surtout vus au périscope |
| Forces de l'Entente : frégate (surface \+ carène), avion de patrouille, bouée sonar 🔧, grenade d'exercice | P3 | La frégate a 2 niveaux de détail : périscope (loin) et « beaucoup trop près » |
| Parc éolien offshore, plateforme, bouées de chenal 🔧, iceberg (3), régate de fin (10 voiliers \+ foule low-poly) | P3 |  |

## 11\. PERSONNAGES

| Asset | Prio | Notes |
| :---- | :---- | :---- |
| Matelot de base (corps unique, pataud) | P1 | Rig complet \+ mains 1re personne séparées |
| Têtes (x6) \+ moustaches (x12) \+ coiffures (x8) | P2–P3 | Système modulaire |
| Uniformes : tenue de bord (P1), vareuse, tenue de sortie, tablier de cuisine, pyjama réglementaire | P2–P3 |  |
| Casquettes/bonnets (x10) \+ cosmétiques divers (x50 en 1.0 : la plupart \= petits props) | P3 |  |
| Scaphandre porté (variante complète du personnage) | P2 |  |
| Version « contaminé » (surcouche shader \+ accessoires) | P2 |  |
| État évanoui (pose ragdoll contrôlée \+ traînage) | P2 | Anim, mais prévoir mesh de sels de réveil 📦 |
| Le Commandant Varga | P3 | Un seul modèle, très soigné, vu une fois. Silhouette d'abord (ombre sous la porte) |

## 12\. RÉCAPITULATIF & ESTIMATIONS

| Catégorie | Meshes uniques (\~) | Charge (jours-artiste, mod.+UV+textures) |
| :---- | :---- | :---- |
| Structure bateau int./ext. | 35 | 45 |
| Compartiments 1–6 (postes & mobilier) | 85 | 110 |
| Cargo & mission | 40 | 35 |
| Kits (instruments, outils, papier, dégâts) | 60 | 45 |
| Extérieurs & zones | 70 | 90 |
| Personnages & cosmétiques | 90 (dont 50 petits) | 80 |
| **Total 1.0** | **\~380 meshes** | **\~405 jours-artiste** |

Soit \~18 mois pour 1 artiste 3D, **\~9 mois pour 2** — cohérent avec le plan de production (M+12 beta) si le kit instruments et la structure modulaire sont faits en premier et si les P3 extérieurs sont lissés sur l'alpha.

**Ordre d'attaque recommandé**

1. Kit structure \+ kit instruments \+ sas de cloison (tout le proto en dépend)  
2. Tableau RK-1, SCRAM, tableau électrique, pompes, Manuel (le cœur du jeu)  
3. Le reste des compartiments dans l'ordre 2 → 5 → 6 → 3 → 1  
4. Base du fjord \+ terrain (vertical slice)  
5. Personnage \+ scaphandre  
6. Extérieurs P3, Entente, cosmétiques (en continu jusqu'à la beta)

