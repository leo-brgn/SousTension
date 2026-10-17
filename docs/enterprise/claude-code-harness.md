# Harness d'entreprise Claude Code — Intune, configuration verrouillée, Dev Containers sous Podman

Statut : brouillon technique v0.1. Les commandes et noms de clés marqués **[À VALIDER]** n'ont pas été vérifiés sur la version exacte déployée ; valider sur un poste pilote avant toute généralisation.

## 1. Objectif et périmètre

Déployer sur les postes Windows 11 d'une organisation un « harness » Claude Code qui garantit :

1. une **configuration imposée et non modifiable par l'utilisateur** (permissions, hooks, variables d'environnement, serveurs MCP) ;
2. un **environnement de développement conteneurisé automatique** (Dev Containers) fonctionnant avec **Podman** (pas de Docker Desktop) ;
3. un **déploiement, une mise à jour et une remédiation centralisés** via **Microsoft Intune**.

Le document est écrit d'abord pour Claude Code (sections 5 à 16) puis étendu au **multi-fournisseur** (Claude Code, Gemini CLI, Codex CLI) : voir la section 17, qui précise ce qui est commun et ce qui est propre à chaque outil.

Hors périmètre : authentification des fournisseurs, SSO, proxy d'entreprise (section 12), postes macOS/Linux.

## 2. Décision d'architecture : pourquoi pas MSIX

L'idée initiale était un paquet MSIX portant la configuration. Ce n'est pas adapté :

| Contrainte MSIX | Conséquence |
|---|---|
| Installation dans `C:\Program Files\WindowsApps\<package>`, en lecture seule | Impossible d'écrire dans `C:\Program Files\ClaudeCode\`, l'emplacement lu par Claude Code. |
| Virtualisation du système de fichiers et du registre | Les fichiers/clés « écrits » par le paquet ne sont visibles que par les processus du paquet, pas par un `claude` lancé depuis un terminal ou VS Code. |
| Contournement par service empaqueté (`desktop6:Service`, LocalSystem) | Faisable mais lourd, fragile, difficile à diagnostiquer. |

**Choix retenu : application Win32 Intune (`.intunewin`)** exécutée en contexte SYSTEM. Elle peut écrire dans `Program Files` et dans HKLM, et Intune fournit détection, réparation et mise à jour.

Si MSIX devient une obligation de gouvernance, voir l'annexe B.

## 3. Vue d'ensemble

```
Intune
 ├─ App Win32 « ClaudeCode-Harness »   (SYSTEM)
 │    ├─ C:\Program Files\ClaudeCode\managed-settings.json   (ACL : Admins=Full, Users=Read)
 │    ├─ C:\Program Files\ClaudeCode\managed-mcp.json        (optionnel)
 │    └─ C:\Program Files\ClaudeCode\version.txt             (détection)
 ├─ App Win32 « Podman »                (SYSTEM, MSI officiel)
 ├─ Script de plateforme utilisateur   (contexte USER)
 │    ├─ podman machine init/start
 │    ├─ extensions VS Code
 │    └─ réglages VS Code (dockerPath = podman)
 └─ Configuration WSL2 (fonctionnalité Windows optionnelle)

Poste développeur
 VS Code ──(Dev Containers, dockerPath=podman)──► Podman machine (WSL2)
                                                   └─ conteneur
                                                        ├─ /etc/claude-code  ← bind-mount RO de la config hôte
                                                        └─ claude (feature devcontainer)
```

## 4. Prérequis

- Windows 11 (22H2 ou ultérieur), virtualisation activée dans le BIOS/UEFI.
- Fonctionnalités Windows : `Microsoft-Windows-Subsystem-Linux` et `VirtualMachinePlatform` activées (profil Intune ou script).
- Intune : droits de création d'applications Win32 et de scripts de plateforme.
- Outil de packaging : `IntuneWinAppUtil.exe` (Microsoft Win32 Content Prep Tool).
- Podman for Windows (MSI) — version à figer dans le catalogue d'applications.
- VS Code (installation machine) ; accès aux registres d'images utilisés (section 9).

## 5. Configuration verrouillée

### 5.1 Emplacements lus par Claude Code

| OS / contexte | Chemin |
|---|---|
| Windows | `C:\Program Files\ClaudeCode\managed-settings.json` |
| Linux / conteneur | `/etc/claude-code/managed-settings.json` |
| Serveurs MCP imposés | `managed-mcp.json` dans le même dossier |

Ces fichiers ont la précédence la plus élevée : les réglages utilisateur et projet ne peuvent pas les contredire. Une variante par registre (clé de stratégie sous `HKLM\SOFTWARE\Policies\ClaudeCode`) existe pour les déploiements MDM **[À VALIDER : nom exact de la clé et du format de valeur dans la documentation de la version déployée]**. Le fichier JSON reste la référence de cette documentation.

### 5.2 Exemple `managed-settings.json`

```json
{
  "permissions": {
    "defaultMode": "default",
    "deny": [
      "Read(./.env)",
      "Read(./secrets/**)",
      "Bash(curl:*)",
      "Bash(wget:*)"
    ]
  },
  "env": {
    "DISABLE_TELEMETRY": "1"
  },
  "allowManagedHooksOnly": true,
  "allowManagedPermissionRulesOnly": true
}
```

**[À VALIDER]** : les noms de clés (`allowManagedHooksOnly`, `allowManagedPermissionRulesOnly`, …) doivent être confrontés à la documentation de la version de Claude Code déployée. Utiliser l'agent `claude-code-guide` ou la documentation officielle.

### 5.3 Ce qui est — et n'est pas — « non modifiable »

- **Ce n'est pas une garantie de sécurité** : voir la section 13 (modèle de menace) avant de s'appuyer sur ce verrouillage.
- Un utilisateur **standard** ne peut ni modifier ni supprimer le fichier (ACL, section 6.2).
- Un **administrateur local** le peut. Intune restaure la configuration à la prochaine évaluation de la règle de détection (section 6.3).
- Dans le conteneur, la configuration est montée en **lecture seule** (section 9) ; le conteneur ne peut pas la modifier, mais un utilisateur qui lance un conteneur sans le montage s'en affranchit : voir le contrôle compensatoire section 11.

## 6. Application Win32 Intune « ClaudeCode-Harness »

### 6.1 Arborescence source

```
ClaudeCode-Harness/
├─ install.ps1
├─ uninstall.ps1
├─ detect.ps1
├─ managed-settings.json
├─ managed-mcp.json            (optionnel)
├─ hooks\audit.ps1             (traçabilité, section 16)
├─ container\                  (config + hooks bash du conteneur, section 16.6)
└─ version.txt                 (ex. 1.0.0)
```

### 6.2 `install.ps1`

```powershell
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:ProgramFiles 'ClaudeCode'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

foreach ($f in 'managed-settings.json', 'managed-mcp.json', 'version.txt') {
    $src = Join-Path $PSScriptRoot $f
    if (Test-Path $src) { Copy-Item $src $dir -Force }
}
foreach ($d in 'hooks', 'container') {
    $src = Join-Path $PSScriptRoot $d
    if (Test-Path $src) { Copy-Item $src $dir -Recurse -Force }
}

# Écriture réservée à SYSTEM et aux Administrateurs ; lecture/exécution pour les Utilisateurs
icacls $dir /inheritance:r `
    /grant:r '*S-1-5-18:(OI)(CI)F' `
             '*S-1-5-32-544:(OI)(CI)F' `
             '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
exit 0
```

Les SID (`S-1-5-18` SYSTEM, `S-1-5-32-544` Administrateurs, `S-1-5-32-545` Utilisateurs) évitent les problèmes de localisation des noms de groupes.

### 6.3 `detect.ps1` (règle de détection personnalisée)

```powershell
$dir  = Join-Path $env:ProgramFiles 'ClaudeCode'
$cfg  = Join-Path $dir 'managed-settings.json'
$ver  = Join-Path $dir 'version.txt'
$expectedVersion = '1.0.0'
$expectedHash    = '<SHA256 de managed-settings.json>'   # régénéré à chaque release

