#!/bin/sh
# Regenerates config.json from environment variables at container start, so
# the image built once in CI can be deployed to every environment without a
# rebuild. See src/app/core/config/provide-app-config.ts.
set -eu

CONFIG_PATH="/usr/share/nginx/html/config.json"
API_URL="${IFS_API_URL:-https://localhost:7246}"
OIDC_PROVIDER="${IFS_OIDC_PROVIDER:-keycloak}"
OIDC_AUTHORITY="${IFS_OIDC_AUTHORITY:-https://localhost:8080/realms/ifs}"
OIDC_CLIENT_ID="${IFS_OIDC_CLIENT_ID:-ifs-web}"
OIDC_SCOPE="${IFS_OIDC_SCOPE:-openid profile email}"

cat <<JSON > "$CONFIG_PATH"
{
  "apiUrl": "$API_URL",
  "oidc": {
    "provider": "$OIDC_PROVIDER",
    "authority": "$OIDC_AUTHORITY",
    "clientId": "$OIDC_CLIENT_ID",
    "scope": "$OIDC_SCOPE"
  }
}
JSON
