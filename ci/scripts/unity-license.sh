#!/usr/bin/env bash
# Fonctions de licence partagées (à sourcer). Inspiré de GameCI : retry avec backoff, retour garanti.
# UNITY_LICENSE_MODE = floating | serial | file | none  (déf. floating)
#   floating : unity license activate --floating (serveur configuré dans l'environnement)
#   serial   : UNITY_LICENSE_SERIAL
#   file     : UNITY_LICENSE_FILE_BASE64 (fichier de licence hors ligne, .ulf, en base64)
#   none     : licence déjà active
# NB : UNITY_SERVICE_ACCOUNT_ID / _SECRET donnent accès aux *services* Unity (Cloud, packages) mais
#      ne licencient PAS l'Editor : il faut un des modes ci-dessus en plus.
LICENSE_MODE="${UNITY_LICENSE_MODE:-none}"
LICENSE_ACQUIRED=0
_lic_log() { echo "[license] $*"; }

# retry_backoff <tentatives> <delai_initial_s> <commande...>
retry_backoff() {
  local max="$1" delay="$2"; shift 2
  local n=1
  until "$@"; do
    [[ $n -ge $max ]] && return 1
    _lic_log "échec (tentative $n/$max), nouvelle tentative dans ${delay}s"
    sleep "$delay"; delay=$((delay * 2)); n=$((n + 1))
  done
}

license_acquire() {
  local tries="${UNITY_LICENSE_RETRIES:-5}" delay="${UNITY_LICENSE_RETRY_DELAY:-15}"
  case "$LICENSE_MODE" in
    floating)
      retry_backoff "$tries" "$delay" unity license activate --floating --non-interactive || return 5
      LICENSE_ACQUIRED=1 ;;
    serial)
      [[ -n "${UNITY_LICENSE_SERIAL:-}" ]] || { _lic_log "UNITY_LICENSE_SERIAL manquant"; return 5; }
      retry_backoff "$tries" "$delay" unity license activate --serial "$UNITY_LICENSE_SERIAL" --non-interactive || return 5
      LICENSE_ACQUIRED=1 ;;
    file)
      [[ -n "${UNITY_LICENSE_FILE_BASE64:-}" ]] || { _lic_log "UNITY_LICENSE_FILE_BASE64 manquant"; return 5; }
      local f; f="$(mktemp)"; chmod 600 "$f"
      printf '%s' "$UNITY_LICENSE_FILE_BASE64" | base64 -d > "$f"
      unity license activate --file "$f" --non-interactive; local rc=$?
      shred -u "$f" 2>/dev/null || rm -f "$f"
      [[ $rc -eq 0 ]] || return 5 ;;
    none) _lic_log "licence déjà active (mode none)" ;;
    *) _lic_log "UNITY_LICENSE_MODE inconnu: $LICENSE_MODE"; return 2 ;;
  esac
  return 0
}

license_return() {
  if [[ "$LICENSE_ACQUIRED" == 1 && "$LICENSE_MODE" != file ]]; then
    _lic_log "retour de la licence ($LICENSE_MODE)"
    unity license return --non-interactive || _lic_log "WARN: license return a échoué"
    LICENSE_ACQUIRED=0
  fi
}