if (-not (Test-Path $cfg) -or -not (Test-Path $ver)) { exit 1 }
if ((Get-Content $ver -Raw).Trim() -ne $expectedVersion) { exit 1 }
if ((Get-FileHash $cfg -Algorithm SHA256).Hash -ne $expectedHash) { exit 1 }
Write-Output 'OK'
exit 0
```

Sortie `0` avec texte sur stdout = installé. Une dérive (fichier modifié ou supprimé) fait échouer la détection ; Intune réinstalle selon son cycle d'évaluation (non instantané : compter plusieurs heures).

### 6.4 `uninstall.ps1`

```powershell
Remove-Item -Recurse -Force (Join-Path $env:ProgramFiles 'ClaudeCode') -ErrorAction SilentlyContinue
exit 0
```

### 6.5 Packaging et paramètres Intune

```powershell
IntuneWinAppUtil.exe -c .\ClaudeCode-Harness -s install.ps1 -o .\out
```

| Paramètre | Valeur |
|---|---|
| Commande d'installation | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1` |
| Commande de désinstallation | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File uninstall.ps1` |
| Comportement d'installation | Système |
| Redémarrage | Aucune action spécifique |
| Règle de détection | Script personnalisé `detect.ps1`, **exécuté en 64 bits**, sans contexte de signature obligatoire |
| Attribution | Groupe d'appareils « Postes développeurs » (obligatoire) |

## 7. Podman

### 7.1 Installation

Second package Win32 (MSI officiel Podman for Windows), installation silencieuse en SYSTEM. L'attribuer comme **dépendance** de l'application « ClaudeCode-Harness » si l'on veut garantir l'ordre.

### 7.2 Machine Podman (contexte utilisateur)

La machine Podman est **par utilisateur**. À déployer par un **script de plateforme Intune en contexte utilisateur**, ou une tâche planifiée à l'ouverture de session :

```powershell
if (-not (podman machine list --format '{{.Name}}' 2>$null)) {
    podman machine init
}
podman machine start
```

Remarques :
- WSL2 doit être actif ; un redémarrage peut être nécessaire après activation des fonctionnalités.
- Le mode **rootless** est le défaut et celui à conserver (surface d'attaque réduite).

## 8. VS Code

### 8.1 Extensions (contexte utilisateur)

```powershell
code --install-extension ms-vscode-remote.remote-containers
code --install-extension anthropic.claude-code
```

Les extensions s'installent dans le profil de l'utilisateur ; d'où le script de plateforme en contexte utilisateur, pas SYSTEM.

### 8.2 Réglages Podman

Dans les réglages utilisateur VS Code (`%APPDATA%\Code\User\settings.json`), à fusionner par script sans écraser l'existant :

```json
{
  "dev.containers.dockerPath": "podman",
  "dev.containers.dockerComposePath": "podman-compose"
}
```

Ce réglage n'est pas verrouillé par Intune : un utilisateur peut le changer, mais il n'a alors plus de conteneur fonctionnel, pas de contournement de la configuration Claude Code (section 5).

## 9. Dev Containers

### 9.1 Fichier standard commité dans chaque dépôt

`.devcontainer/devcontainer.json` :

```json
{
  "name": "corp-dev",
  "image": "mcr.microsoft.com/devcontainers/base:ubuntu",
  "features": {
    "ghcr.io/anthropics/devcontainer-features/claude-code:1": {}
  },
  "remoteUser": "vscode",
  "runArgs": ["--userns=keep-id"],
  "mounts": [
    "source=/mnt/c/Program Files/ClaudeCode/container,target=/etc/claude-code,type=bind,readonly"
  ],
  "customizations": {
    "vscode": { "extensions": ["anthropic.claude-code"] }
  }
}
```

### 9.2 Points techniques

- **Chemin source du montage** : résolu côté machine Podman (WSL), donc `/mnt/c/...`. **[À VALIDER]** : la présence et les options du montage `/mnt/c` dépendent de la configuration de la machine Podman et de ses options d'automount.
- **`--userns=keep-id`** : aligne l'UID du conteneur sur l'utilisateur hôte en mode rootless ; évite les erreurs de droits sur les fichiers montés.
- **Lecture seule** (`readonly`) : le conteneur ne peut pas altérer la config.
- **Ouverture automatique** : VS Code propose « Reopen in Container » dès la présence de `.devcontainer/`. Pour forcer l'ouverture, une tâche `runOn: folderOpen` dans `.vscode/tasks.json` peut appeler la commande de réouverture ; comportement à tester.

### 9.3 Solution de repli (si le montage est impraticable)

Construire une **image d'entreprise** contenant `/etc/claude-code/managed-settings.json`, publiée dans le registre interne, et la référencer dans `devcontainer.json`. La configuration est alors immuable dans l'image ; la mise à jour passe par une nouvelle version d'image. Avantage : ne dépend ni des montages Windows ni du contexte utilisateur.

```dockerfile
FROM mcr.microsoft.com/devcontainers/base:ubuntu
COPY managed-settings.json /etc/claude-code/managed-settings.json
RUN chmod 0444 /etc/claude-code/managed-settings.json && chown root:root /etc/claude-code/managed-settings.json
```

## 10. Séquence de déploiement

1. Activer WSL2 / VirtualMachinePlatform (profil de configuration ou script), redémarrer.
2. Pousser l'application **Podman**.
3. Pousser l'application **ClaudeCode-Harness** (dépend de Podman).
4. Pousser le **script utilisateur** : machine Podman, extensions VS Code, réglages.
5. Distribuer les dépôts avec `.devcontainer/` (modèle de dépôt ou PR de masse).
6. Vérifier (section 11).

Déployer d'abord sur un **groupe pilote** (3–5 postes) avant le groupe complet.

## 11. Vérification et contrôle

### Tests d'acceptation

| # | Test | Résultat attendu |
|---|---|---|
| 1 | Utilisateur standard : modifier `C:\Program Files\ClaudeCode\managed-settings.json` | Accès refusé |
| 2 | Administrateur : modifier le fichier, attendre l'évaluation Intune | Fichier restauré |
| 3 | `claude` hôte : demander une action interdite par `deny` | Refusée |
| 4 | Ouvrir un dépôt avec `.devcontainer/` | Proposition d'ouverture en conteneur, build via Podman réussi |
| 5 | Dans le conteneur : `cat /etc/claude-code/managed-settings.json` ; tenter d'y écrire | Lisible, écriture impossible |
| 6 | `claude` dans le conteneur : action interdite par `deny` | Refusée |
| 7 | Désinstaller l'app Intune | Dossier supprimé, comportement par défaut restauré |

### Contrôles compensatoires

- Lancer un conteneur sans le montage contourne la config dans ce conteneur. Atténuations : image d'entreprise (9.3), et politique de revue de `devcontainer.json` (CODEOWNERS) sur les dépôts.
- Rapport Intune : taux de postes conformes à la règle de détection.

## 12. Points ouverts

- Authentification et gestion des clés / SSO.
- Proxy et certificats d'entreprise (CA racine à injecter dans la machine Podman et les conteneurs).
- Registre d'images interne et politique d'images autorisées (`registries.conf` Podman).
- Journalisation et audit des usages.
- Politique de mise à jour de Claude Code lui-même (canal, version figée).

## 13. Modèle de menace et limites

**Principe : la config managed est une couche de réduction de risque, pas une garantie.** Toute garantie forte doit être posée hors de l'outil (réseau, identités, fournisseur).

| Risque | Ce que la config managed apporte | Limite réelle | Contrôle qui garantit |
|---|---|---|---|
| Dev désactive/modifie les règles | Fichier ACL admin, restauré par Intune | Admin local ; latence Intune (heures) | Retirer l'admin local ; attestation de conformité (accès conditionnel) |
| Commande interdite contournée (`curl` → `python -c`) | Règles `deny` par motif | Les motifs ne sont **pas un bac à sable** | Isolation réseau et système de fichiers du conteneur (section 14) |
| `claude` lancé hors conteneur / sans montage | — | Config hôte seule, pas d'isolation | Image d'entreprise (9.3) ; accès fournisseur conditionné à l'appareil conforme |
| Rebond vers la production | Aucun | L'agent atteint ce que le conteneur atteint | Section 14 : pas de secrets, réseau filtré, IAM sans droits prod |
| Injection de prompt (dépôt, page web, ticket) | Permissions limitent l'impact | Ne l'empêchent pas | Moindre privilège ; relecture humaine des actions sensibles ; pas de secrets accessibles |
| Fuite de secrets via l'agent | `deny Read(...)` sur des motifs | Un secret en variable d'environnement reste lisible | Aucun secret durable dans le conteneur ; jetons à courte durée |
| Usage non attribué | Hooks d'audit (section 16) | Contournable hors harness | Identité côté fournisseur (SSO) + corrélation serveur |

Les tests d'acceptation 2 et 3 de la section 11 valident le **comportement attendu**, pas l'absence de contournement : ne pas les présenter comme une preuve de sécurité.

## 14. Exigences d'isolation (anti-rebond vers la production)

À imposer comme **prérequis de mise en service**, par ordre de priorité :

1. **Aucun secret de production sur les postes ni dans les conteneurs** : pas de `kubeconfig`, profil cloud, clé SSH ou jeton de production. Interdiction de monter `~/.ssh`, `~/.aws`, `~/.kube` dans le conteneur (revue de `devcontainer.json` par CODEOWNERS).
2. **Réseau du conteneur en liste d'autorisation** : sortie limitée au registre d'images interne, au dépôt de code, aux endpoints du modèle et au collecteur d'audit, via proxy d'entreprise (podman : réseau dédié + règles pare-feu hôte, ou proxy obligatoire avec `HTTP(S)_PROXY` imposé dans l'image). Les plages de production sont bloquées.
3. **Identités de dev sans droit sur la production** : rôles IAM / groupes Entra distincts ; accès production uniquement par comptes séparés et jetons à courte durée, jamais depuis un poste de dev.
4. **Déploiement en production uniquement par la CI** avec approbation humaine ; la branche protégée refuse les pushes directs.
5. **Mode rootless Podman**, pas de `--privileged`, pas de montage du socket Podman dans le conteneur, `--cap-drop=ALL` par défaut dans `runArgs`.
6. **Règles `deny` en complément** (`kubectl`, `terraform apply`, `aws`, `gcloud`...) : utiles contre l'erreur, inefficaces contre la malveillance.

Critère de recette : depuis le conteneur, une tentative de connexion à une cible de production échoue au niveau réseau **et** au niveau identité.

## 15. Kill switch

Aucun interrupteur unique n'existe ; il faut en combiner plusieurs, du plus rapide au plus lent. **Ces mécanismes sont à tester à blanc** avant la mise en production.

| Niveau | Action | Effet attendu | Délai | Qui |
|---|---|---|---|---|
| 1 — Fournisseur | Révoquer les clés API / désactiver l'organisation ou le SSO du fournisseur de modèle **[À VALIDER selon le plan Anthropic : Teams / Enterprise / API]** | L'agent ne peut plus appeler le modèle, sur tous les postes | Immédiat | Sécurité + admin fournisseur |
| 2 — Réseau | Bloquer les endpoints du modèle au proxy / pare-feu | Idem, même pour un poste déconnecté d'Intune | Minutes | Réseau |
| 3 — Config serveur | Réglages managed côté serveur de la console fournisseur, s'ils sont disponibles pour ton plan **[À VALIDER]** | Désactive des fonctions (ex. modes de contournement des permissions) sans toucher aux postes | Minutes | Admin fournisseur |
| 4 — Intune | Publier une nouvelle version de l'app avec une config durcie (ex. `permissions.deny` large, `disableBypassPermissionsMode`) puis forcer la synchronisation | Durcissement des postes joignables | Heures | Admin Intune |
| 5 — Retrait | Désinstaller l'app « ClaudeCode-Harness » et l'extension VS Code | Retrait de l'outil | Heures | Admin Intune |

Règles :
- Le niveau 1 est **le seul indépendant de l'état des postes** : tout autre niveau suppose que le poste obéit.
- Documenter les **déclencheurs** (compromission suspectée, injection avérée, fuite de secret) et la **personne d'astreinte** habilitée à agir sans validation.
- Prévoir le **retour arrière** (qui rétablit, après quelle vérification).

## 16. Traçabilité : relier une session Claude Code à un ticket Jira

### 16.1 Principe

Chaque session Claude Code a un **`session_id`**. Les hooks le reçoivent en JSON sur l'entrée standard, avec `transcript_path`, `cwd` et le nom de l'événement **[À VALIDER : champs exacts dans la documentation de la version déployée]**. La liaison à un ticket se fait en trois temps :

1. **Déduire le ticket** (clé Jira, ex. `ABC-123`) de la branche git ; à défaut, d'une variable d'environnement `CLAUDE_TICKET`.
2. **Exiger** ce ticket : sans clé valide, la session est bloquée (« pas de ticket, pas de session »).
3. **Émettre un événement d'audit** vers un collecteur interne, qui valide le ticket auprès de Jira et enregistre le lien.

### 16.2 Pourquoi un collecteur et pas Jira directement

Écrire dans Jira exige un jeton d'API. **Ce jeton ne doit jamais se trouver sur le poste ou dans le conteneur** (section 14.1). Le hook poste donc un événement à un **collecteur interne** (service authentifié par l'identité de l'appareil ou de l'utilisateur) qui détient seul le jeton Jira.

```
hook (poste/conteneur) ──HTTPS──► collecteur d'audit ──► Jira (commentaire / lien distant / champ)
                                         └──► stockage d'audit (rétention, recherche)
