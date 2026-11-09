#!/usr/bin/env bash
# patch_triposr_cpu.sh — установка TripoSR на CPU-only машине без CUDA.
#
# Выполнить один раз при подготовке snapshot'а / при первом запуске агента.
#
# Что делает:
#   1. Ставит CPU-сборку PyTorch.
#   2. Ставит остальные python-зависимости TripoSR + PyMCubes
#      (заменитель torchmcubes без требования CUDA).
#   3. Ставит системные libGL/EGL библиотеки + Xvfb (нужны для
#      --bake-texture, в т.ч. дев-версии для libGL.so/libEGL.so
#      symlink'ов — без них moderngl падает в "libGL.so not loaded").
#   4. Клонирует https://github.com/VAST-AI-Research/TripoSR.git в ~/TripoSR.
#   5. Патчит tsr/models/isosurface.py — fallback на PyMCubes когда
#      torchmcubes (CUDA only) недоступен.

set -euo pipefail

TRIPOSR_DIR="${TRIPOSR_DIR:-$HOME/TripoSR}"

echo "[patch_triposr_cpu] 1/4 PyTorch CPU"
pip install --quiet torch==2.6.0+cpu torchvision==0.21.0+cpu \
    --index-url https://download.pytorch.org/whl/cpu

echo "[patch_triposr_cpu] 2/4 Python deps"
pip install --quiet omegaconf einops transformers==4.45.2 trimesh \
    "rembg[cpu]" onnxruntime huggingface-hub xatlas==0.0.9 \
    imageio[ffmpeg] moderngl PyMCubes Pillow

echo "[patch_triposr_cpu] 3/4 System libs (libGL/EGL for moderngl + Xvfb for headless)"
if command -v sudo >/dev/null 2>&1; then
    # Runtime libs:
    sudo apt-get install -y libgl1 libegl1 libegl1-mesa libgles2-mesa libosmesa6 >/dev/null || true
    # -dev packages provide the unversioned libGL.so / libEGL.so symlinks
    # that glcontext (used by moderngl) dlopens. Without them you'll see
    # "libGL.so not loaded" from moderngl even though libGL.so.1 exists.
    sudo apt-get install -y libgl-dev libegl-dev libgles-dev libosmesa6-dev >/dev/null || true
    # Xvfb to provide a virtual X display for the EGL context.
    sudo apt-get install -y xvfb >/dev/null || true
fi

echo "[patch_triposr_cpu] 4/4 Clone + patch TripoSR"
if [ ! -d "$TRIPOSR_DIR" ]; then
    git clone https://github.com/VAST-AI-Research/TripoSR.git "$TRIPOSR_DIR"
fi

ISOSURFACE="$TRIPOSR_DIR/tsr/models/isosurface.py"
if ! grep -q "import mcubes" "$ISOSURFACE"; then
    python3 - "$ISOSURFACE" <<'PY'
import sys
path = sys.argv[1]
src = open(path).read()
old = "from torchmcubes import marching_cubes"
new = (
    "try:\n"
    "    from torchmcubes import marching_cubes\n"
    "except ImportError:\n"
    "    import mcubes\n"
    "    def marching_cubes(level, threshold):\n"
    "        v, f = mcubes.marching_cubes(level.detach().cpu().numpy(), threshold)\n"
    "        return torch.from_numpy(v.copy()).float(), torch.from_numpy(f.copy()).long()"
)
if old not in src:
    sys.exit("expected import line not found")
open(path, "w").write(src.replace(old, new))
print("patched", path)
PY
fi

echo "[patch_triposr_cpu] done. Test with: ~/img_to_3d.sh \"$TRIPOSR_DIR/examples/chair.png\" /tmp/triposr_test 128"
