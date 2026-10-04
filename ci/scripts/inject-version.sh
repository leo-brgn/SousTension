#!/usr/bin/env bash
# Injecte la version applicative dans le projet (PlayerSettings.bundleVersion => Application.version) avant le build.
# Usage : inject-version.sh <chemin_projet> <version>      (ex. 1.4.0 ou 1.4.0.123)
# Modifie uniquement l'espace de travail CI (jamais commité). Aucun -executeMethod nécessaire.
set -euo pipefail
PROJECT="${1:?chemin projet}"; VERSION="${2:?version}"
F="$PROJECT/ProjectSettings/ProjectSettings.asset"
[[ "$VERSION" =~ ^[0-9]+(\.[0-9]+){1,3}([-+][0-9A-Za-z.-]+)?$ ]] || { echo "[version] format invalide: $VERSION" >&2; exit 2; }
[[ -f "$F" ]] || { echo "[version] $F introuvable" >&2; exit 2; }
grep -qE '^  bundleVersion: ' "$F" || { echo "[version] clé bundleVersion absente de $F" >&2; exit 2; }
OLD="$(grep -E '^  bundleVersion: ' "$F" | head -1 | sed 's/^  bundleVersion: //')"
sed -i -E "s/^(  bundleVersion: ).*/\1${VERSION}/" "$F"
echo "[version] bundleVersion: ${OLD} -> ${VERSION}"
