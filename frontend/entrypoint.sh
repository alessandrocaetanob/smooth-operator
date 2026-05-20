#!/bin/sh
set -e

# ── Runtime API URL ──────────────────────────────────────────────────────────
# The frontend nginx proxies /api/* to the backend. To support deploying the
# API and frontend on different hosts, the upstream host:port is templated
# into /tmp/nginx-runtime/default.conf at startup from $API_URL.
# Default keeps the single-host `docker compose up` flow working unchanged.
API_URL="${API_URL:-http://backend:8080}"

# Parse scheme/host/port from the URL using POSIX parameter expansion (ash-safe).
url_no_proto="${API_URL#*://}"
host_port="${url_no_proto%%/*}"
case "$host_port" in
  *:*)
    API_BACKEND_HOST="${host_port%:*}"
    API_BACKEND_PORT="${host_port##*:}"
    ;;
  *)
    API_BACKEND_HOST="$host_port"
    case "$API_URL" in
      https://*) API_BACKEND_PORT=443 ;;
      *)         API_BACKEND_PORT=80  ;;
    esac
    ;;
esac
export API_BACKEND_HOST API_BACKEND_PORT

mkdir -p /tmp/nginx-runtime

# Restrict envsubst to our two variables so nginx's own $uri / $host / etc.
# survive untouched.
envsubst '${API_BACKEND_HOST} ${API_BACKEND_PORT}' \
  < /etc/nginx/templates/default.conf.template \
  > /tmp/nginx-runtime/default.conf

# ── Runtime app config ───────────────────────────────────────────────────────
# Inject runtime configuration from environment variables into the config file
# served at /config/config.json. Fall back to default values when vars are unset.
export APP_HELP_URL="${APP_HELP_URL:-http://localhost:3000}"
export APP_DOCS_URL="${APP_DOCS_URL:-http://localhost:3000}"
export APP_FEATURE_FLAGS="${APP_FEATURE_FLAGS:-{}}"

cat > /tmp/nginx-runtime/config.json <<EOF
{
  "helpUrl": "${APP_HELP_URL}",
  "docsUrl": "${APP_DOCS_URL}",
  "featureFlags": ${APP_FEATURE_FLAGS}
}
EOF

exec nginx -g 'daemon off;'
