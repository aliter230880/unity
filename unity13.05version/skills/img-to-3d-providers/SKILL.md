---
name: img-to-3d-providers
description: Multi-provider image-to-3D pipeline with automatic fallback. Picks the best available service (Tripo3D > Meshy > HF Hunyuan3D-2mv multi-view > HF TripoSG > Neural4D > local TripoSR) based on which API keys are set in the environment. Always falls back to local CPU TripoSR if no cloud paths are available, so something always succeeds.
---

# img-to-3d-providers

Unified image-to-3D pipeline with **automatic provider selection**. The dispatcher script `tools/img_to_3d_dispatch.sh` examines available environment variables and the type of input (single image vs multi-view), then tries providers in quality order until one succeeds.

## When to use

- You have one or more reference images of a real object/character and want a `.glb` mesh
- You want the result regardless of which paid services / free tokens are available
- You want one stable interface even as new providers come and go

## Quick start

```bash
# Single-view, use whatever's available (always falls back to TripoSR)
tools/img_to_3d_dispatch.sh --out out.glb photo.png

# Multi-view, prefer free Hunyuan3D-2mv (needs HF_TOKEN)
tools/img_to_3d_dispatch.sh --out out.glb --prefer hf-mv \
    views/front.png views/back.png views/left.png views/right.png

# Paid AAA — Meshy with PBR + 4K texture + A-pose
tools/img_to_3d_dispatch.sh --out out.glb --prefer meshy \
    --pbr --hd --pose a-pose photo.png

# Paid AAA — Tripo3D
tools/img_to_3d_dispatch.sh --out out.glb --prefer tripo3d photo.png
```

## Provider matrix

| Provider | Env var | Cost | Quality | Multi-view? | CPU local? |
|---|---|---|---|---|---|
| **Tripo3D** | `TRIPO3D_API_KEY` | Paid (~$0.005/call after 1000-credit pack) | AAA, PBR, A/T-pose | Yes (multiview_to_model) | No |
| **Meshy.ai** | `MESHY_API_KEY` | Free 200 cr/mo, then $20/mo | AAA, PBR, 4K, A/T-pose | No (single-image only) | No |
| **HF Hunyuan3D-2mv** | `HF_TOKEN` (free) | Free 5min GPU/day | Very good, multi-view | **Yes (1-4 views)** | No |
| **HF TripoSG** | `HF_TOKEN` (free) | Free 5min GPU/day | Very good (flow-based) | No | No |
| **Neural4D** | `NEURAL4D_API_KEY` | $0.15/call | Good, 2K, watertight | No | No |
| **TripoSR local** | — | Free, always | PS2/PS3-era, baked UV | No | **Yes** |

## Files

- `tools/img_to_3d_dispatch.sh` — main dispatcher (env-aware, multi-provider)
- `tools/providers/tripo3d_img_to_3d.sh` — Tripo3D API client
- `tools/providers/meshy_img_to_3d.sh` — Meshy.ai client
- `tools/providers/hf_hunyuan3d_mv.sh` — HF Hunyuan3D-2mv multi-view client
- `tools/providers/hf_triposg.sh` — HF TripoSG single-view client
- `tools/providers/neural4d_img_to_3d.sh` — Neural4D client
- `tools/providers/triposr_local.sh` — wraps `img_to_3d_baked.sh` (always available)
- `tools/bake_mv_texture.sh` — helper to project 4 input photos onto an untextured mesh (use after Hunyuan3D-2 MV; see below)

## Getting API keys (free / cheap paths)

### Hugging Face (best free path)

1. Sign up at https://huggingface.co/join (email only, no card)
2. https://huggingface.co/settings/tokens → New token → Read scope → copy `hf_...`
3. `export HF_TOKEN=hf_...`

Free quota: ~5 min GPU/day per account. Anonymous requests get rejected with "GPU duration > 90s" errors — token is required.

### Tripo3D (cheapest paid)

1. https://platform.tripo3d.ai/ → sign up → **API Keys** → New → copy `tsk_...`
2. **Crucial**: web Basic plan's 300 credits/mo are NOT usable through the API. You must buy a separate API-credit pack (~$5 for 1000 credits = ~20-30 image-to-3d calls).
3. `export TRIPO3D_API_KEY=tsk_...`

### Meshy

1. https://www.meshy.ai/ → sign up → Settings → API → New key
2. Free 200 credits/mo (~10 generations). Pro: $20/mo for 1000.
3. `export MESHY_API_KEY=msk_...`

### Neural4D

1. https://www.neural4d.com/api → sign up → get key
2. Pay-as-go ~$0.15/call.
3. `export NEURAL4D_API_KEY=...`

## Dispatcher logic

Default order (best quality first):
```
tripo3d  →  meshy  →  hf-mv  →  hf-sv  →  neural4d  →  local (TripoSR)
```