```

### 16.3 Événement d'audit

```json
{
  "event": "session_start",
  "session_id": "<id fourni par Claude Code>",
  "ticket": "ABC-123",
  "user": "<UPN>",
  "repo": "org/projet",
  "branch": "feature/ABC-123-titre",
  "host": "<nom de machine ou id conteneur>",
  "in_container": true,
  "ts": "2026-10-05T09:12:00Z"
}
```

Événements recommandés : `session_start`, `prompt` (hash ou métadonnées seulement), `session_end`. **Ne pas envoyer le contenu des prompts ou du transcript par défaut** (données personnelles, secrets possibles) ; le stocker en option, avec rétention et base légale définies.

### 16.4 Hooks imposés dans `managed-settings.json`

```json
{
  "allowManagedHooksOnly": true,
  "hooks": {
    "SessionStart": [
      { "hooks": [ { "type": "command",
        "command": "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\ClaudeCode\\hooks\\audit.ps1\" session_start" } ] }
    ],
    "UserPromptSubmit": [
      { "hooks": [ { "type": "command",
        "command": "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\ClaudeCode\\hooks\\audit.ps1\" prompt" } ] }
    ],
    "SessionEnd": [
      { "hooks": [ { "type": "command",
        "command": "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\ClaudeCode\\hooks\\audit.ps1\" session_end" } ] }
    ]
  }
}
```

`allowManagedHooksOnly` empêche les devs d'ajouter ou de retirer des hooks. **[À VALIDER]** : nom de la clé, événements disponibles, et comportement du code de sortie `2` (bloquant) pour `UserPromptSubmit`.

### 16.5 Script `audit.ps1` (hôte)

```powershell
$ErrorActionPreference = 'Stop'
$evt  = $args[0]
$in   = [Console]::In.ReadToEnd() | ConvertFrom-Json
$cwd  = $in.cwd
$br   = (git -C $cwd rev-parse --abbrev-ref HEAD 2>$null)
$tk   = $env:CLAUDE_TICKET
if (-not $tk -and $br -match '([A-Z][A-Z0-9]+-\d+)') { $tk = $Matches[1] }

if (-not $tk -and $evt -in 'session_start','prompt') {
    [Console]::Error.WriteLine("Aucun ticket Jira détecté (branche '$br'). Nommez la branche <CLE-123>-titre ou définissez CLAUDE_TICKET.")
    exit 2   # bloque (UserPromptSubmit) ; à valider pour SessionStart
}

