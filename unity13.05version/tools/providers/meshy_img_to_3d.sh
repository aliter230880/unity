#!/usr/bin/env bash
# meshy_img_to_3d.sh — Meshy.ai Image-to-3D API client.
#
# Meshy is a commercial cloud service with the best general image-to-3D
# quality currently available (PBR maps, 4K textures, A/T-pose support, GLB/
# FBX/USDZ output).
#
# Pricing (2026):
#   - Free tier: 200 credits/month (~10 generations).
#   - Pro: $20/mo for 1000 credits.
#   - Each image-to-3D task: 5-25 credits depending on options.
#
# Requires:
#   - MESHY_API_KEY env var. Get one at https://www.meshy.ai/ -> Settings -> API.
#   - The input image must be publicly reachable (URL) OR a local file <10 MB
#     (we will encode as base64 data URI).
#
# Usage:
#   meshy_img_to_3d.sh <input_image_or_url> <output_glb> \
#       [ai_model=latest] [pose_mode=""] [hd_texture=false] [enable_pbr=false]
#
# Docs: https://docs.meshy.ai/api/image-to-3d

set -euo pipefail

INPUT="${1:?Usage: $0 <input_image_or_url> <output_glb> [ai_model] [pose_mode] [hd_texture] [enable_pbr]}"
OUT_GLB="${2:?Usage: $0 <input_image_or_url> <output_glb> [ai_model] [pose_mode] [hd_texture] [enable_pbr]}"
AI_MODEL="${3:-latest}"
POSE_MODE="${4:-}"          # "", "a-pose", "t-pose"
HD_TEXTURE="${5:-false}"
ENABLE_PBR="${6:-false}"

if [[ -z "${MESHY_API_KEY:-}" ]]; then
    echo "[meshy] MESHY_API_KEY env var is not set." >&2
    echo "  Get one at: https://www.meshy.ai/ -> Settings -> API" >&2
    exit 64
fi

API="https://api.meshy.ai/openapi/v1/image-to-3d"

# Build image_url field (URL or data URI)
if [[ "$INPUT" =~ ^https?:// ]]; then
    IMG_URL="$INPUT"
else
    if [[ ! -f "$INPUT" ]]; then
        echo "[meshy] ERROR: input file not found: $INPUT" >&2
        exit 2
    fi
    case "${INPUT,,}" in
        *.png)         MIME="image/png" ;;
        *.jpg|*.jpeg)  MIME="image/jpeg" ;;
        *) echo "[meshy] ERROR: input must be .png/.jpg/.jpeg" >&2; exit 2 ;;
    esac
    B64="$(base64 -w0 < "$INPUT")"
    IMG_URL="data:${MIME};base64,${B64}"
fi

# Submit task
echo "[meshy] Submitting image-to-3d task (ai_model=$AI_MODEL pbr=$ENABLE_PBR hd=$HD_TEXTURE pose=$POSE_MODE)..." >&2
REQ_BODY="$(python3 -c "
import json, sys
print(json.dumps({
    'image_url': sys.argv[1],
    'ai_model': sys.argv[2],
    'pose_mode': sys.argv[3],
    'hd_texture': sys.argv[4] == 'true',
    'enable_pbr': sys.argv[5] == 'true',
    'target_formats': ['glb'],
}))
" "$IMG_URL" "$AI_MODEL" "$POSE_MODE" "$HD_TEXTURE" "$ENABLE_PBR")"

RESP="$(curl -sS -X POST "$API" \
    -H "Authorization: Bearer ${MESHY_API_KEY}" \
    -H "Content-Type: application/json" \
    -d "$REQ_BODY")"

TASK_ID="$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('result') or d.get('id') or '')" "$RESP" 2>/dev/null || true)"
if [[ -z "$TASK_ID" ]]; then
    echo "[meshy] ERROR: no task id in response: $RESP" >&2
    exit 3
fi
echo "[meshy] task_id=$TASK_ID, polling for completion..." >&2

# Poll task status
DEADLINE=$(( $(date +%s) + 600 ))   # 10 min cap
while (( $(date +%s) < DEADLINE )); do
    sleep 5
    STATUS_JSON="$(curl -sS "$API/$TASK_ID" -H "Authorization: Bearer ${MESHY_API_KEY}")"
    STATUS="$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('status',''))" "$STATUS_JSON" 2>/dev/null || echo "")"
    PROGRESS="$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('progress',''))" "$STATUS_JSON" 2>/dev/null || echo "")"
    echo "[meshy]   status=$STATUS progress=$PROGRESS" >&2
    case "$STATUS" in
        SUCCEEDED)
            GLB_URL="$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('model_urls',{}).get('glb',''))" "$STATUS_JSON")"
            if [[ -z "$GLB_URL" ]]; then
                echo "[meshy] ERROR: SUCCEEDED but no glb url: $STATUS_JSON" >&2
                exit 4
            fi
            mkdir -p "$(dirname "$OUT_GLB")"
            curl -sS -L "$GLB_URL" -o "$OUT_GLB"
            echo "[meshy] OK -> $OUT_GLB ($(stat -c %s "$OUT_GLB") bytes)" >&2
            exit 0
            ;;
        FAILED|CANCELED|EXPIRED)
            echo "[meshy] ERROR: task $STATUS: $STATUS_JSON" >&2
            exit 5
            ;;
    esac
done

echo "[meshy] ERROR: task did not complete within 10 minutes" >&2
exit 6
