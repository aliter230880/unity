#!/usr/bin/env bash
# img_to_3d_dispatch.sh — unified image-to-3D dispatcher.
#
# Picks the best available provider based on what environment variables are
# set, what views you supplied, and an optional preference. Always falls back
# to local TripoSR (CPU-only, no API key) if all cloud paths fail.
#
# Provider order (default; better quality first):
#   1. Meshy.ai           — MESHY_API_KEY     (paid; AAA quality, PBR, A/T-pose)
#   2. HF Hunyuan3D-2mv   — HF_TOKEN          (free; multi-view, ~85-90% match)
#   3. HF TripoSG         — HF_TOKEN          (free; single-view, flow-based)
#   4. Neural4D           — NEURAL4D_API_KEY  (pay-as-go; 2K texture, watertight)
#   5. TripoSR local      — none (always)    (CPU, ~70% match, no cost)
#
# If you pass >=2 views, only providers that accept multi-view input are
# considered before falling back. If you pass --prefer X, that provider is
# tried first; on failure the dispatcher continues with the normal order.
#
# Usage:
#   img_to_3d_dispatch.sh --out OUT.glb [--prefer meshy|hf-mv|hf-sv|neural4d|local]
#                         [--mc 256] [--tex 1024] [--pose a-pose|t-pose]
#                         [--pbr] [--hd]
#                         IMAGE1 [IMAGE2 IMAGE3 ...]
#
# Examples:
#   # Single-view, take best available
#   img_to_3d_dispatch.sh --out out.glb photo.png
#
#   # Multi-view, prefer free Hunyuan3D-2mv
#   img_to_3d_dispatch.sh --out out.glb --prefer hf-mv \
#       front.png back.png left.png right.png
#
#   # Best paid quality
#   img_to_3d_dispatch.sh --out out.glb --prefer meshy --pbr --hd --pose a-pose photo.png

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROVIDERS_DIR="$SCRIPT_DIR/providers"

OUT_GLB=""
PREFER=""
MC_RES="256"
TEX_RES="1024"
POSE_MODE=""
HD_TEXTURE="false"
ENABLE_PBR="false"
INPUTS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --out)    OUT_GLB="$2"; shift 2 ;;
        --prefer) PREFER="$2"; shift 2 ;;
        --mc)     MC_RES="$2"; shift 2 ;;
        --tex)    TEX_RES="$2"; shift 2 ;;
        --pose)   POSE_MODE="$2"; shift 2 ;;
        --pbr)    ENABLE_PBR="true"; shift ;;
        --hd)     HD_TEXTURE="true"; shift ;;
        -h|--help) sed -n '2,30p' "$0"; exit 0 ;;
        --)       shift; INPUTS+=("$@"); break ;;
        -*)       echo "Unknown option: $1" >&2; exit 2 ;;
        *)        INPUTS+=("$1"); shift ;;
    esac
done

if [[ -z "$OUT_GLB" || ${#INPUTS[@]} -eq 0 ]]; then
    echo "Usage: $0 --out OUT.glb [opts] IMAGE1 [IMAGE2 ...]" >&2
    exit 2
fi

MAIN_INPUT="${INPUTS[0]}"
HAS_MULTIVIEW=$(( ${#INPUTS[@]} >= 2 ? 1 : 0 ))

# Catalogue of providers (id|requires_env|supports_multi|description)
declare -A AVAILABLE
AVAILABLE[meshy]="${MESHY_API_KEY:-}"
AVAILABLE[hf-mv]="${HF_TOKEN:-}"
AVAILABLE[hf-sv]="${HF_TOKEN:-}"
AVAILABLE[neural4d]="${NEURAL4D_API_KEY:-}"
AVAILABLE[local]="always"

try_provider() {
    local id="$1"
    case "$id" in
        meshy)
            [[ -n "${AVAILABLE[meshy]}" ]] || return 1
            echo "[dispatch] Trying Meshy.ai ..." >&2
            bash "$PROVIDERS_DIR/meshy_img_to_3d.sh" \
                "$MAIN_INPUT" "$OUT_GLB" "latest" "$POSE_MODE" "$HD_TEXTURE" "$ENABLE_PBR"
            ;;
        hf-mv)
            [[ -n "${AVAILABLE[hf-mv]}" ]] || return 1
            if (( HAS_MULTIVIEW == 0 )); then
                echo "[dispatch] hf-mv requires multiple views; skipping" >&2
                return 1
            fi
            echo "[dispatch] Trying HF Hunyuan3D-2mv ..." >&2
            bash "$PROVIDERS_DIR/hf_hunyuan3d_mv.sh" "$OUT_GLB" "${INPUTS[@]}"
            ;;
        hf-sv)
            [[ -n "${AVAILABLE[hf-sv]}" ]] || return 1
            echo "[dispatch] Trying HF TripoSG ..." >&2
            bash "$PROVIDERS_DIR/hf_triposg.sh" "$MAIN_INPUT" "$OUT_GLB"
            ;;
        neural4d)
            [[ -n "${AVAILABLE[neural4d]}" ]] || return 1
            echo "[dispatch] Trying Neural4D ..." >&2
            bash "$PROVIDERS_DIR/neural4d_img_to_3d.sh" "$MAIN_INPUT" "$OUT_GLB"
            ;;
        local)
            echo "[dispatch] Falling back to local TripoSR ..." >&2
            bash "$PROVIDERS_DIR/triposr_local.sh" "$MAIN_INPUT" "$OUT_GLB" "$MC_RES" "$TEX_RES"
            ;;
        *)
            echo "[dispatch] Unknown provider: $id" >&2
            return 2
            ;;
    esac
}

# Determine order
DEFAULT_ORDER=(meshy hf-mv hf-sv neural4d local)
ORDER=()
if [[ -n "$PREFER" ]]; then
    ORDER+=("$PREFER")
    for p in "${DEFAULT_ORDER[@]}"; do [[ "$p" != "$PREFER" ]] && ORDER+=("$p"); done
else
    ORDER=("${DEFAULT_ORDER[@]}")
fi

echo "[dispatch] views=${#INPUTS[@]} out=$OUT_GLB order=${ORDER[*]}" >&2
echo "[dispatch] keys: meshy=$([[ -n ${AVAILABLE[meshy]} ]] && echo yes || echo no) hf=$([[ -n ${AVAILABLE[hf-mv]} ]] && echo yes || echo no) neural4d=$([[ -n ${AVAILABLE[neural4d]} ]] && echo yes || echo no)" >&2

LAST_ERR=""
for p in "${ORDER[@]}"; do
    if try_provider "$p"; then
        echo "[dispatch] Success via $p -> $OUT_GLB" >&2
        exit 0
    fi
    LAST_ERR="$p"
done

echo "[dispatch] All providers failed; last attempt was: $LAST_ERR" >&2
exit 1
