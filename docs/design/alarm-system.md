# Dérives et alarmes (E3-09, #44) — affinage proposé

> **Statut : proposition à valider** (workflow CLAUDE.md : une issue XL est affinée et découpée avant d'être codée). Aucune ligne de code n'est écrite. Sources : GDD §3.2 (boucle micro), §3.3 (réacteur), §3.5 (Manuel qui s'ouvre à la bonne page), `docs/design/reactor-dependency-tree.md`.

## 1. Ce qui existe déjà
- Dérive du réacteur : fermeture aléatoire des vannes du primaire (`reactor.js`, cadence ∝ R²), pompe cassée, fuite du primaire, SCRAM automatique à T critique.
- Autres systèmes qui produisent déjà un état « anormal » : tension du bus (`power.js`), disjoncteurs qui sautent, pompes de cale, fuites de coque et eau (`leaks.js`, `water.js`), batterie de secours, télégraphe / bruit.
- **Il n'y a pas de notion d'alarme** : chaque vue montre sa jauge, rien ne dit « ceci est une alarme », ne la numérote, ne la lie à une page du Manuel.

## 2. Périmètre proposé
**Dans le périmètre de E3-09** : un **moteur d'alarmes** côté serveur, **piloté par des données**, et le catalogue des ~40 alarmes ; le **générateur de pannes** avec budget d'attention ; la **correction excessive**.
**Hors périmètre** : le son des alarmes (E12-03, décision audio ouverte), l'ouverture automatique du Manuel (E5-04, dépend de E3-09), la rédaction des procédures (E5-05), les alarmes des systèmes qui n'existent pas encore (air/CO₂ E6-06, radiation E7, sonar E8, scaphandre E9-15).

## 3. Conception
**Une alarme = une donnée** (`server/src/alarms.js`, tableau `ALARMS`, aucune logique en dur dans la boucle) :
`{ id, system, severity: "info|warn|crit", when(state) → bool, clearWhen(state) (hystérésis), page: "<id de page du Manuel>", cause: "<id système>", compartment }`.
Le moteur évalue le catalogue à chaque tick dans `simStep` (nouvelle étape « alarmes », après réacteur/électricité/propulsion), tient pour chaque alarme `{ on, sinceTick, ackBy }`, et diffuse la liste des alarmes actives (`al: [id, ...]`) dans l'état, donc **resynchronisée à la reconnexion** (règle 6). Déterministe (règle 1), aucun aléa dans le moteur.
**Diégétique** (zéro HUD) : un **annonciateur** (panneau de lampes `LampDome`, une lampe + une plaque par alarme, rouge réservé aux alarmes `crit`) dans le compartiment du système concerné, plus un panneau central ; l'alarme sonne (E12) et le Manuel s'ouvre à `page` (E5-04). Acquitter = appuyer sur la lampe (un joueur, une main) : coupe le son, la lampe reste allumée tant que la cause dure.
**Dérive en chaîne** : une dérive (ex. vanne qui se ferme) ne crée pas l'alarme directement ; elle change l'état, l'alarme se déclenche quand la condition est vraie, avec le retard déjà modélisé par la chaîne.
**Générateur de pannes avec budget d'attention** : à chaque instant le nombre de pannes actives « non traitées » est borné par `budget = nb_joueurs + marge` (1 en début de patrouille, croissant avec le temps) ; quand il y a de la place le générateur (graine du match) choisit une panne parmi celles dont les préconditions tiennent et qui ne sont pas déjà actives, en privilégiant un système **différent** du dernier. « Plus de systèmes que de joueurs » = le budget dépasse le nombre de joueurs.
**Correction excessive** : quand un joueur corrige une dérive de plus que nécessaire (ex. vannes ouvertes à fond alors que 0,6 suffisait, régime poussé), la correction produit elle-même une alarme d'un autre système (surdébit → température basse → tension qui monte → disjoncteur). Représenté par des alarmes « haut » symétriques des alarmes « bas » dans le catalogue, pas par un mécanisme séparé.

## 4. Catalogue de départ (≈ 40, par système ; ✔ = condition déjà calculable aujourd'hui)
| Système | Alarmes (id) |
|---|---|
| Réacteur (8) | `rx_temp_warn` ✔, `rx_temp_crit` ✔, `rx_temp_low`, `rx_valve_low` ✔ (vanne < 0,5), `rx_flow_low` ✔, `rx_pump_broken` ✔ ×2, `rx_scram_auto` ✔ |
| Électricité (8) | `pw_v_low` ✔, `pw_v_dark` ✔, `pw_battery_low` ✔, `pw_breaker_tripped` ✔, `pw_overload`, `pw_essential_short`, `pw_emergency_bus` ✔, `pw_surplus_high` |
| Eau / coque (8) | `wt_leak_open` ✔ (par compartiment), `wt_level_high` ✔, `wt_flood` ✔, `wt_trim_high` ✔, `wt_list_high` ✔, `wt_hatch_open_flooded`, `wt_bilge_off_flooded`, `wt_bilge_broken` ✔ |
| Propulsion (4) | `pr_telegraph_mismatch`, `pr_speed_low`, `pr_noise_high` ✔, `pr_depth_drift` ✔ |
| Divers (≈12) | à écrire avec les systèmes à venir : air, CO₂, ventilation, radio, pneumatique, samovar… |
Les ✔ permettent un **premier tranche livrable sans nouveau système** ; le reste suit les épics concernés.

## 5. Découpage proposé (sous-issues, XL → S/M)
1. **E3-09a (M)** — moteur d'alarmes serveur + format de données + diffusion + resync + tests déterministes (empreinte) ; catalogue réacteur ✔ (8).
2. **E3-09b (M)** — alarmes électricité et eau/coque ✔ (≈ 12) ; test « chaque alarme se déclenche et se lève sur un scénario scripté » (outil `tools/balance.js`).
3. **E3-09c (M)** — annonciateur client : lampes diégétiques, plaques, acquittement par clic aimé (`use: "alarm:<id>"`), tests EditMode.
4. **E3-09d (L)** — générateur de pannes + budget d'attention (+ réglage dans l'outil d'équilibrage : durée moyenne avant saturation).
5. **E3-09e (M)** — correction excessive : alarmes « haut » + scénarios d'équilibrage.
6. **E3-09f (M)** — le reste du catalogue, au fil des systèmes (E6-06, E8-xx…).

## 6. Critères d'acceptation (E3-09a, la première tranche)
- [ ] Le catalogue est une donnée ; ajouter une alarme n'ajoute aucune ligne de logique.
- [ ] Une alarme s'active quand sa condition est vraie, se lève avec hystérésis, jamais de clignotement sur un seuil.
- [ ] L'état diffusé contient les alarmes actives ; un client qui se reconnecte les retrouve.
- [ ] Déterminisme : l'empreinte dorée est régénérée, le test 5 minutes reste identique d'une exécution à l'autre.
- [ ] `tools/balance.js` affiche, par scénario, les alarmes déclenchées et leur ordre.

## 7. Questions ouvertes (à trancher avant de coder)
1. **Combien d'alarmes distinctes par système et par compartiment ?** (une alarme par compartiment pour l'eau = 6 lampes, ou une alarme « eau » + la lampe du compartiment ?)
2. **Acquittement** : utile (coupe le son) ou superflu (le son s'arrête tout seul) ? Proposition : un clic, une main, sans conséquence de jeu.
3. **Rouge** : uniquement les alarmes `crit` (règle de design « rouge réservé aux urgences réelles ») ; les `warn` en ambre. D'accord ?
4. **Budget d'attention** : départ à `nb_joueurs` pannes simultanées max et montée progressive — ou fixe par régime ?
5. **Dépendances** : E5-04 (Manuel auto) et E12-03 (sons) attendent cette issue ; D-09 (arbre réacteur) est tranché dans le code.
