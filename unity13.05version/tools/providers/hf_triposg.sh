#!/usr/bin/env bash
# hf_triposg.sh — Hugging Face Spaces client for VAST-AI/TripoSG.
#
# Single-view image-to-3D via TripoSG (VAST AI's flow-based successor to TripoSR).
# Higher fidelity than TripoSR; runs server-side on HF GPU.
#
# Requires:
#   - HF_TOKEN env var (free read token: https://huggingface.co/settings/tokens)
#   - python3 with `gradio_client` (auto-installed)
#
# Usage:
#   hf_triposg.sh <input_image> <output_glb> [num_faces=20000]

set -euo pipefail

INPUT="${1:?Usage: $0 <input_image> <output_glb> [num_faces=20000]}"
OUT_GLB="${2:?Usage: $0 <input_image> <output_glb> [num_faces=20000]}"
NUM_FACES="${3:-20000}"

if [[ -z "${HF_TOKEN:-}" ]]; then
    echo "[hf_triposg] HF_TOKEN env var is not set." >&2
    echo "  Get one at: https://huggingface.co/settings/tokens (free, read scope)" >&2
    exit 64
fi

python3 -c "import gradio_client" 2>/dev/null || {
    echo "[hf_triposg] Installing gradio_client..." >&2
    python3 -m pip install --user --quiet "gradio_client>=1.4.0"
}

mkdir -p "$(dirname "$OUT_GLB")"

python3 - "$INPUT" "$OUT_GLB" "$NUM_FACES" << 'PY'
import os
import shutil
import sys
import time

from gradio_client import Client, file as gr_file

inp, out_glb, num_faces = sys.argv[1], sys.argv[2], int(sys.argv[3])
token = os.environ["HF_TOKEN"]

print(f"[hf_triposg] Connecting to VAST-AI/TripoSG ...", flush=True)
client = Client("VAST-AI/TripoSG", hf_token=token)

print(f"[hf_triposg] Submitting {inp} (faces={num_faces}) ...", flush=True)
t0 = time.time()
result = client.predict(
    gr_file(inp),
    42,            # seed
    7.0,           # guidance_scale
    50,            # num_inference_steps
    True,          # remove_background
    num_faces,     # face count target
    api_name="/run",
)
print(f"[hf_triposg] Generation finished in {time.time()-t0:.1f}s", flush=True)

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
    print(f"[hf_triposg] ERROR: no .glb in response: {result!r}", file=sys.stderr)
    sys.exit(3)

shutil.copy(candidates[0], out_glb)
print(f"[hf_triposg] OK -> {out_glb} ({os.path.getsize(out_glb)} bytes)")
PY