$body = @{
    event = $evt; session_id = $in.session_id; ticket = $tk
    user = $env:USERNAME; branch = $br; host = $env:COMPUTERNAME
    in_container = $false; ts = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri 'https://audit.corp.example/v1/claude-events' `
        -Method Post -Body $body -ContentType 'application/json' `
        -UseDefaultCredentials -TimeoutSec 5 | Out-Null
} catch {
    # Politique à choisir : fail-closed (exit 2) ou fail-open (journaliser localement)
    Add-Content "$env:LOCALAPPDATA\ClaudeAudit\queue.jsonl" $body
}
exit 0
```

Décision à prendre : **fail-closed** (collecteur injoignable → pas de session ; plus fiable, plus gênant) ou **fail-open avec file locale** (rejouée plus tard ; moins fiable). Le choix dépend de la criticité de l'attribution.

### 16.6 Variante conteneur

Le conteneur est sous Linux : la config Windows (`powershell.exe`) n'y fonctionne pas. Prévoir **un second jeu de fichiers** :

```
C:\Program Files\ClaudeCode\
├─ managed-settings.json            (hôte : hooks PowerShell)
└─ container\
   ├─ managed-settings.json         (conteneur : hooks bash)
   └─ hooks\audit.sh
```

Le montage de la section 9.1 pointe alors sur le sous-dossier `container` et la valeur `in_container` passe à `true`. Le principe de `audit.sh` est identique (`jq` pour lire le JSON, `curl` vers le collecteur). Le conteneur n'ayant pas les identifiants Windows, authentifier l'appel par un **jeton court délivré à l'ouverture du conteneur** (hors de ce document : à concevoir avec l'équipe sécurité).

### 16.7 Traitement côté collecteur

1. Valider la clé via l'API Jira (existence, projet autorisé, ticket non clos).
2. Rejeter ou marquer « non conforme » sinon.
3. Enregistrer `session_id ↔ ticket ↔ user` et l'événement.
4. Écrire dans Jira : commentaire ou **lien distant** (`remote link`) vers la fiche d'audit de la session, ou champ personnalisé listant les sessions.
5. À la fin de session, joindre la durée et, si disponible, les métriques d'usage (section 16.8).

### 16.8 Métriques d'usage et coûts

Claude Code peut exporter de la télémétrie OpenTelemetry (activation par variables d'environnement `CLAUDE_CODE_ENABLE_TELEMETRY` et `OTEL_*`, imposables via `env` dans `managed-settings.json`) **[À VALIDER : noms exacts, attributs et métriques disponibles]**. Les événements portent un identifiant de session : la **jointure par `session_id`** avec la table d'audit donne coût et volume par ticket. Les attributs de ressource OTel étant fixés au lancement, ne pas y mettre le ticket : le rattachement se fait côté serveur.

### 16.9 Limites de fiabilité

- Un dev qui lance `claude` **hors harness** n'émet aucun événement. L'attribution fiable repose donc sur le **fournisseur** (identité SSO, clés individuelles) recoupée avec les événements ; une session visible côté fournisseur mais absente de l'audit est une anomalie à investiguer.
- La clé de ticket déduite de la branche est **déclarative** : seul le collecteur la valide contre Jira, et seule une revue humaine confirme qu'elle correspond au travail réel.
- Un `session_id` repris (`--resume`) peut changer de ticket si la branche change : décider si le rattachement initial prime.

## 17. Harness multi-fournisseur (Claude Code, Gemini CLI, Codex CLI, …)

### 17.1 Principe

Les sections 5 à 16 décrivent Claude Code. Pour couvrir plusieurs agents, **ne pas multiplier les harness** : séparer ce qui est **commun** (déploiement, isolation, passerelle, audit, kill switch) de ce qui est **propre à chaque outil** (format de config verrouillée, hooks).

```
 Claude Code ─┐                      ┌─► Anthropic
 Gemini CLI  ─┼─► passerelle LLM ────┼─► Google (Gemini / Vertex)
 Codex CLI   ─┘   d'entreprise       └─► OpenAI
   (conteneur Podman,     │
    réseau filtré)        ├─► audit  (session ↔ ticket ↔ utilisateur ↔ fournisseur)
                          └─► kill switch, quotas, coûts
