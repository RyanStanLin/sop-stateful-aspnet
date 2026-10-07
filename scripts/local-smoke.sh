#!/usr/bin/env bash
set -euo pipefail
python3 scripts/create-local-env.py
docker compose --env-file .env.local -f compose.dev.yml up -d --wait
set -a
source .env.local
set +a
dotnet src/App/bin/Release/net10.0/App.dll > /tmp/paas-local-api.log 2>&1 &
app_pid=$!
trap 'kill "$app_pid" 2>/dev/null || true; docker compose --env-file .env.local -f compose.dev.yml down' EXIT
ready=false
for attempt in {1..30}; do
  if curl --fail --silent http://localhost:8080/healthz > /dev/null; then ready=true; break; fi
  sleep 1
done
if [ "$ready" != true ]; then echo 'Local application health failed'; exit 1; fi
python3 scripts/api-smoke.py http://localhost:8080 --local
node scripts/ws-smoke.mjs http://localhost:8080
