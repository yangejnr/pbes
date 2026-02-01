#!/usr/bin/env bash
set -euo pipefail

AWS_REGION="eu-west-2"
AWS_ACCOUNT_ID="169136975305"
ECR_REGISTRY="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

require_cmd docker
require_cmd aws
require_cmd ngrok

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose v2 is required (docker compose)." >&2
  exit 1
fi

if ! command -v ollama >/dev/null 2>&1; then
  echo "Ollama is not installed or not in PATH." >&2
  exit 1
fi

echo "Ensuring Ollama is running and reachable from containers..."
pkill -f "ollama serve" >/dev/null 2>&1 || true
OLLAMA_HOST=0.0.0.0:11434 nohup ollama serve >/tmp/ollama.log 2>&1 &
for i in {1..20}; do
  if curl -s http://127.0.0.1:11434/api/tags >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! curl -s http://127.0.0.1:11434/api/tags >/dev/null 2>&1; then
  echo "Ollama did not start. Check /tmp/ollama.log" >&2
  exit 1
fi

echo "Logging in to ECR (${ECR_REGISTRY})..."
aws ecr get-login-password --region "${AWS_REGION}" | \
  docker login --username AWS --password-stdin "${ECR_REGISTRY}"

echo "Pulling latest images..."
docker compose pull

echo "Starting services..."
docker compose up -d

echo "Starting ngrok tunnel (gateway on :8088)..."
pkill -f ngrok >/dev/null 2>&1 || true
nohup ngrok http 8088 >/tmp/ngrok-gateway.log 2>&1 &

sleep 2

echo "Done."
echo "Gateway:  http://localhost:8088 (frontend + backend via /api)"
echo "Frontend: http://localhost:3000"
echo "Backend:  http://localhost:8080"

echo "Ngrok URL:"
if command -v python3 >/dev/null 2>&1; then
  python3 - <<'PY'
import json, urllib.request
try:
    data = urllib.request.urlopen('http://127.0.0.1:4040/api/tunnels', timeout=2).read()
    tunnels = json.loads(data).get('tunnels', [])
    for t in tunnels:
        print(t.get('public_url'))
except Exception as e:
    print('Unable to fetch ngrok URL:', e)
PY
else
  echo "(install python3 to auto-print ngrok URL)"
fi

echo "Logs: /tmp/ollama.log, /tmp/ngrok-gateway.log"
