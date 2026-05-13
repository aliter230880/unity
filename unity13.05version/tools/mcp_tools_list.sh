#!/usr/bin/env bash
# mcp_tools_list.sh — получить полный каталог MCP-инструментов из подключённого Unity.
#
# Output: JSON со списком инструментов и их inputSchema. Сохраняйте в файл
# и парсите дальше (jq / python).
#
# Конфигурация через MCP_URL / MCP_TOKEN (см. mcp_call.sh).

set -euo pipefail

CONFIG_DIR="${MCP_CONFIG_DIR:-$HOME/.config/unity-mcp}"
if [ -z "${MCP_URL:-}" ] && [ -f "$CONFIG_DIR/url" ]; then MCP_URL=$(cat "$CONFIG_DIR/url"); fi
if [ -z "${MCP_TOKEN:-}" ] && [ -f "$CONFIG_DIR/token" ]; then MCP_TOKEN=$(cat "$CONFIG_DIR/token"); fi

: "${MCP_URL:?MCP_URL not set}"
: "${MCP_TOKEN:?MCP_TOKEN not set}"

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

H_AUTH="Authorization: Bearer $MCP_TOKEN"
H_CT="Content-Type: application/json"
H_ACC="Accept: application/json, text/event-stream"

TMP_HDR=$(mktemp); trap "rm -f $TMP_HDR" EXIT

curl -sS $RESOLVE_OPT -D "$TMP_HDR" -o /dev/null -X POST "$MCP_URL" \
    -H "$H_AUTH" -H "$H_CT" -H "$H_ACC" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"agent","version":"1.0"}}}'
SID=$(grep -i "^mcp-session-id:" "$TMP_HDR" | awk '{print $2}' | tr -d '\r\n')

curl -sS $RESOLVE_OPT -o /dev/null -X POST "$MCP_URL" \
    -H "$H_AUTH" -H "$H_CT" -H "$H_ACC" -H "mcp-session-id: $SID" \
    -d '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}'

curl -sS $RESOLVE_OPT -X POST "$MCP_URL" \
    -H "$H_AUTH" -H "$H_CT" -H "$H_ACC" -H "mcp-session-id: $SID" \
    -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' \
    | sed -n 's/^data: //p'
