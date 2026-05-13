#!/usr/bin/env bash
# serve_file.sh — поднять локальный HTTP-сервер на одиночный файл, чтобы Unity
# мог его скачать через `script-execute` + UnityWebRequest / HttpClient.
#
# Используется как часть pipeline'а glb-import-to-unity:
#   1. Скрипт копирует выбранный файл в /tmp/serve.
#   2. Запускает `python3 -m http.server <port> --directory /tmp/serve`.
#   3. Печатает локальный URL.
#   После этого нужно опубликовать порт <port> наружу (например, через
#   `start_tunnel.sh` или devin `deploy expose`), чтобы Unity на машине
#   пользователя смог достучаться.
#
# Usage:
#   ./serve_file.sh <file_path> [port]
#
# Defaults:
#   port = 8765

set -euo pipefail

FILE="${1:?Usage: $0 <file_path> [port]}"
PORT="${2:-8765}"

if [ ! -f "$FILE" ]; then
    echo "ERROR: file not found: $FILE" >&2
    exit 1
fi

SERVE_DIR="${SERVE_DIR:-/tmp/serve}"
mkdir -p "$SERVE_DIR"
DEST="$SERVE_DIR/$(basename "$FILE")"
cp -f "$FILE" "$DEST"

# Прибиваем старый процесс если занимает порт
OLD=$(lsof -t -i ":${PORT}" 2>/dev/null || true)
if [ -n "$OLD" ]; then
    echo "[serve_file] killing previous server on port $PORT (pid $OLD)"
    kill "$OLD" 2>/dev/null || true
    sleep 1
fi

nohup python3 -m http.server "$PORT" --directory "$SERVE_DIR" >/tmp/serve.log 2>&1 &
sleep 1

echo "[serve_file] serving $DEST"
echo "[serve_file] local URL: http://localhost:${PORT}/$(basename "$FILE")"
echo "[serve_file] now expose port $PORT publicly (e.g. cloudflared / deploy expose)"
