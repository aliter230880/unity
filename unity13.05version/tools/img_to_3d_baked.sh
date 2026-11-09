#!/usr/bin/env bash
# img_to_3d_baked.sh — высококачественная локальная генерация 3D-меша с
# запечённой UV-текстурой из одиночной картинки. Заметно лучше вершинных
# цветов на людях/лицах/одежде.
#
# Под капотом тот же TripoSR, но с `--bake-texture` и упаковкой
# `mesh.obj + texture.png` в самодостаточный GLB через trimesh.
#
# Usage:
#   ./img_to_3d_baked.sh <input_image_path> [output_dir] [mc_resolution] [texture_resolution]
#
# Examples:
#   ./img_to_3d_baked.sh ~/attachments/photo.png
#   ./img_to_3d_baked.sh ~/attachments/photo.png /tmp/out 256 1024
#   ./img_to_3d_baked.sh ~/attachments/photo.png /tmp/out 256 2048   # лучше, дольше, ~6.5 GB RAM
#
# Defaults:
#   output_dir         = /tmp/triposr_baked
#   mc_resolution      = 256 (256 — потолок для 7-GB RAM с bake-texture)
#   texture_resolution = 1024 (2048 = резче, но ~6.5 GB RAM)
#
# Output:
#   <output_dir>/0/mesh_textured.glb — финальный самодостаточный GLB (UV + текстура внутри)
#   <output_dir>/0/mesh.obj          — промежуточный OBJ (можно удалить)
#   <output_dir>/0/texture.png       — UV-атлас (можно удалить)
#   <output_dir>/0/input.png         — изображение с удалённым фоном
#
# Зависимости (см. patch_triposr_cpu.sh):
#   - TripoSR клонирован в ~/TripoSR с CPU-патчем isosurface.py
#   - libgl-dev, libegl-dev, libgles-dev, libosmesa6-dev, xvfb
#   - python: trimesh, Pillow, moderngl, xatlas
#
# Smoke-test EGL (если что-то идёт не так):
#   xvfb-run -a python3 -c "import moderngl; print(moderngl.create_context(standalone=True, backend='egl').info['GL_VERSION'])"

set -euo pipefail

INPUT="${1:?Usage: $0 <input_image> [output_dir] [mc_resolution] [texture_resolution]}"
OUTDIR="${2:-/tmp/triposr_baked}"
MCRES="${3:-256}"
TEXRES="${4:-1024}"

if [ ! -f "$INPUT" ]; then
    echo "ERROR: input file not found: $INPUT" >&2
    exit 1
fi

TRIPOSR_DIR="${TRIPOSR_DIR:-$HOME/TripoSR}"
if [ ! -d "$TRIPOSR_DIR" ]; then
    echo "ERROR: $TRIPOSR_DIR not present. Run patch_triposr_cpu.sh first." >&2
    exit 1
fi

if ! command -v xvfb-run >/dev/null 2>&1; then
    echo "ERROR: xvfb-run not installed. Run patch_triposr_cpu.sh (or sudo apt-get install xvfb)." >&2
    exit 1
fi

mkdir -p "$OUTDIR"
echo "[img_to_3d_baked] Generating 3D mesh + UV texture from $INPUT (mc=$MCRES, tex=$TEXRES)..."

cd "$TRIPOSR_DIR"
xvfb-run -a python3 run.py "$INPUT" \
    --device cpu \
    --output-dir "$OUTDIR" \
    --mc-resolution "$MCRES" \
    --bake-texture \
    --texture-resolution "$TEXRES" \
    --model-save-format obj

OBJ="$OUTDIR/0/mesh.obj"
TEX="$OUTDIR/0/texture.png"
GLB="$OUTDIR/0/mesh_textured.glb"

if [ ! -f "$OBJ" ] || [ ! -f "$TEX" ]; then
    echo "ERROR: TripoSR did not produce expected files in $OUTDIR/0/" >&2
    exit 1
fi

echo "[img_to_3d_baked] Packaging OBJ + texture.png into single GLB..."
python3 - "$OBJ" "$TEX" "$GLB" <<'PY'
import sys
import os
import trimesh
from PIL import Image

obj_path, tex_path, glb_path = sys.argv[1], sys.argv[2], sys.argv[3]
mesh = trimesh.load(obj_path, force='mesh', process=False)
img = Image.open(tex_path)
material = trimesh.visual.material.PBRMaterial(baseColorTexture=img)
if hasattr(mesh.visual, 'uv') and mesh.visual.uv is not None:
    mesh.visual = trimesh.visual.TextureVisuals(uv=mesh.visual.uv, image=img, material=material)
else:
    print("WARNING: mesh has no UVs; output will lack texture mapping", file=sys.stderr)
mesh.export(glb_path)
print(f"verts={len(mesh.vertices)} faces={len(mesh.faces)} glb={os.path.getsize(glb_path)} bytes")
PY

if [ ! -f "$GLB" ]; then
    echo "ERROR: GLB not produced at $GLB" >&2
    exit 1
fi
echo "[img_to_3d_baked] Success -> $GLB ($(du -h "$GLB" | cut -f1))"
