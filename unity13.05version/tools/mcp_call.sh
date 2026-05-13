#!/usr/bin/env bash
# mcp_call.sh — позвать конкретный MCP-инструмент через streamableHttp.
#
# Usage:
#   ./mcp_call.sh <tool_name> '<json_args>'
#
# Example:
#   ./mcp_call.sh scene-list-opened '{}'
#   ./mcp_call.sh gameobject-find '{"path":"Main Camera"}'
#
# Конфигурация через переменные окружения:
#   MCP_URL   — публичный URL Cloudflare tunnel (или любой другой reverse proxy).
#               По умолчанию: значение из ~/.config/unity-mcp/url или ошибка.
#   MCP_TOKEN — Bearer-токен из Unity-плагина AI Game Developer.
#               По умолчанию: значение из ~/.config/unity-mcp/token или ошибка.
#
# Поведение:
#   1. Если URL — это *.trycloudflare.com, локальный DNS-резолвер обычно
#      не знает таких поддоменов. Делаем явный резолв через Google DoH и
#      передаём результат в curl через --resolve.
#   2. Открываем JSON-RPC сессию: initialize → notifications/initialized → tools/call.
#   3. Печатаем ответ сервера на stdout. Парсить дальше — задача вызывающего.
#
# Зависимости: curl, jq, awk, sed, date, stat.

set -euo pipefail

CONFIG_DIR="${MCP_CONFIG_DIR:-$HOME/.config/unity-mcp}"
if [ -z "${MCP_URL:-}" ] && [ -f "$CONFIG_DIR/url" ]; then
    MCP_URL=$(cat "$CONFIG_DIR/url")
fi
if [ -z "${MCP_TOKEN:-}" ] && [ -f "$CONFIG_DIR/token" ]; then
    MCP_TOKEN=$(cat "$CONFIG_DIR/token")
fi

: "${MCP_URL:?MCP_URL not set; export it or write $CONFIG_DIR/url}"
: "${MCP_TOKEN:?MCP_TOKEN not set; export it or write $CONFIG_DIR/token}"

# Local resolver can't resolve random trycloudflare.com subdomains — use Google DoH.
HOST=$(echo "$MCP_URL" | sed -E 's|https?://([^/]+).*|\1|')
IP_CACHE="/tmp/.mcp_ip_${HOST}"
if [ ! -f "$IP_CACHE" ] || \
   [ $(( $(date +%s) - $(stat -c %Y "$IP_CACHE" 2>/dev/null || echo 0) )) -gt 60 ]; then
    IP=$(curl -s "https://dns.google/resolve?name=$HOST&type=A" \
        | sed -nE 's/.*"data":"([0-9.]+)".*/\1/p' | head -1)
    [ -n "$IP" ] && echo "$IP" > "$IP_CACHE"
fi
IP=$(cat "$IP_CACHE" 2>/dev/null || true)
RESOLVE_OPT=""
[ -n "$IP" ] && RESOLVE_OPT="--resolve $HOST:443:$IP"

TOOL="${1:?tool name required}"
ARGS="${2-}"
[ -z "$ARGS" ] && ARGS='{}'

H_AUTH="Authorization: Bearer $MCP_TOKEN"
H_CT="Content-Type: application/json"
H_ACC="Accept: application/json, text/event-stream"

TMP_HDR=$(mktemp)
trap "rm -f $TMP_HDR" EXIT

# 1. initialize → выясняем mcp-session-id
curl -sS $RESOLVE_OPT -D "$TMP_HDR" -o /dev/null -X POST "$MCP_URL" \
    -H "$H_AUTH" -H "$H_CT" -H "$H_ACC" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"agent","version":"1.0"}}}'

SID=$(grep -i "^mcp-session-id:" "$TMP_HDR" | awk '{print $2}' | tr -d '\r\n')
if [ -z "$SID" ]; then
    echo "Failed to get mcp-session-id from initialize response." >&2
    cat "$TMP_HDR" >&2
    exit 1
fi
H_SID="mcp-session-id: $SID"

# 2. notifications/initialized
curl -sS $RESOLVE_OPT -o /dev/null -X POST "$MCP_URL" \
    -H "$H_AUTH" -H "$H_CT" -H "$H_ACC" -H "$H_SID" \
    -d '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}'

# 3. tools/call
PAYLOAD=$(jq -nc --arg name "$TOOL" --argjson args "$ARGS" \
    '{jsonrpc:"2.0",id:2,method:"tools/call",params:{name:$name,arguments:$args}}')

curl -sS $RESOLVE_OPT -X POST "$MCP_URL" \
    -H "$H_AUTH" -H "$H_CT" -H "$H_ACC" -H "$H_SID" \
    -d "$PAYLOAD" | sed -n 's/^data: //p'