```

| Couche | Commune ou propre à l'outil |
|---|---|
| Déploiement Intune, Podman, VS Code | Commune |
| Isolation (section 14) | Commune : un seul conteneur, un seul filtre réseau |
| Passerelle LLM, audit, kill switch | Communs (17.3) |
| Config verrouillée | **Propre à chaque outil** (17.2) |
| Hooks d'audit | **Propre à chaque outil**, vers le même collecteur (17.4) |

### 17.2 Config verrouillée par outil

| Outil | Emplacement système (Windows) | Mécanisme | Remarques |
|---|---|---|---|
| Claude Code | `C:\Program Files\ClaudeCode\managed-settings.json` | Fichier managed, précédence maximale | Voir section 5 |
| Gemini CLI | `C:\ProgramData\gemini-cli\settings.json` (« system overrides » : dernier mot) et `system-defaults.json` (valeurs par défaut) | Le fichier système l'emporte sur utilisateur et espace de travail ; chemin modifiable par `GEMINI_CLI_SYSTEM_SETTINGS_PATH` | Cette variable d'environnement permet de **détourner** le fichier : voir 17.5 |
| Codex CLI | `%ProgramData%\OpenAI\Codex\requirements.toml` (contraintes que l'utilisateur ne peut pas lever) et `managed_config.toml` (valeurs par défaut **modifiables** par l'utilisateur) | `requirements.toml` encadre politique d'approbation, bac à sable, recherche web, hooks managed, serveurs MCP autorisés | Utiliser `requirements.toml` pour tout ce qui doit être imposé ; ne rien imposer via `managed_config.toml` |

Sources : documentation [Codex — managed configuration](https://developers.openai.com/codex/enterprise/managed-configuration.md) et [Gemini CLI — enterprise](https://geminicli.com/docs/cli/enterprise/), consultées le 2026-10-05. **[À VALIDER]** sur les versions réellement déployées : les chemins, le schéma et la précédence évoluent vite.

Ajouter à l'application Intune (section 6) un dossier par outil et étendre `install.ps1` et `detect.ps1` pour copier **et vérifier le hash** de chaque fichier :

```
ClaudeCode-Harness\
├─ claude\managed-settings.json        → C:\Program Files\ClaudeCode\
├─ gemini\settings.json                → C:\ProgramData\gemini-cli\
└─ codex\requirements.toml             → C:\ProgramData\OpenAI\Codex\
```

L'ACL de la section 6.2 (Administrateurs en contrôle total, Utilisateurs en lecture) s'applique à **chaque** dossier. Rappel : `C:\ProgramData` est inscriptible par les utilisateurs par défaut ; l'héritage doit être coupé explicitement, sinon le verrouillage est illusoire.

Principe de **parité de politique** : écrire la politique une fois (« lecture de `.env` interdite », « pas de réseau sortant hors liste », « approbation requise avant écriture hors dépôt ») dans un document source, puis la traduire par outil. Documenter pour chaque règle les outils qui **ne savent pas** l'exprimer ; ces écarts sont couverts par l'isolation (section 14), jamais par la confiance dans l'outil.

### 17.3 Passerelle LLM d'entreprise (option, voir 18.2 avant de choisir)

> **Option retenue** (décision B). Sa mise en œuvre détaillée, avec l'identité OIDC, est en section 19. Elle est incompatible, pour Claude Code, avec `forceLoginMethod`.

C'est le principal gain du multi-fournisseur : **un point de contrôle unique**, indépendant de l'outil.

- Chaque outil est configuré (par la config verrouillée, ou par variables d'environnement imposées) pour appeler la **passerelle interne** plutôt que le fournisseur. **[À VALIDER]** : chaque CLI accepte-t-elle une URL de base personnalisée et le format d'API attendu (par exemple via une variable d'URL de base pour Claude Code, un `model_provider` personnalisé pour Codex, une URL de base pour Gemini) ? Si un outil ne l'accepte pas, il reste en accès direct avec des clés d'organisation.
- La passerelle détient **seule** les clés des fournisseurs : les postes et conteneurs n'ont que des **jetons courts par utilisateur** (section 14.1 : aucun secret durable côté poste).
- Elle fournit : quotas et coûts par utilisateur / équipe / ticket, journalisation uniforme, liste de modèles autorisés par politique, et le **kill switch unifié** (17.6).
- Le pare-feu du conteneur (14.2) n'autorise alors que la passerelle ; contourner la passerelle revient à contourner le réseau, pas seulement une config.

Compromis à accepter : un composant critique à exploiter (disponibilité, mises à jour de compatibilité d'API), et des fonctions spécifiques à un fournisseur qui peuvent ne pas traverser la passerelle. Si ce coût est refusé, le niveau 1 du kill switch reste par fournisseur (17.6) et l'attribution repose sur les clés individuelles de chacun.

### 17.4 Audit et traçabilité unifiés

Le collecteur de la section 16 est **unique** ; seul le hook d'émission varie par outil. Ajouter deux champs à l'événement :

```json
{ "provider": "anthropic | google | openai", "tool": "claude-code | gemini-cli | codex-cli", "tool_version": "<x.y.z>" }
```

- `session_id` n'est **pas** garanti unique entre outils : la clé de corrélation devient `(tool, session_id)`.
- Les mécanismes de hooks diffèrent. État vérifié le 2026-10-05 (voir 18.5) : les trois outils fournissent `session_id` en JSON sur l'entrée standard et savent bloquer. Les détails d'imposition (hooks verrouillés ou non) sont en 18.5. Les noms d'événements et le comportement exact restent **[À VALIDER]** sur la version déployée.
- **Repli commun si un outil n'offre pas de hook fiable** : l'**enveloppe de lancement**. Une commande `corp-agent <outil>` (script dans le PATH) qui exige le ticket, émet `session_start`, puis lance l'outil, et émet `session_end`. Le défaut : elle est contournable en appelant directement le binaire ; la passerelle (17.3) reste alors la source d'attribution, puisque chaque requête y porte l'identité et peut y être rattachée au ticket par en-tête.
- **Ticket sur la passerelle** : transmettre la clé Jira dans un en-tête de requête (ou un jeton de session émis par le hook de démarrage). La passerelle écrit alors l'usage **par ticket**, y compris pour un outil sans hook, ce qui rend la corrélation indépendante du comportement de chaque CLI.

### 17.5 Pièges propres au multi-outil

| Piège | Risque | Parade |
|---|---|---|
| Variables d'environnement qui redirigent la config (ex. `GEMINI_CLI_SYSTEM_SETTINGS_PATH`) | Un utilisateur pointe l'outil vers sa propre config | Les variables ne sont pas verrouillables côté utilisateur : compter sur l'isolation du conteneur (variables imposées dans l'image) et sur la passerelle |
| Fichiers « defaults » vs « overrides » | Croire qu'une valeur par défaut est imposée | Imposer uniquement via les fichiers à précédence maximale ; test d'acceptation par outil |
| Outils non couverts (Copilot, Cursor, Aider…) | Usage hors harness | Liste blanche d'applications (WDAC / AppLocker) et blocage réseau des endpoints non passés par la passerelle |
| Divergence de politique entre outils | Un outil moins restreint devient la voie de contournement | Matrice de parité (17.2), revue à chaque montée de version |
| Mises à jour automatiques des CLI | Changement de schéma de config sans préavis | Versions figées dans l'image / le catalogue, canal de test sur le groupe pilote |
| Gestion des clés par fournisseur | Multiplication des secrets | Passerelle unique ; sinon coffre de secrets et jetons à courte durée |

### 17.6 Kill switch multi-fournisseur

Adapter la table de la section 15 :

| Niveau | Action | Portée |
|---|---|---|
| 1 — Passerelle | Désactiver un utilisateur, un fournisseur, un modèle ou tout le parc, en un point | Tous les outils passés par la passerelle, **immédiat** |
| 2 — Fournisseur | Révoquer les clés / l'organisation chez Anthropic, Google, OpenAI | Par fournisseur ; nécessaire en cas de clé compromise ou si la passerelle est contournée |
| 3 — Réseau | Bloquer les endpoints des trois fournisseurs au proxy | Tous outils, y compris hors passerelle |
| 4 — Config | Durcir les fichiers managed (Intune) ou les réglages côté serveur disponibles chez chaque fournisseur **[À VALIDER]** | Postes joignables, latence en heures |
| 5 — Retrait | Désinstaller outils et extensions | Postes joignables |

Chaque niveau doit être testé **par fournisseur** : une révocation Anthropic n'arrête pas Codex.

### 17.7 Tests d'acceptation complémentaires

| # | Test | Résultat attendu |
|---|---|---|
| 8 | Pour chaque outil : tentative de modification du fichier de config système par un utilisateur standard | Accès refusé |
| 9 | Pour chaque outil : action interdite par la politique de parité | Refusée, ou bloquée par l'isolation si l'outil ne sait pas l'exprimer |
| 10 | Lancer chaque outil sans ticket dans la branche | Session bloquée (hook ou enveloppe), ou requête refusée à la passerelle |
| 11 | Appeler directement un fournisseur depuis le conteneur, sans passer par la passerelle | Refusé par le réseau |
| 12 | Désactiver un fournisseur à la passerelle | Les sessions des outils concernés échouent, les autres continuent |
| 13 | Une session de chaque outil sur un même ticket | Trois lignes distinctes `(tool, session_id)` rattachées au ticket, coûts agrégés |

### 17.8 Décisions à prendre

1. Passerelle LLM : construite ou achetée ? Qui l'exploite et avec quel engagement de disponibilité ?
2. Liste des outils autorisés et des **versions** figées, avec le calendrier de montée de version.
3. Politique pour les outils qui ne supportent pas l'URL de base personnalisée : interdits, ou autorisés en accès direct avec clés individuelles ?
4. Rétention et contenu des journaux (prompts, sorties) : le même régime pour tous les fournisseurs.

## 18. Identité unique OIDC : tout par SSO, aucune clé API

> **Décision prise : lecture B (passerelle OIDC).** La lecture A décrite ici n'est **pas retenue** ; la section 19 la remplace. Les sections 18.1 à 18.3 sont conservées comme référence et comme point de comparaison. Les sections 18.5 (hooks), 18.6 (preuve), 18.7 (tests) et 18.8 (conteneur) restent à lire en tenant compte des adaptations de 19.9.

Exigence : **100 % des accès aux modèles passent par le SSO de l'entreprise (OIDC), jamais par une clé API.** Cette section remplace, là où elle les contredit, les recommandations des sections 14.1 et 17.3. Faits vérifiés par recherche le 2026-10-05 ; chaque point reste **[À VALIDER]** sur les versions déployées.

### 18.1 Deux lectures de l'exigence

| Lecture | Mécanisme | Ce que voit le fournisseur |
|---|---|---|
| **A. SSO natif** (recommandée, c'est la lecture littérale) | Chaque fournisseur est fédéré à l'OIDC d'entreprise ; l'utilisateur se connecte avec son identité d'entreprise dans le tenant de l'entreprise ; **les clés API sont refusées par l'outil** | Un utilisateur de l'organisation, facturé à l'organisation |
| **B. Passerelle OIDC** | Les outils présentent un jeton OIDC à une passerelle interne qui appelle ensuite les fournisseurs avec **ses** identifiants | La passerelle ; l'utilisateur n'est connu que de la passerelle |

La lecture B repose, en amont, sur des identifiants détenus par la passerelle : ce n'est « pas de clé API côté utilisateur », pas « pas d'API ». **Décision à prendre par la direction sécurité** ; la suite de cette section suppose **A**.

### 18.2 Contrainte propre à Claude Code

Avec `forceLoginMethod` / `forceLoginOrgUUID` posés, les sessions authentifiées par `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN` ou `apiKeyHelper` sont bloquées au démarrage. Donc **on ne peut pas** à la fois imposer la connexion SSO et faire passer un jeton OIDC par `apiKeyHelper` vers une passerelle : il faut choisir A ou B pour Claude Code. Les versions récentes appliquent aussi ces règles aux sessions de fournisseurs tiers et par clé **[À VALIDER : version minimale, voir le journal des modifications]**.

### 18.3 Imposer la connexion SSO, outil par outil

| Outil | Réglage verrouillé | Effet |
|---|---|---|
| Claude Code | `managed-settings.json` : `forceLoginMethod` (`claudeai` pour le compte organisationnel) et `forceLoginOrgUUID` (UUID de l'organisation, ou liste) | Connexion limitée à l'organisation ; sessions par clé API refusées |
| Codex CLI | `forced_login_method = "chatgpt"` et `forced_chatgpt_workspace_id = "<id>"` (configuration managed) | Seule la connexion ChatGPT de l'espace de travail de l'entreprise est acceptée ; si les identifiants ne correspondent pas, l'outil déconnecte et quitte. **[À VALIDER]** : dans quel fichier (`managed_config.toml` ou `requirements.toml`) le réglage est réellement imposé, sachant que `managed_config.toml` est modifiable par l'utilisateur |
| Gemini CLI | `settings.json` système : `security.auth.enforcedType` (valeur correspondant à la connexion Google d'entreprise, pas `gemini-api-key`) | Impose un type d'authentification à tous les utilisateurs. **[À VALIDER]** : valeurs acceptées, et priorité sur les variables `GEMINI_API_KEY` / `GOOGLE_API_KEY` |

Point d'attention Gemini CLI : la connexion Google va directement à Google, **sans passer par un proxy**. C'est cohérent avec la lecture A, mais le contrôle est alors celui du tenant Google, pas du réseau.

### 18.4 Fédération OIDC côté fournisseurs

À faire une fois par fournisseur, hors poste de travail :

1. Déclarer l'OIDC d'entreprise comme fournisseur d'identité : espace Claude (offre entreprise), espace ChatGPT Business/Enterprise, Google Workspace / Cloud Identity (avec la fédération d'identité des personnels pour Google Cloud si besoin) **[À VALIDER selon les offres souscrites]**.
2. **Interdire les comptes personnels** et la création de comptes hors domaine vérifié.
3. **Provisionnement et dé-provisionnement automatiques (SCIM)** quand le fournisseur le propose : c'est ce qui rend le départ d'un salarié effectif chez les trois fournisseurs.
4. **Désactiver la création de clés API** pour les utilisateurs (console, projets, politique d'organisation cloud) et révoquer les clés existantes. Les clés de service nécessaires à la CI sont gérées à part, hors postes de développement.
5. Claims OIDC à normaliser : identifiant stable, e-mail, groupes (pour les rôles et l'accès par projet). Durée de vie des sessions et MFA décidées au niveau de l'IdP.

### 18.5 Hooks vérifiés

| Sujet | Claude Code | Codex CLI | Gemini CLI |
|---|---|---|---|
| Entrée du hook | JSON sur l'entrée standard, `session_id` | JSON, `session_id`, `transcript_path`, `cwd`, `model`, `permission_mode`, `source` | JSON, `session_id`, `transcript_path`, `cwd`, `hook_event_name`, horodatage |
| Événements utiles | `SessionStart`, `UserPromptSubmit`, `SessionEnd` | `SessionStart`, `UserPromptSubmit` (portée tour), Stop | `SessionStart`, `BeforeTool`, autres événements de cycle de vie |
| Bloquer | Code de sortie `2` | `"decision": "block"` avec raison (`UserPromptSubmit`) | Code de sortie `2` (stderr = raison) ; autre code non nul = avertissement sans blocage |
| Hooks imposés | `allowManagedHooksOnly` dans `managed-settings.json` | `[hooks]` dans `requirements.toml`, `managed_dir` / `windows_managed_dir`, `allow_managed_hooks_only = true`, et `[features].hooks = true` épinglé pour empêcher la désactivation locale | **Activation explicite requise** (`tools.enableHooks` et `hooks.enabled`) ; les hooks se déclarent dans `settings.json`, donc dans le fichier système (« overrides ») pour les imposer |
| Hooks utilisateur ou projet | Ignorés si `allowManagedHooksOnly` | Ignorés si `allow_managed_hooks_only` ; hooks non managés soumis à validation de confiance | **[À VALIDER]** : pas d'équivalent « managed only » trouvé ; un hook de projet ou utilisateur peut s'ajouter aux hooks système |

Conséquences :
- Pour Codex, déposer les scripts dans un dossier managed (`C:\ProgramData\OpenAI\...` ou `windows_managed_dir`) avec l'ACL de la section 6.2.
- Pour Gemini CLI, ne pas supposer que les hooks système sont exclusifs : l'audit côté fournisseur (18.6) reste l'autorité.
- Les trois hooks peuvent envoyer au collecteur de la section 16 le même événement, avec `tool` et `session_id`.

### 18.6 Ce qui prouve la conformité

Le hook d'un poste ne prouve pas **comment** la session s'est authentifiée. La preuve vient des **journaux d'audit des fournisseurs** (identité SSO de chaque requête) et de l'IdP :

| Contrôle | Source | Fréquence |
|---|---|---|
| Aucune clé API active pour les utilisateurs | Consoles des fournisseurs, politique d'organisation | Continue (alerte) |
| Toute session du fournisseur rattachée à un utilisateur du SSO | Journaux d'audit fournisseur recoupés avec la table d'audit (section 16) | Quotidienne |
| Session fournisseur sans événement d'audit poste | Anomalie : usage hors harness | Quotidienne |
| Comptes hors domaine ou personnels | Liste des membres de l'organisation | Hebdomadaire |

### 18.7 Tests d'acceptation (SSO)

| # | Test | Résultat attendu |
|---|---|---|
| 14 | Définir `ANTHROPIC_API_KEY` puis lancer `claude` | Session refusée au démarrage |
| 15 | Définir `OPENAI_API_KEY` ou connecter un compte ChatGPT personnel (Codex) | Refusé ; déconnexion et sortie en cas de compte hors espace de travail |
| 16 | Définir `GEMINI_API_KEY` ou `GOOGLE_API_KEY` (Gemini CLI) | Refusé, ou ignoré au profit de la connexion imposée **[À VALIDER]** |
| 17 | Se connecter avec un compte personnel (chaque outil) | Refusé |
| 18 | Créer une clé API depuis un compte utilisateur chez chaque fournisseur | Impossible (politique d'organisation) |
| 19 | Désactiver l'utilisateur dans l'IdP puis attendre le dé-provisionnement | Accès coupé chez les trois fournisseurs |

### 18.8 Authentification dans le conteneur

Le conteneur n'a pas de navigateur : prévoir la **connexion par code d'appareil** (device code) quand l'outil le propose (documentée pour Codex ; **[À VALIDER]** pour Claude Code et Gemini CLI), ou une connexion faite sur l'hôte avec partage contrôlé des jetons. Les jetons obtenus sont stockés dans le profil du conteneur : volume nommé, **non partagé** entre utilisateurs, effacé à la destruction du conteneur. C'est une **exception explicite** à la règle « aucun secret durable côté conteneur » (14.1) : ces jetons sont personnels, limités à l'organisation et révocables par le dé-provisionnement (18.4).

### 18.9 Kill switch avec SSO natif

Mettre à jour la section 15 :

| Niveau | Action | Remarque |
|---|---|---|
| 1 | Désactiver l'utilisateur dans l'IdP **et** le suspendre chez chaque fournisseur (SCIM ou console) | Les jetons déjà émis peuvent survivre jusqu'à expiration : vérifier la durée par fournisseur |
| 2 | Suspendre ou révoquer l'organisation / l'espace de travail chez un fournisseur | Coupe tous les utilisateurs de ce fournisseur |
| 3 | Bloquer les endpoints au proxy | Efficace pour tous les outils ; **la passerelle n'étant plus utilisée, ce n'est plus un levier par utilisateur** |
| 4–5 | Config managed durcie ; retrait des outils | Inchangé |

### 18.10 Décisions

1. Lecture A (SSO natif) ou B (passerelle) : à trancher avant tout déploiement.
2. Offres fournisseurs à souscrire pour disposer de la fédération OIDC, du SCIM et des journaux d'audit.
3. Politique pour les usages sans équivalent SSO (CI, automatisations) : comptes de service séparés, hors postes de développement, avec revue périodique.
4. Durée de vie des sessions SSO et exigence MFA.

Sources : [Codex — managed configuration](https://developers.openai.com/codex/enterprise/managed-configuration.md), [Codex — hooks](https://developers.openai.com/codex/hooks), [Codex — authentification](https://developers.openai.com/codex/auth), [Gemini CLI — hooks](https://geminicli.com/docs/hooks/reference/), [Gemini CLI — enterprise](https://geminicli.com/docs/cli/enterprise/), [Gemini CLI — configuration](https://geminicli.com/docs/cli/configuration), [Claude Code — authentification](https://code.claude.com/docs/en/authentication).

## 19. Architecture retenue : passerelle OIDC, aucune clé sur les postes

Faits vérifiés par recherche le 2026-10-05 ; tout point marqué **[À VALIDER]** reste à confirmer sur les versions déployées.

### 19.1 Ce que la décision garantit et ne garantit pas

| Garanti (par conception) | Non garanti |
|---|---|
| Aucun utilisateur ne détient de clé API de fournisseur | « Aucune API » en amont : la passerelle appelle les fournisseurs avec ses propres identifiants |
| Toute requête porte une identité OIDC vérifiée par la passerelle | Qu'un outil n'ait pas d'autre chemin si le réseau le permet (voir 19.6) |
| Attribution par utilisateur, équipe et ticket au même endroit pour les trois fournisseurs | Que la passerelle ne devienne pas un point de défaillance (voir 19.8) |

Pour rendre « pas d'API côté utilisateur » vraie en pratique, **trois conditions** : le réseau des conteneurs n'atteint que la passerelle (19.6), les identifiants amont ne sont lisibles que par la passerelle (19.5), et les comptes ou clés individuels chez les fournisseurs sont désactivés (19.7).

### 19.2 Architecture

```
 Poste / conteneur Podman                          Réseau d'entreprise
 ┌──────────────────────────┐                   ┌─────────────────────┐
 │ Claude Code / Gemini CLI │  jeton de session │  Passerelle LLM     │──► Anthropic
 │ / Codex CLI              │ ────────────────► │  - valide le JWT    │──► Google
 │   ▲                      │                   │  - quotas, ticket   │──► OpenAI
 │   │ corp-token (helper)  │                   │  - audit, kill      │
 └───┼──────────────────────┘                   └──────────▲──────────┘
     │ OIDC (code + PKCE / device code)                      │ JWKS / token exchange
     └─────────────────────────────► IdP OIDC d'entreprise ◄─┘
