---
name: img-to-3d-triposr
description: Generate a 3D mesh (GLB with vertex colors) from a single PNG/JPG image locally on CPU using open-source TripoSR. No API keys, no subscriptions. Useful for putting a "3D version" of a reference photo into a Unity project (via glTFast). Trigger when the user asks for a 3D model from a 2D image and refuses paid services.
---

# Image-to-3D with TripoSR (local, CPU, free)

When the user asks you to generate a 3D model from a single picture and
refuses paid image-to-3D APIs (Tripo3D, Meshy, Rodin, Unity AI), use
**TripoSR** locally.

TripoSR is the open-source LRM by VAST AI + Stability AI. MIT-licensed.
Runs in ~45 s per image on a 2-core CPU with no GPU. Outputs a GLB with
vertex colors that Unity can import via `com.unity.cloud.gltfast`.

## One-shot setup

Run [`tools/patch_triposr_cpu.sh`](../../tools/patch_triposr_cpu.sh) once
per VM / snapshot. It:

1. Installs CPU-only PyTorch.
2. Installs the rest of TripoSR's Python deps + PyMCubes (CPU substitute
   for the CUDA-only `torchmcubes`).
3. Installs `libgl1 / libegl1 / libegl1-mesa / libgles2-mesa` (needed
   only if you want `--bake-texture`).
4. Clones https://github.com/VAST-AI-Research/TripoSR.git to `~/TripoSR`.
5. Patches `tsr/models/isosurface.py` to fall back from `torchmcubes`
   to PyMCubes when CUDA isn't available.

## Usage

```bash
./tools/img_to_3d.sh <input_image> [output_dir] [mc_resolution]
```

Defaults: `output_dir=/tmp/triposr_out`, `mc_resolution=256`.

```bash
./tools/img_to_3d.sh ~/attachments/photo.png                # default 256
./tools/img_to_3d.sh ~/attachments/photo.png /tmp/out 128   # fast preview
./tools/img_to_3d.sh ~/attachments/photo.png /tmp/out 320   # higher detail
```

Output: `<output_dir>/0/mesh.glb` (vertex colors, no PBR maps) plus
`<output_dir>/0/input.png` (background-removed input).

Approximate timings on 2-core CPU, no GPU:

| mc-resolution | time | mesh size |
|---|---|---|
| 128 | ~25 s | ~170 KB |
| 256 | ~45 s | ~700 KB |
| 320 | ~80 s | ~1.2 MB |

## Pipeline: image → Unity scene

Full flow combining this skill with `glb-import-to-unity` and
`mesh-orient-scale`:

```bash
# 1. Generate
./tools/img_to_3d.sh ~/attachments/photo.png /tmp/out 256

# 2. Make sure Unity has the importer
./tools/mcp_call.sh package-add '{"packageId":"com.unity.cloud.gltfast"}'

# 3. Serve the file + expose port
./tools/serve_file.sh /tmp/out/0/mesh.glb 8765
# (then `deploy expose port=8765` to get a public URL with basic auth)

# 4. Download into Assets/ via script-execute — see skills/glb-import-to-unity/SKILL.md

# 5. Instantiate
./tools/mcp_call.sh assets-prefab-instantiate '{
    "prefabAssetPath":"Assets/1/mesh.glb",
    "gameObjectPath":"GeneratedFromImage",
    "position":{"x":0,"y":5,"z":0},
    "rotation":{"x":0,"y":0,"z":0},
    "scale":{"x":2.5,"y":2.5,"z":2.5}
}'

# 6. Orient upright (see skills/mesh-orient-scale/SKILL.md)
```

## Limitations to surface to the user

- **Single-view LRM** — the back of the model is hallucinated. The
  result is recognizable from the input angle and weaker from other
  angles.
- **Mesh quality is PS2 / early-PS3 era** (~17 k verts, 35 k tris at
  mc-resolution 256). No PBR maps — vertex colors only.
- **Face is heavily stylized** for anthropomorphic inputs.
- **TripoSR has no content filter** (it's just feedforward 3D
  reconstruction over open weights). Be careful when sharing the
  generated mesh / renders.
- **`--bake-texture` is fragile in headless environments.** It uses
  moderngl + EGL which may fail with `libGL.so not found`. Skip the
  flag and use vertex colors unless EGL is confirmed working.

## Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `torchmcubes` import error | Repo not patched | Run `patch_triposr_cpu.sh` |
| `libGL.so not found` | Bake-texture without EGL | Drop `--bake-texture` or install `libgl1 libegl1` |
| OOM during mesh extraction | mc-resolution too high for RAM | Drop to 256 or 128 |
| Output is just a blob | Input is non-object / not foreground-isolatable | Try a clearer reference image |

## Comparison with alternatives (as of 2026-05)

| Approach | Free? | Local? | Quality | Notes |
|---|---|---|---|---|
| **TripoSR (this skill)** | yes | yes | medium | reliable baseline |
| Hunyuan3D-2 HF Space | no (token + GPU quota) | no | high | anonymous access blocked |
| TripoSG HF Space | no (token) | no | high | anonymous blocked |
| TRELLIS HF Space | partial | no | very high | often paused |
| Tripo3D API | trial credits then paid | no | very high | NSFW filter strict |
| Meshy API | trial credits then paid | no | high | NSFW filter strict |
| Unity AI Assistant | requires Unity Muse subscription | no | high (Hunyuan 3D 3.0 Pro) | "best for simple props, not characters" |

Conclusion: **TripoSR is the only truly free, reliable, persona-friendly
image-to-3D path in 2026**. Recommend paid options only if the user
asks for AAA detail and is willing to pay.
