#!/usr/bin/env bash
# Build Windows (Mono ou IL2CPP) via le Unity CLI, avec garde-fous. Adapté de ton pipeline GitLab/RHEL pour SousTension.
# Variables :
#   PROJECT_PATH (déf. .)   OUTPUT_DIR (déf. Build/Windows)   OUTPUT_NAME (déf. SousTension.exe)
#   BUILD_PROFILE (optionnel : --profile au lieu de --target)  UNITY_BUILD_TIMEOUT (s, déf. 3600)
#   APP_VERSION (optionnel : injecte PlayerSettings.bundleVersion avant le build)
#   UNITY_LICENSE_MODE = floating | serial | file | none (déf. none : licence déjà active sur le runner)
set -uo pipefail

PROJECT_PATH="${PROJECT_PATH:-.}"
OUTPUT_DIR="${OUTPUT_DIR:-Build/Windows}"
OUTPUT_NAME="${OUTPUT_NAME:-SousTension.exe}"
TIMEOUT="${UNITY_BUILD_TIMEOUT:-3600}"
export UNITY_LICENSE_MODE="${UNITY_LICENSE_MODE:-none}"
HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/unity-license.sh"

log() { echo "[ci-build] $*"; }
die() { echo "[ci-build] ERREUR: $*" >&2; exit "${2:-1}"; }
trap license_return EXIT

# --- 1. Garde-fous ---
PV="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"
[[ -f "$PV" ]] || die "ProjectVersion.txt introuvable dans $PROJECT_PATH"
UNITY_VERSION="$(awk '/m_EditorVersion:/ {print $2}' "$PV")"
log "version projet: $UNITY_VERSION"
unity editors -i | grep -q "^${UNITY_VERSION}[[:space:]]" || die "Editor $UNITY_VERSION non installé sur ce runner"

# Sur Linux, un exe Windows ne peut être produit qu'en Mono. Sur un runner Windows, IL2CPP est permis.
if [[ "$(uname -s)" == Linux ]]; then
  PS="$PROJECT_PATH/ProjectSettings/ProjectSettings.asset"
  if awk '/^  scriptingBackend:/{f=1;next} f&&/^  [A-Za-z]/{f=0} f&&/Standalone: 1/{found=1} END{exit !found}' "$PS"; then
    die "Standalone en IL2CPP : impossible de produire un exe Windows depuis Linux. Passer en Mono." 3
  fi
fi

grep -q "path: Assets/" "$PROJECT_PATH/ProjectSettings/EditorBuildSettings.asset" || [[ -n "${BUILD_PROFILE:-}" ]] \
  || die "aucune scène dans EditorBuildSettings (Build Settings)" 4

if [[ -n "${APP_VERSION:-}" ]]; then
  "$HERE/inject-version.sh" "$PROJECT_PATH" "$APP_VERSION" || die "injection de version impossible" 2
fi

# --- 2. Licence ---
unity doctor --ci || { rc=$?; die "unity doctor --ci a échoué (code $rc)" "$rc"; }
license_acquire || die "activation de licence impossible (mode $LICENSE_MODE)" 5

# --- 3. Build ---
mkdir -p "$OUTPUT_DIR"
TARGET_ARGS=(--target StandaloneWindows64)
[[ -n "${BUILD_PROFILE:-}" ]] && TARGET_ARGS=(--profile "$BUILD_PROFILE")
log "build ${TARGET_ARGS[*]} -> $OUTPUT_DIR/$OUTPUT_NAME"
timeout "$TIMEOUT" unity build "$PROJECT_PATH" "${TARGET_ARGS[@]}" \
  --output-path "$OUTPUT_DIR/$OUTPUT_NAME" --no-tail --non-interactive
RC=$?
[[ $RC -eq 124 ]] && die "timeout après ${TIMEOUT}s" 124
[[ $RC -eq 0 ]] || { log "logs Unity:"; ls "$PROJECT_PATH"/Logs 2>/dev/null; die "build en échec (code $RC)" "$RC"; }

# --- 4. Vérification du résultat ---
[[ -f "$OUTPUT_DIR/$OUTPUT_NAME" ]] || die "exe absent : $OUTPUT_DIR/$OUTPUT_NAME" 6
[[ -f "$OUTPUT_DIR/UnityPlayer.dll" ]] || die "UnityPlayer.dll absent" 6
log "OK: $OUTPUT_DIR/$OUTPUT_NAME"
du -sh "$OUTPUT_DIR"