```

### 19.3 Flux d'identité

1. **Connexion** de l'utilisateur à l'IdP : code d'autorisation avec PKCE sur l'hôte ; **code d'appareil** (device code) dans le conteneur, qui n'a pas de navigateur.
2. Le helper **`corp-token`** (outil d'entreprise, section 19.4) obtient un jeton d'accès OIDC court (60 minutes ou moins) et le met en cache : trousseau Windows sur l'hôte, volume nommé non partagé dans le conteneur.
3. **Échange de jeton** (RFC 8693) : le helper présente le jeton OIDC et la clé du ticket Jira (déduite de la branche, section 16) à la passerelle, qui renvoie un **jeton de session** court, portant l'utilisateur, les groupes et le ticket. Ce principe fonctionne pour les trois outils, **qu'ils sachent ou non envoyer des en-têtes personnalisés**.
4. L'outil présente ce jeton à la passerelle à chaque requête. La passerelle le valide (signature par JWKS, émetteur, audience, expiration, groupes) et applique quotas et politique.
5. Un jeton expiré provoque un nouvel appel du helper ; sans ticket valide ou sans session IdP active, aucun jeton n'est émis : c'est ainsi que « pas de ticket, pas de session » est appliqué quel que soit l'outil.

Alternative sans échange de jeton : envoyer directement le jeton d'accès OIDC (les passerelles du marché savent valider un JWT d'IdP : documenté pour LiteLLM avec Okta, valable pour tout IdP OIDC émettant des JWT). Le ticket doit alors passer par un en-tête, ce que les trois outils ne permettent pas tous **[À VALIDER]** : c'est pourquoi l'échange est préféré.

### 19.4 Configuration par outil

**Claude Code** (`managed-settings.json`, ou conteneur `/etc/claude-code/`) :

```json
{
  "env": { "ANTHROPIC_BASE_URL": "https://llm-gw.corp.example" },
  "apiKeyHelper": "corp-token --audience llm-gateway",
  "allowManagedPermissionRulesOnly": true
}
```

- **Ne pas** définir `forceLoginMethod` / `forceLoginOrgUUID` : ils bloquent les sessions par `apiKeyHelper` (18.2).
- Priorité : `ANTHROPIC_AUTH_TOKEN` et `ANTHROPIC_API_KEY` l'emportent sur `apiKeyHelper`. Un utilisateur peut donc les définir, mais la passerelle rejette tout jeton qu'elle n'a pas émis ; le risque est un échec de service, pas un contournement. Le contrôle compensatoire est réseau (19.6).
- Fréquence de rafraîchissement de `apiKeyHelper` : régler pour qu'elle soit inférieure à la durée du jeton **[À VALIDER : variable d'intervalle de rafraîchissement]**.

**Codex CLI** (fournisseur personnalisé avec authentification par commande) :

```toml
model_provider = "corp"

