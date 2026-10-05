# Actions « Deux Joueurs » — liste et état (E4-01 / D-08)

> **Statut : proposition à valider.** Elle consolide le GDD §3.4 (« aucune action critique ne peut être exécutée par un joueur seul ») et ce que le jeu implémente déjà. Rien ici ne change le GDD : les cases « à décider » attendent ton arbitrage avant d'être reportées en annexe du GDD.

## Le mécanisme (implémenté : E4-02, mesuré : E4-06)

Une action critique = **deux commandes éloignées** (clés, manivelles, bouton + pédale) activées par **deux joueurs différents** dans une fenêtre de **3 s**, validée par le serveur avec **0,3 s de tolérance de latence** (le banc de latence montre 100 % de réussite à 3,0 s entre 150 et 250 ms de ping avec 2 % de perte). Un même joueur ne peut jamais tenir les deux commandes. Les actions sont des données (`COUPLED_ACTIONS`, `server/src/coupled.js`) ; l'effet est enregistré par le système concerné (`COUPLED_EFFECTS`).

Types de commandes : `press` (une pression arme la commande) existe ; `hold` (manivelle, pédale : à tenir) sera ajouté avec la première action qui en a besoin.

## Les actions

| # | Action | Commande A | Commande B | Si c'est raté / hors fenêtre | État |
|---|---|---|---|---|---|
| 1 | **Redémarrage du réacteur** après un SCRAM | près du tableau RK-1 (compartiment 4) | à la proue (compartiment 1), 13 m plus loin | rien ne se passe (la première pression expire après 3,3 s) ; refusée tant que levier / vannes / pompes ne sont pas prêts (`refused`) | **fait** (E3-05) |
| 2 | **Porter la fiole de combustible** (40 kg) | un porteur, deux mains | un second porteur, deux mains | la fiole reste au sol (le premier porteur la lâche après 3 s) | **fait** (E2-03/E2-04, avec les objets) |
| 3 | **Arrêt contrôlé du réacteur** (GDD : « démarrage/arrêt réacteur ») | à définir | à définir | à définir | **à décider** : le sélecteur de régime est à un joueur ; faut-il une action à deux pour passer le réacteur à l'arrêt complet, distincte du SCRAM ? |
| 4 | **Ouverture des vannes principales de ballast** | à définir (poste central) | à définir (machines) | le bateau ne plonge pas | à faire (E6-08) |
| 5 | **Pose du bateau sur le fond** | idem 4 | idem 4 | échouage / dégâts | à faire (E6-08) |
| 6 | **Tir de leurres** | à définir (poste central) | à définir (torpilles) | leurre non tiré, bruit | à faire (poursuites, E9) |
| 7 | **Ouverture du sas extérieur** | à définir (sas, compartiment 6) | à définir (poste central) | sas fermé | à faire (E10/E7) |
| 8 | **Purge du circuit primaire** | à définir (compartiment 4) | à définir (compartiment 5) | la pression reste, danger de zone chaude | à faire (E7) |
| 9 | **Casque de scaphandre** (mise en place) | à définir | à définir | sortie impossible | à décider (E4-01 le cite, le GDD ne le détaille pas) |
| 10 | **Envoi du Signal** (acte III, sommet du jeu) | à définir | à définir | compte à rebours à deux clés raté (clip de 40 s du GDD) | à faire (acte III) |

## Exceptions volontaires (une seule personne suffit)

- **SCRAM** : levier rouge sous capot plombé, « accessible à n'importe qui, n'importe quand, sans vote » (GDD §3.3). Deux gestes (soulever le capot, tirer), un joueur. **Fait** (E3-04).
- Choisir un régime, rouvrir les vannes, mettre en marche ou arrêter une pompe, manœuvrer un disjoncteur, le télégraphe machine : opérations courantes, un joueur.

## Règles de conception retenues

1. **Éloignées** : les deux commandes sont dans des compartiments différents, jamais visibles l'une de l'autre (« sans se voir »).
2. **Fenêtre unique** : 3 s partout (affichée), 0,3 s de tolérance serveur ; le mode duo (E4-05) élargira les minuteries sans toucher à ces valeurs.
3. **Retour diégétique** (E4-03) : voyants (déjà : orange en attente, vert au succès, rouge à l'échec) et sons ; jamais de HUD. Les sons attendent le choix audio (E12-01).
4. **Pas de partie en solo** (E4-04) et **reconnexion** : l'état en cours (qui a armé quoi, temps restant) est dans la diffusion `cp`.

## Questions pour toi (D-08)

1. Veux-tu une action à deux pour **l'arrêt contrôlé** (ligne 3), ou le sélecteur de régime à un joueur suffit-il ?
2. Le **casque de scaphandre** (ligne 9) est-il bien une action à deux, ou un simple objet à une main ?
3. Valides-tu de ranger **toute action à deux commandes éloignées** dans cette liste unique, avec une ligne par action, avant de la reporter en annexe du GDD ?
