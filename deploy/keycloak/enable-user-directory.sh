#!/usr/bin/env bash
# Upgrades an EXISTING Keycloak realm "tnwatch" for F11 (assignments): the app reads the list of assignable
# users via the Keycloak Admin API using the service account of its client "tnwatch-web".
# New installations get this from tnwatch-realm.json; Keycloak does not re-import an existing realm,
# so run this once on installations created before F11. Safe to run repeatedly.
#
#   ./deploy/keycloak/enable-user-directory.sh            (uses deploy/.env for the admin password)
set -euo pipefail
cd "$(dirname "$0")/.."

ADMIN_PASSWORD="$(grep -E '^KEYCLOAK_ADMIN_PASSWORD=' .env | cut -d= -f2-)"
KC="docker exec tn-watch-keycloak /opt/keycloak/bin/kcadm.sh"

$KC config credentials --server http://localhost:8080 --realm master --user admin --password "$ADMIN_PASSWORD" >/dev/null
CLIENT_ID="$($KC get clients -r tnwatch -q clientId=tnwatch-web --fields id --format csv --noquotes)"
$KC update "clients/$CLIENT_ID" -r tnwatch -s serviceAccountsEnabled=true
$KC add-roles -r tnwatch --uusername service-account-tnwatch-web --cclientid realm-management \
  --rolename view-users --rolename query-users --rolename view-realm
echo "Service account of tnwatch-web can now read users (view-users, query-users, view-realm)."
