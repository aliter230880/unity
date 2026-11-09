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

### Mode 1 — fast vertex-color GLB (default)

```bash
./tools/img_to_3d.sh <input_image> [output_dir] [mc_resolution]
```

Defaults: `output_dir=/tmp/triposr_out`, `mc_resolution=256`. Output:
`<output_dir>/0/mesh.glb` (vertex colors, no PBR maps).

```bash
./tools/img_to_3d.sh ~/attachments/photo.png                # default 256
./tools/img_to_3d.sh ~/attachments/photo.png /tmp/out 128   # fast preview
./tools/img_to_3d.sh ~/attachments/photo.png /tmp/out 320   # higher detail
```

Approximate timings on 2-core CPU, no GPU:

| mc-resolution | time | mesh size | notes |
|---|---|---|---|
| 128 | ~25 s | ~170 KB | quick preview, blocky |
| 256 | ~45 s | ~700 KB | default — best speed/quality on 7-GB RAM |
| 320 | ~80 s | ~1.2 MB | sharper, fits in 7 GB RAM only without --bake-texture |
| 512 | ~5 min | ~5 MB | OOM on 7 GB RAM; needs 16+ GB |

### Mode 2 — high-quality UV-textured OBJ + texture.png (recommended for human subjects)

Vertex colors at 17–27k vertices give a blurry result — the photo's
detail is averaged across triangles. **`--bake-texture` samples colors
from the original photo into a UV-mapped texture atlas**, which Unity
renders as a real PBR base-color map. The improvement on faces, skin,
fabric is dramatic.

```bash
cd ~/TripoSR
xvfb-run -a python3 run.py <input_image> \
    --device cpu \
    --output-dir /tmp/triposr_baked \
    --mc-resolution 256 \
    --bake-texture \
    --texture-resolution 1024 \
    --model-save-format obj
```

Output:
- `/tmp/triposr_baked/0/mesh.obj` (≈ 4 MB)
- `/tmp/triposr_baked/0/texture.png` (≈ 700 KB — a UV atlas with photo chunks)
- `/tmp/triposr_baked/0/input.png` (background-removed input)

Approximate timings on 2-core CPU:

| mc-resolution + texture | time | result | RAM peak |
|---|---|---|---|
| 256 + 1024 | ~4 min | sharp UV-mapped | ~5 GB |
| 256 + 2048 | ~6 min | very sharp | ~6.5 GB |
| 320 + 2048 | OOM on 7 GB | — | crashes |

**Convert OBJ + texture into a single GLB** (Unity prefers self-contained binary GLB over OBJ + MTL + PNG):

```bash
python3 << 'PY'
import trimesh
from PIL import Image
import os
out_dir = '/tmp/triposr_baked/0'
mesh = trimesh.load(f'{out_dir}/mesh.obj', force='mesh', process=False)
img = Image.open(f'{out_dir}/texture.png')
material = trimesh.visual.material.PBRMaterial(baseColorTexture=img)
mesh.visual = trimesh.visual.TextureVisuals(
    uv=mesh.visual.uv, image=img, material=material)
mesh.export(f'{out_dir}/mesh_textured.glb')
print(f'GLB: {os.path.getsize(f"{out_dir}/mesh_textured.glb")} bytes')
PY
```

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
- **`--bake-texture` needs an EGL context.** It uses moderngl + EGL
  which fails with `libGL.so not found` on most headless VMs out of
  the box. See "Setting up EGL for --bake-texture" below for the
  exact apt packages + `xvfb-run` invocation that works.

## Setting up EGL for `--bake-texture`

Default apt packages for `libegl1` install only the SONAME files
(`libGL.so.1`, `libEGL.so.1`), but glcontext (used by moderngl) does
`dlopen("libGL.so")` without version suffix — which only exists in
the `-dev` packages. Install the dev packages and add Xvfb:

```bash
sudo apt-get install -y \
    xvfb libgl-dev libegl-dev libgles-dev libosmesa6-dev \
    libgl1 libegl1 libegl1-mesa libgles2-mesa
```

Then run TripoSR under `xvfb-run` (provides a virtual X display):

```bash
xvfb-run -a python3 run.py <image> --bake-texture ...
```

Smoke-test EGL standalone:

```bash
xvfb-run -a python3 -c "
import moderngl
ctx = moderngl.create_context(standalone=True, backend='egl')
print('EGL ok:', ctx.info.get('GL_VERSION'))
"
# Expected: EGL ok: 4.5 (Core Profile) Mesa <version>
```

If this prints `EGL ok: ...`, `--bake-texture` will work.

## Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `torchmcubes` import error | Repo not patched | Run `patch_triposr_cpu.sh` |
| `libGL.so not loaded` (in moderngl) | Missing `-dev` symlinks | `sudo apt-get install libgl-dev libegl-dev libgles-dev libosmesa6-dev` |
| `EGL context creation failed` | No X / EGL display | Wrap command in `xvfb-run -a ...` |
| OOM during mesh extraction | mc-resolution too high for RAM | Drop to 256 or 128; if using `--bake-texture`, drop texture-resolution to 1024 |
| OOM at start of `bake_texture` | Texture resolution too high | Texture 2048 needs ~7 GB; on 8-GB VMs use 1024 |
| Output is just a blob | Input is non-object / not foreground-isolatable | Try a clearer reference image |
| `xatlas.export()` writes only `.obj`, not `.glb` | TripoSR uses xatlas for textured output | Use the trimesh post-processing snippet above to repackage as GLB |

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
