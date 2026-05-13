#!/usr/bin/env bash
# hf_hunyuan3d_mv.sh — Hugging Face Spaces client for Hunyuan3D-2mv (Tencent).
#
# Calls the public Gradio API of `tencent/Hunyuan3D-2mv` to generate a 3D mesh
# from multiple view images. Multi-view input gives substantially better back/
# side geometry than single-view models (~85-90% photo correspondence vs ~70%
# for TripoSR).
#
# Requires:
#   - HF_TOKEN env var (free read token: https://huggingface.co/settings/tokens)
#   - python3 with `gradio_client` installed (auto-installed on first run)
#
# Usage:
#   hf_hunyuan3d_mv.sh <output_glb> <front.png> [back.png] [left.png] [right.png]
#
# At least the front view is required. Provide more views (in any order) to
# improve coverage. The space accepts up to 4 views.
#
# Notes:
#   - Anonymous calls return "GPU duration > 90s" errors. A free token unlocks
#     ~5 minutes of GPU time per day, plenty for a few generations.
#   - Spaces occasionally pause. If the request fails with "Space is sleeping"
#     or 503, just retry — first request wakes the container.

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

from gradio_client import Client, file as gr_file

out_glb = sys.argv[1]
views = sys.argv[2:]
token = os.environ["HF_TOKEN"]

# Pad to 4 views (front, back, left, right). Repeat last available view for missing slots.
slots = views + [views[-1]] * (4 - len(views))
slots = slots[:4]

print(f"[hf_hunyuan3d_mv] Connecting to tencent/Hunyuan3D-2mv ...", flush=True)
client = Client("tencent/Hunyuan3D-2mv", hf_token=token)

print(f"[hf_hunyuan3d_mv] Submitting {len(views)} unique views (padded to 4 slots)...", flush=True)
t0 = time.time()
# The exact API signature depends on the Space; this is a representative call.
# Adjust `api_name` if upstream changes their endpoint.
result = client.predict(
    gr_file(slots[0]),
    gr_file(slots[1]),
    gr_file(slots[2]),
    gr_file(slots[3]),
    1234,           # seed
    20,             # num_inference_steps
    7.5,            # guidance_scale
    True,           # remove_background
    api_name="/generation_all",
)
print(f"[hf_hunyuan3d_mv] Generation finished in {time.time()-t0:.1f}s", flush=True)

# result is typically (gradio_file_path_to_glb, ...). Locate the .glb in the tuple.
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
PY
