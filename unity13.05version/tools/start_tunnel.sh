#!/usr/bin/env bash
# start_tunnel.sh — публикует локальный порт в интернет через `cloudflared` quick-tunnel.
#
# Usage:
#   ./start_tunnel.sh [local_port] [scheme]
#
# Параметры:
#   local_port — порт, на котором слушает unity-mcp-server (по умолчанию 443).
#   scheme     — http или https (по умолчанию http).
#                Контейнер unity-mcp-server слушает обычный HTTP без TLS, поэтому
#                cloudflared должен дёргать его по http://localhost:<port>.
#                (Если поставить https — упрёшься в "SSL handshake failed" 502.)
#
# ВАЖНО: `--protocol http2` обязателен. По умолчанию cloudflared использует QUIC (UDP/443
# к Cloudflare edge), который во многих окружениях (CI, контейнеры, VPN) блокируется на
# фаерволе — тоннель часами висит в "failed to dial quic" и не поднимается. HTTP/2 (TCP/443)
# работает везде, где есть исходящий HTTPS.
#
# Скрипт пишет публичный URL в $HOME/.config/unity-mcp/url, чтобы mcp_call.sh
# его подхватил автоматически. Сам процесс cloudflared логируется в /tmp/cloudflared.log.
#
# Зависимости: cloudflared (https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/).

set -euo pipefail

PORT="${1:-443}"
SCHEME="${2:-http}"
CONFIG_DIR="${MCP_CONFIG_DIR:-$HOME/.config/unity-mcp}"
mkdir -p "$CONFIG_DIR"

# Если уже запущен — не плодим.
EXISTING_PID=$(pgrep -f "cloudflared.*--url.*localhost:${PORT}" 2>/dev/null || true)
if [ -n "$EXISTING_PID" ]; then
    echo "[start_tunnel] cloudflared already running (pid $EXISTING_PID)"
    EXISTING_URL=$(cat "$CONFIG_DIR/url" 2>/dev/null || true)
    [ -n "$EXISTING_URL" ] && echo "[start_tunnel] URL: $EXISTING_URL"
    exit 0
fi

if ! command -v cloudflared >/dev/null 2>&1; then
    echo "ERROR: cloudflared not installed. Install: https://github.com/cloudflare/cloudflared/releases" >&2
    exit 1
fi

LOG="/tmp/cloudflared.log"
ORIGIN_URL="${SCHEME}://localhost:${PORT}"
EXTRA_OPTS="--protocol http2"
if [ "$SCHEME" = "https" ]; then
    EXTRA_OPTS="$EXTRA_OPTS --no-tls-verify"
fi
# shellcheck disable=SC2086
nohup cloudflared tunnel --url "$ORIGIN_URL" $EXTRA_OPTS >"$LOG" 2>&1 &
TUNNEL_PID=$!
echo "[start_tunnel] launched cloudflared pid $TUNNEL_PID, waiting for URL... (origin=$ORIGIN_URL, opts=$EXTRA_OPTS)"

# Ждём появления URL вида *.trycloudflare.com в логе.
URL=""
for i in $(seq 1 30); do
    URL=$(grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' "$LOG" | head -1 || true)
    if [ -n "$URL" ]; then break; fi
    sleep 1
done

if [ -z "$URL" ]; then
    echo "ERROR: failed to obtain quick-tunnel URL within 30s. Last log lines:" >&2
    tail -30 "$LOG" >&2
    exit 1
fi

echo "$URL/" > "$CONFIG_DIR/url"
echo "[start_tunnel] URL: $URL"
echo "[start_tunnel] saved to $CONFIG_DIR/url"