A provider is **skipped** if:
- Its required env var is not set
- It is `hf-mv` but only 1 input view was provided (Hunyuan3D-2mv needs multi-view)

With `--prefer X`, `X` is tried first; on failure the normal order continues.

## Integration with Unity

After the dispatcher writes a `.glb`, use the existing flow from `skills/glb-import-to-unity/`:

1. Serve the file via `deploy expose <port>` or any HTTP server reachable from Unity
2. Run a Unity `script-execute` that downloads via `UnityWebRequest` and places it under `Assets/`
3. Trigger `AssetDatabase.ImportAsset` and instantiate the imported prefab
4. Apply the mesh orientation/scale logic from `skills/mesh-orient-scale/`

## Quality notes (empirical, from a magenta-bodysuit character test)

Measured on the same input photo across providers:

- **TripoSR local + bake-texture** (single-view, 2048 UV atlas): mesh geometry is a deformed blob; texture covers it up. 51k verts, 65k faces. ~60-70% photo correspondence. Acceptable when no GPU is available but topology is unusable for animation.
- **HF Hunyuan3D-2 MV** (4 views, `/shape_generation`): clean anatomical geometry of a standing figure with face/hair/torso/hips/legs visible from all 4 sides. 115k verts, 396k faces. ~85% photo correspondence on shape alone (no texture). Generation ~12-15s on the Space's GPU.
- **HF Hunyuan3D-2 MV + `bake_mv_texture.sh`**: adds vertex colors projected from the 4 input photos. Bodysuit color, belt, hair, skin tone are all recovered to ~85% accuracy. Final mesh: 169k verts after UV unwrap, ~7 MB GLB. Total time including bake: ~60s.
- **Tripo3D / Meshy** with single image: ~85-95% (proprietary multi-view distillation on cloud).

The biggest quality jump is multi-view input. If you only have one photo, paid services beat TripoSR significantly but free single-view HF (TripoSG) is the next best step up from TripoSR.

## Hunyuan3D-2 multi-view recipe (verified end-to-end, 2026-05-13)

The `tencent/Hunyuan3D-2` Space exposes `/generation_all` (shape + texture) and `/shape_generation` (shape only). As of 2026-05-13, `/generation_all` returns `AppError: 'NameError'` consistently — their Gradio wrapper has a server-side bug. `/shape_generation` is stable. The workaround:

```bash
# 1) Generate the untextured shape from 4 views (front/back/left/right)
HF_TOKEN=hf_xxx tools/providers/hf_hunyuan3d_mv.sh \
    /tmp/shape.glb front.png back.png left.png right.png

# 2) Project the 4 photos back onto the geometry as vertex colors
tools/bake_mv_texture.sh /tmp/shape.glb /tmp/colored.glb \
    front.png back.png left.png right.png
```

Key API details (worth remembering, since they're not in the Space's UI):
- `gradio_client.Client` parameter is `token=`, not `hf_token=` (changes between gradio versions; use `inspect.signature` to verify)
- `/shape_generation` validates that EITHER `caption` or `image` is set, even when MV slots are provided — always pass the front view as the `image` argument
- `octree_resolution=256` is the sweet spot for full-body humans (512 is marginally sharper but 4x slower)
- `rembg=True` cleans up backgrounds reliably on photo-style inputs

## bake_mv_texture.sh details

The baker:
1. Loads mesh, normalizes to `[-1, 1]` cube
2. Detects figure bbox in each photo by thresholding non-black pixels (assumes dark background — Hunyuan3D outputs are pre-rembg'd onto black)
3. Runs `xatlas` UV unwrap at 2048 atlas, padding 4 (typically inflates vert count ~1.5x due to seam splits)
4. For each re-indexed vertex, projects to all 4 photos using orthographic camera and samples the pixel
5. Weights samples by `max(0, dot(vertex_normal, camera_direction))^1.5`
6. Blends across views and writes vertex colors back

Limitations:
- Orthographic projection causes slight stretching near image edges (lens distortion not modeled)
- Vertex colors are coarser than a real UV texture map. For PBR-quality, run Blender's bake operator on the output
- Faces/limbs in different poses across views will not match; if your input photos have arms in 3/4 vs straight side, Hunyuan3D-2 MV will fuse them into the torso. Use T-pose or A-pose photos for cleaner separations.

## Common errors

- `MESHY/TRIPO3D/etc API_KEY env var not set` → see Getting API keys section above
- Tripo3D `code:2010 "no credits"` despite seeing credits on dashboard → buy a separate API-credit pack (web credits do NOT cover API usage)
- HF `GPU duration > 90s` → make sure `HF_TOKEN` is set in env, not just in dashboard
- HF Space `503 / "is sleeping"` → first request wakes the container; retry once
- All providers fail → dispatcher falls back to local TripoSR which always works given the EGL stack is installed (see `tools/patch_triposr_cpu.sh`)
