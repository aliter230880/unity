#!/usr/bin/env bash
# tripo3d_img_to_3d.sh — Tripo3D platform Image-to-3D API client.
#
# Tripo3D is the commercial cloud service from VAST AI (the same team that
# released open-source TripoSR/TripoSG). Currently the highest-quality
# image-to-3D service on the market: PBR textures, A/T-pose option, GLB/FBX/
# OBJ output, automatic rigging available.
#
# Pricing (2026):
#   - Basic (free):       300 credits/mo  (~10 generations)
#   - Professional:       3000 credits/mo ($15.90)
#   - Advanced:           8000 credits/mo ($39.90)
#   - Pay-as-go:          ~1000 credits / $5
#   - image_to_model task: ~30-50 credits depending on params
#
# Requires:
#   - TRIPO3D_API_KEY env var (key format: "tsk_...").
#     Get one at https://platform.tripo3d.ai/api-key (free signup).
#   - curl, python3.
#
# Usage:
#   tripo3d_img_to_3d.sh <input_image_or_url> <output_glb> \
#       [texture_quality=detailed] [model_version=v2.5-20250123] [face_limit=0]
#
# For multi-view (much higher fidelity), use the dispatcher with --prefer
# tripo3d-mv and pass 2-4 view images.
#
# Docs: https://platform.tripo3d.ai/docs (and https://api.tripo3d.ai/v2/openapi)

set -euo pipefail

INPUT="${1:?Usage: $0 <input_image_or_url> <output_glb> [tex_quality] [model_version] [face_limit]}"
OUT_GLB="${2:?Usage: $0 <input_image_or_url> <output_glb> [tex_quality] [model_version] [face_limit]}"
TEX_QUALITY="${3:-detailed}"   # "standard" | "detailed"
MODEL_VERSION="${4:-v2.5-20250123}"
FACE_LIMIT="${5:-0}"           # 0 = no cap

if [[ -z "${TRIPO3D_API_KEY:-}" ]]; then
    echo "[tripo3d] TRIPO3D_API_KEY env var is not set." >&2
    echo "  Get one at: https://platform.tripo3d.ai/api-key" >&2
    exit 64
fi

API="https://api.tripo3d.ai/v2/openapi"

# Step 1: upload local image -> image_token (skip if input is already URL)
if [[ "$INPUT" =~ ^https?:// ]]; then
    IMAGE_URL="$INPUT"
    UPLOAD_BODY="$(python3 -c "import json,sys; print(json.dumps({'type':'image_to_model','file':{'type':'png','url':sys.argv[1]},'texture_quality':sys.argv[2],'model_version':sys.argv[3]}))" "$IMAGE_URL" "$TEX_QUALITY" "$MODEL_VERSION")"
else
    if [[ ! -f "$INPUT" ]]; then
        echo "[tripo3d] ERROR: input file not found: $INPUT" >&2
        exit 2
    fi
    case "${INPUT,,}" in
        *.png)         MIME="image/png"; TYPE="png" ;;
        *.jpg|*.jpeg)  MIME="image/jpeg"; TYPE="jpg" ;;
        *.webp)        MIME="image/webp"; TYPE="webp" ;;
        *) echo "[tripo3d] ERROR: input must be .png/.jpg/.webp" >&2; exit 2 ;;
    esac
    echo "[tripo3d] Uploading $INPUT ..." >&2
    UP_RESP="$(curl -sS -X POST "$API/upload/sts" \
        -H "Authorization: Bearer ${TRIPO3D_API_KEY}" \
        -F "file=@${INPUT};type=${MIME}" --max-time 120)"
    IMAGE_TOKEN="$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('data',{}).get('image_token',''))" "$UP_RESP")"
    if [[ -z "$IMAGE_TOKEN" ]]; then
        echo "[tripo3d] ERROR: upload failed: $UP_RESP" >&2
        exit 3
    fi
    echo "[tripo3d] image_token=$IMAGE_TOKEN" >&2
    UPLOAD_BODY="$(python3 -c "
import json, sys
body = {
    'type': 'image_to_model',
    'file': {'type': sys.argv[1], 'file_token': sys.argv[2]},
    'texture_quality': sys.argv[3],
    'model_version': sys.argv[4],
}
fl = int(sys.argv[5])
if fl > 0:
    body['face_limit'] = fl
print(json.dumps(body))
" "$TYPE" "$IMAGE_TOKEN" "$TEX_QUALITY" "$MODEL_VERSION" "$FACE_LIMIT")"
fi

# Step 2: create task
echo "[tripo3d] Creating image_to_model task (texture=$TEX_QUALITY model=$MODEL_VERSION face_limit=$FACE_LIMIT)..." >&2
TASK_RESP="$(curl -sS -X POST "$API/task" \
    -H "Authorization: Bearer ${TRIPO3D_API_KEY}" \
    -H "Content-Type: application/json" \
    -d "$UPLOAD_BODY" --max-time 60)"
TASK_ID="$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('data',{}).get('task_id',''))" "$TASK_RESP")"
if [[ -z "$TASK_ID" ]]; then
    echo "[tripo3d] ERROR: task creation failed: $TASK_RESP" >&2
    exit 4
fi
echo "[tripo3d] task_id=$TASK_ID, polling..." >&2

# Step 3: poll
DEADLINE=$(( $(date +%s) + 900 ))   # 15 min cap
while (( $(date +%s) < DEADLINE )); do
    sleep 5
    ST="$(curl -sS "$API/task/$TASK_ID" -H "Authorization: Bearer ${TRIPO3D_API_KEY}" --max-time 30)"
    STATUS="$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('data',{}).get('status',''))" "$ST" 2>/dev/null || echo "")"
    PROGRESS="$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('data',{}).get('progress',''))" "$ST" 2>/dev/null || echo "")"
    echo "[tripo3d]   status=$STATUS progress=$PROGRESS" >&2
    case "$STATUS" in
        success)
            GLB_URL="$(python3 -c "
import json, sys
d = json.loads(sys.argv[1]).get('data', {})
out = d.get('output') or {}
for k in ('pbr_model', 'model', 'rendered_image', 'base_model'):
    v = out.get(k)
    if isinstance(v, dict): v = v.get('url')
    if v and isinstance(v, str): print(v); break
else:
    print('')
" "$ST")"
            if [[ -z "$GLB_URL" ]]; then
                echo "[tripo3d] ERROR: success but no model url: $ST" >&2
                exit 5
            fi
            mkdir -p "$(dirname "$OUT_GLB")"
            curl -sS -L "$GLB_URL" -o "$OUT_GLB" --max-time 300
            echo "[tripo3d] OK -> $OUT_GLB ($(stat -c %s "$OUT_GLB") bytes)" >&2
            exit 0
            ;;
        failed|cancelled|expired)
            echo "[tripo3d] ERROR: task $STATUS: $ST" >&2
            exit 6
            ;;
    esac
done

echo "[tripo3d] ERROR: task did not complete within 15 minutes" >&2
exit 7