[model_providers.corp]
name = "Corp gateway"
base_url = "https://llm-gw.corp.example/v1"
wire_api = "responses"

[model_providers.corp.auth]
command = "corp-token"
args = ["--audience", "llm-gateway"]
timeout_ms = 5000
refresh_interval_ms = 1800000
```

- Le mode par commande est **exclusif** de `env_key`, `experimental_bearer_token` et `requires_openai_auth`.
- **[À VALIDER]** : quel fichier **impose** `model_provider` (`requirements.toml` ou seulement `managed_config.toml`, modifiable). Si le fournisseur ne peut pas être épinglé, un utilisateur peut revenir à la connexion ChatGPT ; le blocage réseau (19.6) devient alors la seule garantie.
- Poser aussi les contraintes de sécurité (approbation, bac à sable, hooks managed) dans `requirements.toml`, comme en section 17.2.

**Gemini CLI** (`settings.json` système et variables du processus) :

```json
{
  "security": { "auth": { "enforcedType": "gemini-api-key" } }
}
```

```text
GOOGLE_GEMINI_BASE_URL=https://llm-gw.corp.example
GEMINI_API_KEY=<jeton de session émis par la passerelle>
```

- `GEMINI_API_KEY` doit contenir le **jeton de session de la passerelle**, pas une clé Google.
- Le jeton étant lu comme variable d'environnement, il est **fixé à la durée du processus** : lancer l'outil par l'enveloppe `corp-agent gemini` qui appelle `corp-token` puis exporte la variable pour ce processus uniquement, avec une durée de jeton supérieure à la durée habituelle d'une session (8 à 12 heures) ou un redémarrage en cas d'expiration.
- La connexion « Login with Google » envoie directement chez Google **sans passer par la passerelle** : c'est le chemin à fermer. `enforcedType` doit restreindre au type par clé **[À VALIDER : valeurs acceptées, et refus effectif de `oauth-personal` et `vertex-ai`]**.
- Gemini CLI ne parle nativement qu'à l'API Gemini ; l'accès aux autres modèles par cet outil dépend de la configuration de la passerelle et reste hors de l'objectif.

**Helper `corp-token`** : binaire unique (Windows et Linux) à écrire ou à acheter, qui :
- réalise code + PKCE ou device code auprès de l'IdP ;
- met en cache jeton d'accès et jeton de rafraîchissement (trousseau / volume) ;
- déduit le ticket depuis la branche git du répertoire courant (`CLAUDE_TICKET` prioritaire) ;
- appelle l'échange de jeton de la passerelle ;
- écrit **uniquement le jeton de session** sur la sortie standard (aucun journal contenant un jeton) ;
- échoue avec un code non nul et un message lisible si le ticket est absent ou si la session IdP est expirée.

### 19.5 Exigences de la passerelle

| Domaine | Exigence |
|---|---|
| Authentification | Validation JWT par JWKS ; contrôle `iss`, `aud`, `exp`, `nbf` ; rotation des clés ; refus de tout autre mécanisme (pas de clé statique utilisateur) |
| Autorisation | Groupes IdP → rôles ; modèles et fournisseurs autorisés par groupe ; plafonds de budget par utilisateur, équipe et ticket |
| Identifiants amont | Détenus **seulement** par la passerelle, dans un coffre de secrets, rotation planifiée ; privilégier les identités fédérées sans secret statique quand le fournisseur le permet (par exemple Google Cloud, Azure, AWS) **[À VALIDER par fournisseur]** ; clés Anthropic et OpenAI directes dans le coffre sinon |
| Journalisation | Utilisateur, équipe, ticket, outil, modèle, volume, coût ; contenu des prompts selon la décision de rétention (16.3) |
| Contrôle | Interrupteur par utilisateur, par fournisseur, par modèle, global (19.8) |
| Disponibilité | Cible d'engagement à définir (17.8) ; plan de repli documenté (19.8) ; pas de repli automatique vers un accès direct |
| Compatibilité | Formats d'API de chaque outil (Anthropic Messages, OpenAI Responses, Gemini) ; suivi des versions des CLI |

Exemple de produit : LiteLLM propose la validation de JWT d'IdP et un parcours de connexion SSO pour Claude Code (voir les sources) ; ce n'est **pas** une recommandation de produit, seulement la preuve que le schéma existe.

### 19.6 Fermeture des autres chemins (réseau)

Sans cela, la décision B est contournable :

1. Le réseau des conteneurs et des postes de développement n'autorise vers l'extérieur que **la passerelle**, le dépôt de code, le registre d'images interne, le collecteur d'audit et l'IdP (section 14.2).
2. Les endpoints des fournisseurs sont **bloqués** au proxy pour ces réseaux : Anthropic, OpenAI/ChatGPT, Google (API Gemini, Vertex, connexion Google pour l'outil). Maintenir les listes de domaines à jour.
3. Les postes hors réseau d'entreprise (télétravail) : VPN ou accès par la seule passerelle exposée avec l'IdP ; sinon la règle 2 ne s'applique pas. **Décision à prendre.**
4. Un outil non listé (autre CLI d'agent) est refusé par la liste blanche d'applications (section 17.5).

### 19.7 Fermeture côté fournisseurs

- Plus de comptes personnels ni de clés individuelles : organisation vérifiée uniquement, création de clés API désactivée aux utilisateurs, clés existantes révoquées.
- Une **identité de service unique par fournisseur pour la passerelle**, avec droits limités et alertes sur son usage hors passerelle (adresses IP sources attendues).
- Surveillance des tableaux de bord fournisseurs : toute requête qui ne vient pas des IP de la passerelle est une anomalie.

### 19.8 Kill switch et continuité

| Niveau | Action | Effet |
|---|---|---|
| 1 | Interrupteur de la passerelle (utilisateur, équipe, ticket, fournisseur, modèle, global) | **Immédiat**, pour tous les outils |
| 2 | Désactivation de l'utilisateur dans l'IdP | Plus aucun nouveau jeton de session ; les jetons déjà émis vivent jusqu'à expiration : durée courte, ou **liste de révocation** consultée par la passerelle |
| 3 | Révocation de l'identité de service d'un fournisseur | Coupe ce fournisseur pour tous, y compris la passerelle |
| 4 | Blocage des endpoints au proxy | Coupe tout |
| 5 | Config managed durcie ; retrait des outils | Postes joignables |

Continuité : la passerelle est un point unique de défaillance. Prévoir deux instances au minimum, une supervision, et un **procédé de repli manuel** décidé à l'avance (qui peut autoriser un accès direct temporaire, pour qui, combien de temps). Jamais de repli automatique vers un accès direct.

### 19.9 Adaptations des sections précédentes

| Section | Adaptation |
|---|---|
| 14.1 | Exception : les jetons OIDC courts en cache (trousseau, volume nommé) sont tolérés ; aucune clé de fournisseur sur les postes ni dans les conteneurs |
| 16 | Le collecteur d'audit reçoit aussi les événements de la passerelle ; la jointure se fait par `(tool, session_id)` et par jeton de session |
| 17.3 | Remplacée par 19.3 à 19.6 |
| 18.3 | Non applicable (lecture A) ; ne pas poser `forceLoginMethod` pour Claude Code |
| 18.4 | Reste utile pour 19.7 (comptes personnels, clés), pas pour la fédération des utilisateurs |
| 18.5 | Les hooks restent valables ; les imposer comme en 18.5 |
| 18.6 | La preuve vient désormais de la **passerelle**, recoupée avec les journaux d'organisation des fournisseurs (anomalie : requête hors IP de la passerelle) |
| 18.7 | Remplacée par 19.10 |
| 18.8 | Device code via `corp-token`, pas par chaque outil |

### 19.10 Tests d'acceptation (B)

| # | Test | Résultat attendu |
|---|---|---|
| 14 | Lancer chaque outil sans session IdP | Aucun jeton, démarrage ou requête refusé |
| 15 | Lancer chaque outil sur une branche sans clé de ticket | Aucun jeton de session émis |
| 16 | Définir une clé API de fournisseur (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `GEMINI_API_KEY` valide) | Rejetée par la passerelle, et, si elle vise directement le fournisseur, par le réseau |
| 17 | Depuis le conteneur, appeler directement chaque fournisseur | Bloqué par le réseau |
| 18 | Gemini CLI : « Login with Google » | Impossible (type imposé) et, à défaut, bloqué par le réseau |
| 19 | Codex : revenir à la connexion ChatGPT ou au fournisseur par défaut | Impossible si le fournisseur est épinglé, sinon bloqué par le réseau |
| 20 | Utilisateur désactivé dans l'IdP | Plus de nouveau jeton ; sessions coupées après expiration ou révocation |
| 21 | Interrupteur de la passerelle sur un fournisseur | Les outils concernés échouent, les autres continuent |
| 22 | Une requête sur chaque outil pour un même ticket | Trois lignes de coût rattachées au ticket, utilisateur identifié |
| 23 | Jeton expiré en cours de session | Renouvellement transparent, ou échec clair pour Gemini CLI (variable fixée) |

### 19.11 Risques résiduels

- **Contournement par un appareil non géré** : un poste personnel sans filtre réseau ; parade : accès à la passerelle conditionné à la conformité de l'appareil (accès conditionnel).
- **Jeton de session volé** : valable jusqu'à expiration ; parade : durées courtes, liaison à l'appareil si l'IdP le propose, révocation.
- **Évolution des CLI** : un changement de format d'authentification casse le schéma ; parade : versions figées, groupe pilote (17.5).
- **Concentration du risque** : la passerelle voit tous les prompts ; parade : contrôle d'accès strict, journaux sans contenu par défaut, revue de sécurité de la passerelle.

### 19.12 Décisions restantes

1. Passerelle : construite ou achetée, et son exploitation.
2. Accès hors réseau d'entreprise (VPN, accès direct à la passerelle) : voir 19.6.
3. Durée du jeton de session, en particulier pour Gemini CLI.
4. Procédé de repli en cas de panne de la passerelle.
5. Mêmes décisions que 17.8 : liste d'outils et versions, rétention des journaux.

Sources : [Claude Code — passerelle LLM](https://docs.claude.com/en/docs/claude-code/llm-gateway), [LiteLLM — Claude Code avec SSO Okta (JWT)](https://docs.litellm.ai/docs/tutorials/claude_code_okta_sso), [LiteLLM — Claude Code Gateway (connexion SSO)](https://docs.litellm.ai/docs/tutorials/claude_code_gateway), [Codex — fournisseurs de modèles personnalisés](https://codex.danielvaughan.com/2026/04/23/codex-cli-custom-model-providers-configuration-guide/), [LiteLLM — Gemini CLI](https://docs.litellm.ai/docs/tutorials/litellm_gemini_cli), [Gemini CLI — configuration](https://geminicli.com/docs/cli/configuration).

## Annexe A — Dépannage

| Symptôme | Piste |
|---|---|
| `podman machine init` échoue | WSL2/VMP non activés, virtualisation désactivée, ou redémarrage manquant |
| Erreurs de droits sur les fichiers montés | Ajouter `--userns=keep-id`, vérifier `remoteUser` |
| Montage `/mnt/c/Program Files/...` vide | Automount WSL désactivé ; utiliser le repli image (9.3) |
| Dev Containers ne trouve pas Podman | `dev.containers.dockerPath` ; `podman` absent du PATH utilisateur (relancer la session) |
| Intune ne restaure pas la config | Cycle d'évaluation non écoulé ; forcer une synchronisation de l'appareil |

## Annexe B — Si MSIX est imposé

Un paquet MSIX peut embarquer un **service empaqueté** (`desktop6:Service`, LocalSystem, démarrage automatique) dont le seul rôle est de copier `managed-settings.json` du paquet vers `C:\Program Files\ClaudeCode\` puis d'appliquer l'ACL. À réserver aux cas où la gouvernance l'exige : surcoût de développement, de signature (certificat de confiance) et de diagnostic, pour un résultat identique à la solution Win32.
