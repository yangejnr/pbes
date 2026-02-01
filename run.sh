#!/usr/bin/env bash
set -euo pipefail

AWS_REGION="eu-west-2"
AWS_ACCOUNT_ID="169136975305"
ECR_REGISTRY="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is not installed or not in PATH." >&2
  exit 1
fi

if ! command -v aws >/dev/null 2>&1; then
  echo "AWS CLI is not installed or not in PATH." >&2
  exit 1
fi

echo "Logging in to ECR (${ECR_REGISTRY})..."
aws ecr get-login-password --region "${AWS_REGION}" | \
  docker login --username AWS --password-stdin "${ECR_REGISTRY}"

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose v2 is required (docker compose)." >&2
  exit 1
fi

echo "Pulling latest images..."
docker compose pull

echo "Starting services..."
docker compose up -d

if ! command -v ngrok >/dev/null 2>&1; then
  echo "ngrok is not installed or not in PATH." >&2
  exit 1
fi

echo "Starting ngrok tunnels..."
pkill -f ngrok >/dev/null 2>&1 || true
nohup ngrok http 8088 >/tmp/ngrok-gateway.log 2>&1 &

echo "Done."
echo "Frontend: http://localhost:3000"
echo "Backend:  http://localhost:8080"
echo "Gateway:  http://localhost:8088 (frontend + backend via /api)"
echo "Ngrok log: /tmp/ngrok-gateway.log"
