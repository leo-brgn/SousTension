#!/usr/bin/env bash
# Tests EditMode/PlayMode via `unity test` (natif du CLI) : rapport JUnit pour GitLab, shards, retries, couverture.
# Variables : PROJECT_PATH (déf. .)  TEST_MODE (EditMode|PlayMode)  TEST_SHARD (ex. 1/3, déf. $CI_NODE_INDEX/$CI_NODE_TOTAL si définis)
#             TEST_SHARD_INVENTORY (rapport NUnit d'une exécution complète, requis pour sharder)
#             TEST_COVERAGE_OPTIONS (déf. assemblyFilters:-*Tests* : exclut les assemblies de tests de la couverture)
#             TEST_RETRIES (déf. 1)  TEST_COVERAGE=1  TEST_OUT_DIR (déf. test-results)  UNITY_LICENSE_MODE (voir unity-license.sh)
set -uo pipefail
. "$(dirname "$0")/unity-license.sh"
PROJECT_PATH="${PROJECT_PATH:-.}"; OUT="${TEST_OUT_DIR:-test-results}"; MODE="${TEST_MODE:-EditMode}"
log() { echo "[ci-test] $*"; }
trap license_return EXIT
mkdir -p "$OUT"

unity doctor --ci || { rc=$?; log "unity doctor --ci a échoué (code $rc)"; exit "$rc"; }
license_acquire || { log "activation de licence impossible"; exit 5; }

ARGS=(test "$PROJECT_PATH" --mode "$MODE" --report-format nunit,junit --output "$OUT/$MODE-nunit.xml" --junit-output "$OUT/$MODE-junit.xml"
      --retries "${TEST_RETRIES:-1}" --non-interactive)
SHARD="${TEST_SHARD:-}"; [[ -z "$SHARD" && -n "${CI_NODE_TOTAL:-}" ]] && SHARD="${CI_NODE_INDEX:-1}/${CI_NODE_TOTAL}"
[[ -n "$SHARD" ]] && ARGS+=(--shard "$SHARD")
[[ -n "$SHARD" && -n "${TEST_SHARD_INVENTORY:-}" ]] && ARGS+=(--shard-inventory "$TEST_SHARD_INVENTORY")
[[ "${TEST_COVERAGE:-0}" == 1 ]] && ARGS+=(--coverage --coverage-output "$OUT/coverage" --coverage-options "${TEST_COVERAGE_OPTIONS:-assemblyFilters:-*Tests*}")
log "unity ${ARGS[*]}"
unity "${ARGS[@]}"; RC=$?
log "code retour: $RC"
exit $RC
