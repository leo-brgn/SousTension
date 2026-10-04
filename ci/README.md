# CI SousTension

Adapté de ton pipeline GitLab/RHEL (`C:\Users\leobr\Documents\UnityCI\public-release`) vers GitHub Actions.
Workflow : `.github/workflows/ci.yml`. Scripts : `ci/scripts/`.

## Ce qui est repris
| Source (GitLab) | Ici |
|---|---|
| `ci-test.sh` (`unity test`, JUnit, retries, shards, couverture) | repris tel quel |
| `unity-license.sh` (retry + backoff, retour garanti) | repris ; mode par défaut `none` (au lieu de `floating`) |
| `inject-version.sh` | repris |
| `ci-build.sh` (build Windows, garde-fous) | adapté : sortie `Build/Windows/SousTension.exe`, garde-fou IL2CPP seulement sous Linux, plus de `file` ni de contrôle du module `windows-mono` |
| `secrets:scan` (gitleaks), `deps:lock-check` | repris (jobs sur runner GitHub) |

## Volontairement non repris
Images RHEL/UBI + registre, MSIX et signature, SonarQube, Codecov, SAST/Trivy, durcissement et analyse du binaire,
analyse dynamique, semver/release : conçus pour une app VR distribuée en MSIX, hors sujet pour un jeu Steam à ce stade.
À reprendre au besoin (`semver.sh`, `release.sh`, `sonar-scan.sh`…) ; ils restent dans ton dépôt UnityCI.

## À mettre en place (non fait automatiquement)
1. **Runner auto-hébergé** étiqueté `self-hosted, unity` (Settings → Actions → Runners) avec Git Bash, Git LFS, le Unity CLI,
   l'éditeur de `ProjectSettings/ProjectVersion.txt`, et pour le build le module Windows Build Support.
2. **Licence** : variable de dépôt `UNITY_LICENSE_MODE` (`none` si déjà activée sur le runner, sinon `serial`/`file`/`floating`)
   et, selon le mode, les secrets `UNITY_LICENSE_SERIAL` ou `UNITY_LICENSE_FILE_BASE64` (à ajouter aux `env:` des jobs).
3. **Dépôt public** : Settings → Actions → General → « Require approval for all outside collaborators ». Les jobs Unity
   sont déjà exclus des PR de forks.
4. **Build Settings** : le projet doit contenir au moins une scène dans `EditorBuildSettings.asset` pour que le build passe.
5. Les actions GitHub sont référencées par tag (`@v4`), pas par SHA : à épingler si tu veux durcir.

## Statut de vérification
Le workflow n'a pas tourné sur GitHub. Seul `ci-test.sh` est exercé en local (voir le message de livraison).
