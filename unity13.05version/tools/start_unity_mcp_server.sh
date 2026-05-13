#!/usr/bin/env bash
# start_unity_mcp_server.sh — поднять Docker `unity-mcp-server` в `streamableHttp` режиме.
#
# Usage:
#   ./start_unity_mcp_server.sh <token>
#
# Где <token> — Authorization Token из Unity-плагина AI Game Developer
# (Window → AI Game Developer → пункт 3 → строка `Authorization: Bearer ...`).
# Без правильного токена плагин и сервер не «увидят» друг друга.
#
# Параметры окружения:
#   IMAGE   — Docker-образ, по умолчанию ivanmurzakdev/unity-mcp-server:0.72.1
#   PORT    — внешний и внутренний порт, по умолчанию 443.
#   NAME    — имя контейнера, по умолчанию unity-mcp-server-${PORT}.
#
# После запуска сервер слушает на 0.0.0.0:${PORT}. Для публикации в интернет
# смотри start_tunnel.sh.

set -euo pipefail

TOKEN="${1:?token required: $0 <token>}"
IMAGE="${IMAGE:-ivanmurzakdev/unity-mcp-server:0.72.1}"
PORT="${PORT:-443}"
NAME="${NAME:-unity-mcp-server-${PORT}}"

# Если уже запущено с тем же токеном — оставляем как есть.
if docker inspect "$NAME" >/dev/null 2>&1; then
    EXISTING_TOKEN=$(docker inspect "$NAME" --format '{{range .Config.Env}}{{println .}}{{end}}' \
        | sed -nE 's/^MCP_PLUGIN_TOKEN=(.*)$/\1/p')
    if [ "$EXISTING_TOKEN" = "$TOKEN" ]; then
        docker start "$NAME" >/dev/null 2>&1 || true
        echo "[start_unity_mcp_server] $NAME already running with same token"
        exit 0
    fi
    echo "[start_unity_mcp_server] removing old $NAME (different token)"
    docker rm -f "$NAME" >/dev/null
fi

docker run -d \
    -p "${PORT}:${PORT}" \
    -e MCP_PLUGIN_CLIENT_TRANSPORT=streamableHttp \
    -e MCP_PLUGIN_PORT="$PORT" \
    -e MCP_PLUGIN_CLIENT_TIMEOUT=10000 \
    -e MCP_AUTHORIZATION=required \
    -e MCP_PLUGIN_TOKEN="$TOKEN" \
    --name "$NAME" \
    "$IMAGE" >/dev/null

sleep 1
docker logs --tail 5 "$NAME" 2>&1 | sed 's/^/[server] /'
echo "[start_unity_mcp_server] running on 0.0.0.0:${PORT}"
