# CLAUDE.md — Sous Pression / Dernière Patrouille

Co-op 2–4 joueurs, première personne, parodie de sous-marin nucléaire (Steam, < 10 €). Le titre n'est pas fixé (issue D-01) ; le repo s'appelle `SousTension`.

## Sources de vérité

- `GDD_Sous_Pression.md` — design (v0.3). En cas de doute de design, il prime ; s'il est ambigu ou contradictoire, **demande** plutôt que d'inventer.
- `BACKLOG.md` — backlog complet. Chaque tâche est aussi une issue GitHub `[Exx-yy]` ; les numéros d'issue suivent l'ordre du backlog (#1 = D-01 … #179 = E18-04).
- `moodboard/` — direction artistique (`v2_references/` est la version courante).
- `tools/create_issues.js` — génère les issues depuis `BACKLOG.md`. Ne le relance pas sans vérifier `tools/issues_created.log` (il saute les issues déjà créées).

## Workflow d'une issue

**Une issue n'est jamais démarrée telle quelle : elle doit d'abord être affinée.** Les issues actuelles sont des lignes de backlog, pas des spécifications.

1. **Lire** l'issue, la section GDD qu'elle cite, ses dépendances (`#n`) et le code existant concerné.
2. **Affiner avant de coder** : rédige (dans un commentaire de l'issue ou en le proposant à l'utilisateur) :
   - le périmètre exact et ce qui est **hors périmètre** ;
   - les **critères d'acceptation** vérifiables (testables ou observables en jeu) ;
   - les décisions de design/technique à trancher, et les questions ouvertes (D-xx liées) ;
   - les dépendances bloquantes (issues non terminées) et le plan de découpage si la taille est `L`/`XL` ;
   - l'approche technique et les tests prévus (simulation → tests EditMode déterministes).
3. **Faire valider** l'issue affinée par l'utilisateur. Sans validation, ne commence pas l'implémentation. Une issue dont les dépendances bloquantes ne sont pas faites, ou qui dépend d'une décision ouverte, reste en attente : signale-le au lieu de contourner.
4. **Implémenter** sur une branche dédiée (`feature/<id>-court-titre`, ex. `feature/E3-02-reactor-sim`), par petits commits référençant l'issue (`Refs #n`).
5. **Vérifier** les critères d'acceptation (tests, lancement) et le respect des règles d'architecture et de design de ce fichier.
6. **PR** avec `Closes #n` et les critères d'acceptation cochés. Les issues `XL` sont découpées en sous-issues avant de démarrer.

## Moteur et statut

- **Moteur : Unity** (D-02 tranché). Langage C#.
- **Unity 6000.3.25f1** (URP). **Backend et réseau de gameplay : Nakama** (décision utilisateur : « Nakama pour tout ») — comptes, salons, persistance, **et** matches autoritatifs en temps réel (serveur dans `server/`, JavaScript, 10 Hz ; SDK `nakama-unity` v3.10.1). NGO et Fish-Net sont écartés. Le spike E1-01 (#16) valide ce choix pour le référentiel mobile ; sa conclusion peut le remettre en cause.
- **Encore à décider** (ne choisis pas à la place de l'utilisateur) : audio (FMOD ou Wwise), voix (Vivox ou Dissonance, à confronter à Nakama).
- **Pressenti, à confirmer avant d'installer :** URP, Input System, package Localization, Steamworks.NET.

## Règles d'architecture (issues du GDD §8 — non négociables)

1. **Simulation à pas fixe, 10 Hz, déterministe.** Réacteur, électricité, eau, bruit = logique pure, indépendante du framerate et du rendu. Pas de `Time.deltaTime`, `Random` non seedé ni ordre d'itération non déterministe dans la simulation.
2. **Simulation séparée du moteur.** Le modèle du réacteur (barres → chaleur → vapeur → turbine → électricité ; refroidissement alimenté par l'électricité produite) vit dans du code C# pur, sans `MonoBehaviour`, testable en headless (E3-10).
3. **Autorité serveur (match autoritatif Nakama)** sur bateau, eau, réacteur. Les clients envoient des entrées à cadence fixe (10 Hz) et prédisent seulement leur propre déplacement, avec réconciliation sur l'état serveur. Toute règle de simulation existe en double (serveur JS, client C#) : garde-les identiques et couvre-les par des tests des deux côtés.
4. **Référentiel mobile** : les joueurs marchent dans un bateau qui bouge et s'incline. Tout code de mouvement/physique/réseau doit en tenir compte (risque n°1).
5. **Règle des Deux Joueurs** : une action critique = deux commandes éloignées activées dans une fenêtre de 3 s, validée par le serveur avec tolérance à la latence. Jamais d'action critique exécutable seul. Pas de partie en solo (minimum 2 joueurs).
6. **Reconnexion en cours de partie** obligatoire : tout état de partie doit pouvoir être resynchronisé.
7. **Données dans des assets de données** (ScriptableObject ou équivalent), pas codées en dur : régimes, alarmes (~40), procédures, missions, notes de patrouille, cargo.
8. **Persistance** : les dégâts du bateau persistent d'une run à l'autre ; la dose de radiation et l'état du joueur sont réinitialisés entre les runs (D-06 à confirmer).

## Serveur Nakama (`server/`)

- **Les sources sont dans `server/src/*.js`** ; `server/modules/index.js` est **généré** (Nakama exige un seul fichier). Après toute modification : `node server/build.js`, puis commiter les deux. Le CI échoue si `index.js` est périmé (`node server/build.js --check`).
- Tests : `node --test server/test/match.test.js server/test/reactor.test.js server/test/water.test.js server/test/leaks.test.js` (sans Nakama), `node server/test/e2e.js` (Nakama lancé : `docker compose -f server/docker-compose.yml up -d`).
- Le **réacteur est simulé uniquement côté serveur** (`server/src/reactor.js`, déterministe, graine ; conception : `docs/design/reactor-dependency-tree.md`). Le client affiche les jauges de `rx`, il ne simule ni ne prédit le réacteur.
- Fins de ligne : sous Windows, git peut convertir en CRLF ; les scripts de build normalisent en LF.

## Architecture client : MVCS (Model · View · Controller · Service)

Chaque fonctionnalité est un module organisé en quatre couches, avec une dépendance **à sens unique**.

| Couche | Rôle | Contient | Interdit |
|---|---|---|---|
| **Model** | État et règles du domaine | classes C# pures (POCO), propriétés observables, événements C# ; le code de simulation vit dans `Sim` | `UnityEngine`, E/S, réseau, connaissance du Controller ou de la View |
| **View** | Affichage et capture d'entrées | `MonoBehaviour` passifs : lisent un Model (ou un état) et se mettent à jour ; exposent des **événements** pour les entrées | logique de jeu, appel direct à un Service, état de jeu propre |
| **Controller** | Orchestration | classes C# pures (avec `ITickable`/`IDisposable`) qui relient Models, Services et Views ; s'abonnent/se désabonnent proprement | accès direct à l'API d'une bibliothèque externe (Nakama, Steam…) |
| **Service** | Accès au monde extérieur | **interface d'abord** (`INetworkService`, `IClockService`, `IInputService`, `ISaveService`…) + implémentation (Nakama, Steam, fichiers, audio) | dépendre d'une View ou d'un Controller |

**Sens des dépendances** : `View → (événements) → Controller → Model` et `Controller → Service (interface)`. Les Models ne connaissent personne ; les Services ne connaissent que les Models (types de données).

**Règles d'implémentation**
- **Composition root unique par scène** : un `MonoBehaviour` de bootstrap construit services → models → controllers → views par **injection par constructeur** (pas de singleton, pas de `static` mutable, pas de `FindObjectOfType`). Aucun conteneur DI tiers sans accord (VContainer est le candidat si le câblage manuel devient pénible).
- **Les Controllers ne sont pas des `MonoBehaviour`** (sauf adaptateur de cycle de vie très fin) : ils sont testables en EditMode avec de faux Services.
- **Communication** : événements C# (`event Action<T>`) ou petits objets de commande ; pas de messages chaînés par chaînes de caractères.
- **Asynchronisme** : `Awaitable` (Unity 6) ou `Task` ; jamais de bloquant sur le thread principal ; annulation via `CancellationToken` liée à la durée de vie du Controller.
- **Données** : configuration en `ScriptableObject` injectée dans le composition root, jamais lue directement par un Model.
- **Assemblies** : `Sim` ← `Gameplay` (Models/Controllers/Services interfaces) ← `Net`/`Views`/`Bootstrap`. Les implémentations de Services tiers (Nakama) sont isolées dans leur propre assembly pour que `Gameplay` reste testable sans SDK.
- **Organisation par fonctionnalité** : `Features/<Nom>/{Models,Views,Controllers,Services}/` (ou l'équivalent dans l'arborescence `_Spikes` pour un spike).
- **Tests** : Models et Controllers en EditMode avec des Services factices ; Views seulement en PlayMode si nécessaire.

## Règles de design à respecter dans tout le code et le contenu

- **Zéro HUD.** Toute information passe par un instrument diégétique (cadran, aiguille, papier, dosimètre). Seuls éléments d'écran : sous-titres et nom du joueur qui parle. Ne crée jamais de barre de vie, minimap ou texte flottant.
- **Une main = une chose.** Deux mains + une poche de poitrine (un petit objet). Un objet à deux mains verrouille les autres actions.
- **Pas de monstres, pas de mort violente.** La « mort » est un évanouissement théâtral. La radiation n'est jamais létale.
- **Trois interdits de ton** : aucune blague écrite (l'humour vient des systèmes et de la panique), aucune horreur, aucun cynisme. Les textes du Commandant sont solennels, déconnectés de la réalité.
- **Rouge réservé aux urgences réelles** (UI, instruments, éclairage).
- **Jouable sans micro** : tout besoin de communication doit avoir un équivalent (roue d'émotes).
- Communication : **chat de proximité + interphones** uniquement.
- Le Manuel OK-114 : procédures ≤ 5 étapes, illustrées ; il s'ouvre à la bonne page sur alarme (pas de mini-jeu d'index).

## Conventions de code (Unity / C#)

- Code, identifiants et commentaires de code en **anglais** ; textes de jeu, GDD, backlog, issues et commits en **français** sont acceptés.
- Tout texte joueur passe par la **localisation** (9 langues cibles) — jamais de chaîne en dur dans le code ou les scènes.
- Préfère composition + événements à l'héritage profond ; un système = un module testable.
- Découpe en **assembly definitions** (`.asmdef`) : au minimum `Sim` (simulation pure, sans référence `UnityEngine`), `Gameplay`, `Net`, `Tests.EditMode` / `Tests.PlayMode`. La simulation ne dépend de rien d'autre.
- Données en `ScriptableObject` ; pas de singletons statiques mutables ; pas de `Find*`/`GetComponent` dans `Update`.
- Fichiers `.meta` toujours commités avec leurs assets ; ne les édite pas à la main. Évite de modifier scènes et prefabs en YAML brut : passe par le code ou l'éditeur (demande à l'utilisateur si une modification d'éditeur est nécessaire).
- Tests : Unity Test Framework (EditMode pour la simulation, PlayMode pour le gameplay).
- Écris des **tests** pour la simulation (réacteur, électricité, eau, bruit, Deux Joueurs, notation). Un changement de formule d'équilibrage sans test déterministe est incomplet.
- Pas de logique de gameplay dans les scènes ou prefabs : garde-les configurables par des données.
- Évite les allocations dans les boucles chaudes (tick de simulation, réplication).
- N'ajoute pas de dépendance ni de package sans le dire.

## Assets 3D (pour les tâches 🎨)

- Style : **réalisme pataud** — gros boutons/leviers, proportions légèrement cartoon, matériaux crédibles (laiton, bakélite, émail écaillé). Palette : vert bouteille, crème administratif, rouille, **un seul rouge**.
- Construction **modulaire** : kit structure + kit instruments d'abord (~15 meshes de base → des centaines d'instances), un objet interactif 🔧 = 2 à 4 sous-meshes (corps, levier, aiguille, capot) avec pivots animables et colliders précis. Objets transportables 📦 : masse définie.
- Priorités P1/P2/P3 et ordre d'attaque : annexe d'assets du GDD.

## Commandes

Le projet Unity n'existe pas encore dans le repo. À remplir à sa création (chemin de l'éditeur, version, build, tests, multijoueur local). Tests en ligne de commande une fois le projet créé :

```
Unity -batchmode -projectPath <chemin> -runTests -testPlatform EditMode -testResults results.xml
```

Ne suppose pas d'autres commandes qui n'existent pas encore.

## Git et GitHub

- Dépôt : `leo-brgn/SousTension` (public). Labels : `role:game-programmer`, `role:3d-designer`, `type:*`, `size:*`, `epic:NN`, `decision`, `asset`. Jalons : PROTO, VS, ALPHA, BETA, GOLD, POST.
- Commits courts et ciblés ; ne commite ni binaires volumineux hors Git LFS, ni `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build/`.
- Ne crée pas, ne ferme pas et ne modifie pas d'issues en masse sans demande explicite.

## Quand demander à l'utilisateur

Demande avant de trancher : la version d'Unity, le netcode, l'audio, le titre (D-01), l'Acte IV (D-04), les règles du mode duo (D-10), tout changement du GDD, toute dépendance externe payante, et toute commande qui télécharge ou exécute du code d'une source tierce.
