#!/usr/bin/env bash
# neural4d_img_to_3d.sh — Neural4D Image-to-3D API client.
#
# Commercial cloud service. Outputs are 2K-textured, manifold/watertight (3D
# print-ready). Cheaper per call than Meshy ($0.15/call pay-as-you-go).
#
# Requires:
#   - NEURAL4D_API_KEY env var. Get one at https://www.neural4d.com/.
#
# Usage:
#   neural4d_img_to_3d.sh <input_image_or_url> <output_glb> [format=glb] [resolution=high]
#
# Note: This client follows the Bearer-token + JSON-task-poll pattern that all
# major image-to-3D APIs use (Meshy/Neural4D/Tripo3D/Rodin). Endpoint and field
# names may need to be updated based on the current Neural4D docs at
# https://www.neural4d.com/api when you first set up.

set -euo pipefail

INPUT="${1:?Usage: $0 <input_image_or_url> <output_glb> [format=glb] [resolution=high]}"
OUT_GLB="${2:?Usage: $0 <input_image_or_url> <output_glb> [format=glb] [resolution=high]}"
FORMAT="${3:-glb}"
RESOLUTION="${4:-high}"

if [[ -z "${NEURAL4D_API_KEY:-}" ]]; then
    echo "[neural4d] NEURAL4D_API_KEY env var is not set." >&2
    echo "  Get one at: https://www.neural4d.com/" >&2
    exit 64
fi

API_BASE="${NEURAL4D_API_BASE:-https://api.neural4d.com/v1}"

# Build image_url field
if [[ "$INPUT" =~ ^https?:// ]]; then
    IMG_URL="$INPUT"
else
    if [[ ! -f "$INPUT" ]]; then
        echo "[neural4d] ERROR: input file not found: $INPUT" >&2
        exit 2
    fi
    case "${INPUT,,}" in
        *.png)         MIME="image/png" ;;
        *.jpg|*.jpeg)  MIME="image/jpeg" ;;
        *) echo "[neural4d] ERROR: input must be .png/.jpg/.jpeg" >&2; exit 2 ;;
    esac
    B64="$(base64 -w0 < "$INPUT")"
    IMG_URL="data:${MIME};base64,${B64}"
fi

REQ_BODY="$(python3 -c "
import json, sys
print(json.dumps({
    'image': sys.argv[1],
    'format': sys.argv[2],
    'resolution': sys.argv[3],
}))
" "$IMG_URL" "$FORMAT" "$RESOLUTION")"

echo "[neural4d] Submitting image-to-3d task..." >&2
RESP="$(curl -sS -X POST "$API_BASE/image-to-3d" \
    -H "Authorization: Bearer ${NEURAL4D_API_KEY}" \
    -H "Content-Type: application/json" \
    -d "$REQ_BODY")"
TASK_ID="$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('task_id') or d.get('id') or '')" "$RESP" 2>/dev/null || true)"
if [[ -z "$TASK_ID" ]]; then
    echo "[neural4d] ERROR: no task id in response: $RESP" >&2
    exit 3
fi
echo "[neural4d] task_id=$TASK_ID, polling..." >&2

DEADLINE=$(( $(date +%s) + 600 ))
while (( $(date +%s) < DEADLINE )); do
    sleep 4
    STATUS_JSON="$(curl -sS "$API_BASE/tasks/$TASK_ID" -H "Authorization: Bearer ${NEURAL4D_API_KEY}")"
    STATUS="$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('status',''))" "$STATUS_JSON" 2>/dev/null || echo "")"
    echo "[neural4d]   status=$STATUS" >&2
    case "$STATUS" in
        success|completed|SUCCEEDED)
            GLB_URL="$(python3 -c "
import json,sys
d = json.loads(sys.argv[1])
# Try several common shapes
for k in ('url','glb_url','model_url','result_url','download_url'):
    if k in d and d[k]: print(d[k]); break
else:
    r = d.get('result',{}) or d.get('output',{}) or {}
    for k in ('url','glb','model_url','download_url'):
        if r.get(k): print(r[k]); break
    else: print('')
" "$STATUS_JSON")"
            if [[ -z "$GLB_URL" ]]; then
                echo "[neural4d] ERROR: success but no model url: $STATUS_JSON" >&2
                exit 4
            fi
            mkdir -p "$(dirname "$OUT_GLB")"
            curl -sS -L "$GLB_URL" -o "$OUT_GLB"
            echo "[neural4d] OK -> $OUT_GLB ($(stat -c %s "$OUT_GLB") bytes)" >&2
            exit 0
            ;;
        failed|error|FAILED)
            echo "[neural4d] ERROR: task failed: $STATUS_JSON" >&2
            exit 5
            ;;
    esac
done

echo "[neural4d] ERROR: task did not complete within 10 minutes" >&2
exit 6
