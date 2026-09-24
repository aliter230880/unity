#!/usr/bin/env bash
# triposr_local.sh — local TripoSR provider (CPU-only, no API key required).
#
# Always available fallback. Quality: PS2/early-PS3 era; texture quality via
# --bake-texture is dramatically better than vertex colours.
#
# Usage:
#   triposr_local.sh <input_image> <output_glb> [mc_resolution=256] [texture_resolution=1024]
#
# Requires: TripoSR cloned at $TRIPOSR_DIR (default $HOME/TripoSR) with CPU
# PyTorch and EGL stack installed via tools/patch_triposr_cpu.sh.

set -euo pipefail
INPUT="${1:?Usage: $0 <input_image> <output_glb> [mc_res=256] [tex_res=1024]}"
OUT_GLB="${2:?Usage: $0 <input_image> <output_glb> [mc_res=256] [tex_res=1024]}"
MCRES="${3:-256}"
TEXRES="${4:-1024}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
TMP_OUT="$(mktemp -d)"
trap 'rm -rf "$TMP_OUT"' EXIT

# Delegate to the baked-texture pipeline (already proven on CPU).
bash "$SCRIPT_DIR/img_to_3d_baked.sh" "$INPUT" "$TMP_OUT" "$MCRES" "$TEXRES" >&2

# img_to_3d_baked.sh writes to "$TMP_OUT/0/mesh_textured.glb"
SRC_GLB="$TMP_OUT/0/mesh_textured.glb"
if [[ ! -s "$SRC_GLB" ]]; then
    echo "[triposr_local] ERROR: mesh not produced at $SRC_GLB" >&2
    exit 2
fi

mkdir -p "$(dirname "$OUT_GLB")"
cp "$SRC_GLB" "$OUT_GLB"
echo "[triposr_local] OK -> $OUT_GLB ($(stat -c %s "$OUT_GLB") bytes)" >&2
