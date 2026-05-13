#!/usr/bin/env bash
# img_to_3d.sh — локальная генерация 3D-меша из одиночной картинки (PNG/JPG)
# через open-source TripoSR. Без API-ключей и подписок.
#
# Usage:
#   ./img_to_3d.sh <input_image_path> [output_dir] [mc_resolution]
#
# Examples:
#   ./img_to_3d.sh ~/attachments/photo.png
#   ./img_to_3d.sh photo.png /tmp/out 256
#
# Defaults:
#   output_dir     = /tmp/triposr_out
#   mc_resolution  = 256 (128 fast, 256 normal, 320 detailed)
#
# Output:
#   <output_dir>/0/mesh.glb   — 3D-меш с vertex-цветами (импортируется в Unity через glTFast)
#   <output_dir>/0/input.png  — изображение с удалённым фоном
#
# Подготовка окружения (выполнить один раз — см. patch_triposr_cpu.sh).

set -euo pipefail

INPUT="${1:?Usage: $0 <input_image> [output_dir] [mc_resolution]}"
OUTDIR="${2:-/tmp/triposr_out}"
MCRES="${3:-256}"

if [ ! -f "$INPUT" ]; then
    echo "ERROR: input file not found: $INPUT" >&2
    exit 1
fi

TRIPOSR_DIR="${TRIPOSR_DIR:-$HOME/TripoSR}"
if [ ! -d "$TRIPOSR_DIR" ]; then
    echo "ERROR: $TRIPOSR_DIR not present. Run patch_triposr_cpu.sh first." >&2
    exit 1
fi

mkdir -p "$OUTDIR"
echo "[img_to_3d] Generating 3D mesh from $INPUT (mc-resolution=$MCRES)..."
cd "$TRIPOSR_DIR"
python3 run.py "$INPUT" \
    --device cpu \
    --output-dir "$OUTDIR" \
    --mc-resolution "$MCRES" \
    --model-save-format glb

GLB="$OUTDIR/0/mesh.glb"
if [ ! -f "$GLB" ]; then
    echo "ERROR: GLB not produced at $GLB" >&2
    exit 1
fi
echo "[img_to_3d] Success -> $GLB ($(du -h "$GLB" | cut -f1))"
