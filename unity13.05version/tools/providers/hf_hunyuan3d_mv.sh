#!/usr/bin/env bash
# hf_hunyuan3d_mv.sh — Hugging Face Spaces client for Hunyuan3D-2 (Tencent).
#
# Calls the public Gradio API of `tencent/Hunyuan3D-2` Space to generate a 3D
# mesh from one or more view images. Multi-view input gives substantially
# better back/side geometry than single-view models.
#
# Verified working (2026-05-13):
#   - Space:     tencent/Hunyuan3D-2 (not -2mv, see note below)
#   - Endpoint:  /shape_generation
#                Returns untextured white_mesh.glb only.
#                Time: ~15s for 4 views @ octree_resolution=256.
#   - /generation_all (textured mesh) currently returns AppError "NameError"
#     — server-side bug in their Gradio wrapper. /shape_generation works.
#     Workaround: bake texture locally from MV images via bake_mv_texture.sh.
#
# Requires:
#   - HF_TOKEN env var (free read token: https://huggingface.co/settings/tokens)
#   - python3 with `gradio_client` installed (auto-installed on first run)
#
# Usage:
#   hf_hunyuan3d_mv.sh <output_glb> <front.png> [back.png] [left.png] [right.png]
#
# At least the front view is required. The endpoint also requires the front
# image be passed as the single fallback `image` parameter (validation in the
# upstream Space requires either caption or image to be set).
#
# Notes:
#   - Initial cold-start on the Space adds ~30-60s latency.
#   - HF free tier limits anonymous GPU usage; a token gives ~5 GPU-min/day.
#   - The "-2mv" sibling Space (multi-view only) was in RUNTIME_ERROR state at
#     time of writing. The full Hunyuan3D-2 Space supports MV via 4 mv_image_*
#     slots — that's what we use here.

set -euo pipefail

if [[ $# -lt 2 ]]; then
    echo "Usage: $0 <output_glb> <front.png> [back.png] [left.png] [right.png]" >&2
    exit 1
fi

OUT_GLB="$1"; shift
VIEWS=("$@")

if [[ -z "${HF_TOKEN:-}" ]]; then
    echo "[hf_hunyuan3d_mv] HF_TOKEN env var is not set." >&2
    echo "  Get one at: https://huggingface.co/settings/tokens (free, read scope)" >&2
    exit 64
fi

# Ensure gradio_client is available
python3 -c "import gradio_client" 2>/dev/null || {
    echo "[hf_hunyuan3d_mv] Installing gradio_client..." >&2
    python3 -m pip install --user --quiet "gradio_client>=1.4.0"
}

mkdir -p "$(dirname "$OUT_GLB")"

python3 - "$OUT_GLB" "${VIEWS[@]}" << 'PY'
import os
import shutil
import sys
import time

from gradio_client import Client, handle_file

out_glb = sys.argv[1]
views = sys.argv[2:]
token = os.environ["HF_TOKEN"]

# Map views to roles: front, back, left, right
front = views[0]
back  = views[1] if len(views) > 1 else None
left  = views[2] if len(views) > 2 else None
right = views[3] if len(views) > 3 else None

print(f"[hf_hunyuan3d_mv] Connecting to tencent/Hunyuan3D-2 ...", flush=True)
# gradio_client API: token=... (NOT hf_token)
client = Client("tencent/Hunyuan3D-2", token=token, verbose=False)

print(f"[hf_hunyuan3d_mv] Submitting {len(views)} view(s) to /shape_generation...", flush=True)
t0 = time.time()

# Signature (from `client.view_api()`):
#   /shape_generation(caption, image, mv_image_front, mv_image_back,
#                     mv_image_left, mv_image_right, steps, guidance_scale,
#                     seed, octree_resolution, check_box_rembg, num_chunks,
#                     randomize_seed) -> (file, output, mesh_stats, seed)
#
# IMPORTANT: the Space validates that EITHER caption OR `image` is set, even
# when MV slots are provided. We always pass the front image to `image` to
# satisfy that check.
result = client.predict(
    None,                                # caption
    handle_file(front),                  # image (required validation)
    handle_file(front),                  # mv_image_front
    handle_file(back)  if back  else None,
    handle_file(left)  if left  else None,
    handle_file(right) if right else None,
    30,        # steps (1-100)
    5.0,       # guidance_scale
    1234,      # seed (0..1e7)
    256,       # octree_resolution (16-512); 256 is sweet spot for body
    True,      # check_box_rembg
    8000,      # num_chunks (1000-5000000)
    False,     # randomize_seed
    api_name="/shape_generation",
)

elapsed = time.time() - t0
print(f"[hf_hunyuan3d_mv] Generation finished in {elapsed:.1f}s", flush=True)

# Locate the .glb in the response
candidates = []
def walk(obj):
    if isinstance(obj, (list, tuple)):
        for x in obj: walk(x)
    elif isinstance(obj, dict):
        for v in obj.values(): walk(v)
    elif isinstance(obj, str) and obj.lower().endswith(".glb") and os.path.exists(obj):
        candidates.append(obj)
walk(result)

if not candidates:
    print(f"[hf_hunyuan3d_mv] ERROR: no .glb in response: {result!r}", file=sys.stderr)
    sys.exit(3)

shutil.copy(candidates[0], out_glb)
size = os.path.getsize(out_glb)
print(f"[hf_hunyuan3d_mv] OK -> {out_glb} ({size} bytes)")
print(f"[hf_hunyuan3d_mv] NOTE: result is untextured. To add texture, run:")
print(f"[hf_hunyuan3d_mv]   bake_mv_texture.sh {out_glb} <front.png> <back.png> <left.png> <right.png>")
PY
