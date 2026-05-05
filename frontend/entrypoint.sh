#!/bin/sh
set -e

# Inject runtime configuration from environment variables into the config file
# served at /config/config.json. Fall back to default values when vars are unset.
export APP_HELP_URL="${APP_HELP_URL:-http://localhost:3000}"
export APP_DOCS_URL="${APP_DOCS_URL:-http://localhost:3000}"
export APP_FEATURE_FLAGS="${APP_FEATURE_FLAGS:-{}}"

mkdir -p /tmp/nginx-runtime

cat > /tmp/nginx-runtime/config.json <<EOF
{
  "helpUrl": "${APP_HELP_URL}",
  "docsUrl": "${APP_DOCS_URL}",
  "featureFlags": ${APP_FEATURE_FLAGS}
}
EOF

exec nginx -g 'daemon off;'
